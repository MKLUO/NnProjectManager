using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    partial class NnTask {

        RPath TestOpenNotepadPath => FSPath.SubPath("Test");

        NnModule TestOpenNotepad(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "OpenNotepad",
                () => true,
                () => true,
                TestOpenNotepadExecute,
                () => "(done)",
                TestOpenNotepadDefaultOption.ToImmutableDictionary(),
                options);
            
        public static ImmutableDictionary<string, string> TestOpenNotepadDefaultOption = 
            new Dictionary<string, string>{
                {"TestOption","Yes"}
            }.ToImmutableDictionary();

        bool TestOpenNotepadExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {

            RPath logFilePath = TestOpenNotepadPath.SubPath(NnAgent.logFileName);

            var tsLog = Util.StartLogParser<NnMainLog>(logFilePath, ct, SetStatus);

            Util.StartAndWaitProcess(
                "notepad",
                "",
                ct
            );
            
            tsLog.Cancel();

            return !ct.IsCancellationRequested;
        }
    }

    class TestOpenNotepadLog : LogBase {
        public override string? Push(string line) => null;
    }
}