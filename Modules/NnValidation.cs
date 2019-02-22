using System.Threading;

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

            public bool NnValidationExecute(CancellationToken ct) {
                NnAgent.RunNnStructure(
                    path.SubPath("Validation"),
                    content,
                    ct
                );

                // TODO: 
                if (true) // Validation success
                    return true;
            }
        }
    }
}