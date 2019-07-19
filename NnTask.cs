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
    using ModuleOptionDict = Dictionary<ModuleType, Dictionary<string, string>>;

    public partial class NnTask : Notifier, INotifyPropertyChanged {

        public string Name { get; }
        public NnType Type { get; }
        public RPath FSPath { get; }
        public string Content { get; }

        public ModuleOptionDict BuiltInModuleOptions { get; }

        string? status;
        public string? Status {
            get => status;
            private set => SetField(ref status, value);
        }
        public void SetStatus(string? s) => Status = s;

        NnModuleRecord? currentModule;
        public NnModuleRecord? CurrentModule {
            get => currentModule;
            private set => SetField(ref currentModule, value);
        }

        List<NnModuleRecord> moduleDone;
        public IEnumerable<NnModuleRecord> ModuleDone => moduleDone;

        ConcurrentQueue<NnModuleRecord> moduleQueue;
        public IEnumerable<NnModuleRecord> ModuleQueue => moduleQueue;

        public IEnumerable<NnModuleRecord> Modules {
            get {
                foreach (var item in moduleQueue)
                    yield return item;
                foreach (var item in moduleDone)
                    yield return item;
                if (currentModule != null) yield return currentModule;
            }
        }

        Task? task;

        CancellationTokenSource? ts;
        CancellationTokenSource Ts =>
            ts ?? (ts = new CancellationTokenSource());

        public NnTask(
            string name,
            NnType type,
            RPath path,
            string content,
            ModuleOptionDict dict
        ) : this(name, type, path, content, dict, new List<NnModuleRecord>(), new List<NnModuleRecord>()) {}

        NnTask(
            string name,
            NnType type,
            RPath path,
            string content,
            ModuleOptionDict dict,
            List<NnModuleRecord> moduleDone,
            List<NnModuleRecord> moduleQueue
        ) {
            this.Name = name;
            this.Type = type;
            this.FSPath = path;
            this.Content = content;
            this.BuiltInModuleOptions = dict;
            this.currentModule = null;
            this.moduleDone = moduleDone;
            this.moduleQueue = new ConcurrentQueue<NnModuleRecord>(moduleQueue);

            Save();
        }

        [Serializable]
        struct SaveData {
            public SaveData(NnTask task) {
                name = task.Name;
                type = task.Type;
                content = task.Content;
                moduleOptions = task.BuiltInModuleOptions;
                modulesDone = task.moduleDone;
                moduleQueue = task.moduleQueue.ToList();
            }
            readonly public string name;
            readonly public NnType type;
            readonly public string content;
            readonly public ModuleOptionDict moduleOptions;
            readonly public List<NnModuleRecord> modulesDone;
            readonly public List<NnModuleRecord> moduleQueue;
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
                    taskData.type, 
                    path, 
                    taskData.content,
                    taskData.moduleOptions,
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
            NnModuleRecord module;
            while (moduleQueue.Count > 0) {
                moduleQueue.TryDequeue(out module);
            }
            // FIXME: COUPLED to view!!!
            OnPropertyChanged("CollectionModuleQueue");
        }

        public void QueueModule(
            NnModuleRecord module
        ) {
            moduleQueue.Enqueue(module);
            OnPropertyChanged("CollectionModuleQueue");
        }

        public bool ClearModules() {
            try {
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
            } catch {
                return false;
            }
        }

        public bool TryDequeueAndRunModule() {
            if (moduleQueue.Count == 0)
                return false;

            if (IsBusy())
                return false;

            NnModuleRecord modulePeeked;
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

            // if (task != null)
            //     if (!task.IsCompleted)
            //         return true;

            return false;
        }

        public void Execute(
            NnModuleRecord moduleData
        ) {
            if (IsBusy()) return;

            NnModule module = GetModule(moduleData.Type, moduleData.Options.ToImmutableDictionary());

            if (!module.CanExecute()) return;

            CurrentModule = moduleData;            
            OnPropertyChanged("CollectionModuleQueue");
            task = Task.Run(
                () => {
                    try {
                        OnPropertyChanged("Status");
                        ts = new CancellationTokenSource();
                        if (module.Execute(Ts.Token)) 
                            moduleData.SetResult(module.GetResult());
                        else moduleData.SetResult("(error)");
                    } catch { 
                        moduleData.SetResult("(error)");
                    } finally {
                        NnModuleRecord? data = null;
                        while (data == null)
                            moduleQueue.TryDequeue(out data);
                        moduleDone.Add(data);

                        CurrentModule = null;
                        Status = null;

                        OnPropertyChanged("Status");                        
                        OnPropertyChanged("CollectionModuleQueue");
                    }
                    Save();
                }
            );
        }

        public void Terminate() {
            if (!IsBusy()) return;
            Ts.Cancel();
            OnPropertyChanged("Status");                        
            OnPropertyChanged("CollectionModuleQueue");
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