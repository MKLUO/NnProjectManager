using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Data;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;

    [Serializable]
    public enum NnType {
        Nn3,
        NnPP
    }   

    [Serializable]
    public class NnTemplate {        
        public string Name { get; }
        public NnType Type { get; }
        public RPath? FSPath { get; }
        List<Element> Elements { get; }
        public ImmutableDictionary<string, string?> Variables { get; }
        public ImmutableDictionary<string, string> DerivedVariables { get; }

        [Serializable]
        class Element {
            enum Type {
                Content,
                Variable
            }

            Element(
                Type type,
                string name
            ) {
                this.type = type;
                this.Name = name;
            }

            public static Element NewContent(string key) {
                return new Element(
                    Type.Content,
                    key
                );
            }
            public static Element NewVariable(string key) {
                return new Element(
                    Type.Variable,
                    key
                );
            }

            public bool IsVariable() {
                return type == Type.Variable;
            }

            public bool Equals(Element ele) {
                return (type == ele.type) && (Name == ele.Name);
            }

            readonly Type type;
            public string Name {
                get;
                private set;
            }
        }

        NnTemplate(
            string name,
            NnType type,
            RPath? path,
            List<Element> elements,
            ImmutableDictionary<string, string?> variables,
            ImmutableDictionary<string, string> derivedVariables
        ) {
            this.Name = name;
            this.Type = type;
            this.FSPath = path;
            this.Elements = elements;
            this.Variables = variables;
            this.DerivedVariables = derivedVariables;
            
            Save();
        }

        // FIXME: put SaveData utilities in notifier?

        [Serializable]
        struct SaveData {
            public SaveData(NnTemplate temp) {
                name = temp.Name;
                type = temp.Type;
                elements = temp.Elements;
                variables = new Dictionary<string, string?>(temp.Variables);
                derivedVariables = new Dictionary<string, string>(temp.DerivedVariables);
            }

            readonly public string name;
            readonly public NnType type;
            readonly public List<Element> elements;
            public Dictionary<string, string?> variables;
            public Dictionary<string, string> derivedVariables;
        }

        public void Save(RPath? path = null) {
            if ((FSPath ?? path) == null)
                return;
            Util.SerializeToFile(
                new SaveData(this),
                path == null ?
                    FSPath.SubPath(NnAgent.tempFileName):
                    path.SubPath(NnAgent.tempFileName)                
            );
        }

        public static NnTemplate? Load(RPath path) {
            try {
                var tempData =
                    (SaveData) Util.DeserializeFromFile(
                        path.SubPath(NnAgent.tempFileName)
                    );

                NnTemplate temp = new NnTemplate(
                    tempData.name,
                    tempData.type,
                    path,
                    tempData.elements,
                    tempData.variables.ToImmutableDictionary(),
                    tempData.derivedVariables.ToImmutableDictionary()
                );

                return temp;
            } catch {
                Util.ErrorHappend($"Error while loading template!");
                return null;
            }
        }

        public static NnTemplate? NewTemplate(
            string name,
            string content,
            RPath? path = null
        ) {
            List<Element> elements = new List<Element>();

            string[] lines =
                content.Splitter("([\r\n|\r|\n]+)");

            // Test parse (to identify NnType)
            NnType type = NnType.Nn3;
            foreach (string line in lines)
                if (Regex.IsMatch(line, @"^[ |\t]*}.*")) {
                    type = NnType.NnPP; break;
                }

            string tokenVariable = "\\$", tokenComment = "#";
            switch (type) {
                case NnType.Nn3:
                    tokenVariable = "%";
                    tokenComment = "!";
                break;
                case NnType.NnPP:
                    tokenVariable = "\\$";
                    tokenComment = "#";
                break;
            }

            Dictionary<string, string> defaultValues = new Dictionary<string, string>();

            Dictionary<string, string> derivedVariables = new Dictionary<string, string>();
            Dictionary<string, string?> variables = new Dictionary<string, string?>();

            List<string> variableKeys = new List<string>();
            foreach (string oriline in lines) {
                string line = Regex.Replace(oriline, $"{tokenComment}.*$", "");
                if (Regex.IsMatch(
                        line,
                        $"^[ \\t]*{tokenVariable}[0-9A-Za-z_]+[ \\t]*=.*")) {
                    // Regex.Replace(line, @"[ \t]+", "");
                    string[] tokens = line.TrimSpaces().Splitter("=");

                    string var = tokens[0].Substring(1);
                    string val = tokens.Count() >= 2 ? tokens[1] : "";

                    if (defaultValues.ContainsKey(var)) {
                        Util.ErrorHappend($"Multiple definition of key \"{tokens[0]}\"!");
                        return null;
                    }

                    defaultValues.Add(var, val);

                    if (!val.Contains(tokenVariable.Last()))
                        variables.Add(var, val);

                } else {
                    if (Regex.IsMatch(line, "directory"))
                            if (!Util.WarnAndDecide($"Output directory specification {line} in template file will be discarded.\nContinue parsing?")) {
                                return null;
                            } else continue;

                    string[] tokens =
                        line.Splitter($"({tokenVariable}[0-9A-Za-z_]+)");

                    foreach (string token in tokens) {
                        string var;
                        if (token[0] == tokenComment.Last()) {
                            elements.Add(Element.NewContent(token));
                        } else if (token[0] == tokenVariable.Last()) {
                            var = token.Substring(1);
                            // var = token;
                            variableKeys.Add(var);
                            elements.Add(Element.NewVariable(var));
                        } else {
                            elements.Add(Element.NewContent(token));
                        }
                    }
                }
            }                   

            // FIXME: clutter!
            // foreach (string key in variableKeys) {
            //     if (defaultValues.ContainsKey(key))
            //         if (defaultValues[key].Contains(tokenVariable.Last()))
            //             derivedVariables[key] = defaultValues[key];
            //         else 
            //             variables[key] = defaultValues[key];
            //     else 
            //         variables[key] = null;
            // }

            foreach (string key in defaultValues.Keys) {
                if (defaultValues[key].Contains(tokenVariable.Last()))
                    derivedVariables[key] = defaultValues[key];
                else 
                    variables[key] = defaultValues[key];
            }

            foreach (string key in variableKeys)
                if (!derivedVariables.ContainsKey(key) && !variables.ContainsKey(key))
                    variables[key] = null;

            return new NnTemplate(
                name, 
                type,
                path,
                elements, 
                variables.ToImmutableDictionary(),
                derivedVariables.ToImmutableDictionary()
            );
        }

        // public bool Equals(NnTemplate temp) =>
        //     Elements.OrderBy(x => x).SequenceEqual(
        //         temp.Elements.OrderBy(x => x)) &&
        //     Variables.OrderBy(x => x.Key).SequenceEqual(
        //         temp.Variables.OrderBy(x => x.Key)) &&
        //     Consts.OrderBy(x => x.Key).SequenceEqual(
        //         temp.Consts.OrderBy(x => x.Key));        

        public Dictionary<ModuleType, Dictionary<string, string>> GenerateModuleOptions(
            ImmutableDictionary<string, string> param) {

            var result = new Dictionary<ModuleType, Dictionary<string, string>>();
            
            foreach (var type in Enum.GetValues(typeof(ModuleType)).Cast<ModuleType>()) {    
                result[type] = new Dictionary<string, string>();
                foreach (var key in Variables.Keys.Where(
                    k => Regex.IsMatch(k, $"{type.ToString()}_\\w+")
                )) {
                    var varKey = key.Replace($"{type.ToString()}_", "");
                    var varValue = Variables[key];
                    if (param.ContainsKey(key))                        
                        result[type][varKey] = param[key];
                    else if (varValue != null)
                        result[type][varKey] = varValue;
                }
                foreach (var key in DerivedVariables.Keys.Where(
                    k => Regex.IsMatch(k, $"{type.ToString()}_\\w+")
                )) {
                    var varKey = key.Replace($"{type.ToString()}_", "");
                    var evalResult = Evaluate(DerivedVariables[key], param);
                    try {
                        result[type][varKey] = 
                            Convert.ToDouble(
                                new DataTable().Compute(evalResult, null)
                            ).ToString();
                    } catch {
                        Util.ErrorHappend($"Error in evaluation of module option {key}!");
                    }
                }
            }

            return result;
        }
      
        public string GenerateContent(
            ImmutableDictionary<string, string> param,
            bool nonSC = false) {

            // FIXME: WET!
            string tokenVariable = "\\$";
            switch (Type) {
                case NnType.Nn3:
                    tokenVariable = "%";
                break;
                case NnType.NnPP:
                    tokenVariable = "\\$";
                break;
            }

            // NOTE: Reserved Meta-Parameters (e.g., Non-SC)
            var paramWithMeta = new Dictionary<string, string>(param);
            paramWithMeta["NnMainNonSC_ON"] = "#";
            paramWithMeta["NnMainNonSC_OFF"] = "";
            if (nonSC) {
                paramWithMeta["NnMainNonSC_ON"] = "";
                paramWithMeta["NnMainNonSC_OFF"] = "#";
            }

            string result = "";

            foreach (Element element in Elements) {
                if (element.IsVariable()) {
                    result += Evaluate(tokenVariable.Last() + element.Name, paramWithMeta.ToImmutableDictionary());
                } else {
                    result += element.Name;
                }
            }
            return result;
        }

        string Evaluate(string input, ImmutableDictionary<string, string> param) {
            
            string tokenVariable = "\\$";
            switch (Type) {
                case NnType.Nn3:
                    tokenVariable = "%";
                break;
                case NnType.NnPP:
                    tokenVariable = "\\$";
                break;
            }

            // var eval = tokenVariable.Last() + input;
            var eval = input;
            int depth = 0;
            while (eval.Contains(tokenVariable.Last())) {       
                if (depth++ > 10) {
                    Util.ErrorHappend("Evaluation of derived parameter exceeds depth limit!");
                    throw new Exception();
                }
                string[] tokens =
                    eval.Splitter($"({tokenVariable}[0-9A-Za-z_]+)");
                eval = "";
                foreach (string token in tokens) {
                    if (token[0] == tokenVariable.Last()) {
                        string var = token.Substring(1);
                        if (DerivedVariables.ContainsKey(var)) {
                            // eval += DerivedVariables[var];
                            if (DerivedVariables[var][0] == '[')
                                eval += $"{DerivedVariables[var]}";
                            else
                                eval += $"({DerivedVariables[var]})";
                        } else if (param.ContainsKey(var))
                            eval += param[var];
                        else if (Variables.ContainsKey(var))
                            eval += Variables[var];
                        else {
                            Util.ErrorHappend($"Token {token} used in {input} is not defined!");
                            throw new Exception();
                        }
                    } else {
                        eval += token;
                    }
                }
            }

            return eval;
        }
    }
}