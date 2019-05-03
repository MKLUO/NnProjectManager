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

    partial class NnTask {

        public NnModule Dummy(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "(Module not implemented)",
                () => true, 
                () => true, 
                (CancellationToken ct, ImmutableDictionary<string, string> options) => true, 
                () => "(none)",
                NnModule.GetDefaultOptions(ModuleType.Dummy));
    }
}