using System;
using System.Threading;
using System.Threading.Tasks;

namespace NnManager {

    public partial class Project {

        class NnTask {

            enum Status {
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
                status = Status.Done;
            }

            // Time comsuming!
            public void Run() {
                status = Status.Done;
            }

            readonly string content;

            Task task;
            Status status;
        }

    }
}
