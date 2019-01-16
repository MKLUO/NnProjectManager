using System;
using System.Threading;
using System.Threading.Tasks;

namespace NnManager {

    public partial class Project {

        class NnTask {

            // TODO: All File system related operations should be encapsulated here.

            public enum Status {
                New,
                Done,
                Error
            };

            public NnTask(
                string name,
                string content) {
                    // TODO: Check if name is unique
                    this.name = name;
                    this.content = content;

                    task = null;
                    status = Status.New;
                }

            public void Launch() {
                // TODO: switch status

                if (task != null) return;

                // TODO: generate input file and directory

                task = new Task(
                    () => Utilities.NnAgent.RunNn(name)
                );

                task.Start();
            }

            public Status GetStatus() {
                return status;
            }

            readonly string name;
            readonly string content;

            Task task;
            Status status;
        }

    }
}
