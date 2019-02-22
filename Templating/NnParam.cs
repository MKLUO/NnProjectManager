using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

#nullable enable

namespace NnManager {

    public partial class Project {

        [Serializable]
        class Param {

            [Serializable]
            enum ParamType {
                Text,
                Value
            }

            Param(ParamType type, string? value) {
                this.type = type;
                this.Value = value;
            }

            public static Param NewText(string? value) {
                return new Param(ParamType.Text, value);
            }
            public static Param NewValue(string? value) {
                // TODO: check if value is valid
                return new Param(ParamType.Value, value);
            }

            public string? Value {
                get;
                private set;
            }
            ParamType type;
        }

        [Serializable]
        class NnParam {

            readonly Dictionary<string, Param> parameters;

            public NnParam(Dictionary<string, Param> param) {
                parameters = param;
            }

            // TODO: Excel input?
            static public HashSet<NnParam> ? FromTable(string tableContent) {

                string[] lines =
                    tableContent.Splitter("([\r\n|\r|\n]+)");

                // foreach (string line in lines) {
                List<string>? signature;
                Dictionary<string, Param> texts = new Dictionary<string, Param>();
                HashSet<NnParam> result = new HashSet<NnParam>();

                for (int index = 0; index < lines.Length; index++) {
                    string line = lines[index];
                    if (Regex.IsMatch(
                            line,
                            "//.*")) {
                    } else if (Regex.IsMatch(
                            line,
                            "[ |\t]*[0-9|A-Z|a-z|_]+[ |\t]+[0-9|A-Z|a-z|_|\"]+[ |\t]*")) {
                        string[] tokens = line.Splitter("[ |\t]+");
                        texts[tokens[0]] = 
                            Param.NewText(
                                (tokens[1] == "-") ? 
                                null : 
                                tokens[1]
                            );
                    } else 
                    if (Regex.IsMatch(
                            line.TrimSpaces(),
                            "#Values")) {

                        string nextLine = lines[index + 1];
                        signature = new List<string>(
                            nextLine.Splitter("[ |\t]+")
                        );                        
                        for (int index2 = index + 2; index2 < lines.Length; index2++) {
                            List<string> values = new List<string>(
                                lines[index2].Splitter("[ |\t]+")
                            );
                            if (values.Count > signature.Count) return null;
                            result.Add(new NnParam(
                                signature.Zip(values, Tuple.Create).ToDictionary(
                                    x => x.Item1,
                                    x => Param.NewValue(
                                        (x.Item2 == "-") ? 
                                        null : 
                                        x.Item2
                                    )
                                ).Concat(texts).ToDictionary(x => x.Key, x => x.Value)
                            ));
                        }
                        break;
                    }
                }
                return result;
            }

            public bool IsFilled() {
                // TODO:
                return true;
            }

            public static bool HasSameSignatureAndText(NnParam param1, NnParam param2) {
                // TODO: Compare signature and text
                return false;
            }

            public static bool HasSameSignature(NnParam param1, NnParam param2) {
                // TODO: Compare signature
                return false;
            }

            public static NnParam operator /(NnParam nnParam, double value) => (1.0 / value) * nnParam;
            public static NnParam operator *(NnParam nnParam, double value) => value * nnParam;
            public static NnParam operator *(double value, NnParam nnParam) {
                // TODO:
                return nnParam;
            }

            public static NnParam operator -(NnParam left, NnParam right) => left + (-1) * right;
            public static NnParam operator +(NnParam left, NnParam right) {
                // TODO: check for signature
                return left;
            }

            public string? Get(string key) => parameters[key].Value;
        }
    }
}