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

    partial class NnPlan {        
        NnPlanKernel? kernel; 
        void KernelInitialize() {
            switch (Type) {
                case PlanType.NoPlan:
                    kernel = NoPlan();
                    return;
                
                case PlanType.CBDiamond:
                    kernel = CBDiamond();
                    return;
            }
        }
    }

    public class NnPlanKernel {
        // public abstract string Name { get; }
        // protected abstract NnTask Task { get; }
        // protected abstract Dictionary<string, string> Options { get; }

        public NnPlanKernel(
            Func<bool> step, 
            Dictionary<string, string>? options = null
        ) {
            this.step = step;
            this.Options = options ?? new Dictionary<string, string>();
        }

        Dictionary<string, string> Options { get; }

        Func<bool> step;
        public bool Step() => step();
    }
}