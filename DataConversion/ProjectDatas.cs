using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;

#nullable enable

// FIXME: Move all the conversion & FS realated operation here?

// FIXME: Is it possible to hide the components Lists in model (and expose them here)?

namespace NnManager {

    using RPath = Util.RestrictedPath;

    public class NnProjectData : Notifier, INotifyPropertyChanged {

        public interface IRefCompare<TD> { bool HasSameRef(TD another); }

        public interface IRefFind<T> { bool IsRef(T data); }

        public class NnPlanData : 
            Notifier, 
            INotifyPropertyChanged, 
            IRefCompare<NnPlanData>, 
            IRefFind<NnPlan>
        {
            
            protected override Dictionary<string, List<string>>? Derivatives =>
                new Dictionary<string, List<string>>
                {
                    {"Status", new List<string>{
                        "BusyTaskAmount"}}
                };

            NnPlan Plan { get; }
            public string Id => Plan.Name;

            public NnPlanData(NnPlan plan) {
                this.Plan = plan;
                Subscribe(plan);
            }

            public bool HasSameRef(NnPlanData another) {
                return this.Plan == another?.Plan;
            }

            public bool IsRef(NnPlan data) =>
            this.Plan == data;

            public IEnumerable<NnTaskData> TaskDatas {
                get {
                    foreach (var item in Plan.Tasks) {
                        NnTaskData data = new NnTaskData(item.Value, item.Key);
                        Subscribe(data);
                        yield return data;
                    }
                }
            }

            public NnParamForm ParamForm(NnTaskData? task = null) {
                if (task == null) {
                    if (Plan.Consts == null)
                        return new NnParamForm(
                            Plan.Template.Variables,
                            Plan.Template.Consts
                        );
                    else
                        return new NnParamForm(
                            Plan.Template.Variables,
                            Plan.Template.Consts,
                            new NnParam(
                                Plan.Template.Variables.ToDictionary(
                                    x => x.Key,
                                    x => x.Value ?? 0.0
                                ),
                                new Dictionary<string, string>(Plan.Consts)
                            )
                        );
                } else {
                    return new NnParamForm(
                        Plan.Template.Variables,
                        Plan.Template.Consts,
                        task.Param
                    );
                }
            }

            public NnTaskData? AddTask(NnParamForm param) {
                if (!param.IsFilled)
                    return null;
                NnParam? nnParam;

                if ((nnParam = param.ToNnParam()) == null)
                    return null;

                var task = Plan.AddTask(nnParam);
                if (task == null)
                    return null;

                return new NnTaskData(task, nnParam);
            }

            public void AddTaskFromFile(string content) {
                var nnParams = NnParam.NewParamsFromList(content);
                foreach (NnParam param in nnParams)
                    Plan.AddTask(param);
            }

            public bool DeleteTask(NnTaskData taskData) {
                foreach (var task in Plan.Tasks.Values)
                    if (taskData.IsRef(task)) {
                        Plan.DeleteTask(task);
                        return true;
                    }
                return false;
            }

            public void QueueModule(NnModuleForm mData) {
                foreach (var task in Plan.Tasks.Values) {
                    task.QueueModule(
                        new NnModuleRecord(
                            mData.Type,
                            new Dictionary<string, string>(mData.OptionsResult)
                        )
                    );
                }
            }

            public void ClearModules() {
                foreach (var task in Plan.Tasks.Values) {
                    task.ClearModules();
                }
            }

            // FIXME:  HACK!
            public void Nuke0323() {
                foreach (var task in Plan.Tasks.Values) {
                    if (task.ModuleDone.Where(x => x.Type == ModuleType.NnMain).Count() == 0)
                    if (task.ModuleQueue.Where(x => x.Type == ModuleType.NnMain).Count() == 0)
                        task.QueueModule(
                            new NnModuleRecord(
                                ModuleType.NnMain,
                                new Dictionary<string, string>{}
                            )
                        );
                }

                NnModuleRecord? occupRecord = null;

                foreach (var task in Plan.Tasks.Values) {
                    if (task.ModuleDone.Where(x => x.Type == ModuleType.NnOccup).Count() != 0) {
                        occupRecord = task.ModuleDone.Where(x => x.Type == ModuleType.NnOccup).First();
                        break;
                    }
                    if (task.ModuleQueue.Where(x => x.Type == ModuleType.NnOccup).Count() != 0) {
                        occupRecord = task.ModuleQueue.Where(x => x.Type == ModuleType.NnOccup).First();
                        break;
                    }
                }

                if (occupRecord == null) return;
                
                foreach (var task in Plan.Tasks.Values) {  
                    if (task.ModuleDone.Where(x => x.Type == ModuleType.NnOccup).Count() == 0)
                    if (task.ModuleQueue.Where(x => x.Type == ModuleType.NnOccup).Count() == 0)  
                        task.QueueModule(
                            occupRecord
                        );
                }                
            }
            // FIXME:  HACK!
            public void GenerateTReport0323() {
                string result = Plan.GetReport(ReportType.Occup2D, new Dictionary<string, string>{
                    {"X", "T"},
                    {"Y", "vol_TD"}
                });

                File.WriteAllText(Plan.FSPath.SubPath("TReport0323.txt"), result);
            }
            // FIXME:  HACK!
            public void GenerateBReport0323() {
                string result = Plan.GetReport(ReportType.Occup2D, new Dictionary<string, string>{
                    {"X", "B"},
                    {"Y", "vol_TD"}
                });

                File.WriteAllText(Plan.FSPath.SubPath("BReport0323.txt"), result);
            }

            public int TaskAmount =>
            Plan.Tasks.Count;

            public int BusyTaskAmount =>
            Plan.RunningTasks;

            //public string? TemplateId { get; }            

            // public string? Status => 
            //     Plan.Status;                
        }

