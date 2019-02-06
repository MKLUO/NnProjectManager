using System.IO;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public partial class Project {
        public partial class NnTask {
            public NnModule Test {
                get {
                    return new NnModule(
                        TestExecute,
                        TestCanExecute);
                }
            }

            public bool TestCanExecute() {
                return true;
            }

            public void TestExecute() {
                NnAgent.InitNnFolder(path, content);

                string logFilePath = path.SubPath("log.txt");

                using(FileStream stream = File.Create(logFilePath)) {
                    StreamWriter sw = new StreamWriter(stream);
                    for (int i = 0; i < 10; ++i) {
                        sw.WriteLine(Util.RandomString(20));
                        sw.Flush();
                        System.Threading.Thread.Sleep(500);
                    }
                    sw.Close();
                }

                File.Create(path.SubPath("done.txt")).Close();
                Status = NnTaskStatus.Done;
            }
        }
    }
}