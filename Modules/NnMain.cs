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

        public NnModule NnMain(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "NN Main",
                NnMainCanExecute, 
                NnMainIsDone, 
                NnMainExecute, 
                NnMainDefaultOption.ToImmutableDictionary(), 
                options);

        // FIXME:
        public static ImmutableDictionary<string, string> NnMainDefaultOption = 
            new Dictionary<string, string>().ToImmutableDictionary();

        bool NnMainCanExecute() {
            // FIXME: 
            return true;
        }

        bool NnMainIsDone() {
            // FIXME: Do hashing after execution.
            // return Directory.Exists(path.SubPath("main", false));
            return true;
        }

        bool NnMainExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {

            RPath logFilePath = NnMainPath.SubPath(NnAgent.logFileName);
            
            var tsLog = Util.StartLogParser<NnMainLog>(logFilePath, ct, SetStatus);

            NnAgent.RunNnStructure(
                NnMainPath, Content, ct
            );
            NnAgent.RunNn(
                NnMainPath, Content, ct
            );

            tsLog.Cancel();

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