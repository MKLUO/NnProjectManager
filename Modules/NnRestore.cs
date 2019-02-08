using System.IO;
using System.Threading;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    // public partial class Project {
    //     public partial class NnTask {
            
    //         public NnModule NnRestore {
    //             get {
    //                 return new NnModule(
    //                     NnRestoreExecute,
    //                     NnRestoreCanExecute);
    //             }
    //         }

    //         public bool NnRestoreCanExecute() {
    //             return Status == NnTaskStatus.New;
    //         }

    //         public void NnRestoreExecute() {
    //             // TODO: Hashing take too much time. Maybe integration check is not necessery?
    //             // if (outputHash == null) return;
    //             // string newHash = Util.HashPath(path);                
    //             // if (newHash == outputHash)
    //             //     Status = NnTaskStatus.Done; 

    //             Thread.Sleep(3000);

    //             if (Directory.Exists(path))
    //                 Status = NnTaskStatus.Done;
    //             else
    //                 Status = NnTaskStatus.New;
    //         }
    //     }
    // }
}