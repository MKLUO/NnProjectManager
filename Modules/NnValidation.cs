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
                return true;
            }

            public bool NnValidationExecute() {
                NnAgent.RunNnStructure(path.SubPath("Validation"));

                // TODO: 
                if (true) // Validation success
                    return true;
            }
        }
    }
}