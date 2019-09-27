
using System.Collections.Generic;

namespace NnManager {
    interface INnProject {
        IEnumerable<INnPlan> Plans { get; }
        IEnumerable<INnTemplate> Templates { get; }
    }

    interface INnPlan
    {
        
    }

    interface INnTemplate
    {
        
    }
}