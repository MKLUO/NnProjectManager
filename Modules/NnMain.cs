using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public partial class Project {
        public partial class NnTask {
            public NnModule NnMain {
                get {
                    return new NnModule(
                        NnMainExecute,
                        NnMainCanExecute,
                        NnMainRestore);
                }
            }

            public bool NnMainCanExecute() {
                // FIXME:
                // return !moduleDone.Contains("NN Main");
                return true;
            }

            public bool NnMainRestore() {
                // FIXME: Hashing to restore?
                return Directory.Exists(path.SubPath("main", false));
            }

            public bool NnMainExecute(CancellationToken ct) {
                RPath nnMainPath = path.SubPath("main");
                RPath logFilePath = nnMainPath.SubPath(NnAgent.logFileName);
                File.Create(logFilePath).Close();

                CancellationTokenSource tsLog = new CancellationTokenSource();
                Task logTask = Task.Run(
                    () => LogParser<NnMainLog>(logFilePath, tsLog.Token),
                    ct
                );

                NnAgent.RunNnStructure(
                    nnMainPath, content, ct
                );
                NnAgent.RunNn(
                    nnMainPath, content, ct
                );                

                tsLog.Cancel();
                logTask.Wait(10000);

                if (ct.IsCancellationRequested) return false;
                return true;
            }

            class NnMainLog : LogBase, ILog {
                public NnMainLog() { }
                public NnMainLog(Action<string> setStatus) : base(setStatus) { }

                int quantumPossionItCount = 0;

                public void Push(string line) {
                    string match;
                    if ((match = Regex.Match(line, @"(Newton step: \d+)").Value) != "") {
                        int newtonStep = Int32.Parse(
                            Regex.Split(match, " ")
                            .Where(s => s != String.Empty).ElementAt(2)
                        );
                        SetStatus(
                            "Q.Poisson #" + quantumPossionItCount.ToString() +
                            ", Newton #" + newtonStep.ToString()
                        );
                    } else if ((match = Regex.Match(line, @"(QUANTUM-POISSON:  its = \d+)").Value) != "") {
                        quantumPossionItCount = Int32.Parse(
                            Regex.Split(match, "[ ]+")
                            .Where(s => s != String.Empty).ElementAt(3)
                        );
                    }
                }
            }
        }
    }
}