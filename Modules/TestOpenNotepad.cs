using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public partial class Project {
        public partial class NnTask {
            public NnModule TestOpenNotepad {
                get {
                    return new NnModule(
                        TestOpenNotepadExecute,
                        TestOpenNotepadCanExecute,
                        () => true);
                }
            }

            bool TestOpenNotepadCanExecute() {
                return true;
            }

            bool TestOpenNotepadExecute(CancellationToken ct) {
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
}