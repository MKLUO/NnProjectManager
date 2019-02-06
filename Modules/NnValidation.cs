namespace NnManager {
    using RPath = Util.RestrictedPath;

    public partial class Project {
        public partial class NnTask {
            public NnModule NnValidation {
                get {
                    return new NnModule(
                        NnValidationExecute,
                        NnValidationCanExecute);
                }
            }

            public bool NnValidationCanExecute() {
                return Status == NnTaskStatus.New;
            }

            public void NnValidationExecute() {
                try {
                    NnAgent.RunNnStructure(path);
                } catch {
                    Status = NnTaskStatus.Error;
                }
            }
        }
    }
}