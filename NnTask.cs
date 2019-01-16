using System;
using System.Threading;
using System.Threading.Tasks;

namespace NnManager {

    public partial class Project {

        class NnTask {

            public enum Status {
                New,
                Done,
                Error
            };

            public NnTask(
                string content_) {
                    content = content_;

                    task = null;
                    status = Status.New;
                }

            public void RunAsync() {
                // TODO: switch status

                if (task != null) return;

                task = new Task(
                    () => Run()
                );

                task.Start();
            }

            // Time comsuming!
            // TODO: Parse for NN output
            // TODO: Thread-safe execution of NN.
            void Run() {
                // switch: status

                // TODO: Directory is initialized in template? (must be unique)

                // TODO: For testing:
                Thread.Sleep(10000);

                status = Status.Done;
            }

            public Status GetStatus() {
                return status;
            }
            
            readonly string content;

            Task task;
            Status status;
        }

    }
}
