using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
// using System.Text.RegularExpressions;
using System.Threading;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;
    // using Dim = ScalarField.Dim;

    partial class NnTask {

        RPath NnDQDJPath => FSPath.SubPath("NnDQDJ");
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
            new Dictionary<string, string> { { "x0", "-" },
                { "x1", "-" },
                { "y0", "-" },
                { "y1", "-" },
                { "z0", "-" },
                { "z1", "-" },
                { "order", "2" },
                { "3particle", "no" },
                { "enableFTDict", "no" }
            }.ToImmutableDictionary();

        bool NnDQDJCanExecute() => NnDQDReportIsDone();

        bool NnDQDJIsDone() => File.Exists(NnDQDJResultPath);

        string NnDQDJGetResult() => File.ReadAllText(NnDQDJResultPath);

        public List<double> NnDQDJEnergies() {
            if (!NnDQDJIsDone())
                return new List<double>();

            string result = NnDQDJGetResult();
            string[] energies = { };

            foreach (var line in result.Split('\n'))
                if (line.Contains("Ensemble"))
                    energies = line.Substring(line.IndexOf(':')).Splitter(",");

            return energies.Select(s => Double.Parse(s)).ToList();
        }

        public int NnDQDJCS => NnDQDJEnergies().IndexOf(NnDQDJEnergies().Min());

        bool NnDQDJExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {

            // FIXME: Can it be reduced?
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
            options.TryGetValue("order", out string? orders);
            int order = orders != "-" ? Convert.ToUInt16(orders) : 2;

            //// NOTE: Read in spectrum & info (from DQD Report) ////
            SetStatus("Reading spectrum ...");
            if (NnDQDReportEnergyPath?.Content == null)
                return false;
            if (NnDQDReportIdPath?.Content == null)
                return false;
            if (NnDQDReportAllPath?.Content == null)
                return false;

            var specLU = new List < (int Id, double Energy) > ();
            var specLD = new List < (int Id, double Energy) > ();
            var specRU = new List < (int Id, double Energy) > ();
            var specRD = new List < (int Id, double Energy) > ();

            var occups = NnAgent.ReadNXYIntDouble(NnDQDReportAllPath.Content, 0) ??
                throw new Exception();

            foreach (var(spec, idx) in new [] {
                    (specLU, 0),
                    (specLD, 1),
                    (specRU, 2),
                    (specRD, 3)
                }) {
                var id = NnAgent.ReadNXY(NnDQDReportIdPath.Content, idx);
                var energy = NnAgent.ReadNXY(NnDQDReportEnergyPath.Content, idx);

                if (id == null)
                    return false;
                if (energy == null)
                    return false;

                for (int i = 0; i < new [] { energy.Count(), id.Count() }.Min(); i++)
                    spec.Add((
                        int.Parse(id[i].Item2),
                        Double.Parse(energy[i].Item2)
                    ));
            }

            //// NOTE: Read in WF files ( # = order'

            if (order < 1)
                return false;
            if (order > new [] { specLU.Count(), specLD.Count(), specRU.Count(), specRD.Count() }.Min())
                return false;

            var lUWF = new List < (double energy, ScalarField wf) > ();
            var lDWF = new List < (double energy, ScalarField wf) > ();
            var rUWF = new List < (double energy, ScalarField wf) > ();
            var rDWF = new List < (double energy, ScalarField wf) > ();

            var allWFwithOccup = new List < (double occup, ScalarField wf) > ();

            for (int i = 0; i < order; i++) {
                foreach ((var wfCollection, var spec, var spin) in new [] {
                        (lDWF, specLD, NnAgent.Spin.Down),
                        (lUWF, specLU, NnAgent.Spin.Up),
                        (rDWF, specRD, NnAgent.Spin.Down),
                        (rUWF, specRU, NnAgent.Spin.Up)
                    }) {
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

            ////// NOTE: Calculate Coulomb Kernel
            var coulomb = NnAgent.CoulombKernel(lDWF[0].wf);

            //// NOTE: Substrate out extra Hartree energy included in NN mean-field calculation

            // NOTE: Construct Fermi-dirac calculated bound charge density

            ScalarField? boundChargeDensity = null;
            foreach (var(occup, wf) in allWFwithOccup) {
                var norm = (wf * wf.Conj()) * occup;
                if (boundChargeDensity == null)
                    boundChargeDensity = norm;
                else
                    boundChargeDensity += norm;
            }

            if (!NnMainNonSCIsNonSC())
                foreach (var wfCollection in new [] {
                        lDWF,
                        lUWF,
                        rDWF,
                        rUWF
                    })
                    for (int i = 0; i < order; i++) {
                        wfCollection[i] = (
                            wfCollection[i].energy -
                            ScalarField.Coulomb(
                                boundChargeDensity,
                                wfCollection[i].wf * wfCollection[i].wf.Conj(),
                                coulomb).Real,
                            wfCollection[i].wf
                        );
                    }

            //// NOTE: Assemble WFs into pseudo densities (direct product) (den[i, j] = n(r) = <j| * |i>)
            /*
                NOTE: Since the spin basis chosen here is spin-z eigenbasis, 
                coulomb repulsion of parallel (P) and antiparallel (AP) spin states are decoupled (selection rule), and can be calculated separately.
            */
            SetStatus("Assembling WF ...");

            var uWF = lUWF.Concat(rUWF).ToList();
            var dWF = lDWF.Concat(rDWF).ToList();

            var denUp = new ScalarField[order * 2, order * 2];
            var denDown = new ScalarField[order * 2, order * 2];

            foreach (var(den, wf) in new [] {
                    (denUp, uWF),
                    (denDown, dWF)
                })
                for (int i = 0; i < order * 2; i++)
                    for (int j = i; j < order * 2; j++) {
                        var wf1 = wf[i].wf;
                        var wf2 = wf[j].wf;
                        den[i, j] = wf1 * wf2.Conj();
                        den[j, i] = den[i, j].Conj();
                    }

            // NOTE: 2-particle 

            //// 2-particle basis (notation: (spin-up WF, spin-down WF)) (ordering: left-GS, left-1stEX, ... , right-GS, right-1stEX, ...)
            //// Expected AP (1, 1) GSs: apb[order] (0, order), apb[2*order*order]       (order, 0)
            //// Expected AP (2, 0) GSs: apb[0]     (0, 0),     apb[2*order*order+order] (order, order)
            //// Expected P  (1, 1) GSs: pb[order]  (0, order - 1)
            var apb = new List < (int i, int j, string name) > ();
            for (int i = 0; i < order * 2; i++)
                for (int j = 0; j < order * 2; j++)
                    apb.Add((i, j, $""));

            var pb = new List < (int i, int j, string name) > ();
            for (int i = 0; i < order * 2; i++)
                for (int j = i + 1; j < order * 2; j++)
                    pb.Add((i, j, $""));

            //// NOTE: Evaluate Hamiltonians & diagonalize
            ////// energy = bound state energy + coulomb repulsion

            var hamAP = new Complex[apb.Count(), apb.Count()];
            var hamUU = new Complex[pb.Count(), pb.Count()];
            var hamDD = new Complex[pb.Count(), pb.Count()];

            var cHamAP = new Complex[apb.Count(), apb.Count()];
            var cHamUU = new Complex[pb.Count(), pb.Count()];
            var cHamDD = new Complex[pb.Count(), pb.Count()];

            // options.TryGetValue("enableFTDict", out string? enableFTDicts);
            Dictionary<Complex[, , ], Complex[, , ]> ? ftDict = null;
            // if (enableFTDicts == "yes")
            //     ftDict = new Dictionary<Complex[, , ], Complex[, , ]>();

            foreach (var(name, ham, cHam, basis, selCoef, denSet1, denSet2, spb1, spb2) in new [] {
                    ("AP", hamAP, cHamAP, apb, 0.0, denUp, denDown, uWF, dWF),
                    ("UU", hamUU, cHamUU, pb, -1.0, denUp, denUp, uWF, uWF),
                    ("DD", hamDD, cHamDD, pb, -1.0, denDown, denDown, dWF, dWF)
                }) {
                for (int i = 0; i < basis.Count(); i++)
                    for (int j = i; j < basis.Count(); j++) {

                        SetStatus($"Evaluating {name}({i},{j}) ...");

                        var den1 = denSet1[basis[i].i, basis[j].i]; // xx
                        var den2 = denSet2[basis[i].j, basis[j].j]; // yy

                        var den3 = denSet1[basis[i].i, basis[j].j]; // xy
                        var den4 = denSet2[basis[i].j, basis[j].i]; // yx

                        cHam[i, j] = ScalarField.Coulomb(den1, den2, coulomb, ftDict);

                        //// NOTE: In parallel spin states, conjugate term of orbital WF also contributes to ham.
                        if (selCoef != 0.0)
                            cHam[i, j] += selCoef * ScalarField.Coulomb(den3, den4, coulomb, ftDict);

                        var eigenEnergy = 0.0;
                        if ((basis[i].i == basis[j].i) && (basis[i].j == basis[j].j))
                            eigenEnergy += spb1[basis[i].i].energy + spb2[basis[i].j].energy;
                        if ((basis[i].i == basis[j].j) && (basis[i].j == basis[j].i))
                            eigenEnergy += selCoef * (spb1[basis[i].i].energy + spb2[basis[i].j].energy);

                        //// Hamiltonians are Hermitian
                        cHam[j, i] =
                            new Complex(
                                cHam[i, j].Real, -cHam[i, j].Imaginary
                            );

                        ham[i, j] = cHam[i, j] + eigenEnergy;

                        if (i == j)
                            ham[i, j] = ham[i, j].Real;

                        ham[j, i] =
                            new Complex(
                                ham[i, j].Real, -ham[i, j].Imaginary
                            );
                    }
            }

            // NOTE: 3-particle 
            // FIXME: Should I unify CSD calculation for 2/3/n-particles?

            options.TryGetValue("3particle", out string? do3Particle);

            double? p3GSE = null;
            if (do3Particle == "yes") {
                //// NOTE: 3-particle basis (notation: (spin-up WF, spin-down WF, spin-down WF)) (ordering: left-GS, left-1stEX, ... , right-GS, right-1stEX, ...)
                var apb3 = new List < (int i, int j, int k, string name) > ();
                for (int i = 0; i < order * 2; i++)
                    for (int j = 0; j < order * 2; j++)
                        for (int k = j + 1; k < order * 2; k++)
                            apb3.Add((i, j, k, $""));

                var pb3 = new List < (int i, int j, int k, string name) > ();
                for (int i = 0; i < order * 2; i++)
                    for (int j = i + 1; j < order * 2; j++)
                        for (int k = j + 1; k < order * 2; k++)
                            pb3.Add((i, j, k, $""));

                var hamUUU = new Complex[pb3.Count(), pb3.Count()];
                var hamDDD = new Complex[pb3.Count(), pb3.Count()];
                var hamUDD = new Complex[apb3.Count(), apb3.Count()];
                var hamDUU = new Complex[apb3.Count(), apb3.Count()];

                Complex CircularCoulomb(ScalarField x, ScalarField y, ScalarField z) =>
                    ScalarField.Coulomb(x, y, coulomb) +
                    ScalarField.Coulomb(y, z, coulomb) +
                    ScalarField.Coulomb(z, x, coulomb);

                // FIXME: Since what we need here is only 3-particle GS energy, can I omit UDD & DUU? (Probably no. It's possible that energies of DDD exceed UDD & DUU.)
                foreach (var(name, ham, basis, denSet1, denSet2, denSet3, spb1, spb2, spb3) in new [] {
                        // ("UUU", hamUUU, pb3, denUp, denUp, denUp, uWF, uWF, uWF), // NOTE: UUU should never be GS
                        ("DDD", hamDDD, pb3, denDown, denDown, denDown, dWF, dWF, dWF),
                        ("UDD", hamUDD, apb3, denUp, denDown, denDown, uWF, dWF, dWF),
                        ("DUU", hamDUU, apb3, denDown, denUp, denUp, dWF, uWF, uWF),
                    }) {
                    for (int i = 0; i < basis.Count(); i++)
                        for (int j = i; j < basis.Count(); j++) {

                            SetStatus($"Evaluating {name}({i},{j}) ...");

                            var den11 = denSet1[basis[i].i, basis[j].i];
                            var den22 = denSet2[basis[i].j, basis[j].j];
                            var den33 = denSet3[basis[i].k, basis[j].k];

                            var den12 = denSet1[basis[i].i, basis[j].j];
                            var den23 = denSet2[basis[i].j, basis[j].k];
                            var den31 = denSet3[basis[i].k, basis[j].i];

                            var den13 = denSet1[basis[i].i, basis[j].k];
                            var den21 = denSet2[basis[i].j, basis[j].i];
                            var den32 = denSet3[basis[i].k, basis[j].j];

                            if (basis == pb3)
                                ham[i, j] = +1.0 * CircularCoulomb(den11, den22, den33) +
                                +2.0 * CircularCoulomb(den12, den23, den31).Real +
                                -1.0 * CircularCoulomb(den11, den23, den32) +
                                -1.0 * CircularCoulomb(den22, den31, den13) +
                                -1.0 * CircularCoulomb(den33, den12, den21);
                            else if (basis == apb3)
                                ham[i, j] = +1.0 * CircularCoulomb(den11, den22, den33) +
                                -1.0 * CircularCoulomb(den11, den23, den32);

                            if (i == j)
                                ham[i, j] += spb1[basis[i].i].energy + spb2[basis[i].j].energy + spb3[basis[i].k].energy;

                            //// Hamiltonians are Hermitian
                            if (i == j)
                                ham[i, j] = ham[i, j].Real;
                            ham[j, i] =
                                new Complex(
                                    ham[i, j].Real, -ham[i, j].Imaginary
                                );
                        }
                }

                p3GSE = new [] {
                    Eigen.EVD(hamDDD, 1) [0].val,
                        Eigen.EVD(hamUDD, 1) [0].val,
                        Eigen.EVD(hamDUU, 1) [0].val
                }.Min();
            }

            ////// NOTE: Diagonalization (of 2-particle Ham.)
            SetStatus("Diagonalizing Hamiltonians ...");

            var apEigen = Eigen.EVD(hamAP, 3);
            var uuEigen = Eigen.EVD(hamUU, 2);
            var ddEigen = Eigen.EVD(hamDD, 2);

            //// NOTE: Create Reports (Verbal & NXY data)
            SetStatus("Writing Report ...");
            string verbalReport = "";

            ////// NOTE: Corrected energy

            string reportEnergy = "";
            reportEnergy += "no. energy-left-up(eV) energy-left-down(eV) energy-right-up(eV) energy-right-down(eV)\n";
            for (int i = 0; i < order; i++) {
                reportEnergy += $"{i} {lUWF[i].energy} {lDWF[i].energy} {rUWF[i].energy} {rDWF[i].energy}\n";
            }
            File.WriteAllText(NnDQDJCorrectedEnergyPath, reportEnergy);

            ////// NOTE: J
            var J = apEigen[0].val + apEigen[1].val - uuEigen[0].val - ddEigen[0].val;
            verbalReport += $"J = {J.ToString("E04")} (eV), order = {order}\n";

            ////// NOTE: Pickup eigenstates (candidates), label them by their leading components.
            string BasisIdxToTag(int idx) {
                if (idx >= order) return $"R{idx - order}";
                else return $"L{idx}";
            }

            string
            udInfo = $"[1, 0]: none",
                duInfo = $"[0, 1]: none",
                uuInfo = $"[1, 1]: none",
                ddInfo = $"[0, 0]: none";
            foreach (var(eig, basis, spb1, spb2) in new [] {
                    (apEigen[0], apb, uWF, dWF),
                    (apEigen[1], apb, uWF, dWF),
                    (uuEigen[0], pb, uWF, uWF),
                    (ddEigen[0], pb, dWF, dWF)
                }) {
                var energy = eig.val;

                var compList = new List < (int index, Complex occup) > ();
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

            /// NOTE: Pick up lowest 3 energy in AP
            /// 

            double pGS = (uuEigen[0].val + ddEigen[0].val) * 0.5;
            string apGSinfo = $"\nLowest 3 energy in AP:\n {apEigen[0].val - pGS}, {apEigen[1].val - pGS}, {apEigen[2].val - pGS}";

            /// NOTE: Pick up ensemble GS for each particle num
            /// 

            double p0GSE = 0.0;
            double p1GSE = lDWF.Concat(lUWF).Concat(rDWF).Concat(rUWF).Select(x => x.energy).Min();
            double p2GSE = apEigen.Concat(uuEigen).Concat(ddEigen).Select(x => x.val).Min();

            string ensembleGSinfo;
            if (p3GSE != null) {
                ensembleGSinfo = $"\nEnsemble GS energy for 0/1/2/3 particles:\n {p0GSE}, {p1GSE}, {p2GSE}, {p3GSE}";
                // NnDQDJEnergies = new List<double>{p0GSE, p1GSE, p2GSE, p3GSE ?? 0.0};
            } else {
                ensembleGSinfo = $"\nEnsemble GS energy for 0/1/2 particles:\n {p0GSE}, {p1GSE}, {p2GSE}";
                // NnDQDJEnergies = new List<double>{p0GSE, p1GSE, p2GSE};
            }

            verbalReport +=
                uuInfo + "\n" +
                udInfo + "\n" +
                duInfo + "\n" +
                ddInfo + "\n" +
                ensembleGSinfo +
                apGSinfo;

            File.WriteAllText(NnDQDJResultPath, $"\n{J}");
            File.WriteAllText(NnDQDJReportPath, verbalReport);

            return true;
        }

        public string[] ? GetEnergies() {
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