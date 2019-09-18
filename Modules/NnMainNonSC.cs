using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;     

    partial class NnTask {

        RPath NnMainNonSCPath => FSPath.SubPath("NnMainNonSC");
        
        RPath NnMainNonSCReportPath => NnMainNonSCPath.SubPath("_Report.txt");

        public NnModule NnMainNonSC(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "NN Main Non-SC",
                () => true, 
                NnMainNonSCIsDone, 
                NnMainNonSCExecute, 
                NnMainNonSCGetResult,
                NnModule.GetDefaultOptions(ModuleType.NnMainNonSC),
                options);

        public static ImmutableDictionary<string, string> NnMainNonSCDefaultOption =>
            new Dictionary<string, string> { 
                { "x0", "-" },
                { "x1", "-" },
                { "y0", "-" },
                { "y1", "-" },
                { "z0", "-" },
                { "z1", "-" },
                { "order", "2" },
                { "3particle", "no" },
                { "enableFTDict", "no" }
            }.ToImmutableDictionary(); 

        bool NnMainNonSCIsDone() {
            // FIXME: Check report?
            return true;
        }

        string NnMainNonSCGetResult() {
            try {
                // FIXME: done (converge) / diverge.
                File.ReadAllText(NnMainNonSCReportPath);
                return "(done)";
            } catch {
                return "(error)";
            }
        }

        bool NnMainNonSCExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {

            // RPath logFilePath = NnMainPath.SubPath(NnAgent.logFileName);
            
            // var tsLog = Util.StartLogParser<NnMainLog>(logFilePath, ct, SetStatus);

            // NnAgent.RunNnStructure(
            //     NnMainPath, Content, ct, Type
            // );
            // NnAgent.RunNn(
            //     NnMainPath, Content, ct, Type
            // );

            // tsLog.Cancel();

            // string? report = NnAgent.GenerateNnReport(
            //     NnMainPath, ct, Type
            // );

            // if (report != null)
            //     File.WriteAllText(NnMainReportPath, report);

            // return !ct.IsCancellationRequested;
            return true;
        }
    }
}