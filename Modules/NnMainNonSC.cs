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

            // Extract potential and bound charge from NnMain.
            var potentialFile = NnAgent.GetCoordAndDat(
                FSPath, NnAgent.NnPotentialFileEntry());

            var potential = ScalarField.FromNnDatAndCoord(
                potentialFile.data, potentialFile.coord
            );

            // Evalute corrected potential.

            // Oridinary NN run (just like NnMain).

            // Checkpoint: As usual, Output WFs to be analyzed by NnDQDReport.
            
            return true;
        }
    }
}