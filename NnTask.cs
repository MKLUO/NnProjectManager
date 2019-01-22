using System;
using System.Threading;
using System.Threading.Tasks;

using System.Collections.Generic;

using System.Runtime.Serialization;

namespace NnManager {

    public partial class Project {

        [Serializable]
        class NnTask {

            // TODO: All File system related operations should be encapsulated here.

            public enum Status {
                New,
                Running,
                Done,
                Error
            };

            // New Task
            public NnTask(
                string name,
                string content) {
                    this.name = name;
                    this.content = content;

                    this.outputHash = null;

                    this.task = null;
                    this.status = Status.New;
                }

            // Check for integrity
            public bool IsIntegrited(string path) {
                // TODO: check output existance/integrity accoding to id 
                // TODO: if corrupted or output is missing, reset it

                return true;
            }

            public void Reset() {

            }

            public void Launch(
                string path
            ) {
                // TODO: switch status
                if (status != Status.New) return;

                status = Status.Running;

                // TODO: generate input file and directory
                // TODO: verify path

                try {
                    NnAgent.InitNnFolder(path, content);
                    task = new Task(
                        () => {
                            NnAgent.RunNn(path);
                            AfterRun(path);
                        }
                    );

                    task.Start();
                } catch {
                    Util.Log("Error occured in task launch!");
                } finally {
                    AfterRun(path);
                }                
            }

            public void KillTask() {
                // TODO: 
            }

            void AfterRun(
                string path
            ) {
                // TODO: parse log to look for errors & warnings (to decide status)
                // TODO: Event to proc GUI refreash?
                status = Status.Done;
                outputHash = Util.HashPath(path);
            }

            public Status GetStatus() {
                return status;
            }
            
            readonly string name;
            readonly string content;

            string outputHash;

            [NonSerialized]
            Task task;

            Status status;
        }

    }
}
