using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;
        
    [Serializable]
    public enum ModuleType {
        NnMain,
        NnOccup,
        TestOpenNotepad
    }    

    partial class NnTask {
        NnModule GetModule(
            ModuleType type, 
            ImmutableDictionary<string, string> options
        ) {
            NnModule module;
            switch (type) {
                case ModuleType.NnMain:
                    module = NnMain(
                        options
                    ); break;
                case ModuleType.NnOccup:
                    module = NnOccup(
                        options
                    ); break;
                case ModuleType.TestOpenNotepad:
                    module = TestOpenNotepad(
                        options
                    ); break;
                default: throw new Exception();                
            }
            return module;
        }
    }
    
    [Serializable]
    public class NnModuleRecord {
        public ModuleType Type { get; }
        public Dictionary<string, string> Options { get; }
        public string? Result { get; private set; }

        public NnModuleRecord(
            ModuleType type, 
            ImmutableDictionary<string, string> options,
            string? result = null
        ) {
            Type = type;
            Options = new Dictionary<string, string>(options);
            Result = result;
        }

        public void SetResult(string result) =>
            Result = result;
    }

    public class NnModule {
        // public abstract string Name { get; }
        // protected abstract NnTask Task { get; }
        // protected abstract Dictionary<string, string> Options { get; }

        public static ImmutableDictionary<string, string> GetDefaultOptions(ModuleType type) {
            switch (type) {
                case ModuleType.NnMain:
                    return NnTask.NnMainDefaultOption;
                case ModuleType.NnOccup:
                    return NnTask.NnOccupDefaultOption;
                case ModuleType.TestOpenNotepad:
                    return NnTask.TestOpenNotepadDefaultOption;
                default: throw new Exception();
            }
        }        

        public NnModule(
            string name,
            Func<bool> canExecute, 
            Func<bool> isDone, 
            Func<CancellationToken, ImmutableDictionary<string, string>, bool> execute, 
            Func<string> getResult, 
            ImmutableDictionary<string, string> defaultOptions,
            ImmutableDictionary<string, string>? options = null
        ) {
            this.Name = name;
            this.canExecute = canExecute;
            this.isDone = isDone;
            this.execute = execute;
            this.getResult = getResult;
            this.defaultOptions = new Dictionary<string, string>(defaultOptions);

            this.options = options != null ?
                new Dictionary<string, string>(options) :
                null;
        }

        public string Name { get; }

        Dictionary<string, string> defaultOptions;
        ImmutableDictionary<string, string> DefaultOptions => defaultOptions.ToImmutableDictionary();

        Dictionary<string, string>? options;
        ImmutableDictionary<string, string>? Options => options?.ToImmutableDictionary();

        Func<bool> canExecute;
        public bool CanExecute() => canExecute();

        Func<bool> isDone;
        public bool IsDone() => isDone();

        Func<CancellationToken, ImmutableDictionary<string, string>, bool> execute;
        public bool Execute(CancellationToken ct) {
            var newOption = new Dictionary<string, string>();

            if (Options != null) {
                foreach (var key in DefaultOptions.Keys) {                
                    if (Options.ContainsKey(key))
                        newOption[key] = Options[key];
                    else newOption[key] = DefaultOptions[key];
                }
                return execute(ct, newOption.ToImmutableDictionary());
            } else return execute(ct, DefaultOptions);
        }

        Func<string> getResult;
        public string GetResult() => getResult();
    }

    public abstract class LogBase {
        // public LogBase() { }
        public abstract string? Push(string line);
    }

    // TODO: Extensible Module (User create ther own modules)
    // TODO: Task content shouldnt be transparent to modules? 
}