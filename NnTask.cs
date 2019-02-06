using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace NnManager {

    using RPath = Util.RestrictedPath;

    public partial class Project {

        [Serializable]
        public partial class NnTask : INotifyPropertyChanged {
            // INFO: NnTask is aware of the directory where actual execution takes place. It stores NN input content and module execution status.

            #region INotifyPropertyChanged
            [field : NonSerialized]
            public event PropertyChangedEventHandler PropertyChanged;

            void OnPropertyChanged(string str) {
                OnPropertyChanged(new PropertyChangedEventArgs(str));
            }

            protected void OnPropertyChanged(PropertyChangedEventArgs e) {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                    handler(this, e);
            }
            #endregion

            [Serializable]
            public enum NnTaskStatus {
                New,
                Done,
                Error
            }

            readonly RPath path;
            readonly string content;

            Dictionary<string, object> info;
            public Dictionary<string, object> Info {
                get { return info; }
                private set { }
            }

            [NonSerialized]
            Task task;

            [NonSerialized]
            NnTaskStatus status;
            public NnTaskStatus Status {
                get {
                    return status;
                }
                private set {
                    if (value != status) {
                        status = value;
                        OnPropertyChanged("Status");
                    }
                }
            }

            NnTask() {
                RegisterModules();
                CurrentModule = null;
                this.Status = NnTaskStatus.New;
            }

            // public NnTask(
            //     string content,
            //     RPath path) : this(content, path, null) { }

            public NnTask(
                string content,
                RPath path,
                Dictionary<string, object> info
            ) : this() {
                this.content = content;
                this.info = info;
                this.path = path;
            }

            string currentModule;
            public string CurrentModule {
                get {
                    return currentModule;
                }
                private set {
                    if (value != currentModule)
                        currentModule = value;

                    OnPropertyChanged("CurrentModule");
                }
            }

            [NonSerialized]
            ConcurrentQueue<string> moduleQueue;
            public ConcurrentQueue<string> ModuleQueue {
                get { 
                    if (moduleQueue == null)
                        moduleQueue = new ConcurrentQueue<string>();
                    return moduleQueue; 
                }
                private set { }
            }

            public void QueueModule(string moduleName) {
                if (!modules.ContainsKey(moduleName))
                    throw new Exception("Module \"" + moduleName + "\" does not exist!");

                ModuleQueue.Enqueue(moduleName);
            }

            public string DequeueAndRunModule() {
                if (ModuleQueue.Count == 0)
                    return null;

                string modulePeeked;
                if (!ModuleQueue.TryPeek(out modulePeeked))
                    return null;

                if (!CanExecute(modulePeeked))
                    return null;

                string modulePoped;
                if (!ModuleQueue.TryDequeue(out modulePoped))
                    return null;

                Execute(modulePoped);

                return modulePoped;
            }

            public bool CanExecute(
                string moduleName
            ) {
                if (!modules.ContainsKey(moduleName))
                    return false;

                if (!modules[moduleName].CanExecute())
                    return false;

                if (task != null)
                    if (!task.IsCompleted)
                        return false;

                return true;
            }

            public void Execute(
                string moduleName
            ) {
                if (!CanExecute(moduleName))
                    throw new Exception("Module \"" + moduleName + "\" can't execute!");

                task = Task.Run(
                    () => {
                        CurrentModule = moduleName;
                        try {
                            modules[moduleName].Execute();
                        } catch {
                            throw new Exception("Exception in module \"" + moduleName + "\"!");
                        } finally {
                            CurrentModule = null;
                        }
                    }
                );
            }

            // BLOCKING!
            public void WaitForTask() {
                task?.Wait();
            }
        }
    }
}