using System.Collections.Generic;

namespace NnManager {

    public partial class Project {

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
            Dictionary<string, string> param) {
            tasks.Add(
                name,
                new NnTask(
                    templates[templateId].generateContent(param))
            );
        }

        public void Load() {

        }

        public void Save() {
            
        }

        readonly string name;

        Dictionary<string, Template> templates;
        Dictionary<string, NnTask> tasks;
    }
}