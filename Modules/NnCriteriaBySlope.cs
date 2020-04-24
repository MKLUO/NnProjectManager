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

        RPath CriteriaBySlopePath => FSPath.SubPath("CriteriaBySlope");
        
        NnModule CriteriaBySlope(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "三腿測試用的",
                CriteriaBySlopeCanExecute,
                CriteriaBySlopeIsDone,
                CriteriaBySlopeExecute,
                CriteriaBySlopeGetResult,
                CriteriaBySlopeDefaultOption,
                options);

        public static ImmutableDictionary<string, string> CriteriaBySlopeDefaultOption => 
            new Dictionary<string, string>{
                {"portion", "0.7"},
                {"L_x0", "-"}, {"L_y0", "-"}, {"L_z0", "-"}, {"L_x1", "-"}, {"L_y1", "-"}, {"L_z1", "-"}, 
                {"R_x0", "-"}, {"R_y0", "-"}, {"R_z0", "-"}, {"R_x1", "-"}, {"R_y1", "-"}, {"R_z1", "-"}, 
                {"band", "X1"}
            }.ToImmutableDictionary();

        bool CriteriaBySlopeCanExecute() => NnMainIsDone();

        bool CriteriaBySlopeIsDone() => File.Exists(CriteriaBySlopeAllPath);        

        string CriteriaBySlopeGetResult() => File.ReadAllText(CriteriaBySlopeMiscPath);

        bool CriteriaBySlopeExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {

            //==========Clearing old outputs==========
            if (CriteriaBySlopePath.Exists())
                foreach (var file in Directory.GetFiles(CriteriaBySlopePath))
                    File.Delete(file);



            //==========Remove data of other bands==========
            // FIXME: What would happen if there's multiple quantum region ?
            options.TryGetValue("band", out string? band);
            if (band == "X1") {
                foreach (var file in Directory.GetFiles(FSPath, "*_X2*"))
                    File.Delete(file);
                foreach (var file in Directory.GetFiles(FSPath, "*_X3*"))
                    File.Delete(file);
            } else {
                // FIXME: Bands other than X1 is not implemented yet!
                return false;
            }


            //==========Categorize 1d & 2d data (1d & 2d are not needed)==========
            string[] subDirs = {"1d", "2d"};
            foreach (string subDir in subDirs) {
                var path = FSPath.SubPath(subDir);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                foreach (var file in Directory.GetFiles(FSPath, $"*_{subDir}_*")) {
                    var dest =  path.SubPath(Path.GetFileName(file), false);
                    if (dest.Exists()) File.Delete(dest);
                    File.Move(file, dest);
                }
            }



            //==========Read electricfeild from spectrum==========
            // TODO: Bands other than X1 is not implemented yet!
            RPath? spectrumFile = FSPath.SubPath("wf_spectrum_quantum_region_X1.dat");
            if (spectrumFile?.Content == null) 
                return false;
            
            Dictionary<int, double>? energy = 
                NnAgent.ReadNXY(spectrumFile.Content, 0).ToDictionary(
                    p => int.Parse(p.Item1),
                    p => Double.Parse(p.Item2, System.Globalization.NumberStyles.Float)
                );
            if (energy == null) 
                return false;

            Dictionary<int, double>? occup = 
                NnAgent.ReadNXY(spectrumFile.Content, 1).ToDictionary(
                    p => int.Parse(p.Item1),
                    p => Double.Parse(p.Item2, System.Globalization.NumberStyles.Float)
                );
            if (occup == null) 
                return false;

            
            options.TryGetValue("portion", out string? portions);
            options.TryGetValue("L_x0",    out string? L_x0s);
            options.TryGetValue("L_x1",    out string? L_x1s);
            options.TryGetValue("L_y0",    out string? L_y0s);
            options.TryGetValue("L_y1",    out string? L_y1s);
            options.TryGetValue("L_z0",    out string? L_z0s);
            options.TryGetValue("L_z1",    out string? L_z1s);
            options.TryGetValue("R_x0",    out string? R_x0s);
            options.TryGetValue("R_x1",    out string? R_x1s);
            options.TryGetValue("R_y0",    out string? R_y0s);
            options.TryGetValue("R_y1",    out string? R_y1s);
            options.TryGetValue("R_z0",    out string? R_z0s);
            options.TryGetValue("R_z1",    out string? R_z1s);

            double? portion = portions != "-" ? Convert.ToDouble(portions) : (double?)null;
            double? L_x0    = L_x0s    != "-" ? Convert.ToDouble(L_x0s)    : (double?)null;
            double? L_x1    = L_x1s    != "-" ? Convert.ToDouble(L_x1s)    : (double?)null;
            double? L_y0    = L_y0s    != "-" ? Convert.ToDouble(L_y0s)    : (double?)null;
            double? L_y1    = L_y1s    != "-" ? Convert.ToDouble(L_y1s)    : (double?)null;
            double? L_z0    = L_z0s    != "-" ? Convert.ToDouble(L_z0s)    : (double?)null;
            double? L_z1    = L_z1s    != "-" ? Convert.ToDouble(L_z1s)    : (double?)null;
            double? R_x0    = R_x0s    != "-" ? Convert.ToDouble(R_x0s)    : (double?)null;
            double? R_x1    = R_x1s    != "-" ? Convert.ToDouble(R_x1s)    : (double?)null;
            double? R_y0    = R_y0s    != "-" ? Convert.ToDouble(R_y0s)    : (double?)null;
            double? R_y1    = R_y1s    != "-" ? Convert.ToDouble(R_y1s)    : (double?)null;
            double? R_z0    = R_z0s    != "-" ? Convert.ToDouble(R_z0s)    : (double?)null;
            double? R_z1    = R_z1s    != "-" ? Convert.ToDouble(R_z1s)    : (double?)null;



            //==========Calculate dot-region occupation from e- density==========
            (var eDData, var eDCoord, _, _) = NnAgent.GetCoordAndDat(FSPath, "density_electron");
            if ((eDData?.Content == null) || (eDCoord?.Content == null)) return false;
            ScalarField eD = ScalarField.FromNnDatAndCoord(eDData.Content, eDCoord.Content);

            var occupL = 0.001 * eD.IntegrateInRange((L_x0, L_y0, L_z0), (L_x1, L_y1, L_z1)).Real;
            var occupR = 0.001 * eD.IntegrateInRange((R_x0, R_y0, R_z0), (R_x1, R_y1, R_z1)).Real;


        bool CriteriaBySlopeExecute(CancellationToken ct, ImmutableDictionary<string, string> options)
        {
            options.TryGetValue("FileName", out string? fileName);
            RPath CriteriaBySlopeHelloWorldPath = CriteriaBySlopePath.SubPath($"{fileName}.txt");

            File.WriteAllText(CriteriaBySlopeHelloWorldPath, "還在測試\n\r依舊測試");
            return true;
        }
    }
}