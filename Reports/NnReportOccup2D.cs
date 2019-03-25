using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;     

    partial class NnPlan {

        public NnReport NnReportOccup2D(Dictionary<string, string> options) =>
            new NnReport(
                "Occup2D",
                NnReportOccup2DExecute, 
                NnReportOccup2DDefaultOption, 
                options);

        // FIXME:
        public static Dictionary<string, string> NnReportOccup2DDefaultOption = 
            new Dictionary<string, string>{
                {"X", "-"},
                {"Y", "-"}
            };

        string NnReportOccup2DExecute(ImmutableDictionary<string, string> options) {

            string xTag = options["X"];
            string yTag = options["Y"];

            string result = $"{xTag}(nm),{yTag}(nm),Occupation";
            foreach (var taskData in tasks)
            {
                var param = taskData.Key;
                var task = taskData.Value;

                var records = task.ModuleDone.Where(x => x.Type == ModuleType.NnOccup);
                if (records.Count() == 0) continue;                

                result += $"\r\n{param.Variables[xTag]},{param.Variables[yTag]},{records.Last().Result}";
            }

            return result;
        }
    }
}