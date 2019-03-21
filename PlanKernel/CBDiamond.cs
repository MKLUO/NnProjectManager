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

        NnPlanKernel CBDiamond(Dictionary<string, string>? options = null) =>
            new NnPlanKernel(
                CBDiamondStep, 
                options ?? new Dictionary<string, string>()
            );

        List<NnParam> Window = new List<NnParam>();
        List<NnParam> History = new List<NnParam>();

        // FIXME: CB logic
        // FIXME: Memory management!
        bool CBDiamondStep() {

            // Look for new task completed.

            // Update Window (Discard irrelevent cases and put in good cases)

            return NoPlanStep();            
        }
    }
}