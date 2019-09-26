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

        RPath NnMainPath => FSPath;
        
        RPath NnMainReportPath => NnMainPath.SubPath("_Report.txt");

        public NnModule NnMain(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "NN Main",
                () => true, 
                NnMainIsDone, 
                NnMainExecute, 
                NnMainGetResult,
                NnModule.GetDefaultOptions(ModuleType.NnMain),
                options);
 

        bool NnMainIsDone() {
            // FIXME: Check report?
            return true;
        }

        string NnMainGetResult() {
            try {
                // FIXME: done (converge) / diverge.
                File.ReadAllText(NnMainReportPath);
                return "(done)";
            } catch {
                return "(error)";
            }
        }

        bool NnMainExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {

            RPath logFilePath = NnMainPath.SubPath(NnAgent.logFileName);
            
            var tsLog = Util.StartLogParser<NnMainLog>(logFilePath, ct, SetStatus);

            NnAgent.RunNnStructure(
                NnMainPath, Content, ct, Type
            );
            NnAgent.RunNn(
                NnMainPath, Content, ct, Type
            );

            tsLog.Cancel();

            string? report = NnAgent.GenerateNnReport(
                NnMainPath, ct, Type
            );

            if (report != null)
                File.WriteAllText(NnMainReportPath, report);

            // if (File.Exists(NnMainNonSCToken))
            //     File.Delete(NnMainNonSCToken);

            return !ct.IsCancellationRequested;
        }
    }

    class NnMainLog : LogBase {
        int quantumPossionItCount = 0;

        // FIXME: complete it!
        public override string? Push(string line) {
            string match;
            if ((match = Regex.Match(line, @"(Newton step: \d+)").Value) != "") {
                int newtonStep = Int32.Parse(
                    Regex.Split(match, " ")
                    .Where(s => s != String.Empty).ElementAt(2)
                );
                return $"Q.Poisson #{quantumPossionItCount}, Newton #{newtonStep.ToString()}";
            } else if ((match = Regex.Match(line, @"(QUANTUM-POISSON:  its = \d+)").Value) != "") {
                quantumPossionItCount = Int32.Parse(
                    Regex.Split(match, "[ ]+")
                    .Where(s => s != String.Empty).ElementAt(3)
                );
                return null;
            } else return null;
        }
    }
}