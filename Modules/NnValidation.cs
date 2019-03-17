// using System.Threading;

// namespace NnManager {
//     using RPath = Util.RestrictedPath;

//     partial class NnTask {
//         NnModule NnValidation {
//             get {
//                 return new NnModule(
//                     NnValidationExecute,
//                     NnValidationCanExecute);
//             }
//         }

//         public bool NnValidationCanExecute() {
//             return true;
//         }

//         public bool NnValidationExecute(CancellationToken ct) {
//             NnAgent.RunNnStructure(
//                 path.SubPath("Validation"),
//                 Content,
//                 ct
//             );

//             if (true) // Validation success
//                 return true;
//         }
//     }
// }