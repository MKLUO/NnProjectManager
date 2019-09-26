using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public partial class NnProject : Notifier, INotifyPropertyChanged { 

        public RPath FSPath { get; }

        List<NnTemplate> templates; 
        public IEnumerable<NnTemplate> Templates => templates;
        
        List<NnPlan> plans;
        public IEnumerable<NnPlan> Plans => plans;

        bool schedulerActiveFlag;
        public bool SchedulerActiveFlag { 
            get => schedulerActiveFlag;
            private set => SetField(ref schedulerActiveFlag, value);
        }

        // FIXME: maxConcurrentTaskCount
        const int maxConcurrentTaskCount = 2;
        int CurrentTaskCount => plans.Sum(x => x.RunningTasks);

        Task? scheduler;
        Task Scheduler {
            get => scheduler ?? new Task(SchedulerMainLoop);
            set => scheduler = value;
        }
        
        NnProject(
            RPath path,
            List<NnTemplate> templates,
            List<NnPlan> plans) 
        {
            (this.FSPath, this.templates, this.plans) =
            (path, templates, plans);         

            Save();
        }

        public static NnProject? New(string initPath) {
            try {
                RPath path = RPath.InitRoot(initPath);
                // if (Directory.GetFileSystemEntries(path).Count() != 0)
                //     if (!Util.WarnAndDecide(
                //             "The folder chosen is not empty.\nContinue?"))
                //         return null;

                return new NnProject(
                    path,
                    new List<NnTemplate>(),
                    new List<NnPlan>()
                );
            } catch {
                Util.ErrorHappend(
                    "Error while creating project!");
                return null;
            }
        }

        [Serializable]
        struct SaveData {
            public SaveData(NnProject project) {
                templateIds = project.templates.Select(x => x.Name).ToList();
                planIds = project.plans.Select(x => x.Name).ToList();
            }

            readonly public List<string> templateIds;
            readonly public List<string> planIds;
        }

        public void Save() {
            Util.SerializeToFile(
                new SaveData(this),
                FSPath.SubPath(NnAgent.projFileName)
            );

            // FIXME: 
            // Util.SaveLog(FSPath, log);
        }

        public static NnProject? Load(string initPath) {
            // TODO: Store failed data elsewhere?
            try {
                RPath path = RPath.InitRoot(initPath);
                if (!File.Exists(path.SubPath(NnAgent.projFileName))) {
                    Util.ErrorHappend("No project in this directory!");
                    return null;
                }

                var projectData =
                    (SaveData) Util.DeserializeFromFile(
                        path.SubPath(NnAgent.projFileName)
                    );

                // FIXME: plans and templates coupled to projectdatas!
                List<NnPlan> plans = new List<NnPlan>();
                foreach (var planId in projectData.planIds) {
                    NnPlan? plan = NnPlan.Load(
                        path.SubPath("plans").SubPath(planId)
                    );
                    if (plan != null)
                        plans.Add(plan);
                }

                List<NnTemplate> templates = new List<NnTemplate>();
                foreach (var templateId in projectData.templateIds) {
                    NnTemplate? temp = NnTemplate.Load(
                        path.SubPath("templates").SubPath(templateId)
                    );
                    if (temp != null)
                        templates.Add(temp);
                }

                NnProject project = new NnProject(
                    path, 
                    templates,
                    plans
                );

                return project;

            } catch {
                Util.ErrorHappend(
                    "Error while loading project!");
                return null;
            }
        }

        public bool AddTemplate(
            NnTemplate template
        ) {
            try {
                templates.Add(template);
                OnPropertyChanged("Model - AddTemplate");

                Save();
                return true;

            } catch {
                Util.ErrorHappend("Error while adding template!");
                if (templates.Contains(template))
                    templates.Remove(template);
                return false;
            }
        }

        public bool DeleteTemplate(
            NnTemplate template
        ) {
            try {
                if (!templates.Contains(template))
                    return false;

                templates.Remove(template);
                OnPropertyChanged("Model - DeleteTemplate");

                Save();
                return true;

            } catch {
                Util.ErrorHappend("Error while deleting template!");
                return false;
            }
        }

        public bool AddPlan(
            NnPlan plan
        ) {
            try {
                // if (plans.ContainsKey(id))
                //     if (!Util.WarnAndDecide("Plan with same name exists. Replace it?")) {
                //         return null;
                //     } else if (!DeletePlan(id, true)) {
                //         Util.ErrorHappend("Error while Replacing plan!");
                //         return null;
                //     }

                plans.Add(plan);
                OnPropertyChanged("Model - AddPlan");

                Save();
                return true;

            } catch {
                Util.ErrorHappend("Error while adding plan!");
                return false;
            }
        }

        public bool DeletePlan(
            NnPlan plan
        ) {
            bool success = false;
            try {
                if (!plans.Contains(plan))
                    return false;

                if (!Util.WarnAndDecide($"Deleting plan {plan.Name}. Are you sure?"))
                    return false;

                if (plan.IsBusy())
                    if (Util.WarnAndDecide($"Selected plan {plan.Name} is busy rn. Terminate and delete?")) {
                        plan.TerminateAll();
                    } else return false;

                plans.Remove(plan);

                // FIXME: clutter!
                string path = FSPath.SubPath("plans").SubPath("_removed").SubPath(plan.Name);
                while (Directory.Exists(path))
                    path += "_";
                Directory.Move(plan.FSPath, path);
                success = true;                
            } catch {
                Util.ErrorHappend("Error while deleting plan!");
                success = false;                
            } finally {
                OnPropertyChanged("Model - DeletePlan");
                Save();
            }
            return success;
        }

        public void Terminate() {
            foreach (var plan in plans)
                plan.TerminateAll();
        }

        public void StartScheduler() {
            SchedulerActiveFlag = true;
            if (Scheduler.Status != TaskStatus.Running) {
                Scheduler = Task.Run(
                    () => SchedulerMainLoop());
            }           
        }

        public void StopScheduler() {
            SchedulerActiveFlag = false;
        }

        void SchedulerMainLoop() {
            do {
                // TODO: delay?
                Task.Delay(500).Wait();

                if (CurrentTaskCount >= maxConcurrentTaskCount)
                    continue;

                foreach (NnPlan plan in plans) {
                    Task.Delay(10).Wait();
                    if (plan.DoWork()) break;
                }

            } while (SchedulerActiveFlag);
        }

        public bool IsBusy() {
            foreach (NnPlan plan in plans)
                if (plan.IsBusy()) return true;

            return false;
        }
    }    
}