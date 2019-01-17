using System;
using System.Collections.Generic;

using System.Threading;

using Utilities;

namespace NnManager {

    public partial class Project {

        // New Project
        public Project() {
            templates = new Dictionary<string, Template>();
            tasks = new Dictionary<string, NnTask>();
        }

        // Loaded Project
        public Project(
            string rootPath) {
            this.rootPath = rootPath;    
            this.templates = (Dictionary<string, Template>)Util.DeserializeFromFile(
                rootPath + "\\templates");
            this.tasks = (Dictionary<string, NnTask>)Util.DeserializeFromFile(
                rootPath + "\\tasks");            
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
                pair.Value.Launch(Util.SubFolder(rootPath, id));
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

        public void Save(string filePath) {                        
            // TODO: Serialize
            Util.SerializeToFile(this, filePath);
        }

        readonly string rootPath;

        Dictionary<string, Template> templates;
        Dictionary<string, NnTask> tasks;
    }
}