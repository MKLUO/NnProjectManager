using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;


#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath; 
    using Dim = ScalarField.Dim;

    partial class NnTask {   

        RPath NnDQDJPath       => FSPath.SubPath("NnDQDJ");
        RPath NnDQDJResultPath => NnDQDJPath.SubPath($"_Result.txt");
        RPath NnDQDJReportPath => NnDQDJPath.SubPath($"_Report.txt");
        RPath NnDQDJCorrectedEnergyPath => NnDQDJPath.SubPath("_CorrectedEnergy.dat");

        NnModule NnDQDJ(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "NN DQD J",
                NnDQDJCanExecute, 
                NnDQDJIsDone, 
                NnDQDJExecute, 
                NnDQDJGetResult, 
                NnDQDJDefaultOption,
                options);

        public static ImmutableDictionary<string, string> NnDQDJDefaultOption => 
            new Dictionary<string, string>{
                {"x0", "-"}, {"x1", "-"}, {"y0", "-"}, {"y1", "-"}, {"z0", "-"}, {"z1", "-"},
                {"order", "2"}, {"enableFTDict", "no"}
            }.ToImmutableDictionary();

        bool NnDQDJCanExecute() => NnDQDReportIsDone();

        bool NnDQDJIsDone() => File.Exists(NnDQDJResultPath);        

        string NnDQDJGetResult() => File.ReadAllText(NnDQDJResultPath);

        bool NnDQDJExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {  
            
            options.TryGetValue("x0", out string? x0s);
            options.TryGetValue("x1", out string? x1s);
            options.TryGetValue("y0", out string? y0s);
            options.TryGetValue("y1", out string? y1s);
            options.TryGetValue("z0", out string? z0s);
            options.TryGetValue("z1", out string? z1s);
            double? x0 = x0s != "-" ? Convert.ToDouble(x0s) : (double?)null;
            double? x1 = x1s != "-" ? Convert.ToDouble(x1s) : (double?)null;
            double? y0 = y0s != "-" ? Convert.ToDouble(y0s) : (double?)null;
            double? y1 = y1s != "-" ? Convert.ToDouble(y1s) : (double?)null;
            double? z0 = z0s != "-" ? Convert.ToDouble(z0s) : (double?)null;
            double? z1 = z1s != "-" ? Convert.ToDouble(z1s) : (double?)null;
            options.TryGetValue("order", out string? orders);
            int order = orders != "-" ? Convert.ToUInt16(orders) : 2;

            
            //// Read in spectrum & info (from DQD Report) ////
            SetStatus("Reading spectrum ...");
            if (NnDQDReportEnergyPath?.Content == null) 
                return false;
            if (NnDQDReportIdPath?.Content == null) 
                return false;
            if (NnDQDReportAllPath?.Content == null) 
                return false;

            var specLU = new List<(int Id, double Energy)>();
            var specLD = new List<(int Id, double Energy)>();
            var specRU = new List<(int Id, double Energy)>();
            var specRD = new List<(int Id, double Energy)>();

            
            var occupations = NnAgent.ReadNXY(NnDQDReportAllPath.Content, 0) ??
                throw new Exception();
            var occups = new double[occupations.Count + 1];
            foreach (var (id, occup) in occupations) {
                occups[int.Parse(id)] = Double.Parse(occup);
            }


            foreach (var (spec, idx) in new [] {
                (specLU, 0),
                (specLD, 1),
                (specRU, 2),
                (specRD, 3)
            }) {
                var id     = NnAgent.ReadNXY(NnDQDReportIdPath.Content,     idx);
                var energy = NnAgent.ReadNXY(NnDQDReportEnergyPath.Content, idx);

                if (id == null) 
                    return false;
                if (energy == null) 
                    return false;

                for (int i = 0; i < new []{energy.Count(), id.Count()}.Min(); i++)
                    spec.Add((
                        int.Parse(id[i].Item2),
                        Double.Parse(energy[i].Item2)
                    ));
            }       

            //// Read in WF files ( # = order'

            if (order < 1) 
                return false;
            if (order > new []{specLU.Count(), specLD.Count(), specRU.Count(), specRD.Count()}.Min()) 
                return false;

            
            var lUWF = new List<(double energy, ScalarField wf)>();
            var lDWF = new List<(double energy, ScalarField wf)>();
            var rUWF = new List<(double energy, ScalarField wf)>();
            var rDWF = new List<(double energy, ScalarField wf)>();

            var allWFwithOccup = new List<(double occup, ScalarField wf)>();

            for (int i = 0; i < order; i++) {
                foreach ((var wfCollection, var spec, var spin) in new [] {
                    (lDWF, specLD, NnAgent.Spin.Down), 
                    (lUWF, specLU, NnAgent.Spin.Up), 
                    (rDWF, specRD, NnAgent.Spin.Down), 
                    (rUWF, specRU, NnAgent.Spin.Up)})
                {
                    var wfs = NnAgent.NnAmplFileEntry(NnAgent.BandType.X1, spec[i].Id, spin);
                    (var wfImagData, var wfImagCoord, _, _) = NnAgent.GetCoordAndDat(FSPath, wfs.imag);
                    (var wfRealData, var wfRealCoord, _, _) = NnAgent.GetCoordAndDat(FSPath, wfs.real);
                    if ((wfImagData?.Content == null) || (wfImagCoord?.Content == null) ||
                        (wfRealData?.Content == null) || (wfRealCoord?.Content == null))
                        break;                    

                    var wf = ScalarField.FromNnDatAndCoord(
                            wfRealData.Content, wfRealCoord.Content, 
                            wfImagData.Content, wfImagCoord.Content
                        ).Truncate((x0, y0, z0), (x1, y1, z1));

                    // Truncate WFs
                    wfCollection.Add((
                        spec[i].Energy,
                        wf                       
                    ));

                    allWFwithOccup.Add((
                        occups[spec[i].Id],
                        wf
                    ));
                }
            }

            ////// Calculate Coulomb Kernel
            var refer = lDWF[0].wf;
            int dimX = refer.Coords[Dim.X].Count;
            int dimY = refer.Coords[Dim.Y].Count;
            int dimZ = refer.Coords[Dim.Z].Count;
            var xC = refer.Coords[Dim.X].Count / 2;
            var yC = refer.Coords[Dim.Y].Count / 2;
            var zC = refer.Coords[Dim.Z].Count / 2;
            double gX = refer.Coords[Dim.X][xC] - refer.Coords[Dim.X][xC-1];
            double gY = refer.Coords[Dim.Y][yC] - refer.Coords[Dim.Y][yC-1];
            double gZ = refer.Coords[Dim.Z][zC] - refer.Coords[Dim.Z][zC-1];
            var coulomb = new Complex[
                dimX * 2 + 1,
                dimY * 2 + 1,
                dimZ * 2 + 1
            ];
            foreach (var x in Enumerable.Range(0, dimX * 2 + 1))
            foreach (var y in Enumerable.Range(0, dimY * 2 + 1))
            foreach (var z in Enumerable.Range(0, dimZ * 2 + 1))
                coulomb[x, y, z] = 
                    1.0 / Math.Sqrt(
                        Math.Pow((x - dimX - 0.5)*gX, 2) + 
                        Math.Pow((y - dimY - 0.5)*gY, 2) + 
                        Math.Pow((z - dimZ - 0.5)*gZ, 2));

                        

            //// Substrate out extra Hartree energy included in NN mean-field calculation

            // Construct Fermi-dirac calculated bound charge density

            ScalarField? boundChargeDensity = null;
            foreach (var (occup, wf) in allWFwithOccup) {
                if (boundChargeDensity == null)
                    boundChargeDensity = (wf * wf.Conj()) * occup;
                else {
                    boundChargeDensity += (wf * wf.Conj()) * occup;
                }
            }
            foreach (var wfCollection in new [] {
                lDWF, lUWF, rDWF, rUWF})
            for (int i = 0; i < order; i++)
            {
                wfCollection[i] = (
                    wfCollection[i].energy - 
                        ScalarField.Coulomb(
                            boundChargeDensity, 
                            wfCollection[i].wf * wfCollection[i].wf.Conj(),
                            coulomb).Real, 
                    wfCollection[i].wf
                );
            }

            //// Assemble WFs into pseudo densities (direct product) (den[i, j] = n(r) = <j| * |i>)
            /*
                Since the spin basis chosen here is spin-z eigenbasis, 
                coulomb repulsion of parallel (P) and antiparallel (AP) spin states are decoupled (selection rule), and can be calculated separately.
            */
            SetStatus("Assembling WF ...");

            var uWF = lUWF.Concat(rUWF).ToList();
            var dWF = lDWF.Concat(rDWF).ToList();

            var denUp   = new ScalarField[order * 2, order * 2];
            var denDown = new ScalarField[order * 2, order * 2];
            // var denFT   = new Dictionary<ScalarField, Complex[,,]>();

            foreach (var (den, wf) in new [] {
                (denUp,   uWF), 
                (denDown, dWF)
            }) 
            for (int i = 0; i < order * 2; i++)
            for (int j = i; j < order * 2; j++) {
                var wf1 = wf[i].wf;
                var wf2 = wf[j].wf;
                den[i, j] = wf1 * wf2.Conj();
                den[j, i] = den[i, j].Conj();

                // denFT[den[i, j]] = ScalarField.FFT(den[i, j].Data);
                // denFT[den[j, i]] = ScalarField.FFT(den[j, i].Data);
            }
            


            //// 2-particle basis (notation: (spin-up WF, spin-down WF)) (ordering: left-GS, left-1stEX, ... , right-GS, right-1stEX, ...)
            //// Expected AP (1, 1) GSs: apb[order] (0, order), apb[2*order*order]       (order, 0)
            //// Expected AP (2, 0) GSs: apb[0]     (0, 0),     apb[2*order*order+order] (order, order)
            //// Expected P  (1, 1) GSs: pb[order]  (0, order - 1)
            var apb = new List<(int i, int j, string name)>();
            for (int i = 0; i < order * 2; i++)
            for (int j = 0; j < order * 2; j++)
                apb.Add((i, j, $""));

            var pb = new List<(int i, int j, string name)>();
            for (int i = 0; i < order * 2; i++)
            for (int j = i + 1; j < order * 2; j++)
                pb.Add((i, j, $""));



            //// Evaluate Hamiltonians & diagonalize
            ////// energy = bound state energy + coulomb repulsion
            
            var hamAP = new Complex[apb.Count(), apb.Count()];
            var hamUU = new Complex[ pb.Count(),  pb.Count()];
            var hamDD = new Complex[ pb.Count(),  pb.Count()];

            var cHamAP = new Complex[apb.Count(), apb.Count()];
            var cHamUU = new Complex[ pb.Count(),  pb.Count()];
            var cHamDD = new Complex[ pb.Count(),  pb.Count()];


            string reportEnergy = "";
            reportEnergy += "no. energy-left-up(eV) energy-left-down(eV) energy-right-up(eV) energy-right-down(eV)\n";
            for (int i = 0; i < order ; i++) {
                reportEnergy += $"{i} {lUWF[i].energy} {lDWF[i].energy} {rUWF[i].energy} {rDWF[i].energy}\n";
            }
            File.WriteAllText(NnDQDJCorrectedEnergyPath, reportEnergy);


            // FIXME: VERY DIRTY Coulomb calculation here!

            
            options.TryGetValue("enableFTDict", out string? enableFTDicts);
            

            Dictionary<Complex[,,], Complex[,,]>? ftDict = null;
            if (enableFTDicts == "yes")
                ftDict = new Dictionary<Complex[,,], Complex[,,]>();

            foreach (var (name, ham, cHam, basis, selCoef, denSet1, denSet2, spb1, spb2) in new []{
                ("AP", hamAP, cHamAP, apb,  0.0, denUp,   denDown, uWF, dWF),
                ("UU", hamUU, cHamUU,  pb, -1.0, denUp,   denUp,   uWF, uWF),
                ("DD", hamDD, cHamDD,  pb, -1.0, denDown, denDown, dWF, dWF)
            }) {
                for (int i = 0; i < basis.Count(); i++)
                for (int j = i; j < basis.Count(); j++) {
                    
                    SetStatus($"Evaluating {name}({i},{j}) ...");

                    var den1 = denSet1[basis[i].i, basis[j].i];                
                    var den2 = denSet2[basis[i].j, basis[j].j];

                    var den3 = denSet1[basis[i].i, basis[j].j];
                    var den4 = denSet2[basis[i].j, basis[j].i];                    

                    // cHam[i, j] = 0.5 * (
                    //     ScalarField.Coulomb(den1, den2, coulomb, ftDict) + 
                    //     ScalarField.Coulomb(den2, den1, coulomb, ftDict)
                    // );   

                    cHam[i, j] = ScalarField.Coulomb(den1, den2, coulomb, ftDict); 

                    //// In parallel spin states, conjugate term of orbital WF also contributes to ham.

                    // if (selCoef != 0.0)
                    //     cHam[i, j] += selCoef * 0.5 * (
                    //     ScalarField.Coulomb(den3, den4, coulomb, ftDict) + 
                    //     ScalarField.Coulomb(den4, den3, coulomb, ftDict)
                    // );      

                    if (selCoef != 0.0)
                        cHam[i, j] += selCoef * ScalarField.Coulomb(den3, den4, coulomb, ftDict);        

                    var eigenEnergy = 0.0;
                    if ((basis[i].i == basis[j].i) && (basis[i].j == basis[j].j))
                        eigenEnergy += spb1[basis[i].i].energy + spb2[basis[i].j].energy;
                    if ((basis[i].i == basis[j].j) && (basis[i].j == basis[j].i)) 
                        eigenEnergy += selCoef * (spb1[basis[i].i].energy + spb2[basis[i].j].energy);

                    cHam[j, i] = 
                        new Complex(
                            cHam[i, j].Real,
                            - cHam[i, j].Imaginary
                        );

                    ham[i, j] = cHam[i, j] + eigenEnergy;

                    if (i == j)
                        ham[i, j] = ham[i, j].Real;
                    
                    ham[j, i] = 
                        new Complex(
                            ham[i, j].Real,
                            - ham[i, j].Imaginary
                        );                    
                }
            }

            ////// Diagonalization
            SetStatus("Diagonalizing Hamiltonians ...");

            var apEigen = Eigen.EVD(hamAP, 3);
            var uuEigen = Eigen.EVD(hamUU, 2);
            var ddEigen = Eigen.EVD(hamDD, 2);


            //// Create Reports (Verbal & NXY data)
            SetStatus("Writing Report ...");
            string verbalReport = "";

            ////// J
            var J = apEigen[0].val + apEigen[1].val - uuEigen[0].val - ddEigen[0].val;
            verbalReport += $"J = {J.ToString("E04")} (eV), order = {order}\n";

            ////// Pickup eigenstates (candidates), label them by their leading components.
            /// 
            string BasisIdxToTag(int idx) {
                if (idx >= order) return $"R{idx - order}";
                else return $"L{idx}";
            }
            
            string 
                udInfo = $"[1, 0]: none", 
                duInfo = $"[0, 1]: none", 
                uuInfo = $"[1, 1]: none", 
                ddInfo = $"[0, 0]: none";
            foreach (var (eig, basis, spb1, spb2) in new []{
                (apEigen[0], apb, uWF, dWF), 
                (apEigen[1], apb, uWF, dWF),
                (uuEigen[0], pb , uWF, uWF),
                (ddEigen[0], pb , dWF, dWF)
            }) {
                var energy = eig.val;

                var compList = new List<(int index, Complex occup)>();
                foreach (var i in Enumerable.Range(0, eig.vec.Count()))
                    compList.Add((i, eig.vec[i]));
                compList = compList.OrderByDescending(x => x.occup.Magnitude).ToList();

                var leadPair = basis[compList[0].index];
                var origEnergy = spb1[leadPair.i].energy + spb2[leadPair.j].energy;

                var energyInfo = $"{energy.ToString("E04")} ({origEnergy.ToString("E04")}) (eV)";
                string compInfo = "    ";
                foreach (var comp in compList) {
                    var occup = comp.occup.Magnitude * comp.occup.Magnitude;
                    if (occup > 0.01) {
                        compInfo += $"({BasisIdxToTag(basis[comp.index].i)},{BasisIdxToTag(basis[comp.index].j)}):{occup.ToString("0.000")} ";                        
                    }
                }

                

                if (basis == apb) {
                    // if ((leadPair.i, leadPair.j) == (0, order))
                    //     udInfo = $"[1, 0]: " + energyInfo + "\n" + compInfo;
                    // if ((leadPair.i, leadPair.j) == (order, 0))
                    //     duInfo = $"[0, 1]: " + energyInfo + "\n" + compInfo;
                    if (eig == apEigen[0])
                        udInfo = $"[1, 0]: " + energyInfo + "\n" + compInfo;
                    if (eig == apEigen[1])
                        duInfo = $"[0, 1]: " + energyInfo + "\n" + compInfo;
                }
                
                if (basis == pb) {
                    if (eig == uuEigen[0])
                        uuInfo = $"[1, 1]: " + energyInfo + "\n" + compInfo;
                    if (eig == ddEigen[0])
                        ddInfo = $"[0, 0]: " + energyInfo + "\n" + compInfo;               
                }
            }

            /// Pick up lowest 3 energy in AP
            /// 

            double pGS = (uuEigen[0].val + ddEigen[0].val) * 0.5;
            string apGSinfo = $"\nLowest 3 energy in AP:\n {apEigen[0].val - pGS}, {apEigen[1].val - pGS}, {apEigen[2].val - pGS}";

            /// Pick up ensemble GS for each particle num
            /// 

            double p0GSE = 0.0;
            double p1GSE = lDWF.Concat(lUWF).Concat(rDWF).Concat(rUWF).Select(x => x.energy).Min();
            double p2GSE = apEigen.Concat(uuEigen).Concat(ddEigen).Select(x => x.val).Min();

            string ensembleGSinfo = $"\nEnsemble GS energy for 0/1/2 particles:\n {p0GSE}, {p1GSE}, {p2GSE}";

            verbalReport +=                 
                uuInfo + "\n"  + 
                udInfo + "\n"  + 
                duInfo + "\n"  + 
                ddInfo + "\n"  +
                ensembleGSinfo +
                apGSinfo;

            File.WriteAllText(NnDQDJResultPath, $"\n{J}");
            File.WriteAllText(NnDQDJReportPath, verbalReport);

            return true;
        }

        public string[]? GetEnergies() {
            if (!File.Exists(NnDQDJReportPath)) return null;
            bool targetIsNextLine = false;
            foreach (var line in File.ReadAllLines(NnDQDJReportPath)) {
                if (targetIsNextLine) 
                    return line.Splitter(",");                
                if (line.Contains("Lowest 3 energy in "))
                    targetIsNextLine = true;
            }
            return null;
        }
    }
}