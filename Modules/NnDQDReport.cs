using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;


#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath; 

    partial class NnTask {
        
        RPath NnDQDReportPath       => FSPath.SubPath("NnDQDReport");
        RPath NnDQDReportAllPath    => NnDQDReportPath.SubPath("_AnnotatedSpectrum.dat");
        RPath NnDQDReportEnergyPath => NnDQDReportPath.SubPath("_BoundStateEnergy.dat");
        RPath NnDQDReportIdPath     => NnDQDReportPath.SubPath("_BoundStateId.dat");
        RPath NnDQDReportMiscPath   => NnDQDReportPath.SubPath("_Report.txt");

        NnModule NnDQDReport(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "NN DQD Report",
                NnDQDReportCanExecute, 
                NnDQDReportIsDone, 
                NnDQDReportExecute, 
                NnDQDReportGetResult, 
                NnDQDReportDefaultOption,
                options);

        public static ImmutableDictionary<string, string> NnDQDReportDefaultOption => 
            new Dictionary<string, string>{
                {"portion", "0.7"},
                {"L_x0", "-"}, {"L_y0", "-"}, {"L_z0", "-"}, {"L_x1", "-"}, {"L_y1", "-"}, {"L_z1", "-"}, 
                {"R_x0", "-"}, {"R_y0", "-"}, {"R_z0", "-"}, {"R_x1", "-"}, {"R_y1", "-"}, {"R_z1", "-"}, 
                {"band", "X1"}
            }.ToImmutableDictionary();

        bool NnDQDReportCanExecute() => NnMainIsDone();

        bool NnDQDReportIsDone() => File.Exists(NnDQDReportAllPath);        

        string NnDQDReportGetResult() => File.ReadAllText(NnDQDReportMiscPath);

        public (double l, double r) NnDQDReportChgs() {
            if (!NnDQDReportIsDone())
                return (-1, -1);

            string result = NnDQDReportGetResult();
            string[] entries = {};
            
            foreach (var line in result.Split('\n'))
                if (line.Contains("("))
                    entries = line.Substring(1, line.Length - 2).Splitter(",");

            return (Double.Parse(entries[0]), Double.Parse(entries[1]));
        }

        bool NnDQDReportExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {

            //==========Clearing old outputs==========
            if (NnDQDReportPath.Exists())
                foreach (var file in Directory.GetFiles(NnDQDReportPath))
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



            //==========Read energy & occupation from spectrum==========
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



            //==========Calculate dot-region occupation from wave function (probabilities)==========
            var portionSpin = CalculateSpinPortion();
            var portionL = CalculatePortions((L_x0, L_y0, L_z0), (L_x1, L_y1, L_z1));
            var portionR = CalculatePortions((R_x0, R_y0, R_z0), (R_x1, R_y1, R_z1));

            if (occup.Keys.Except(portionL.Keys).Count() != 0)
                return false;
            if (occup.Keys.Except(portionR.Keys).Count() != 0)
                return false;



            //==========Identify bound states and write spectrum reports==========
            string reportAll   = "";
            reportAll   += "no. occupation[electrons] portion-left portion-right energy(eV)\n";

            var specLU = new List<(int Id, double Energy)>();
            var specRU = new List<(int Id, double Energy)>();
            var specLD = new List<(int Id, double Energy)>();
            var specRD = new List<(int Id, double Energy)>();

            int lUSpecCount = 0, 
                rUSpecCount = 0,
                lDSpecCount = 0, 
                rDSpecCount = 0;
            foreach (var id in occup.Keys.OrderBy(k => k)) {
                reportAll += $"{id} {occup[id]} {portionL[id]} {portionR[id]} {energy[id]}\n";


                // Move bound state prob.s to output directory for reference.
                (RPath? data, RPath? coord, RPath? fld, RPath? v) = 
                    NnAgent.GetCoordAndDat(FSPath, NnAgent.NnProbFileEntry(NnAgent.BandType.X1, id));

                (RPath? dataNew, RPath? coordNew, _, RPath? vNew) = 
                    NnAgent.GetCoordAndDat(NnDQDReportPath, NnAgent.NnProbFileEntry(NnAgent.BandType.X1, id), true);
                (_, _, RPath? fldLUNew, _) = NnAgent.GetCoordAndDat(NnDQDReportPath, $"LU_" + NnAgent.NnProbFileEntry(NnAgent.BandType.X1, lUSpecCount), true);
                (_, _, RPath? fldRUNew, _) = NnAgent.GetCoordAndDat(NnDQDReportPath, $"RU_" + NnAgent.NnProbFileEntry(NnAgent.BandType.X1, rUSpecCount), true);
                (_, _, RPath? fldLDNew, _) = NnAgent.GetCoordAndDat(NnDQDReportPath, $"LD_" + NnAgent.NnProbFileEntry(NnAgent.BandType.X1, lDSpecCount), true);
                (_, _, RPath? fldRDNew, _) = NnAgent.GetCoordAndDat(NnDQDReportPath, $"RD_" + NnAgent.NnProbFileEntry(NnAgent.BandType.X1, rDSpecCount), true);

                if (data == null || coord == null || fld == null || v == null ||
                    dataNew == null || coordNew == null || vNew == null ||
                    fldLUNew == null || fldRUNew == null || fldLDNew == null || fldRDNew == null) 
                    continue;


                // Categorize bound states according to their side and spin
                if (portionL[id] > portion) {                                          
                    File.Copy(data, dataNew);
                    File.Copy(coord, coordNew);
                    File.Copy(v, vNew);
                    
                    if (portionSpin[id] > 0.5) {
                        specLU.Add((id, energy[id]));
                        lUSpecCount++; 
                        File.Copy(fld, fldLUNew);
                    } else {
                        specLD.Add((id, energy[id]));
                        lDSpecCount++; 
                        File.Copy(fld, fldLDNew);
                    }
                } else if (portionR[id] > portion) {
                    File.Copy(data, dataNew);
                    File.Copy(coord, coordNew);
                    File.Copy(v, vNew);

                    if (portionSpin[id] > 0.5) {
                        specRU.Add((id, energy[id]));
                        rUSpecCount++; 
                        File.Copy(fld, fldRUNew);
                    } else {
                        specRD.Add((id, energy[id]));
                        rDSpecCount++; 
                        File.Copy(fld, fldRDNew);
                    }
                }
            }



            //==========Output spectrum reports==========
            string reportEnergy = "";
            string reportId = "";
            reportId     += "no. id-left-up id-left-down id-right-up id-right-down\n";
            reportEnergy += "no. energy-left-up(eV) energy-left-down(eV) energy-right-up(eV) energy-right-down(eV)\n";

            var boundStateCount = new []{specLU.Count(), specLD.Count(), specRU.Count(), specRD.Count()}.Min();

            for (int i = 0; i < boundStateCount ; i++) {
                reportId     += $"{i} {specLU[i].Id} {specLD[i].Id} {specRU[i].Id} {specRD[i].Id}\n";
                reportEnergy += $"{i} {specLU[i].Energy} {specLD[i].Energy} {specRU[i].Energy} {specRD[i].Energy}\n";
            }

            File.WriteAllText(NnDQDReportAllPath,    reportAll);
            File.WriteAllText(NnDQDReportEnergyPath, reportEnergy);
            File.WriteAllText(NnDQDReportIdPath,     reportId);
            File.WriteAllText(NnDQDReportMiscPath, 
                $"\n({occupL}, {occupR})\nBound State #: {boundStateCount}");


            return true;
        }

        ImmutableDictionary<int, double> CalculateSpinPortion() {
            int counter = 1;
            Dictionary<int, double> result = new Dictionary<int, double>();
            while (true) {                
                var entriesDown = NnAgent.NnAmplFileEntry(NnAgent.BandType.X1, counter, NnAgent.Spin.Down);
                var entriesUp = NnAgent.NnAmplFileEntry(NnAgent.BandType.X1, counter, NnAgent.Spin.Up);
                (RPath? dataImagD, RPath? coordImagD, _, _) = NnAgent.GetCoordAndDat(FSPath, entriesDown.imag);
                (RPath? dataRealD, RPath? coordRealD, _, _) = NnAgent.GetCoordAndDat(FSPath, entriesDown.real);
                (RPath? dataImagU, RPath? coordImagU, _, _) = NnAgent.GetCoordAndDat(FSPath, entriesUp.imag);
                (RPath? dataRealU, RPath? coordRealU, _, _) = NnAgent.GetCoordAndDat(FSPath, entriesUp.real);
                if ((dataImagD?.Content == null) || (coordImagD?.Content == null) ||
                    (dataImagU?.Content == null) || (coordImagU?.Content == null) ||
                    (dataRealD?.Content == null) || (coordRealD?.Content == null) ||
                    (dataRealU?.Content == null) || (coordRealU?.Content == null))
                    break;

                ScalarField fieldImagD = ScalarField.FromNnDatAndCoord(dataImagD.Content, coordImagD.Content);  
                ScalarField fieldImagU = ScalarField.FromNnDatAndCoord(dataImagU.Content, coordImagU.Content);  
                ScalarField fieldRealD = ScalarField.FromNnDatAndCoord(dataRealD.Content, coordRealD.Content);  
                ScalarField fieldRealU = ScalarField.FromNnDatAndCoord(dataRealU.Content, coordRealU.Content);  

                var normU = fieldImagU.Norm() + fieldRealU.Norm();
                var normD = fieldImagD.Norm() + fieldRealD.Norm();

                result[counter] = normU / (normU + normD);
                counter++;
            }

            return result.ToImmutableDictionary();
        }

        ImmutableDictionary<int, double> CalculatePortions(
            // (string x, string y, string z) coord0,
            // (string x, string y, string z) coord1
            (double? x, double? y, double? z) coord0,
            (double? x, double? y, double? z) coord1
        ) {
            int counter = 1;
            RPath? data, coord;

            Dictionary<int, double> result = new Dictionary<int, double>();
            while( ((data, coord, _, _) = NnAgent.GetCoordAndDat(FSPath, NnAgent.NnProbFileEntry(NnAgent.BandType.X1, counter))) != (null, null, null, null)) {                
                if ((data?.Content == null) || (coord?.Content == null))
                    continue;

                ScalarField field = ScalarField.FromNnDatAndCoord(data.Content, coord.Content);  
                result[counter] = field.PortionInRange(coord0, coord1);
                counter++;
            }

            return result.ToImmutableDictionary();
        }
    }
}