        public class NnTaskData : 
            Notifier, 
            INotifyPropertyChanged, 
            IRefCompare<NnTaskData>, 
            IRefFind<NnTask>
        {
            

            NnTask Task { get; }
            public string Id => Task.Name;
            public NnParam Param { get; }

            public NnTaskData(NnTask task, NnParam param) {
                this.Task = task;
                this.Param = param;
                Subscribe(task);
            }

            public bool HasSameRef(NnTaskData another) {
                return this.Task == another?.Task;
            }

            public bool IsRef(NnTask data) =>
                this.Task == data;

            public void QueueModule(NnModuleForm mData) =>
                Task.QueueModule(
                    new NnModuleRecord(
                        mData.Type,
                        new Dictionary<string, string>(mData.OptionsResult)
                    )
                );

            public void ClearModules() => Task.ClearModules();

            public void Launch() => Task.TryDequeueAndRunModule();

            public void Terminate() => Task.Terminate();

            // public string ModuleDone {
            //     get {
            //         string result = "";
            //         foreach (var module in Task.ModuleDone)
            //             result += module.Type.ToString() + " ";
            //         return result;
            //     }
            // }

            // public string ModuleQueue {
            //     get {
            //         string result = "";
            //         foreach (var module in Task.ModuleQueue)
            //             if (module != Task.CurrentModule)
            //                 result += module.Type.ToString() + " ";

            //         return result;
            //     }
            // }            

            public string? CurrentModule => 
                Task.CurrentModule != null ? 
                Task.CurrentModule.Type.ToString() :
                null;

            public IEnumerable<NnModuleForm> ModuleList {
                get {
                    foreach (var module in Task.ModuleDone)
                        yield return new NnModuleForm(module);

                    if (Task.CurrentModule != null)
                        yield return new NnModuleForm(Task.CurrentModule);

                    foreach (var module in Task.ModuleQueue)
                        if (module != Task.CurrentModule)
                            yield return new NnModuleForm(module);                    
                }
            }

            public string? Status => 
                Task.Status ?? (
                    Task.IsBusy() ? 
                    $"Running {CurrentModule}..." :
                    null
                );
        }

        public class NnTemplateData : 
            IRefCompare<NnTemplateData>, 
            IRefFind<NnTemplate> 
        {

            NnTemplate Template { get; }
            public string Id => Template.Name;

            public NnTemplateData(NnTemplate template) {
                this.Template = template;
            }

            public bool HasSameRef(NnTemplateData another) {
                return this.Template == another?.Template;
            }

            public bool IsRef(NnTemplate data) =>
            this.Template == data;

            public NnParamForm GetForm() =>
            new NnParamForm(
                Template.Variables,
                Template.Consts
            );
        }

        public class Variable {
            public string Name { get; }
            public string? Default { get; }
            public string? Value { get; set; }

            public Variable(string name, string? defa, string? value) {
                Name = name;
                Default = defa;
                Value = value;
            }
        }

        public class NnParamForm {

            public bool IsFilled =>
                (Consts.Count + Variables.Count == 0) ||
                (
                    (Consts.Where(
                        x => (x.Value ?? x.Default) == null
                    ).Count() == 0) &&
                    (Variables.Where(
                        x => (x.Value ?? x.Default) == null
                    ).Count() == 0)
                );

            public bool FixConst { get; }
            public ImmutableList<Variable> Variables { get; }
            public ImmutableList<Variable> Consts { get; }

            public NnParamForm(
                ImmutableDictionary<string, double?> variableDefaults,
                ImmutableDictionary<string, string?> constDefaults,
                NnParam? param = null
            ) {
                this.FixConst = param != null;

                var variables = new List<Variable>();
                var consts = new List<Variable>();

                foreach (var item in constDefaults)
                    consts.Add(
                        new Variable(
                            item.Key,
                            item.Value,
                            param?.Get(item.Key)));

                foreach (var item in variableDefaults)
                    variables.Add(
                        new Variable(
                            item.Key,
                            item.Value?.ToString(),
                            param?.Get(item.Key)));

                this.Variables = variables.ToImmutableList();
                this.Consts = consts.ToImmutableList();
            }

