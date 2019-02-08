using System;
using System.IO;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public partial class Project {
        public partial class NnTask {
            public NnModule Test {
                get {
                    return new NnModule(
                        TestExecute,
                        TestCanExecute,
                        () => true);
                }
            }

            public bool TestCanExecute() {
                return true;
            }

            public bool TestExecute() {
                NnAgent.InitNnFolder(path.SubPath("test"), content);

                string logFilePath = path.SubPath("test").SubPath("log.txt");

                using(FileStream stream = File.Create(logFilePath)) {
                    StreamWriter sw = new StreamWriter(stream);
                    Random random = new Random();
                    for (int i = 0; i < 10; ++i) {
                        sw.WriteLine(Util.RandomString(20));
                        sw.Flush();
                        Status = "Writing line " + i.ToString() + " ...";
                        System.Threading.Thread.Sleep(
                            random.Next(1000, 2000)
                        );
                    }
                    sw.Close();
                }

                return true;
            }
        }
    }
}