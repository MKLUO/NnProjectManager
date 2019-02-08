using System.IO;

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
                return !moduleDone.Contains("NN Main");
            }

            public bool NnMainRestore() {
                return Directory.Exists(path);
            }

            public bool NnMainExecute() {
                NnAgent.InitNnFolder(path, content);
                NnAgent.RunNnStructure(path);
                NnAgent.RunNn(path);

                //TODO: parse nn log
                
                return true;
            }
        }
    }
}