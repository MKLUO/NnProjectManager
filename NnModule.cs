using System;
using System.Collections.Generic;

namespace NnManager {
    using RPath = Util.RestrictedPath;
    // using Status = Project.NnTask.NnTaskStatus;

    public partial class Project {
        public partial class NnTask {

            [NonSerialized]
            Dictionary<string, NnModule> modules;
            public void RegisterModules() {
                modules = new Dictionary<string, NnModule> { { "NN Main", NnMain },
                    // { "NN Restore", NnRestore },
                    { "NN Validation", NnValidation },
                    { "Test", Test },
                    { "Test Analyze", TestAnalyze }
                };

                // foreach (NnModule module in modules.Values)
                //     module.FireStatusHandler += SetStatus;
            }

            public class NnModule {
                readonly Func<bool> _execute;
                readonly Func<bool> _canExecute;
                readonly Func<bool> _restore;
                readonly Func<List<string>> _getLog;

                public NnModule(
                    Func<bool> execute,
                    Func<bool> canExecute = null,
                    Func<bool> restore = null,
                    Func<List<string>> getLog = null) {
                    _execute = execute;
                    _canExecute = canExecute ?? DefaultCanExecute;
                    _restore = restore ?? DefaultRestore;
                    _getLog = getLog ?? DefaultGetLog;
                }

                // #region status
                // public delegate void StatusFiredHandler(string status);
                // public event StatusFiredHandler FireStatusHandler;
                // void OnFireStatus(string status) {
                //     StatusFiredHandler handler = FireStatusHandler;
                //     if (handler != null)
                //         handler(status);
                // }
                // #endregion

                public Boolean CanExecute() {
                    return _canExecute();
                }

                public Boolean Restore() {
                    return _restore();
                }

                public Boolean Execute() {
                    return _execute();
                }

                public List<string> GetLog() {
                    return _getLog();
                }

                static readonly Func<bool> DefaultCanExecute =
                    () => true;

                static readonly Func<bool> DefaultRestore =
                    () => false;

                static readonly Func<List<string>> DefaultGetLog =
                    () => new List<string>();
            }
        }
    }
}