            public NnParam? ToNnParam() {
                try {
                    return new NnParam(
                        Variables.ToDictionary(
                            x => x.Name,
                            x => Convert.ToDouble(x.Value ?? x.Default)
                        ),
                        Consts.ToDictionary(
                            x => x.Name,
                            x => x.Value ?? x.Default ??
                            throw new Exception()
                        )
                    );
                } catch {
                    return null;
                }
            }            
        }

        public class NnModuleForm {

            public ModuleType Type { get; }

            public ImmutableList<Variable> Options { get; }
            public ImmutableDictionary<string, string> OptionsResult =>
                Options.ToImmutableDictionary(
                    x => x.Name,
                    x => x.Value ?? x.Default ?? ""
                );

            public string? Result { get; } 

            public string Tag {
                get {
                    if (Result == null) return Type.ToString();
                    else return $"{Type.ToString()}: {Result}";
                }
            }

            public NnModuleForm(                
                ModuleType type
            ) {
                this.Type = type;

                var options = new List<Variable>();

                foreach (var item in NnModule.GetDefaultOptions(type))
                    options.Add(
                        new Variable(
                            item.Key,
                            item.Value,
                            null));

                this.Options = options.ToImmutableList();
            }

            public NnModuleForm(                
                NnModuleRecord record
            ) {
                this.Type = record.Type;

                var options = new List<Variable>();
                foreach (var item in NnModule.GetDefaultOptions(record.Type))
                    options.Add(
                        new Variable(
                            item.Key,
                            item.Value,
                            record.Options?[item.Key]));

                this.Options = options.ToImmutableList();
                this.Result = record.Result;
            }
        }

        NnProject project;

        NnProjectData(NnProject project) {
            this.project = project;
            Subscribe(project);
        }

        public static NnProjectData? New(string initPath) {
            NnProject? project = NnProject.New(initPath);

            return project != null?
            new NnProjectData(project):
                null;
        }

        public static NnProjectData? Load(string initPath) {
            NnProject? project = NnProject.Load(initPath);

            return project != null?
            new NnProjectData(project):
                null;
        }

        // Helper Functions to generate ViewModel datas.

        public IEnumerable<NnPlanData> PlanDatas {
            get {
                foreach (var plan in project.Plans) {
                    NnPlanData data = new NnPlanData(plan);
                    Subscribe(data);
                    yield return data;
                }
            }
        }

        public IEnumerable<NnTemplateData> TemplateDatas {
            get {
                foreach (var item in project.Templates)
                    yield return new NnTemplateData(item);
            }
        }

        public IEnumerable<ModuleType> Modules =>
            Enum.GetValues(typeof(ModuleType)).Cast<ModuleType>();

        public bool IsBusy => project.IsBusy();

        public bool IsSchedularRunning => project.SchedulerActiveFlag;

        // public string Log => project.Log;

        public void Save() => project.Save();

        public NnTemplateData? AddTemplate(string id, string content) {
            var template = NnTemplate.NewTemplate(
                id, 
                content, 
                project.FSPath.SubPath("templates").SubPath(id)
            );
            if (template == null) return null;

            if (project.AddTemplate(template))
                return new NnTemplateData(template);
            else return null;
        }

        public bool DeleteTemplate(NnTemplateData data) {
            foreach (var temp in project.Templates)
                if (data.IsRef(temp)) {
                    project.DeleteTemplate(temp);
                    return true;
                }
            return false;
        }

        NnTemplate? UnBox(NnTemplateData data) {
            foreach (var temp in project.Templates)
                if (data.IsRef(temp))
                    return temp;
            return null;
        }

        public NnPlanData? AddPlan(string planIdBase, NnTemplateData template, string? content = null) {
            // FIXME: ommiting planType!
            var temp = UnBox(template);
            if (temp == null)
                return null;

            // FIXME: plan name logic
            string planId = planIdBase;
            int counter = 1;
            while (project.Plans.Where(x => x.Name == planId).Count() != 0)
                planId = planIdBase + "_" + (counter++).ToString();

            var plan = new NnPlan(
                planId,
                project.FSPath.SubPath("plans").SubPath(planId),
                temp
            );
            if (plan == null)
                return null;

            if (project.AddPlan(plan))
                return new NnPlanData(plan);
            else return null;
        }

        public bool DeletePlan(NnPlanData planData) {
            foreach (var plan in project.Plans)
                if (planData.IsRef(plan)) {
                    project.DeletePlan(plan);
                    return true;
                }
            return false;
        }

        public void Terminate() => project.Terminate();

        public void StartScheduler() => project.StartScheduler();

        public void StopScheduler() => project.StopScheduler();

        public void ToggleScheduler() {
            if (IsSchedularRunning)
                project.StopScheduler();
            else
                project.StartScheduler();
        }
    }
}