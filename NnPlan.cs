using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
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
        ) {
            var tasks = AddTask(new List<NnParam> { param });
            if (tasks?.Count() > 0) return tasks[0];
            else return null;
        }

        public List<NnTask> AddTask(
            List<NnParam> pars
        ) {
            var newTasks = new Dictionary<NnParam, NnTask>();
            try {
                foreach (var param in pars) {
                    if (!param.Pad(Template))
                        continue;

                    string tag = param.GetTag(Template.Variables);
                    NnTask newTask =
                        new NnTask(
                            tag,
                            Template.Type,
                            FSPath.SubPath("tasks").SubPath(tag),
                            Template.GenerateContent(param.Variables),
                            Template.GenerateModuleOptions(param.Variables)
                        );

                    if (tasks.All(x => !x.Value.Equals(newTask)))
                        newTasks[param] = newTask;
                }
            } catch {
                Util.ErrorHappend("Error while creating task!");
                return new List<NnTask>();
            }

            try {
                foreach (var newTask in newTasks)
                    tasks[newTask.Key] = newTask.Value;

                UpdateCommonData(newTasks.Keys.ToList());

                OnPropertyChanged("Plan - AddTask");
                OnPropertyChanged("TaskAmount");
                OnPropertyChanged("BusyTaskAmount");

                Save();
                return newTasks.Values.ToList();
            } catch {
                Util.ErrorHappend("Error while adding task!");
                return newTasks.Values.ToList();
            }
        }

        public bool DeleteTask(
            NnTask task
        ) {
            bool success = false;
            try {
                if (task.IsBusy())
                    if (Util.WarnAndDecide("Selected task is busy rn. Terminate and delete?")) {
                        task.Terminate();
                    } else {
                        return false;
                    }

                foreach (var item in tasks.Where(kvp => kvp.Value == task).ToList()) {
                    tasks.Remove(item.Key);
                    string path = FSPath.SubPath("tasks").SubPath("_removed").SubPath(task.Name);
                    while (Directory.Exists(path))
                        path += "_";
                    Directory.Move(task.FSPath, path);
                    DeleteParamInCommonData(item.Key);
                }
                success = true;
            } catch {
                Util.ErrorHappend("Error while deleting task!");
                success = false;
            } finally {
                OnPropertyChanged("Plan - DeleteTask");
                OnPropertyChanged("TaskAmount");
                OnPropertyChanged("BusyTaskAmount");

                Save();
            }
            return success;
        }

        Dictionary<string, Dictionary<string, int>> ? paramDataStat;
        public List<string> CommonData {
            get {
                if (paramDataStat == null) {
                    paramDataStat = new Dictionary<string, Dictionary<string, int>>();
                    UpdateCommonData(tasks.Keys.ToList(), true);
                }

                var result = new List<string>();
                foreach (var param in paramDataStat)
                    if (param.Value.Count() <= 1)
                        result.Add(param.Key);

                return result;
            }
        }

        void UpdateCommonData(List<NnParam> pars, bool reset = false) {
            if (reset) paramDataStat = new Dictionary<string, Dictionary<string, int>>();
            if (pars.Count == 0) return;
            if (paramDataStat == null) return;
            foreach (var par in pars)
                foreach (var vari in par.Variables) {
                    if (!paramDataStat.ContainsKey(vari.Key))
                        paramDataStat[vari.Key] = new Dictionary<string, int>();
                    if (!paramDataStat[vari.Key].ContainsKey(vari.Value))
                        paramDataStat[vari.Key][vari.Value] = 1;
                    else
                        paramDataStat[vari.Key][vari.Value] += 1;
                }
        }

        void DeleteParamInCommonData(NnParam par) {
            if (paramDataStat == null) return;
            foreach (var vari in par.Variables) {
                if (paramDataStat.ContainsKey(vari.Key))
                    if (paramDataStat[vari.Key].ContainsKey(vari.Value)) {
                        paramDataStat[vari.Key][vari.Value] -= 1;
                        if (paramDataStat[vari.Key][vari.Value] <= 0)
                            paramDataStat[vari.Key].Remove(vari.Value);
                    }
            }
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
        // public string GetReport(ReportType type, Dictionary<string, string> options) {
        //     return Report(type, options).Execute();
        // }

        // FIXME: Temp func to get some report
        public void GenerateSomeReport0701() {

            string report = "EN,Vol_DET(V),Energy(eV)\n";
            List<(string detVol, string[] ene)> entries = new List<(string, string[] ene)>();
            foreach (var nnTask in Tasks) {
                var param = nnTask.Key;
                var task = nnTask.Value;

                if (task.GetEnergies() is string[] taskEnergies)
                    if (param.GetValue("Vol_DET") is string volDet)
                        entries.Add((volDet, taskEnergies));
            }

            foreach (var i in new int[]{0, 1, 2}) 
            foreach (var entry in entries) {
                report += i + "," + entry.detVol + "," + entry.ene[i] + "\n";
            }

            File.WriteAllText(FSPath.SubPath("AntiCrossing.txt"), report);
        }
    }
}