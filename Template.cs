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

                readonly Type type;
                readonly string name;
            }

            class Variable {
                public Variable(
                    string name,
                    string defaultValue
                ) {
                    this.name = name;
                    this.defaultValue = defaultValue;
                }
                readonly string name;
                readonly string defaultValue;
            }

            public Template(
                string content
            ) {
                // TODO: Parse for template
                elements = new List<Element>();
                variables = new List<Variable>();

                string[] lines = 
                    Regex.Split(content, ("([\r\n|\r|\n]+)"))
                        .Where(s => s != String.Empty)
                        .ToArray<string>();
                foreach(string line in lines) {
                    if (Regex.IsMatch(line, "[ |\t]*@define[ |\t]+[0-9|A-Z|a-z|_]+[ |\t]+[0-9|A-Z|a-z|_|\"]+[ |\t]*"
                    )) {
                        string[] tokens = 
                            Regex.Split(line, "[ |\t]+")
                                .Where(s => s != String.Empty)
                                .ToArray<string>();

                        variables.Add(
                            new Variable(
                                tokens[1],
                                tokens[2]
                            ));
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

            public string generateContent(
                Dictionary<string, string> param) {
                return "";
            }

            readonly List<Element> elements;
            readonly List<Variable> variables;
        }
    }
}