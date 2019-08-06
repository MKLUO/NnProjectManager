using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;


#nullable enable

namespace NnManager
{
    using RPath = Util.RestrictedPath;

    partial class NnTask
    {

        RPath Nn3LegsTestPath => FSPath.SubPath("Nn3LegsTest");
        
        NnModule Nn3LegsTest(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "三腿測試用的",
                Nn3LegsTestCanExecute,
                Nn3LegsTestIsDone,
                Nn3LegsTestExecute,
                Nn3LegsTestGetResult,
                Nn3LegsTestDefaultOption,
                options);

        public static ImmutableDictionary<string, string> Nn3LegsTestDefaultOption =>
            new Dictionary<string, string>{
                {"FileName", "hello"}
            }.ToImmutableDictionary();

        bool Nn3LegsTestCanExecute() => true;

        bool Nn3LegsTestIsDone() => false;

        string Nn3LegsTestGetResult() => "well";

        bool Nn3LegsTestExecute(CancellationToken ct, ImmutableDictionary<string, string> options)
        {
            options.TryGetValue("FileName", out string? fileName);
            RPath nn3LegsTestHelloWorldPath = Nn3LegsTestPath.SubPath($"{fileName}.txt");

            File.WriteAllText(nn3LegsTestHelloWorldPath, "還在測試\n\r依舊測試");
            return true;
        }
    }
}