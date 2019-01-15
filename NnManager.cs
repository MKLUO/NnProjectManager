using System.Collections.Generic;

namespace NnManager {
    public class Project {

        class Template {

            public Template(
                string content
            ) {

            }
            public string generateContent() {
                return "";
            }
        }

        class Task {

            enum Status {
                New,
                Done,
                Error
            };

            public Task(
                string content_) {
                    content = content_;
                    status = Status.New;
                }

            public void Run() {
                status = Status.Done;
            }

            readonly string content;

            Status status;
        }

        public Project(
            string name_) {
            name = name_;
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
            string param) {
            tasks.Add(
                name,
                new Task(
                    templates[templateId].gene)
            );
        }

        public void Load() {

        }

        public void Save() {
            
        }

        readonly string name;

        Dictionary<string, Template>    templates;
        Dictionary<string, Task>        tasks;
    }
}