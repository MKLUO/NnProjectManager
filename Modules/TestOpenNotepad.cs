using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    partial class NnTask {
        NnModule TestOpenNotepad(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "OpenNotepad",
                () => true,
                () => true,
                TestOpenNotepadExecute,
                () => "",
                TestOpenNotepadDefaultOption.ToImmutableDictionary(),
                options);
            
        public static ImmutableDictionary<string, string> TestOpenNotepadDefaultOption = 
            new Dictionary<string, string>{
                {"TestOption","Yes"}
            }.ToImmutableDictionary();

        bool TestOpenNotepadExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {

            Util.StartAndWaitProcess(
                "notepad",
                "",
                ct
            );

            if (ct.IsCancellationRequested) return false;
            return true;
        }
    }
}