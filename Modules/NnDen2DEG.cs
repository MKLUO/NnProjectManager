using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath; 

    partial class NnTask {   

        RPath NnDen2DEGPath => FSPath.SubPath("NnDen2DEG");
        RPath NnDen2DEGResultPath => NnDen2DEGPath.SubPath("den2DEG.txt");

        NnModule NnDen2DEG(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "NN Occupation",
                NnDen2DEGCanExecute, 
                NnDen2DEGIsDone, 
                NnDen2DEGExecute, 
                NnDen2DEGGetResult, 
                NnDen2DEGDefaultOption.ToImmutableDictionary(),
                options);

        public static ImmutableDictionary<string, string> NnDen2DEGDefaultOption = 
            new Dictionary<string, string>{
                {"x0", "-"},
                {"y0", "-"},
                {"z0", "-"},
                {"x1", "-"},
                {"y1", "-"},
                {"z1", "-"}
            }.ToImmutableDictionary();

        bool NnDen2DEGCanExecute() {
            return NnMainIsDone();
        }

        bool NnDen2DEGIsDone() {
            return File.Exists(NnDen2DEGResultPath);
        }

        string NnDen2DEGGetResult() {
            try {
                return File.ReadAllText(NnDen2DEGResultPath);
            } catch {
                return "(error)";
            }
        }

        bool NnDen2DEGExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {
            // FIXME: This can be very inaccurate with small sampling area!
            

            // (RPath? data, RPath? coord) = NnAgent.GetCoordAndDat(FSPath, $"density_electron");
            // if ((data?.Content == null) || (coord?.Content == null))
            //     return false;

            // double result = 0.0;

            // ScalarField field = ScalarField.FromNnDatAndCoord(data.Content, coord.Content);

            // options.TryGetValue("x0", out string? x0s);
            // options.TryGetValue("x1", out string? x1s);
            // options.TryGetValue("y0", out string? y0s);
            // options.TryGetValue("y1", out string? y1s);
            // options.TryGetValue("z0", out string? z0s);
            // options.TryGetValue("z1", out string? z1s);

            // double? x0, x1, y0, y1, z0, z1;
            // try {
            //     x0 = x0s != "-" ? Convert.ToDouble(x0s) : (double?)null;
            //     x1 = x1s != "-" ? Convert.ToDouble(x1s) : (double?)null;
            //     y0 = y0s != "-" ? Convert.ToDouble(y0s) : (double?)null;
            //     y1 = y1s != "-" ? Convert.ToDouble(y1s) : (double?)null;
            //     z0 = z0s != "-" ? Convert.ToDouble(z0s) : (double?)null;
            //     z1 = z1s != "-" ? Convert.ToDouble(z1s) : (double?)null;
            // } catch {
            //     return false;
            // }

            // double x0d = x0 ?? throw new Exception();
            // double x1d = x1 ?? throw new Exception();
            // double y0d = y0 ?? throw new Exception();
            // double y1d = y1 ?? throw new Exception();            

            // result = field.IntegrateInRange((x0, y0, z0), (x1, y1, z1)) / ((x1d - x0d) * (y1d - y0d));

            // // TODO: Spectrum annotation
            // File.WriteAllText(NnDen2DEGResultPath, result.ToString());
            return true;
        }
    }
}