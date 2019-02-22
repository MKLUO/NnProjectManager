using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace NnManager {
    using RPath = Util.RestrictedPath;
    // using Status = Project.NnTask.NnTaskStatus;

    public partial class Project {
        public partial class NnTask {

            [NonSerialized]
            Dictionary<string, NnModule> modules;
            public Dictionary<string, NnModule> Modules {
                get {
                    if (modules == null)
                        RegisterModules();

                    return modules;
                }
            }

            void RegisterModules() {
                modules = new Dictionary<string, NnModule> { 
                    { "NN Main", NnMain },
                    // { "NN Restore", NnRestore },
                    { "NN Validation", NnValidation },
                    { "Test", Test },
                    { "Test Analyze", TestAnalyze },
                    { "Test - Open Notepad", TestOpenNotepad }
                };
            }

            public class NnModule {
                readonly Func<CancellationToken, bool> _execute;
                readonly Func<bool> _canExecute;
                readonly Func<bool> _restore;
                readonly Func<List<string>> _getLog;

                public NnModule(
                    Func<CancellationToken, bool> execute,
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

                public Boolean Execute(CancellationToken ct) {
                    return _execute(ct);
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

            #region Log

            abstract class LogBase {
                protected Action<string> SetStatus;

                protected LogBase() { }
                protected LogBase(Action<string> SetStatus) {
                    this.SetStatus = SetStatus;
                }
                // abstract public void Push(string line);
            }

            interface ILog {
                void Push(string line);
            }

            void LogParser<Log>(RPath logFilePath, CancellationToken ct) where Log : ILog {

                Log log = (Log) Activator.CreateInstance(
                    typeof(Log), new object[] {
                        (Action<string>) SetStatus }
                );

                FileSystemWatcher watcher = new FileSystemWatcher();
                watcher.Path = Path.GetDirectoryName(logFilePath);
                watcher.Filter = Path.GetFileName(logFilePath);

                StreamReader sr = new StreamReader(
                    new FileStream(
                        logFilePath,
                        FileMode.OpenOrCreate,
                        FileAccess.Read,
                        FileShare.ReadWrite
                    )
                );
                while (!ct.IsCancellationRequested) {
                    watcher.WaitForChanged(WatcherChangeTypes.All, 1000);
                    while (!sr.EndOfStream)
                        log.Push(sr.ReadLine());
                }
                while (!sr.EndOfStream)
                    log.Push(sr.ReadLine());

                SetStatus("");

                sr.Close();
            }

            #endregion
        }
    }
}