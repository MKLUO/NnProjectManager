using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

#nullable enable

namespace NnManager {

    [Serializable]
    public partial class NnParam {

        public class SignatureMismatchException : Exception {
            public SignatureMismatchException() { }
            public SignatureMismatchException(string message) : base(message) { }
            public SignatureMismatchException(string message, Exception inner) :
                base(message, inner) { }
        }

        public class ParameterMissingException : Exception {
            public ParameterMissingException() { }
            public ParameterMissingException(string message) : base(message) { }
            public ParameterMissingException(string message, Exception inner) : 
                base(message, inner) { }
        }

        Dictionary<string, double> variables;
        public ReadOnlyDictionary<string, double> Variables { 
            get {
                return new ReadOnlyDictionary<string, double>(variables);
            } 
        }
        Dictionary<string, string> consts;
        public ReadOnlyDictionary<string, string> Consts { 
            get {
                return new ReadOnlyDictionary<string, string>(consts);
            } 
        }

        public NnParam(
            Dictionary<string, double> variables,
            Dictionary<string, string> consts) {
            (this.variables, this.consts) = (variables, consts);
        }

        // TODO: Excel input?
        public static List<NnParam> NewParamsFromList(string content) {
            string[] lines =
                content.Splitter("[\r\n|\r|\n]+");
                
            Dictionary<string, string> consts = new Dictionary<string, string>();

            List<string>? paramTag = null;
            List<List<string>> paramDatas = new List<List<string>>();
            foreach (string line in lines) {
                if (Regex.IsMatch(
                        line,
                        "[ |\t]*[0-9|A-Z|a-z|_]+[ |\t]*=[ |\t]*[0-9|A-Z|a-z|_|.|-]+[ |\t]*")) {

                    string[] tokens = 
                        line.TrimSpaces().Splitter("=");

                    consts.Add(
                        tokens[0],
                        tokens[1]
                    );

                } else if (Regex.IsMatch(
                        line,
                        "[ |\t]*@.+")) {

                    if (paramTag != null) {
                        Util.ErrorHappend("Syntax error in input file! (Multiple variable tag definition is not allowed!)");
                        return new List<NnParam>();
                    }
                    paramTag =
                        line.TrimSpaces().Splitter(",|@").ToList();
                } else if (Regex.IsMatch(
                        line,
                        "[ |\t]*.+")) {
                    paramDatas.Add(
                        line.TrimSpaces().Splitter(",").ToList()
                    );
                }
            }
            if (paramTag == null) {
                    Util.ErrorHappend("Syntax error in input file! (Multiple variable tag definition is not allowed!)");
                    return new List<NnParam>();
                }

            List<NnParam> result = new List<NnParam>();
            foreach (var paramData in paramDatas) {
                if (paramData.Count != paramTag.Count)
                    continue;       

                result.Add(
                    new NnParam(
                        paramTag.Zip(
                            paramData, 
                            (string x, string y) => new {
                                k=x, 
                                v=Double.Parse(
                                    y, System.Globalization.NumberStyles.Float
                                )
                            }).ToDictionary(x => {
                                if (x.k == null) throw new NullReferenceException();
                                return x.k;
                            }, x => x.v),
                        consts
                    )
                );
            }
            return result;
        }

        public bool Pad(NnTemplate template) {
            foreach (var key in template.Variables.Keys) {
                if (!variables.ContainsKey(key))
                    if (template.Variables[key] != null)
                        variables[key] = template.Variables[key] ?? throw new Exception();
                    else return false;
            }
            foreach (var key in template.Consts.Keys) {
                if (!consts.ContainsKey(key))
                    if (template.Consts[key] != null)
                        consts[key] = template.Consts[key] ?? throw new Exception();
                    else return false;                    
            }
            return true;
        }

        public static bool HasConsts(NnParam p1, IEnumerable<KeyValuePair<string, string>> consts) =>
            p1.consts.OrderBy(x => x.Key).SequenceEqual(
                consts.OrderBy(x => x.Key));

        public static bool HasSameSignature(NnParam p1, NnParam p2) =>
            p1.variables.Keys.OrderBy(x => x).SequenceEqual(
                p2.variables.Keys.OrderBy(x => x)) &&
            p1.consts.OrderBy(x => x.Key).SequenceEqual(
                p2.consts.OrderBy(x => x.Key));

        public static bool AreSame(NnParam p1, NnParam p2) {
            if (!HasSameSignature(p1, p2))
                return false;
            foreach (string key in p1.variables.Keys)
                if (p1.variables[key] != p2.variables[key])
                    return false;
            foreach (string key in p1.consts.Keys)
                if (p1.consts[key] != p2.consts[key])
                    return false;

            return true;
        }

        public static NnParam operator /(NnParam nnParam, double value) => (1.0 / value) * nnParam;
        public static NnParam operator *(NnParam nnParam, double value) => value * nnParam;
        public static NnParam operator *(double value, NnParam nnParam) {
            Dictionary<string, double> newVariables = new Dictionary<string, double>();
            foreach (string key in nnParam.variables.Keys)
                newVariables[key] = nnParam.variables[key] * value;

            return new NnParam(newVariables, nnParam.consts);
        }

        public static NnParam operator -(NnParam left, NnParam right) => left + (-1) * right;
        public static NnParam operator +(NnParam left, NnParam right) {
            if (!HasSameSignature(left, right))
                throw new SignatureMismatchException();

            Dictionary<string, double> newVars = new Dictionary<string, double>();

            foreach (var key in left.variables.Keys)
                newVars[key] = left.variables[key] + right.variables[key];

            return new NnParam(newVars, left.consts);
        }

        public Dictionary<string, string> Content {
            get {
                var result = new Dictionary<string, string>();
                foreach (string key in variables.Keys)
                    result[key] = variables[key].ToString();
                foreach (string key in consts.Keys)
                    result[key] = consts[key];

                return result;
            }
        }

        public string Get(string key) {
            if (variables.ContainsKey(key))
                return variables[key].ToString();
            else if (consts.ContainsKey(key))
                return consts[key];
            else throw new ParameterMissingException();
        }

        public string GetTag() {
            // if ((variables.Count + consts.Count) == 0)
            //     return "(default)";
            //     // TODO: Default?

            // string result = "(";
            // foreach (var key in consts.Keys)
            //     result += $"{key}={consts[key]}, ";
            // foreach (var key in variables.Keys)
            //     result += $"{key}={variables[key].ToString()}, ";
            // return result.Substring(0, result.Length - 2) + ")";

            // FIXME: Using only variables here

            if ((variables.Count) == 0)
                return "(default)";
                // TODO: Default?

            string result = "(";
            // foreach (var key in consts.Keys)
            //     result += $"{key}={consts[key]}, ";
            foreach (var key in variables.Keys)
                result += $"{key}={variables[key].ToString()}, ";
            return result.Substring(0, result.Length - 2) + ")";
        }        
    }
}