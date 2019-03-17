using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;
    using ModuleData = Tuple<ModuleType, Dictionary<string, string>>;

    public partial class NnTask : Notifier, INotifyPropertyChanged {

        public string Name { get; }
        public RPath FSPath { get; }
        public string Content { get; }

        string? status;
        public string? Status {
            get => status;
            private set => SetField(ref status, value);
        }
        public void SetStatus(string? s) => Status = s;

        ModuleData? currentModule;
        public ModuleData? CurrentModule {
            get => currentModule;
            private set => SetField(ref currentModule, value);
        }

        List<ModuleData> moduleDone;
        public IEnumerable<ModuleData> ModuleDone => moduleDone;

        ConcurrentQueue<ModuleData> moduleQueue;
        public IEnumerable<ModuleData> ModuleQueue => moduleQueue;

        Task? task;

        CancellationTokenSource? ts;
        CancellationTokenSource Ts =>
            ts ?? (ts = new CancellationTokenSource());

        public NnTask(
            string name,
            RPath path,
            string content
        ) : this(name, path, content, new List<ModuleData>(), new List<ModuleData>()) {}

        NnTask(
            string name,
            RPath path,
            string content,
            List<ModuleData> moduleDone,
            List<ModuleData> moduleQueue
        ) {
            this.Name = name;
            this.FSPath = path;
            this.Content = content;
            this.currentModule = null;
            this.moduleDone = moduleDone;
            this.moduleQueue = new ConcurrentQueue<ModuleData>(moduleQueue);

            Save();
        }

        [Serializable]
        struct SaveData {
            public SaveData(NnTask task) {
                name = task.Name;
                content = task.Content;
                modulesDone = task.moduleDone;
                moduleQueue = task.moduleQueue.ToList();
            }
            readonly public string name;
            readonly public string content;
            readonly public List<ModuleData> modulesDone;
            readonly public List<ModuleData> moduleQueue;
        }

        public void Save() {
            Util.SerializeToFile(
                new SaveData(this),
                FSPath.SubPath(NnAgent.taskFileName)
            );
        }

        public static NnTask? Load(RPath path) {
            try {
                var taskData = 
                    (SaveData) Util.DeserializeFromFile(
                        path.SubPath(NnAgent.taskFileName)
                    );

                NnTask newTask = new NnTask(
                    taskData.name, 
                    path, 
                    taskData.content, 
                    taskData.modulesDone, 
                    taskData.moduleQueue);                

                newTask.Validation();
                return newTask;

            } catch {
                Util.ErrorHappend("Error while loading task!");
                return null;
            }
        }

        public bool Equals(NnTask task) =>
            Content == task.Content;

        void ClearModuleQueue() {
            ModuleData module;
            while (moduleQueue.Count > 0) {
                moduleQueue.TryDequeue(out module);
            }
            OnPropertyChanged("ModuleQueue");
        }

        public void QueueModule(
            ModuleData module
        ) {
            moduleQueue.Enqueue(module);
            OnPropertyChanged("ModuleQueue");
        }

        public bool ClearModules() {
            if (IsBusy())
                if (Util.WarnAndDecide("Selected task is busy rn. Terminate and clear remaining modules?")) {
                    ClearModuleQueue();
                    Terminate();
                    return true;
                } else return false;
                
            else {
                ClearModuleQueue();                
                return true;
            }
        }

        public bool TryDequeueAndRunModule() {
            if (moduleQueue.Count == 0)
                return false;

            if (IsBusy())
                return false;

            ModuleData modulePeeked;
            // if (!moduleQueue.TryDequeue(out modulePoped))
            //     return false;
            if (!moduleQueue.TryPeek(out modulePeeked))
                return false;
            
            Execute(modulePeeked);

            return true;
        }

        public bool IsBusy() {
            if (CurrentModule != null)
                return true;

            if (task != null)
                if (!task.IsCompleted)
                    return true;

            return false;
        }

        public void Execute(
            ModuleData moduleData
        ) {
            if (IsBusy()) return;

            NnModule module = GetModule(moduleData.Item1, moduleData.Item2.ToImmutableDictionary());

            if (!module.CanExecute()) return;

            CurrentModule = moduleData;            
            OnPropertyChanged("ModuleQueue");
            task = Task.Run(
                () => {
                    try {
                        ts = new CancellationTokenSource();
                        if (module.Execute(Ts.Token)) 
                            // if (!moduleDone.Contains(moduleData)) {
                            //     moduleDone.Add(module);
                            //     OnPropertyChanged("ModuleDone");
                            // }
                            moduleDone.Add(moduleData);

                        ModuleData? data = null;
                        while (data == null)
                            moduleQueue.TryDequeue(out data);
                    } catch {
                        //Log("Exception in execution of module \"" + module.Name + "\"!");
                        ModuleData? data = null;
                        while (data == null)
                            moduleQueue.TryDequeue(out data);
                    } finally {
                        CurrentModule = null;
                        Status = null;
                        OnPropertyChanged("ModuleDone");
                        OnPropertyChanged("ModuleQueue");
                    }
                    Save();
                }
            );
        }

        public void Terminate() {
            if (!IsBusy()) return;
            Ts.Cancel();
        }

        public void Validation() {
            // CurrentModule = "Validating...";
            task = Task.Run(
                () => {
                    try {
                        // FIXME: Validation is turned off here
                        // foreach (NnModule module in 
                        //     moduleDone.Where(x => !x.IsDone()).ToList()) {
                        //     moduleDone.Remove(module);
                        // }
                    } catch {
                        Reset();
                        throw new Exception("Exception in validation!");
                    } finally {
                        CurrentModule = null;
                    }
                }
            );
        }

        public void Reset() {
            moduleDone.Clear();
            Save();
        }
    }
}