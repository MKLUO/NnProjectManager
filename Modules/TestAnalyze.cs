using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public partial class Project {
        public partial class NnTask {
            public NnModule TestAnalyze {
                get {
                    return new NnModule(
                        TestAnalyzeExecute,
                        TestAnalyzeCanExecute);
                }
            }

            public bool TestAnalyzeCanExecute() {
                return moduleDone.Contains("Test");
            }

            public bool TestAnalyzeExecute(CancellationToken ct) {
                string logFilePath = path.SubPath("test").SubPath("log.txt");

                Task.Delay(500).Wait();

                using(FileStream stream = File.OpenRead(logFilePath)) {
                    StreamReader sr = new StreamReader(stream);
                    string line;
                    string result = "";
                    while ((line = sr.ReadLine()) != null) {
                        result += line[0];
                    }
                    Log(result);
                }

                return true;
            }
        }
    }
}