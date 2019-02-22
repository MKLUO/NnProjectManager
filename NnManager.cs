using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public partial class Project : INotifyPropertyChanged {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string str) {
            OnPropertyChanged(new PropertyChangedEventArgs(str));
        }

        void OnPropertyChanged(PropertyChangedEventArgs e) {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, e);
        }

        void OnTaskPropertyChanged(object sender, PropertyChangedEventArgs e) {
            // OnPropertyChanged(((NnTask)sender).GetName() + "-" + e.ToString());
            OnPropertyChanged(e.ToString());
        }
        #endregion

        #region core

        readonly RPath path; // Project root

        Dictionary<string, NnTemplate> templates;
        Dictionary<string, NnTask> tasks;

        // Load Project
        Project(string initPath, bool load = false) {
            path = RPath.InitRoot(initPath);

            templates = new Dictionary<string, NnTemplate>();
            tasks = new Dictionary<string, NnTask>();
            Log = "";

            if (load) {
                var projData =
                    ((Dictionary<string, NnTemplate>, List<string>)) Util.DeserializeFromFile(
                        path.SubPath(NnAgent.projFileName)
                    );

                this.templates = projData.Item1;
                List<string> taskIds = projData.Item2;

                this.tasks = new Dictionary<string, NnTask>();

                foreach (string id in taskIds) {
                    tasks[id] = NnTask.Load(path.SubPath("output").SubPath(id));

                    tasks[id].PropertyChanged += OnTaskPropertyChanged;
                    tasks[id].LogFired += OnTaskLogFired;
                    tasks[id].Restore();
                }
            }
            StartScheduler();
        }

        public static Project? NewProject(string initPath) {
            RPath path = RPath.InitRoot(initPath);

            if (Directory.GetFileSystemEntries(path).Count() != 0)
                if (!Util.WarnAndDecide(
                        "The folder chosen is not empty.\nContinue?"))
                    return null;

            try {
                Project project = new Project(initPath);
                project.Save();
                return project;
            } catch {
                Util.ErrorHappend(
                    "Error while creating project!");
                return null;
            }
        }

        public static Project? LoadProject(string initPath) {
            RPath path = RPath.InitRoot(initPath);

            if (!File.Exists(
                    path.SubPath(NnAgent.projFileName))) {
                Util.ErrorHappend(
                    "No project in this directory!");
                return null;
            }
            try {
                return new Project(initPath, true);
            } catch {
                Util.ErrorHappend(
                    "Error while loading project!");
                return null;
            }
        }

        public bool AddTemplate(
            string id,
            string content
        ) {
            try {
                if (templates.ContainsKey(id))
                    if (!Util.WarnAndDecide("Template with same name exists. Replace it?"))
                        return false;

                templates[id] =
                    new NnTemplate(content);

                Save();
                return true;
            } catch {
                Util.ErrorHappend("Error while adding template!");
                return false;
            }
        }

        public bool DeleteTemplate(
            string id
        ) {
            try {
                if (!templates.ContainsKey(id))
                    return false;

                foreach (NnTask task in tasks.Values)
                    if ((string) task.Info["templateId"] == id)
                        if (Util.WarnAndDecide("Selected template \"" + id + "\" is being referenced by at least one of the generated tasks. Continue deletion?")) {
                            break;
                        } else
                            return false;

                templates.Remove(id);
                Save();
                return true;

            } catch {
                Util.ErrorHappend("Error while deleting template!");
                return false;
            }
        }

        public string? AddTask(
            NnTemplate template,
            NnParam param
        ) {
            string id;
            try {
                // id = templateId + Util.ParamToTag(param);
                // if (tasks.ContainsKey(id))
                //     if (!Util.WarnAndDecide("Task with same name exists. Replace it?"))
                //         return null;

                tasks[id] =
                    new NnTask(
                        template.GenerateContent(param),
                        path.SubPath("output").SubPath(id),
                        new Dictionary<string, object> { { "param", param },
                            { "templateId", templateId }
                        }
                    );

                tasks[id].Save();
                tasks[id].PropertyChanged += OnTaskPropertyChanged;
                tasks[id].LogFired += OnTaskLogFired;

                Save();
                return id;
            } catch {
                Util.ErrorHappend("Error while adding task!");
                return null;
            }
        }

        public bool DeleteTask(
            string taskId
        ) {
            //TODO: check for deletion
            //TODO: Redo?
            try {
                if (!tasks.ContainsKey(taskId))
                    return false;

                if (tasks[taskId].IsBusy())
                    if (Util.WarnAndDecide("Selected task \"" + taskId + "\" is busy rn. Terminate and delete?")) {
                        tasks[taskId].Terminate();
                    } else {
                        return false;
                    }

                tasks.Remove(taskId);
            } catch {
                Util.ErrorHappend("Error while deleting task!");
                return false;
            }

            Save();
            return true;
        }

        // public void AddPlan(
        //     string templateId,
        //     string paramFilePath,

        // ) {

        // }

        #endregion

        #region scheduling
        Task? scheduler;
        Task Scheduler {
            get {
                if (scheduler == null)
                    scheduler = new Task(SchedulerMainLoop);

                return scheduler;
            }
            set {
                scheduler = value;
            }
        }
        bool schedulerActiveFlag;

        const int maxConcurrentTaskCount = 5;
        int CurrentTaskCount {
            get {
                return tasks.Values.Where(x => x.CurrentModule != null).Count();
            }
        }

        public void EnqueueTaskWithModule(string taskId, string moduleName) {
            tasks[taskId].QueueModule(moduleName);
        }

        public void ClearModules(string taskId) {
            tasks[taskId].ClearModules();
        }

        public void StartScheduler() {
            if ((Scheduler.Status != TaskStatus.Running) && (schedulerActiveFlag == false)) {
                schedulerActiveFlag = true;
                Scheduler = Task.Run(
                    () => SchedulerMainLoop());
            }
        }

        public void StopScheduler() {
            schedulerActiveFlag = false;
        }

        void SchedulerMainLoop() {
            do {
                Task.Delay(500).Wait();

                if (CurrentTaskCount >= maxConcurrentTaskCount)
                    continue;

                foreach (NnTask task in tasks.Values) {
                    Task.Delay(10).Wait();
                    task.TryDequeueAndRunModule();
                }

            } while (schedulerActiveFlag);
        }

        #endregion

        public void Save() {
            List<string> taskIds = new List<string>();
            foreach (var pair in tasks) {
                string id = pair.Key;
                NnTask task = pair.Value;

                taskIds.Add(id);
                //task.Save();
            }

            Util.SerializeToFile(
                (templates, taskIds),
                path.SubPath(NnAgent.projFileName)
            );
        }

        #region log

        String? log;
        public String Log {
            get {
                if (log == null)
                    return "";
                else return log;
            }
            private set {
                if (value != log) {
                    log = value;
                    OnPropertyChanged("Log");
                }
            }
        }

        void OnTaskLogFired(NnTask sender, string msg) {
            string id = tasks
                .FirstOrDefault(x => x.Value == (NnTask) sender)
                .Key;

            Log += id + " - " + msg + "\n";
        }

        #endregion

        #region getInfo (ViewModel)

        public bool IsBusy() {
            foreach (NnTask task in tasks.Values)
                if (task.IsBusy()) return true;

            return false;
        }

        public List<string> GetTemplates() {
            List<string> list = new List<string>();

            foreach (var tmpl in templates) {
                list.Add(tmpl.Key);
            }

            return list;
        }

        public Dictionary < string, (string, string, string) > GetTasks() {
            Dictionary < string, (string, string, string) > list =
                new Dictionary < string, (string, string, string) > ();

            foreach (var task in tasks) {

                string queue = "";
                foreach (var item in task.Value.ModuleQueue) {
                    queue += item + " ";
                }

                list[task.Key] = (
                    task.Value.CurrentModule,
                    queue,
                    task.Value.Status
                );
            }

            return list;
        }

        // public Dictionary < string, (string, string) > GetTemplateInfo(string id) {
        //     if (id == null)
        //         return new Dictionary < string, (string, string) > ();
        //     if (!templates.ContainsKey(id))
        //         return new Dictionary < string, (string, string) > ();
        //     return templates[id].GetVariables();
        // }

        public Dictionary<string, object> GetTaskInfo(string id) {
            return tasks[id].Info;
        }

        public List<string> GetModules(string id) {
            return tasks[id].Modules.Keys.ToList();
        }

        public bool IsSchedularRunning() {
            return schedulerActiveFlag;
        }

        #endregion
    }
}