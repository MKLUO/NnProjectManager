using System;
using System.Collections.Generic;

using System.Threading;

namespace NnManager {

    public partial class Project {

        Project(
            string name_ = "") {

            name = name_;
            templates = new Dictionary<string, Template>();
            tasks = new Dictionary<string, NnTask>();
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
            Dictionary<string, string> param) {
            tasks.Add(
                name,
                new NnTask(
                    templates[templateId].generateContent(param))
            );
        }

        public void LaunchAllTask() {
            foreach (var pair in tasks)
                pair.Value.RunAsync();
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

        static public Project New(
            string name = ""
        ) {
            return new Project(name);
        }

        static public Project Load() {
            // TODO:
            return new Project();
        }

        public void Save() {                        
            // TODO:
        }

        readonly string name;

        Dictionary<string, Template> templates;
        Dictionary<string, NnTask> tasks;
    }
}