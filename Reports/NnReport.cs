using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;
        
    public enum ReportType {
        Occup2D
    }    

    partial class NnPlan {
        NnReport Report(
            ReportType type, 
            ImmutableDictionary<string, string> options
        ) {
            NnReport report;
            switch (type) {
                case ReportType.Occup2D:
                    report = NnReportOccup2D(options); break;
                default: throw new Exception();                
            }
            return report;
        }
    }    

    public class NnReport {

        public static ImmutableDictionary<string, string> GetDefaultOptions(ReportType type) {
            switch (type) {
                case ReportType.Occup2D:
                    return NnPlan.NnReportOccup2DDefaultOption;
                default: throw new Exception();
            }
        }        

        public NnReport(
            string name,
            Func<ImmutableDictionary<string, string>, string> execute,
            ImmutableDictionary<string, string> defaultOptions,
            ImmutableDictionary<string, string>? options = null
        ) {
            this.Name = name;
            this.execute = execute;
            this.defaultOptions = new Dictionary<string, string>(defaultOptions);

            this.options = options != null ?
                new Dictionary<string, string>(options) :
                null;
        }

        public string Name { get; }

        Dictionary<string, string> defaultOptions;
        ImmutableDictionary<string, string> DefaultOptions => defaultOptions.ToImmutableDictionary();

        Dictionary<string, string>? options;
        ImmutableDictionary<string, string>? Options => options?.ToImmutableDictionary();

        Func<ImmutableDictionary<string, string>, string> execute;
        public string Execute() {
            var newOption = new Dictionary<string, string>();

            if (Options != null) {
                foreach (var key in DefaultOptions.Keys) {                
                    if (Options.ContainsKey(key))
                        newOption[key] = Options[key];
                    else newOption[key] = DefaultOptions[key];
                }
                return execute(newOption.ToImmutableDictionary());
            } else return execute(DefaultOptions);
        }
    }
}