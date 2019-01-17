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
                string content) {
                    this.content = content;
                    this.id = Utilities.Util.RandomString(20);

                    task = null;
                    status = Status.New;
                }

            public void Launch() {
                // TODO: switch status

                if (task != null) return;

                // TODO: generate input file and directory

                task = new Task(
                    () => Utilities.NnAgent.RunNn(id)
                );

                task.Start();
            }

            public Status GetNnTaskStatus() {
                return status;
            }

            public TaskStatus GetTaskStatus() {
                return task.Status;
            }

            readonly string content;
            readonly string id;

            Task task;
            Status status;
        }

    }
}
