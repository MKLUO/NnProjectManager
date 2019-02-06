using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        public Project(string initPath, bool load = false) {
            path = RPath.InitRoot(initPath);

            templates = new Dictionary<string, NnTemplate>();
            tasks = new Dictionary<string, NnTask>();

            if (load) {
                var projData =
                    (KeyValuePair<Dictionary<string, NnTemplate>, Dictionary<string, NnTask>>) Util.DeserializeFromFile(
                        path.SubPath(NnAgent.projFileName)
                    );

                this.templates = projData.Key;
                this.tasks = projData.Value;

                foreach (var pair in tasks) {
                    string id = pair.Key;
                    NnTask task = pair.Value;
                    // TODO: Restore state & event
                    task.PropertyChanged += OnTaskPropertyChanged;

                    task.RegisterModules();
                    task.QueueModule("NN Restore");

                    // task.WaitForTask();
                }
            }
            StartScheduler();
        }

        public static Project NewProject(string initPath) {
            RPath path = RPath.InitRoot(initPath);

            // if (File.Exists(
            //     path.SubPath(NnAgent.projFileName)))
            if (Directory.GetFileSystemEntries(path).Count() != 0)
                if (!Util.WarnAndDecide(
                        "The folder chosen is not empty.\nContinue?"))
                    return null;

            Project project = new Project(initPath);

            project.Save();
            return project;
        }

        public static Project LoadProject(string initPath) {
            RPath path = RPath.InitRoot(initPath);

            if (!File.Exists(
                    path.SubPath(NnAgent.projFileName))) {
                Util.ErrorHappend(
                    "No project in this directory!");
                return null;
            }

            Project project = new Project(initPath, true);

            // project.Save();
            return project;
        }

        public bool AddTemplate(
            string id,
            string content) {
            // TODO: Same id?
            try {
                templates.Add(
                    id,
                    new NnTemplate(content)
                );
            } catch {
                Util.ErrorHappend("Exception in AddTemplate!");
                // Util.Log("Exception in AddTemplate!");
                return false;
            }

            Save();
            return true;
            //OnPropertyChanged("templates");
        }

        public string AddTask(
            string templateId,
            Dictionary < string, (string, string) > param
        ) {
            string id = templateId + Util.ParamToTag(param);

            try {
                tasks.Add(
                    id,
                    new NnTask(
                        templates[templateId].GenerateContent(param),
                        path.SubPath("output").SubPath(id),
                        new Dictionary<string, object> { { "param", param },
                            { "templateId", templateId }
                        }
                    )
                );
                tasks[id].PropertyChanged += OnTaskPropertyChanged;
            } catch {
                Util.ErrorHappend("Exception in AddTask!");
                // Util.Log("Exception in AddTask!");
                return null;
            }

            Save();
            return id;

            //OnPropertyChanged("tasks");
        }

        #endregion

        #region scheduling

        Task scheduler;
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

        public void StartScheduler() {
            if (scheduler == null)
                scheduler = new Task(SchedulerMainLoop);

            if ((scheduler.Status != TaskStatus.Running) && (schedulerActiveFlag == false)) {
                schedulerActiveFlag = true;
                scheduler = Task.Run(
                    () => SchedulerMainLoop());
            }
        }

        public void StopScheduler() {
            schedulerActiveFlag = false;
        }

        void SchedulerMainLoop() {
            do {
                Thread.Sleep(500);

                if (CurrentTaskCount >= maxConcurrentTaskCount)
                    continue;

                foreach (NnTask task in tasks.Values) {
                    string popedModule = task.DequeueAndRunModule();
                }

            } while (schedulerActiveFlag);
        }

        #endregion

        #region getInfo (ViewModel)

        public List<string> GetTemplates() {
            List<string> list = new List<string>();

            foreach (var tmpl in templates) {
                list.Add(tmpl.Key);
            }

            return list;
        }

        public Dictionary < string, (string, string) > GetTasks() {
            Dictionary < string, (string, string) > list = new Dictionary < string, (string, string) > ();

            foreach (var task in tasks) {

                string queue = "";
                foreach (var item in task.Value.ModuleQueue) {
                    queue += item + " ";
                }

                list[task.Key] = (
                    task.Value.CurrentModule,
                    queue
                );
            }

            return list;
        }

        public Dictionary < string, (string, string) > GetTemplateInfo(string id) {
            if (id == null)
                return new Dictionary < string, (string, string) > ();
            if (!templates.ContainsKey(id))
                return new Dictionary < string, (string, string) > ();
            return templates[id].GetVariables();
        }

        public Dictionary<string, object> GetTaskInfo(string id) {
            return tasks[id].Info;
        }

        public bool IsSchedularRunning() {
            return schedulerActiveFlag;
        }

        #endregion

        public void Save() {
            Util.SerializeToFile(
                new KeyValuePair<Dictionary<string, NnTemplate>, Dictionary<string, NnTask>>
                (templates, tasks),
                path.SubPath(NnAgent.projFileName)
            );
        }
    }
}