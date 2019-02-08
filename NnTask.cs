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

            void OnPropertyChanged(PropertyChangedEventArgs e) {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                    handler(this, e);
            }
            #endregion

            #region Log

            public delegate void LogFiredEventHandler(NnTask task, string str);
            [field : NonSerialized]
            public event LogFiredEventHandler LogFired;
            void Log(string msg) {
                LogFiredEventHandler handler = LogFired;
                if (handler != null)
                    handler(this, msg);
            }

            #endregion

            #region status
            [NonSerialized]
            string status;
            public string Status {
                get { return status ?? ""; }
                private set {
                    if (value != status) {
                        status = value;
                        OnPropertyChanged("Status");
                    }
                }
            }
            #endregion

            // [Serializable]
            // public enum NnTaskStatus {
            //     New,
            //     Done,
            //     Error
            // }

            readonly RPath path;
            readonly string content;

            // The only stateful data in NnTask
            HashSet<string> moduleDone;

            Dictionary<string, object> info;
            public Dictionary<string, object> Info {
                get { return info; }
                private set { }
            }

            [NonSerialized]
            Task task;            

            // [NonSerialized]
            // NnTaskStatus status;
            // public NnTaskStatus Status {
            //     get {
            //         return status;
            //     }
            //     private set {
            //         if (value != status) {
            //             status = value;
            //             OnPropertyChanged("Status");
            //         }
            //     }
            // }

            // public NnTask(
            //     string content,
            //     RPath path) : this(content, path, null) { }

            public NnTask(
                string content,
                RPath path,
                Dictionary<string, object> info
            ) {
                this.content = content;
                this.info = info;
                this.path = path;

                RegisterModules();
                CurrentModule = null;
                moduleDone = new HashSet<string>();
            }

            public void Save() {
                Util.SerializeToFile(
                    this,
                    path.SubPath(NnAgent.taskFileName)
                );
            }

            public static NnTask Load(RPath path) {
                return (NnTask) Util.DeserializeFromFile(
                    path.SubPath(NnAgent.taskFileName)
                );
            }

            [NonSerialized]
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

            public void TryDequeueAndRunModule() {
                if (ModuleQueue.Count == 0)
                    return;

                string modulePeeked;
                string modulePoped;

                // if (!IsNotBusy(modulePeeked))
                if (!IsNotBusy())
                    return;

                if (!ModuleQueue.TryPeek(out modulePeeked))
                    return;

                if (!modules[modulePeeked].CanExecute()) {
                    ModuleQueue.TryDequeue(out modulePeeked);
                    OnPropertyChanged("ModuleQueue");
                    Log("Module \"" + modulePeeked + "\" can't execute!");
                    return;
                }
                
                if (!ModuleQueue.TryDequeue(out modulePoped))
                    return;

                OnPropertyChanged("ModuleQueue");
                Execute(modulePoped);
            }

            public bool IsNotBusy(
                // string moduleName
            ) {
                // if (!modules.ContainsKey(moduleName))
                //     return false;

                if (CurrentModule != null)
                    return false;

                if (task != null)
                    if (!task.IsCompleted)
                        return false;

                return true;
            }

            public void Execute(
                string moduleName
            ) {
                if (!IsNotBusy())
                    throw new Exception("Busy!");

                if (!modules[moduleName].CanExecute()) {
                    Log("Module \"" + moduleName + "\" can't execute!");
                    return;
                }
                    // Returns as if nothing happened (Stateless)

                CurrentModule = moduleName;
                Log("Module \"" + moduleName + "\" launched...");
                task = Task.Run(
                    () => {
                        try {
                            if (modules[moduleName].Execute()) {
                                moduleDone.Add(moduleName);
                                Log("Module \"" + moduleName + "\" successes!");
                            } else {
                                Log("Module \"" + moduleName + "\" failed!");
                            }
                        } catch {
                            throw new Exception("Exception in execution of module \"" + moduleName + "\"!");
                        } finally {
                            CurrentModule = null;
                            Status = null;
                        }
                        // Successful task (NOT necessarily successful module run!)                        
                        Save();
                    }
                );
            }

            public void Restore() {
                CurrentModule = "Restoring...";
                task = Task.Run(
                    () => {
                        try {
                            // Thread.Sleep(500);
                            List<string> moduleDoneRemove = new List<string>();
                            foreach (string moduleName in moduleDone) {
                                // Thread.Sleep(500);
                                if (!modules[moduleName].Restore())
                                    moduleDoneRemove.Add(moduleName); // Restore fail
                            }
                            foreach (string rmv in moduleDoneRemove) {
                                moduleDone.Remove(rmv);
                            }
                        } catch {
                            moduleDone.Clear();
                            throw new Exception("Exception in module restoration!");
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