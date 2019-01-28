using System;
using System.Threading;
using System.Threading.Tasks;

using System.Collections.Generic;

using System.Runtime.Serialization;

using System.ComponentModel;

namespace NnManager {

    using RPath = Util.RestrictedPath;

    public partial class Project {

        [Serializable]
        class NnTask : INotifyPropertyChanged {
            // INFO: NnTask is not aware of the directory where actual execution takes place. It only stores NN input content and execution status.

            #region INotifyPropertyChanged
            public event PropertyChangedEventHandler PropertyChanged;

            void OnPropertyChanged(string str)
            {
                OnPropertyChanged(new PropertyChangedEventArgs(str));
            }

            protected void OnPropertyChanged(PropertyChangedEventArgs e)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                    handler(this, e);
            }
            #endregion

            readonly string name;
            readonly string content;
            readonly Dictionary<string, string> param;

            string outputHash;

            // [NonSerialized]
            // Task task;

            enum NnTaskStatus {
                New,
                Running,
                Done,
                Error
            };

            // TODO: Status should be determined when loaded
            [NonSerialized]
            NnTaskStatus status;
            NnTaskStatus Status {
                get {
                    return status;
                }
                set {
                    if (value != status) {
                        status = value;
                        OnPropertyChanged("Status");
                    }
                }
            }

            public string GetStatus()
            {
                return Status.ToString();
            }

            // TODO: All File system related operations should be encapsulated in Utility.

            public NnTask(
                string content,
                Dictionary<string, string> param):
                    this(
                        content,
                        param,
                        ""
                    ) {}
                

            public NnTask(
                string content,
                Dictionary<string, string> param,
                string name) {
                    this.content = content;
                    this.param = param;
                    this.name = name;

                    this.outputHash = null;

                    // this.task = null;
                    this.Status = NnTaskStatus.New;
                }

            public string GetName() {
                return name;
            }

            // Check for integrity
            public void OutputValidation(
                RPath path
            ) {
                if (outputHash == null) return;

                string newHash = Util.HashPath(path);

                if (newHash == outputHash)
                    Status = NnTaskStatus.Done; 
            }

            // public void Reset() {
            //     this.outputHash = null;
            //     this.task = null;
            //     this.status = Status.New;
            // }

            public void Launch(
                RPath path
            ) {
                try
                {
                    Status = NnTaskStatus.Running;

                    NnAgent.InitNnFolder(
                        path, 
                        content
                    );

                    if (NnAgent.CheckNn(path, true)){
                        // FIXME: testing 
                        // NnAgent.RunNn(path, true);
                        NnAgent.RunNn(path);
                        // TODO: parse log to look for errors & warnings (to decide status)
                        Status = NnTaskStatus.Done;

                        outputHash = Util.HashPath(path);
                    } else {
                        Status = NnTaskStatus.Error;
                    }
                }
                catch
                {             
                    Status = NnTaskStatus.Error;       
                    throw new Exception("Exception encountered in Launch (NnTask)!");
                }                
            }
        }
    }
}
