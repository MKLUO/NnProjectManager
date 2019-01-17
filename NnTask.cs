using System;
using System.Threading;
using System.Threading.Tasks;

using System.Collections.Generic;

using Utilities;

namespace NnManager {

    public partial class Project {

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
            public bool IsIntegrited() {
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
                if (status == Status.Running) return;

                status = Status.Running;

                // TODO: generate input file and directory
                // TODO: verify path

                NnAgent.InitNnFolder(path, content);

                task = new Task(
                    () => {
                        Utilities.NnAgent.RunNn(path);
                        AfterRun(path);
                    }
                );

                task.Start();
            }

            public void KillTask() {
                // TODO: 
            }

            void AfterRun(
                string path
            ) {
                // TODO: parse log to look for errors
                status = Status.Done;
                outputHash = Util.HashPath(path);
            }

            public Status GetStatus() {
                return status;
            }
            
            readonly string name;
            readonly string content;

            string outputHash;

            Task task;
            Status status;
        }

    }
}
