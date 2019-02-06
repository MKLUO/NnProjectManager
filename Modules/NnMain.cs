namespace NnManager {
    using RPath = Util.RestrictedPath;

    public partial class Project {
        public partial class NnTask {
            public NnModule NnMain {
                get {
                    return new NnModule(
                        NnMainExecute,
                        NnMainCanExecute);
                }
            }

            public bool NnMainCanExecute() {
                return Status == NnTaskStatus.New;
            }

            public void NnMainExecute() {
                try {
                    NnAgent.InitNnFolder(path, content);
                    NnAgent.RunNnStructure(path);
                    NnAgent.RunNn(path);

                    Status = NnTaskStatus.Done;
                } catch {
                    Status = NnTaskStatus.Error;
                }
            }
        }
    }
}