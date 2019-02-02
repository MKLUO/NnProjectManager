using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

using System.Threading;
using System.Threading.Tasks;

using System.IO;

using System.ComponentModel;

namespace NnManager
{
    using RPath = Util.RestrictedPath;

    public partial class Project : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string str)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(str));
        }

        void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, e);
        }

        void OnTaskPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(((NnTask)sender).GetName() + "-" + e.ToString());
        }
        #endregion

        #region core

        readonly RPath path; // Project root

        Dictionary<string, Template> templates;
        Dictionary<string, NnTask> tasks;

        // Load Project
        public Project(string initPath, bool load = false)
        {
            path = RPath.InitRoot(initPath);

            runningTasks = new Dictionary<string, Task>();
            queuedTasks = new ConcurrentQueue<string>();

            templates = new Dictionary<string, Template>();
            tasks = new Dictionary<string, NnTask>();

            if (load)
            {
                var projData =
                    (KeyValuePair<
                        Dictionary<string, Template>,
                        Dictionary<string, NnTask>
                    >)Util.DeserializeFromFile(
                        path.SubPath(NnAgent.projFileName)
                    );

                this.templates = projData.Key;
                this.tasks = projData.Value;

                foreach (var pair in tasks)
                {
                    string id = pair.Key;
                    NnTask task = pair.Value;
                    // TODO: Restore state & event
                    task.PropertyChanged += OnTaskPropertyChanged;
                    task.OutputValidation(path.SubPath("output").SubPath(id));
                }
            }            
        }

        public static Project NewProject(string initPath)
        {
            RPath path = RPath.InitRoot(initPath);

            // if (File.Exists(
            //     path.SubPath(NnAgent.projFileName).ToString()))
            if (Directory.GetFileSystemEntries(path.ToString()).Count() != 0)
                if (!Util.WarnAndDecide(
                    "The folder chosen is not empty.\nContinue?"))
                    return null;

            Project project = new Project(initPath);
            project.Save();

            return project;
        }

        public static Project LoadProject(string initPath)
        {
            RPath path = RPath.InitRoot(initPath);

            if (!File.Exists(
                path.SubPath(NnAgent.projFileName).ToString())) {
                Util.ErrorHappend(
                    "No project in this directory!");
                    return null;
                }

            Project project = new Project(initPath, true);
            project.Save();

            return project;
        }

        public void AddTemplate(
            string id,
            string content)
        {
            // TODO: Same id?
            templates.Add(
                id,
                new Template(id, content)
            );

            //OnPropertyChanged("templates");
        }

        public void AddTask(
            string templateId,
            Dictionary<string, (string, string)> param
        )
        {
            // string id;
            // do
            // {
            //     id = Util.RandomString(20);
            // } while (tasks.ContainsKey(id));

            string id = templateId + Util.ParamToTag(param);

            try
            {
                tasks.Add(
                    id,
                    new NnTask(
                        id,
                        templates[templateId].GenerateContent(param),
                        new Dictionary<string, object>
                        {
                            {"param", param},
                            {"templateId", templateId}
                        }                                              
                    )
                );
                tasks[id].PropertyChanged += OnTaskPropertyChanged;
            }
            catch
            {
                Util.Log("Exception in AddTask!");
            }

            //OnPropertyChanged("tasks");
        }

        #endregion

        #region scheduling

        Task scheduler;
        bool schedulerActive = false;
        Dictionary<string, Task> runningTasks;
        ConcurrentQueue<string> queuedTasks;

        // FIXME: bad smell

        public void StartScheduler(bool testing = false)
        {
            if (scheduler == null)
            {
                schedulerActive = true;
                scheduler = Task.Run(() => SchedulerMainLoop(testing));
                return;
            }

            if (!schedulerActive && (scheduler.Status != TaskStatus.Running))
            {
                schedulerActive = true;
                scheduler = Task.Run(() => SchedulerMainLoop(testing));
                return;
            }
        }

        public void StopScheduler()
        {
            schedulerActive = false;
        }

        public string GetSchedulerStatus()
        {
            if (schedulerActive) return "Active";
            else return "Inactive";
        }

        // FIXME: options
        const int maxTasks = 4;
        void SchedulerMainLoop(bool testing)
        {
            do
            {
                runningTasks = runningTasks.Where(
                    pair => !pair.Value.IsCompleted
                ).ToDictionary(pair => pair.Key, pair => pair.Value);

                if (runningTasks.Count < maxTasks)
                {
                    if (queuedTasks.Count > 0)
                    {
                        string id;
                        if (queuedTasks.TryDequeue(out id))
                            runningTasks.Add(
                                id, Task.Run(
                                    () => tasks[id].Launch(
                                        path.SubPath("output").SubPath(id),
                                        testing
                                    )
                                )
                            );
                    }
                }

                Thread.Sleep(1000);
            } while (schedulerActive);
        }

        public void EnqueueTask(string id)
        {
            if ((!queuedTasks.Contains(id)) && (!runningTasks.ContainsKey(id)))
                queuedTasks.Enqueue(id);
        }

        public bool IsSchedulerRunning()
        {
            return schedulerActive;
        }

        #endregion

        #region getInfo (ViewModel)

        public List<string> GetTemplates()
        {
            List<string> list = new List<string>();

            foreach (var tmpl in templates)
            {
                list.Add(tmpl.Key);
            }

            return list;
        }

        public List<Tuple<string, string>> GetTasksStatus()
        {
            List<Tuple<string, string>> list = new List<Tuple<string, string>>();

            foreach (var task in tasks)
            {
                list.Add(new Tuple<string, string>(
                    task.Key,
                    task.Value.GetStatus()));
            }

            return list;
        }

        public List<string> GetQueuedTasksInfo()
        {
            return queuedTasks.ToList();
        }

        public Dictionary<string, (string, string)> GetTemplateParam(string id)
        {
            return templates[id].GetVariables();
        }

        public Dictionary<string, object> GetTaskInfo(string id)
        {
            return tasks[id].GetInfo();
        }

        

        #endregion

        public void Save()
        {
            Util.SerializeToFile(
                new KeyValuePair<
                        Dictionary<string, Template>,
                        Dictionary<string, NnTask>
                    >(templates, tasks),
                path.SubPath(NnAgent.projFileName)
            );
        }
    }
}