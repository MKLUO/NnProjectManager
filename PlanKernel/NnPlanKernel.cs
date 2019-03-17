using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;
        
    public enum PlanType {
        NoPlan,
        CBDiamond
    }

    public class NnPlanKernel {
        // public abstract string Name { get; }
        // protected abstract NnTask Task { get; }
        // protected abstract Dictionary<string, string> Options { get; }

        public NnPlanKernel(
            Action step, 
            Dictionary<string, string>? options = null
        ) {
            this.step = step;
            this.Options = options ?? new Dictionary<string, string>();
        }

        Dictionary<string, string> Options { get; }

        Action step;
        public void Step() => step();
    }
}