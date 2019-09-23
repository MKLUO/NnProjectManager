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

        RPath NnMainNonSCPath => FSPath.SubPath("NnMainNonSC");
        
        RPath NnMainNonSCReportPath => NnMainNonSCPath.SubPath("_Report.txt");

        public NnModule NnMainNonSC(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "NN Main Non-SC",
                NnMainNonSCCanExecute, 
                NnMainNonSCIsDone, 
                NnMainNonSCExecute, 
                NnMainNonSCGetResult,
                NnModule.GetDefaultOptions(ModuleType.NnMainNonSC),
                options);

        public static ImmutableDictionary<string, string> NnMainNonSCDefaultOption =>
            new Dictionary<string, string> { 
                { "x0", "-" },
                { "x1", "-" },
                { "y0", "-" },
                { "y1", "-" },
                { "z0", "-" },
                { "z1", "-" }
            }.ToImmutableDictionary(); 


        bool NnMainNonSCCanExecute() => NnMainIsDone();

        bool NnMainNonSCIsDone() {
            // FIXME: Check report?
            return true;
        }

        RPath NnMainNonSCToken => FSPath.SubPath("IsNonSC");
        bool NnMainNonSCIsNonSC() {
            return File.Exists(NnMainNonSCToken);
        }

        string NnMainNonSCGetResult() {
            try {
                // FIXME: done (converge) / diverge.
                File.ReadAllText(NnMainNonSCReportPath);
                return "(done)";
            } catch {
                return "(error)";
            }
        }

        bool NnMainNonSCExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {

            options.TryGetValue("x0", out string? x0s);
            options.TryGetValue("x1", out string? x1s);
            options.TryGetValue("y0", out string? y0s);
            options.TryGetValue("y1", out string? y1s);
            options.TryGetValue("z0", out string? z0s);
            options.TryGetValue("z1", out string? z1s);

            double? x0 = x0s != "-" ? Convert.ToDouble(x0s) : (double?) null;
            double? x1 = x1s != "-" ? Convert.ToDouble(x1s) : (double?) null;
            double? y0 = y0s != "-" ? Convert.ToDouble(y0s) : (double?) null;
            double? y1 = y1s != "-" ? Convert.ToDouble(y1s) : (double?) null;
            double? z0 = z0s != "-" ? Convert.ToDouble(z0s) : (double?) null;
            double? z1 = z1s != "-" ? Convert.ToDouble(z1s) : (double?) null;

            // Extract potential and bound charge from NnMain.
            var potentialFile = NnAgent.GetCoordAndDat(
                FSPath, NnAgent.NnPotentialFileEntry());
            var potential = ScalarField.FromNnDatAndCoord(
                potentialFile.data.Content, potentialFile.coord.Content
            ); // Unit: eV

            var densityFile = NnAgent.GetCoordAndDat(
                FSPath, NnAgent.NnDensityFileEntry());
            var density = ScalarField.FromNnDatAndCoord(
                densityFile.data.Content, densityFile.coord.Content
            ); // Unit: 1E18/cm3 = 1E-3/nm3
            var boundChargeDensity = 0.001 * density.TruncateAndKeep((x0, y0, z0), (x1, y1, z1));

            // Evalute corrected potential.

            var boundChargePotential = 
                ScalarField.CoulombPotential_ByConvolutionWithKernel(
                    boundChargeDensity,
                    NnAgent.CoulombKernel(boundChargeDensity)
                );

            var correctedPotential = potential + boundChargePotential;

            // Copy correceted potential to NonSC folder

            foreach ((RPath src, string extName) in new [] {
                // (potentialFile.data,  ".dat"),
                (potentialFile.coord, ".coord"),
                (potentialFile.fld,   ".fld"),
                (potentialFile.v,     ".v")
            }) File.Copy(src, NnMainNonSCPath.SubPath("potential" + extName), true);

            File.WriteAllLines(
                NnMainNonSCPath.SubPath("potential.dat"), 
                ScalarField.ToNnRealFieldDatLines(correctedPotential));

            // Oridinary NN run (just like NnMain).

            // NnAgent.RunNnStructure(
            //     NnMainPath, ContentNonSC, ct, Type
            // );
            NnAgent.RunNn(
                NnMainPath, ContentNonSC, ct, Type
            );

            File.Create(NnMainNonSCToken);

            return !ct.IsCancellationRequested;;
        }
    }
}