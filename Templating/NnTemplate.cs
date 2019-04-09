using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;

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
        public RPath FSPath { get; }
        List<Element> Elements { get; }
        public ImmutableDictionary<string, string?> Variables { get; }

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
            RPath path,
            List<Element> elements,
            ImmutableDictionary<string, string?> variables
        ) {
            this.Name = name;
            this.Type = type;
            this.FSPath = path;
            this.Elements = elements;
            this.Variables = variables;
            
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
            }

            readonly public string name;
            readonly public NnType type;
            readonly public List<Element> elements;
            public Dictionary<string, string?> variables;
        }

        public void Save(RPath? path = null) {
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
                    tempData.variables.ToImmutableDictionary()
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
            RPath path
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
            Dictionary<string, string?> variables = new Dictionary<string, string?>();

            List<string> variableKeys = new List<string>();
            foreach (string line in lines) {
                // FIXME: nested variable evaluation!
                if (Regex.IsMatch(
                        line,
                        $"^[ \\t]*{tokenVariable}[0-9A-Za-z_]+[ \\t]*=[ \\t]*[0-9A-Za-z_.\\-{tokenComment}]+[ \\t]*")) {
                    string[] tokens =
                        line.Splitter($"[{tokenVariable}| |\t|=]+");

                    if (defaultValues.ContainsKey(tokens[0])) {
                        Util.ErrorHappend($"Multiple definition of key \"{tokens[0]}\"!");
                        return null;
                    }

                    defaultValues.Add(
                        tokens[0],
                        tokens[1]
                    );
                // TODO: Refactor these into a NnTemplate parsing logic.
                } else {
                    // FIXME: also discard other output dirs!
                    if (Regex.IsMatch(line, "[ \t]*directory[ \t]*=.*"))
                            if (!Util.WarnAndDecide($"The output directory specification {line} in template file will be discarded.\nContinue parsing?")) {
                                return null;
                            } else continue;

                    string[] tokens =
                        line.Splitter($"(#.*$)|({tokenVariable}[0-9A-Za-z_]+)");

                    foreach (string token in tokens) {
                        string vari;
                        if (token[0] == tokenComment.Last()) {
                            elements.Add(Element.NewContent(token));
                        } else if (token[0] == tokenVariable.Last()) {
                            vari = token.Substring(1);
                            variableKeys.Add(vari);
                            elements.Add(Element.NewVariable(vari));
                        // } else if (token[0] == '$') {
                        //     vari = token.Substring(1);
                        //     constKeys.Add(vari);
                        //     elements.Add(Element.NewVariable(vari));
                        } else {
                            elements.Add(Element.NewContent(token));
                        }
                    }
                }
            }

            // FIXME: hack here. for identifying ## (toggle) 
            foreach (string key in variableKeys) {
                variables[key] =
                    defaultValues.ContainsKey(key) ?
                    (defaultValues[key] == "##" ? "" : defaultValues[key]) :
                    (string?) null;
            }

            return new NnTemplate(
                name, 
                type,
                path,
                elements, 
                variables.ToImmutableDictionary()
            );
        }

        // public bool Equals(NnTemplate temp) =>
        //     Elements.OrderBy(x => x).SequenceEqual(
        //         temp.Elements.OrderBy(x => x)) &&
        //     Variables.OrderBy(x => x.Key).SequenceEqual(
        //         temp.Variables.OrderBy(x => x.Key)) &&
        //     Consts.OrderBy(x => x.Key).SequenceEqual(
        //         temp.Consts.OrderBy(x => x.Key));        

        public string GenerateContent(
            // NnParam param) {
            ImmutableDictionary<string, string> param) {
            string result = "";

            foreach (Element element in Elements) {
                if (element.IsVariable()) {
                    result += param[element.Name];
                } else {
                    result += element.Name;
                }
            }
            return result;
        }
    }
}