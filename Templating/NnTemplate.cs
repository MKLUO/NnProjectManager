using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;

#nullable enable

namespace NnManager {

    public partial class Project {

        [Serializable]
        class NnTemplate {

            readonly List<Element> elements;
            public NnParam Signature {
                get;
                private set;
            }

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

                readonly Type type;
                public string Name {
                    get;
                    private set;
                }
            }

            public NnTemplate(
                string content
            ) {
                elements = new List<Element>();
                Dictionary<string, Param> signature = new Dictionary<string, Param>();

                string[] lines =
                    content.Splitter("([\r\n|\r|\n]+)");

                Dictionary<string, string> defaultValues = new Dictionary<string, string>();
                HashSet<string> variableValues = new HashSet<string>();
                HashSet<string> variableTexts = new HashSet<string>();

                foreach (string line in lines) {
                    if (Regex.IsMatch(
                            line,
                            "[ |\t]*@default[ |\t]+[0-9|A-Z|a-z|_]+[ |\t]+[0-9|A-Z|a-z|_|\"]+[ |\t]*")) {

                        string[] tokens = 
                            line.Splitter("[ |\t]+");

                        defaultValues.Add(
                            tokens[1],
                            tokens[2]
                        );

                    } else {
                        string[] tokens =
                            line.Splitter("(@[0-9|A-Z|a-z|_]+)");

                        foreach (string token in tokens) {
                            string vari;
                            if ((token[0], token[1]) == ('@', '@')) {
                                vari = token.Substring(2);
                                variableValues.Add(vari);
                                elements.Add(Element.NewVariable(vari));
                            } else if (token[0] == '@') {                                    
                                vari = token.Substring(1);                        
                                variableTexts.Add(vari);
                                elements.Add(Element.NewVariable(vari));
                            } else {
                                elements.Add(Element.NewContent(token));
                            }
                        }
                    }
                }

                foreach (string key in variableValues)
                    signature.Add(
                        key, 
                        Param.NewValue(
                            defaultValues.ContainsKey(key) ?
                            defaultValues[key] :
                            null
                        )
                    );
                
                foreach (string key in variableTexts)
                    signature.Add(
                        key, 
                        Param.NewText(
                            defaultValues.ContainsKey(key) ?
                            defaultValues[key] :
                            null
                        )
                    );

                Signature = new NnParam(signature);
            }

            public string? GenerateContent(
                NnParam param) {
                string result = "";

                foreach (Element element in elements) {
                    if (element.IsVariable()) {
                        string? value; 
                        if ((value = param.Get(element.Name)) != null) {
                            result += value;
                        } else 
                        if ((value = Signature.Get(element.Name)) != null) {
                            result += value;
                        } else {
                            return null;
                        }
                    } else {
                        result += element.Name;
                    }
                }
                return result;
            }
        }
    }
}