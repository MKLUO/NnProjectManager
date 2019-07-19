using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;     

    partial class NnPlan {

        NnPlanKernel NoPlan(Dictionary<string, string>? options = null) =>
            new NnPlanKernel(
                NoPlanStep, 
                options ?? new Dictionary<string, string>()
            );

        bool NoPlanStep() {
            foreach (var task in tasks.Values)
                if (task.TryDequeueAndRunModule())
                    return true;
                return false;
        }
    }
}