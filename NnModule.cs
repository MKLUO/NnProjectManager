using System;
using System.Collections.Generic;

namespace NnManager {
    using RPath = Util.RestrictedPath;
    using Status = Project.NnTask.NnTaskStatus;

    public partial class Project {
        public partial class NnTask {

            [NonSerialized]
            Dictionary<string, NnModule> modules;
            public void RegisterModules() {
                modules = new Dictionary<string, NnModule> { 
                    { "NN Main", NnMain },
                    { "NN Restore", NnRestore },
                    { "NN Validation", NnValidation },
                    { "Test", Test }
                };
            }

            public class NnModule {
                readonly Func<bool> _canExecute;
                readonly Action _execute;

                public NnModule(Action execute, Func<bool> canExecute) {
                    _execute = execute;
                    _canExecute = canExecute;
                }

                public Boolean CanExecute() {
                    return _canExecute();
                }

                public void Execute() {
                    _execute();
                }
            }
        }
    }
}