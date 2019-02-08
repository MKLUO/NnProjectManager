using System;
using System.IO;

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

            public bool TestAnalyzeExecute() {
                string logFilePath = path.SubPath("test").SubPath("log.txt");

                using(FileStream stream = File.OpenRead(logFilePath)) {
                    StreamReader sr = new StreamReader(stream);
                    Random random = new Random();
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