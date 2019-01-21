using System;
using System.Collections.Generic;

using System.Threading;

using System.IO;

using Utilities;

namespace NnManager {

    public partial class Project {

        // Loaded Project
        public Project(
            string projPath,
            bool load = false) {

            // TODO: Handle exception according to load
            this.path = Path.GetDirectoryName(projPath);            

            if (load) {
                var projData = 
                    (KeyValuePair<
                        Dictionary<string, Template>, 
                        Dictionary<string, NnTask>
                    >)Util.DeserializeFromFile(projPath);
                this.templates  = projData.Key;
                this.tasks      = projData.Value;
            } else {
                this.templates = 
                    new Dictionary<string, Template>();
                this.tasks = 
                    new Dictionary<string, NnTask>();
            }
        }

        public void AddTemplate(
            string id,
            string content) {
            templates.Add(
                id,
                new Template(content)
            );
        }

        public void AddTask(
            string name,
            string templateId,
            Dictionary<string, string> param
        ){
            string id;
            do {
                id = Utilities.Util.RandomString(20);
            } while (tasks.ContainsKey(id));
            
            tasks.Add(
                id,
                new NnTask(
                    name,
                    templates[templateId].generateContent(param))
            );
        }

        // For testing
        public void LaunchAllTask() {
            foreach (var pair in tasks) {
                string id = pair.Key;
                NnTask task = pair.Value;
                // task.Launch(Util.SubPath(rootPath, id));
                task.Launch(path);
            }
        }

        // For testing
        public void WaitForAllTask() {
            bool atLeastOneTaskIsNotDone = true;

            while (atLeastOneTaskIsNotDone){
                atLeastOneTaskIsNotDone = false;
                foreach (var pair in tasks) {
                    string name = pair.Key;
                    NnTask task = pair.Value;
                    if (task.GetStatus() != NnTask.Status.Done) {
                        Console.WriteLine("Task: " + name + " is not done yet. Waiting...");
                        atLeastOneTaskIsNotDone = true;
                        break;
                    }
                }
                Thread.Sleep(1000);
            }

            Console.WriteLine("All tasks done!");
        }

        public void Save() {                        
            // TODO: Serialize
            Util.SerializeToFile(
                new KeyValuePair<
                        Dictionary<string, Template>, 
                        Dictionary<string, NnTask>
                    >(templates, tasks),
                path);  
        }

        readonly string path;

        Dictionary<string, Template> templates;
        Dictionary<string, NnTask> tasks;
    }
}