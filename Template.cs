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

                readonly public Type type;
                readonly public string name;
            }

            public Template(
                string content
            ) {
                // TODO: Parse for template
                elements = new List<Element>();
                variables = new Dictionary<string, string>();

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
                            else if (variables.ContainsKey(element.name))
                                result += variables[element.name];
                            else
                                throw new Exception("Variable missing.");

                            // TODO: Implement custom exception
                            break;
                    }
                }

                foreach (var item in paramCheck)
                    if (item.Value == false)
                        throw new Exception("Parameter \"" + item.Key + "\" is not present in current template!");

                return result;
            }

            readonly List<Element> elements;
            readonly Dictionary<string, string> variables;
        }
    }
}