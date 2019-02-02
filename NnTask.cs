using System;
using System.Threading;
using System.Threading.Tasks;

using System.Collections.Generic;

using System.Runtime.Serialization;

using System.ComponentModel;
using System.IO;

namespace NnManager
{

    using RPath = Util.RestrictedPath;

    public partial class Project
    {

        [Serializable]
        class NnTask : INotifyPropertyChanged
        {
            // INFO: NnTask is not aware of the directory where actual execution takes place. It only stores NN input content and execution status.

            #region INotifyPropertyChanged
            [field: NonSerialized]
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

            [Serializable]
            enum NnTaskStatus
            {
                New,
                Running,
                Done,
                Error
            };

            readonly string name;
            readonly string content;
            string executionPath;

            // readonly Dictionary<string, (string, string)> param;
            // readonly Template template;

            // string outputHash;

            readonly Dictionary<string, object> info;

            // [NonSerialized]
            // Task task;

            // TODO: Status should be determined when loaded
            //[NonSerialized]
            NnTaskStatus status;
            NnTaskStatus Status
            {
                get
                {
                    return status;
                }
                set
                {
                    if (value != status)
                    {
                        status = value;
                        OnPropertyChanged("Status");
                    }
                }
            }

            public string GetStatus()
            {
                return Status.ToString();
            }

            public Dictionary<string, object> GetInfo()
            {
                return info;
            }



            // TODO: All File system related operations should be encapsulated in Utility.

            // public NnTask(
            //     string content,
            //     Dictionary<string, (string, string)> param) :
            //         this(
            //             content,
            //             param,
            //             ""
            //         )
            // { }

            public NnTask(
                string name,
                string content,
                Dictionary<string, object> info
                )
            {
                this.content = content;
                this.info = info;
                this.name = name;

                // this.outputHash = null;

                // this.task = null;
                this.Status = NnTaskStatus.New;
            }

            // public NnTask(
            //     Template template,
            //     Dictionary<string, (string, string)> param,
            //     string name)
            // {
            //     this.template = template;
            //     this.param = param;
            //     this.name = name;

            //     // this.outputHash = null;

            //     // this.task = null;
            //     this.Status = NnTaskStatus.New;
            // }

            public string GetName()
            {
                return name;
            }

            // Check for integrity
            public void OutputValidation(
                RPath path
            )
            {
                // if (outputHash == null) return;

                // TODO: Hashing take too much time. Maybe integration check is not necessery?
                // string newHash = Util.HashPath(path.ToString());

                // if (newHash == outputHash)
                //     Status = NnTaskStatus.Done; 
                switch (Status) {
                    case NnTaskStatus.Done:
                        if (!Directory.Exists(path.ToString()))
                            this.Status = NnTaskStatus.New;

                        break;

                    default:
                        break;
                }

                
            }

            // public void Reset() {
            //     this.Status = NnTaskStatus.New;
            // }

            public void Launch(
                RPath path,
                bool testing = false
            )
            {
                try
                {
                    Status = NnTaskStatus.Running;

                    if (NnAgent.CheckNn(path, content, testing))
                    {
                        executionPath = path.ToString();
                        // FIXME: testing 
                        NnAgent.RunNn(path, content, testing);
                        // TODO: parse log to look for errors & warnings (to decide status)
                        Status = NnTaskStatus.Done;

                        //outputHash = Util.HashPath(path.ToString());
                    }
                    else
                    {
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
