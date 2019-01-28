using System;
using System.Collections.Generic;

using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;

using System.Linq;

namespace NnManager {

    public partial class Project {

        [Serializable]
        class Template {
            
            readonly string name;
            readonly List<Element> elements;
            readonly Dictionary<string, string> defaultParams;
            readonly HashSet<string> variables;

            [Serializable]
            class Element {
                public enum Type {
                    Text,
                    Value
                }

                public Element(
                    Type type,
                    string name
                ) {
                    this.type = type;
                    this.name = name;
                }

                readonly public Type type;
                readonly public string name;
            }

            public Template(
                string name,
                string content
            ) {
                this.name = name;

                // TODO: Parse for template
                elements = new List<Element>();
                defaultParams = new Dictionary<string, string>();
                variables = new HashSet<string>();

                string[] lines = 
                    Regex.Split(content, ("([\r\n|\r|\n]+)"))
                        .Where(s => s != String.Empty)
                        .ToArray<string>();

                foreach(string line in lines) {
                    if (Regex.IsMatch(line, "[ |\t]*@default[ |\t]+[0-9|A-Z|a-z|_]+[ |\t]+[0-9|A-Z|a-z|_|\"]+[ |\t]*"
                    )) {
                        string[] tokens = 
                            Regex.Split(line, "[ |\t]+")
                                .Where(s => s != String.Empty)
                                .ToArray<string>();

                        defaultParams.Add(
                                tokens[1],
                                tokens[2]);
                    } else {
                        string[] tokens = 
                            Regex.Split(line, "(@[0-9|A-Z|a-z|_]+)")
                                .Where(s => s != String.Empty)
                                .ToArray<string>();
                        
                        foreach (string token in tokens)
                        {
                            if (token[0] == '@') {
                                elements.Add(
                                    new Element(
                                        Element.Type.Value,
                                        token.Substring(1)
                                    )
                                );
                                variables.Add(token.Substring(1));
                            } else {
                                elements.Add(
                                    new Element(
                                        Element.Type.Text,
                                        token
                                    )
                                );
                            }
                        }
                    }
                }
            }

            public string GenerateContent(
                Dictionary<string, string> param) {
                string result = "";

                // Check if any param is not used
                Dictionary<string, bool> paramCheck
                    = new Dictionary<string, bool>();
                foreach (var item in param)
                    paramCheck.Add(item.Key, false);

                foreach (Element element in elements)
                {
                    switch (element.type) {

                        case Element.Type.Text:
                            result += element.name;
                            break;

                        case Element.Type.Value:
                            if (param.ContainsKey(element.name)){
                                result += param[element.name];
                                paramCheck[element.name] = true;
                            }
                            else if (defaultParams.ContainsKey(element.name))
                                result += defaultParams[element.name];
                            else
                                throw new Exception("Variable missing in param.");

                            // TODO: Implement custom exception
                            break;
                    }
                }

                // foreach (var item in paramCheck)
                //     if (item.Value == false)
                //         throw new Exception("Parameter \"" + item.Key + "\" is not present in current template!");

                return result;
            }

            public string GetName() {
                return name;
            }

            public List<string> GetVariablesInfo() {
                List<string> info = new List<string>();

                foreach (string variable in variables)
                {
                    if (defaultParams.ContainsKey(variable))
                        info.Add(variable + " (" + defaultParams[variable] + ")");
                    else
                        info.Add(variable);                        
                }

                return info;
            }
        }
    }
}