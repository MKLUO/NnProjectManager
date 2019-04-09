using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;




#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public partial class NnPlan : Notifier, INotifyPropertyChanged {

        public string Name { get; }
        public RPath FSPath { get; }
        public NnTemplate Template { get; }
        public PlanType Type { get; }
        // public ImmutableDictionary<string, string> ? Consts { get; private set; }

        Dictionary<NnParam, NnTask> tasks;
        // public IEnumerable<NnTask> Tasks => tasks.Values;
        public ImmutableDictionary<NnParam, NnTask> Tasks => 
            tasks.ToImmutableDictionary();

        public int RunningTasks => tasks.Where(t => t.Value.IsBusy()).ToList().Count;

        public NnPlan(
            string name,
            RPath path,
            NnTemplate template,
            PlanType type = PlanType.NoPlan
        ) {
            this.Name = name;
            this.FSPath = path;
            this.Template = template;
            this.Type = type;

            tasks = new Dictionary<NnParam, NnTask>();

            KernelInitialize();

            Save();
        }

        NnPlan(
            string name,
            RPath path,
            NnTemplate template,
            Dictionary<NnParam, NnTask> tasks,
            // ImmutableDictionary<string, string> ? consts,
            PlanType type
        ) {
            this.Name = name;
            this.FSPath = path;
            this.Template = template;
            this.tasks = tasks;
            // this.Consts = consts;
            this.Type = type;

            KernelInitialize();

            Save();
        }

        #region SaveAndLoad

        [Serializable]
        struct SaveData {
            public SaveData(NnPlan plan) {
                name = plan.Name;
                Type = plan.Type;
                taskIds = plan.tasks.ToDictionary(x => x.Key, x => x.Value.Name);                
                // consts = plan.Consts != null ?
                //     new Dictionary<string, string>(plan.Consts) :
                //     null;
            }

            readonly public string name;
            readonly public PlanType Type;
            readonly public Dictionary<NnParam, string> taskIds;
            // readonly public Dictionary<string, string> ? consts;
        }

        void Save() {
            Util.SerializeToFile(
                new SaveData(this),
                FSPath.SubPath(NnAgent.planFileName)
            );

            Template.Save(FSPath);
        }

        public static NnPlan? Load(RPath path) {
            try {
                var planData =
                    (SaveData) Util.DeserializeFromFile(
                        path.SubPath(NnAgent.planFileName)
                    );

                Dictionary<NnParam, NnTask> tasks = new Dictionary<NnParam, NnTask>();
                foreach (var taskId in planData.taskIds) {
                    NnTask? task = NnTask.Load(
                        path.SubPath("tasks").SubPath(taskId.Value)
                    );
                    if (task != null)
                        tasks[taskId.Key] = task;
                }

                NnTemplate? template = NnTemplate.Load(path);

                if (template == null) throw new Exception();

                NnPlan plan = new NnPlan(
                    planData.name,
                    path,
                    template,
                    tasks,
                    // planData.consts?.ToImmutableDictionary(),
                    planData.Type
                );

                return plan;
            } catch {
                Util.ErrorHappend($"Error while loading plan!");
                return null;
            }
        }
        #endregion

        public NnTask? AddTask(
            NnParam param
        ) => AddTask(new List<NnParam>{param});

        public List<NnTask> AddTask(
            List<NnParam> pars
        ) {
            var newTasks = new Dictionary<NnParam, NnTask>();
            try {
                foreach (var param in pars) {    
                    if (!param.Pad(Template))
                        continue;

                    foreach (var task in tasks)
                        if (task.Key.GetTag() == param.GetTag()) {
                            Util.ErrorHappend("Task with same parameter exists!");
                            continue;
                        }

                    string newContent;
                    string tag = param.GetTag(Template.Variables);
                    NnTask newTask =
                        new NnTask(
                            tag,
                            Template.Type,
                            FSPath.SubPath("tasks").SubPath(tag),
                            newContent = Template.GenerateContent(param.Variables)
                        );

                    foreach (var oldTask in tasks)
                        if (oldTask.Value.Equals(newTask)) {
                            Util.ErrorHappend("Same task exists!");
                            continue;
                        }

                    tasks[param] = newTask;
                    newTasks[param] = newTask;
                }

                
            } catch {
                Util.ErrorHappend("Error while adding task!");
                return new List<NnTask>();
            }

            try {
                OnPropertyChanged("Plan - AddTask");
                OnPropertyChanged("TaskAmount");
                OnPropertyChanged("BusyTaskAmount");

                Save();
                return newTasks.Values.ToList();
            } catch {

            }
        }

        public bool DeleteTask(
            NnTask task
        ) {
            try {
                if (task.IsBusy())
                    if (Util.WarnAndDecide("Selected task is busy rn. Terminate and delete?")) {
                        task.Terminate();
                    } else {
                        return false;
                    }

                foreach(var item in tasks.Where(kvp => kvp.Value == task).ToList())
                    tasks.Remove(item.Key);

                OnPropertyChanged("Plan - DeleteTask");
                OnPropertyChanged("TaskAmount");
                OnPropertyChanged("BusyTaskAmount");

                Save();
                return true;
            } catch {
                Util.ErrorHappend("Error while deleting task!");
                return false;
            }
        }

        Dictionary<string, string>? CommonDataCache;
        public Dictionary<string, string> CommonData {
            get {
                if (CommonDataCache == null) {
                    CommonDataCache = new Dictionary<string, string>();
                    UpdateKeysOfCommonData(
                        CommonDataCache,
                        tasks.Keys.ToList()
                    );
                }
                return CommonDataCache;
            }
        }

        static void UpdateKeysOfCommonData(Dictionary<string, string> dict, List<NnParam> pars) {
            if (pars.Count == 0) return;

            var newDict = new Dictionary<string, string>();
            foreach (var key in pars[0].Variables.Keys)
                if (pars.All(x => x.GetValue(key) == pars[0].GetValue(key)))
                    newList.Add(key);
        }

        public void TerminateAll() {
            foreach (var task in tasks.Values)
                task.Terminate();
        }

        // TODO: implement priority here
        public bool DoWork() {
            bool suc = kernel?.Step() ?? false;
            if (suc) OnPropertyChanged("BusyTaskAmount");
            return suc;
        }

        public bool IsBusy() {
            foreach (NnTask task in tasks.Values)
                if (task.IsBusy()) return true;
            return false;
        }

        // FIXME: Ignoring multiple module of same type!
        public string GetReport(ReportType type, Dictionary<string, string> options) {
            return Report(type, options).Execute();
        }
    }
}