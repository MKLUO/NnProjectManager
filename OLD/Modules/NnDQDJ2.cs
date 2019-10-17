
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    partial class NnTask {

        RPath NnDQDJ2Path => FSPath.SubPath("NnDQDJ2");
        RPath NnDQDJ2ResultPath => NnDQDJPath.SubPath($"_Result.txt");
        RPath NnDQDJ2ReportPath => NnDQDJPath.SubPath($"_Report.txt");

        NnModule NnDQDJ2(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "NN DQD J 2",
                NnDQDJ2CanExecute,
                NnDQDJ2IsDone,
                NnDQDJ2Execute,
                NnDQDJ2GetResult,
                NnDQDJ2DefaultOption,
                options);

        public static ImmutableDictionary<string, string> NnDQDJ2DefaultOption =>
            new Dictionary<string, string> { 
            }.ToImmutableDictionary();

        bool NnDQDJ2CanExecute() => NnDQDReportIsDone();

        bool NnDQDJ2IsDone() => File.Exists(NnDQDJ2ResultPath);

        string NnDQDJ2GetResult() => File.ReadAllText(NnDQDJ2ResultPath);

        bool NnDQDJ2Execute(CancellationToken ct, ImmutableDictionary<string, string> options) {
            
            /*
                NOTE: After DQDReport is done,
                    single-particle eigenbasis from NN should be directly accessible
                    from an unified interface.

                NOTE: Basis other than NN-solved s.p. basis should be pluggable here.
             */

            /// NOTE: Evaluate coulomb kernel (from a ref. WF)
            
            return true;
        }
    }    
}