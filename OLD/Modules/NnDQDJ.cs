using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
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

            if (!Eigen.Test())
                return false;

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

                for (int i = 0; i < id.Where(x => x.Item2 != "-1").Count(); i++)
                    spec.Add((
                        int.Parse(id[i].Item2),
                        Double.Parse(energy[i].Item2)
                    ));
            }

            //// NOTE: Read in WF files ( # = order'

            if (order < 1)
                return false;
            // var maxOrder = new [] { specLU.Count(), specLD.Count(), specRU.Count(), specRD.Count() }.Min();
            // if (order > maxOrder)
            //     order = maxOrder;

            var orderLU = Math.Min(specLU.Count(), order);
            var orderLD = Math.Min(specLD.Count(), order);
            var orderRU = Math.Min(specRU.Count(), order);
            var orderRD = Math.Min(specRD.Count(), order);

            var lUWF = new List < (double energy, ScalarField wf) > ();
            var lDWF = new List < (double energy, ScalarField wf) > ();
            var rUWF = new List < (double energy, ScalarField wf) > ();
            var rDWF = new List < (double energy, ScalarField wf) > ();

            var allWFwithOccup = new List < (double occup, ScalarField wf) > ();

            var NNPath = NnMainIsNonSC() ? NnMainNonSCPath : NnMainPath;

            // for (int i = 0; i < order; i++) {
            foreach ((var wfCollection, var spec, var spin, var ord) in new [] {
                    (lDWF, specLD, NnAgent.Spin.Down, orderLD),
                    (lUWF, specLU, NnAgent.Spin.Up,   orderLU),
                    (rDWF, specRD, NnAgent.Spin.Down, orderRD),
                    (rUWF, specRU, NnAgent.Spin.Up,   orderRU)
                }) {
                for (int i = 0; i < ord; i++) {
                    var wfs = NnAgent.NnAmplFileEntry(NnAgent.BandType.X1, spec[i].Id, spin);
                    (var wfImagData, var wfImagCoord, _, _) = NnAgent.GetCoordAndDat(NNPath, wfs.imag);
                    (var wfRealData, var wfRealCoord, _, _) = NnAgent.GetCoordAndDat(NNPath, wfs.real);
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
            // }

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

            if (!NnMainIsNonSC())
                foreach ((var wfCollection, var ord) in new [] {
                        (lDWF, orderLD),
                        (lUWF, orderLU),
                        (rDWF, orderRD),
                        (rUWF, orderRU)
                    })
                    for (int i = 0; i < ord; i++) {
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

            var orderU = orderLU + orderRU;
            var orderD = orderLD + orderRD;

            var denUp = new ScalarField[orderU, orderU];
            var denDown = new ScalarField[orderD, orderD];

            foreach (var(den, wf, ord) in new [] {
                    (denUp, uWF, orderU),
                    (denDown, dWF, orderD)
                })
                for (int i = 0; i < ord; i++)
                    for (int j = i; j < ord; j++) {
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
            string BasisIdxToTag(int idx, int order) {
                if (idx >= order) return $"R{idx - order}";
                else return $"L{idx}";
            }

            var apb = new List < (int i, int j, string name) > ();
            for (int i = 0; i < orderU; i++)
                for (int j = 0; j < orderD; j++)
                    apb.Add((i, j, $"({BasisIdxToTag(i, orderLU)},{BasisIdxToTag(j, orderLD)})"));

            var uub = new List < (int i, int j, string name) > ();
            for (int i = 0; i < orderU; i++)
                for (int j = i + 1; j < orderU; j++)
                    uub.Add((i, j, $"({BasisIdxToTag(i, orderLU)},{BasisIdxToTag(j, orderLU)})"));

            var ddb = new List < (int i, int j, string name) > ();
            for (int i = 0; i < orderD; i++)
                for (int j = i + 1; j < orderD; j++)
                    ddb.Add((i, j, $"({BasisIdxToTag(i, orderLD)},{BasisIdxToTag(j, orderLD)})"));

            // NOTE: (1, 1) (oob) / (2, 0) (ztb) subspaces of AP
            var oob = new List < (int i, int j, string name) > ();
            for (int i = 0; i < orderLU; i++)
                for (int j = orderLD; j < orderD; j++)
                    oob.Add((i, j, $"({BasisIdxToTag(i, orderLU)},{BasisIdxToTag(j, orderLD)})"));
            for (int i = orderLU; i < orderU; i++)
                for (int j = 0; j < orderLD; j++)
                    oob.Add((i, j, $"({BasisIdxToTag(i, orderLU)},{BasisIdxToTag(j, orderLD)})"));
            var ztb = new List < (int i, int j, string name) > ();
            for (int i = 0; i < orderLU; i++)
                for (int j = 0; j < orderLD; j++)
                    ztb.Add((i, j, $"({BasisIdxToTag(i, orderLU)},{BasisIdxToTag(j, orderLD)})"));
            for (int i = orderLU; i < orderU; i++)
                for (int j = orderLD; j < orderD; j++)
                    ztb.Add((i, j, $"({BasisIdxToTag(i, orderLU)},{BasisIdxToTag(j, orderLD)})"));

            //// NOTE: Evaluate Hamiltonians & diagonalize
            ////// energy = bound state energy + coulomb repulsion

            var hamAP = new Complex[apb.Count(), apb.Count()];
            var hamUU = new Complex[uub.Count(), uub.Count()];
            var hamDD = new Complex[ddb.Count(), ddb.Count()];

            var hamOO = new Complex[oob.Count(), oob.Count()];
            var hamZT = new Complex[ztb.Count(), ztb.Count()];

            var cHamAP = new Complex[apb.Count(), apb.Count()];
            var cHamUU = new Complex[uub.Count(), uub.Count()];
            var cHamDD = new Complex[ddb.Count(), ddb.Count()];

            var cHamOO = new Complex[oob.Count(), oob.Count()];
            var cHamZT = new Complex[ztb.Count(), ztb.Count()];

            // options.TryGetValue("enableFTDict", out string? enableFTDicts);
            Dictionary<Complex[, , ], Complex[, , ]> ? ftDict = null;
            // if (enableFTDicts == "yes")
            //     ftDict = new Dictionary<Complex[, , ], Complex[, , ]>();

            foreach (var(name, ham, cHam, basis, selCoef, denSet1, denSet2, spb1, spb2) in new [] {
                    ("AP", hamAP, cHamAP, apb, 0.0, denUp, denDown, uWF, dWF),
                    ("UU", hamUU, cHamUU, uub, -1.0, denUp, denUp, uWF, uWF),
                    ("DD", hamDD, cHamDD, ddb, -1.0, denDown, denDown, dWF, dWF),

                    ("OO", hamOO, cHamOO, oob, 0.0, denUp, denDown, uWF, dWF),
                    ("ZT", hamZT, cHamZT, ztb, 0.0, denUp, denDown, uWF, dWF)
                }) {
                for (int i = 0; i < basis.Count(); i++)
                    for (int j = i; j < basis.Count(); j++) {

                        SetStatus($"Evaluating {name}({i},{j}) ...");

                        var den1 = denSet1[basis[i].i, basis[j].i]; // xx
                        var den2 = denSet2[basis[i].j, basis[j].j]; // yy

                        cHam[i, j] = ScalarField.Coulomb(den1, den2, coulomb, ftDict);

                        //// NOTE: In parallel spin states, conjugate term of orbital WF also contributes to ham.
                        if (selCoef != 0.0) {
                            var den3 = denSet1[basis[i].i, basis[j].j]; // xy
                            var den4 = denSet2[basis[i].j, basis[j].i]; // yx
                            cHam[i, j] += selCoef * ScalarField.Coulomb(den3, den4, coulomb, ftDict);
                        }

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
            // var apb3 = new List < (int i, int j, int k, string name) > ();
            // for (int i = 0; i < order * 2; i++)
            //     for (int j = 0; j < order * 2; j++)
            //         for (int k = j + 1; k < order * 2; k++)
            //             apb3.Add((i, j, k, $""));

            var uddb = new List < (int i, int j, int k, string name) > ();
            for (int i = 0; i < orderU; i++)
                for (int j = 0; j < orderD; j++)
                    for (int k = j + 1; k < orderD; k++)
                        uddb.Add((i, j, k, $"({BasisIdxToTag(i, orderLU)},{BasisIdxToTag(j, orderLD)},{BasisIdxToTag(k, orderLD)})"));

            var duub = new List < (int i, int j, int k, string name) > ();
            for (int i = 0; i < orderD; i++)
                for (int j = 0; j < orderU; j++)
                    for (int k = j + 1; k < orderU; k++)
                        duub.Add((i, j, k, $"({BasisIdxToTag(i, orderLD)},{BasisIdxToTag(j, orderLU)},{BasisIdxToTag(k, orderLU)})"));

            // var pb3 = new List < (int i, int j, int k, string name) > ();
            // for (int i = 0; i < order * 2; i++)
            //     for (int j = i + 1; j < order * 2; j++)
            //         for (int k = j + 1; k < order * 2; k++)
            //             pb3.Add((i, j, k, $""));

            var dddb = new List < (int i, int j, int k, string name) > ();
            for (int i = 0; i < orderD; i++)
                for (int j = i + 1; j < orderD; j++)
                    for (int k = j + 1; k < orderD; k++)
                        dddb.Add((i, j, k, $"({BasisIdxToTag(i, orderLD)},{BasisIdxToTag(j, orderLD)},{BasisIdxToTag(k, orderLD)})"));

            // var hamUUU = new Complex[pb3.Count(), pb3.Count()];
            var hamDDD  = new Complex[dddb.Count(), dddb.Count()];
            var hamUDD  = new Complex[uddb.Count(), uddb.Count()];
            var hamDUU  = new Complex[duub.Count(), duub.Count()];
            var cHamDDD = new Complex[dddb.Count(), dddb.Count()];
            var cHamUDD = new Complex[uddb.Count(), uddb.Count()];
            var cHamDUU = new Complex[duub.Count(), duub.Count()];

            if (do3Particle == "yes") {
                //// NOTE: 3-particle basis (notation: (spin-up WF, spin-down WF, spin-down WF)) (ordering: left-GS, left-1stEX, ... , right-GS, right-1stEX, ...)

                // FIXME: Since what we need here is only 3-particle GS energy, can I omit UDD & DUU? (Probably no. It's possible that energies of DDD exceed UDD & DUU.)
                foreach (var(name, ham, cHam, basis, denSet1, denSet2, denSet3, spb1, spb2, spb3) in new [] {
                        // ("UUU", hamUUU, pb3, denUp, denUp, denUp, uWF, uWF, uWF), // NOTE: UUU should never be GS
                        ("DDD", hamDDD, cHamDDD, dddb, denDown, denDown, denDown, dWF, dWF, dWF),
                        ("UDD", hamUDD, cHamUDD, uddb, denUp, denDown, denDown, uWF, dWF, dWF),
                        ("DUU", hamDUU, cHamDUU, duub, denDown, denUp, denUp, dWF, uWF, uWF),
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

                            if (basis == dddb)
                                ham[i, j] = 
                                    +1.0 * ScalarField.CircularCoulomb(den11, den22, den33, coulomb) +
                                    +2.0 * ScalarField.CircularCoulomb(den12, den23, den31, coulomb).Real +
                                    -1.0 * ScalarField.CircularCoulomb(den11, den23, den32, coulomb) +
                                    -1.0 * ScalarField.CircularCoulomb(den22, den31, den13, coulomb) +
                                    -1.0 * ScalarField.CircularCoulomb(den33, den12, den21, coulomb);
                            //TODO:
                            else
                                ham[i, j] = 
                                    +1.0 * ScalarField.CircularCoulomb(den11, den22, den33, coulomb) +
                                    -1.0 * ScalarField.CircularCoulomb(den11, den23, den32, coulomb);

                            cHam[i, j] = ham[i, j];
                            cHam[j, i] = 
                                new Complex(
                                    ham[i, j].Real, -ham[i, j].Imaginary
                                );

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

            // FIXME: For debug
            List<(Complex[] vec, double val)> EVecDDD, EVecUDD, EVecDUU;
            if (do3Particle == "yes") {
                EVecDDD = Eigen.EVD(hamDDD).ToList();
                EVecUDD = Eigen.EVD(hamUDD).ToList();
                EVecDUU = Eigen.EVD(hamDUU).ToList();
            }

            ////// NOTE: Diagonalization (of 2-particle Ham.)
            SetStatus("Diagonalizing Hamiltonians ...");

            var apEigen = Eigen.EVD(hamAP, apb.Count);
            var uuEigen = Eigen.EVD(hamUU, uub.Count);
            var ddEigen = Eigen.EVD(hamDD, ddb.Count);

            var ooEigen = Eigen.EVD(hamOO, oob.Count);
            var ztEigen = Eigen.EVD(hamZT, ztb.Count);

            var apVecs = apEigen.Select(x => x.vec).ToList();
            var ooVecs = ooEigen.Select(x => x.vec).ToList();
            var ztVecs = ztEigen.Select(x => x.vec).ToList();

            //// NOTE: A more representative AP Hamiltonian (w/ (1,1) (2,0)/(0,2) diag. separately).
            //// (1) Construct transformation matrices.
            var ooztEigMat = new Complex[apb.Count, apb.Count];
            var ooztToApMat = new Complex[apb.Count, apb.Count];
            // E: Fill in the eigenvectors.
            for (int i = 0; i < oob.Count; ++i)
                for (int j = 0; j < oob.Count; ++j)
                    ooztEigMat[j, i] = ooEigen[i].vec[j];
            for (int i = 0; i < ztb.Count; ++i)
                for (int j = 0; j < ztb.Count; ++j)
                    ooztEigMat[j + oob.Count, i + oob.Count] = ztEigen[i].vec[j];    
            // P: Permutation (from oo+zt => ap)        
            for (int i = 0; i < orderLU; ++i)
                for (int j = 0; j < orderRD; ++j)
                    ooztToApMat[
                        j + i * orderD  + orderLD, 
                        j + i * orderRD] = 1.0;
            for (int i = 0; i < orderRU; ++i)
                for (int j = 0; j < orderLD; ++j)
                    ooztToApMat[
                        j + i * orderD  + orderLU * orderD, 
                        j + i * orderLD + orderLU * orderRD] = 1.0;
            for (int i = 0; i < orderLU; ++i)
                for (int j = 0; j < orderLD; ++j)
                    ooztToApMat[
                        j + i * orderD, 
                        j + i * orderLD + orderLU * orderRD + orderRU * orderLD] = 1.0;
            for (int i = 0; i < orderRU; ++i)
                for (int j = 0; j < orderRD; ++j)
                    ooztToApMat[
                        j + i * orderD  + orderLU * orderD + orderLD, 
                        j + i * orderRD + orderLU * orderRD + orderRU * orderLD + orderLU * orderLD] = 1.0;
                // if (i < apb.Count / 2)
                //     ooztToApMat[i + apb.Count / 4, i] = 1.0;
                // else if (i < apb.Count / 4 * 3)
                //     ooztToApMat[i - apb.Count / 2, i] = 1.0;
                // else 
                //     ooztToApMat[i, i] = 1.0;
            // Calculate E+P+(hamAP)PE
            var pe = Eigen.Multiply(ooztToApMat, ooztEigMat);
            var peA = Eigen.Adjoint(pe);

            var hamAPdiag = Eigen.Multiply(
                peA, 
                Eigen.Multiply(hamAP, pe));

            var cHamAPdiag = Eigen.Multiply(
                peA, 
                Eigen.Multiply(cHamAP, pe));


            //// NOTE: Create Reports (Verbal & NXY data)
            SetStatus("Writing Report ...");
            string verbalReport = "";

            ////// NOTE: Corrected energy

            // string reportEnergy = "";
            // reportEnergy += "no. energy-left-up(eV) energy-left-down(eV) energy-right-up(eV) energy-right-down(eV)\n";
            // for (int i = 0; i < order; i++) {
            //     reportEnergy += $"{i} {lUWF[i].energy} {lDWF[i].energy} {rUWF[i].energy} {rDWF[i].energy}\n";
            // }
            // File.WriteAllText(NnDQDJCorrectedEnergyPath, reportEnergy);

            ////// NOTE: J
            var J = apEigen[0].val + apEigen[1].val - uuEigen[0].val - ddEigen[0].val;
            verbalReport += $"J = {J.ToString("E04")} (eV), order = {order}\n";

            ////// NOTE: Pickup eigenstates (candidates), label them by their leading components.

            StringBuilder
                ap0Info = new StringBuilder($"[AP#0]: "),
                ap1Info = new StringBuilder($"[AP#1]: "),
                uu0Info = new StringBuilder($"[++#0]: "),
                dd0Info = new StringBuilder($"[--#0]: "),                
                oo0Info = new StringBuilder($"[11#0]: "),
                oo1Info = new StringBuilder($"[11#1]: "),
                zt0Info = new StringBuilder($"[02#0]: ");
            foreach (var (infoBuilder, eig, basis, spb1, spb2) in new [] {
                    (ap0Info, apEigen[0], apb, uWF, dWF),
                    (ap1Info, apEigen[1], apb, uWF, dWF),
                    (uu0Info, uuEigen[0], uub, uWF, uWF),
                    (dd0Info, ddEigen[0], ddb, dWF, dWF),

                    (oo0Info, ooEigen[0], oob, uWF, dWF),
                    (oo1Info, ooEigen[1], oob, uWF, dWF),
                    (zt0Info, ztEigen[0], ztb, uWF, dWF)
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
                    if (occup > 0.003) {
                        compInfo += $"{basis[comp.index].name}:{occup.ToString("0.000")} ";
                    }
                }

                infoBuilder.Append(energyInfo + "\n" + compInfo);

                // if (basis == apb) {
                    // if ((leadPair.i, leadPair.j) == (0, order))
                    //     udInfo = $"[1, 0]: " + energyInfo + "\n" + compInfo;
                    // if ((leadPair.i, leadPair.j) == (order, 0))
                    //     duInfo = $"[0, 1]: " + energyInfo + "\n" + compInfo;
                // if (eig == apEigen[0])
                //     udInfo = $"[1, 0]: " + energyInfo + "\n" + compInfo;
                // if (eig == apEigen[1])
                //     duInfo = $"[0, 1]: " + energyInfo + "\n" + compInfo;
                // }

                // if (basis == pb) {
                // if (eig == uuEigen[0])
                //     uuInfo = $"[1, 1]: " + energyInfo + "\n" + compInfo;
                // if (eig == ddEigen[0])
                //     ddInfo = $"[0, 0]: " + energyInfo + "\n" + compInfo;
                // }
            }

            /// NOTE: Pick up lowest 3 energy in AP
            /// 

            double pGS = (uuEigen[0].val + ddEigen[0].val) * 0.5;
            string apGSinfo = $"\nLowest 4 energy in AP (wrt. P):\n {apEigen[0].val - pGS}, {apEigen[1].val - pGS}, {apEigen[2].val - pGS}, {apEigen[3].val - pGS}";

            /// NOTE: Pick up ensemble GS for each particle num
            /// 

            double p0GSE = 0.0;
            double p1GSE = lDWF.Concat(lUWF).Concat(rDWF).Concat(rUWF).Select(x => x.energy).Min();
            double p2GSE = apEigen.Concat(uuEigen).Concat(ddEigen).Select(x => x.val).Min();

            string ensembleGSInfo;
            if (p3GSE != null) {
                ensembleGSInfo = $"\nEnsemble GS energy for 0/1/2/3 particles:\n {p0GSE} {p1GSE} {p2GSE} {p3GSE}";
                // NnDQDJEnergies = new List<double>{p0GSE, p1GSE, p2GSE, p3GSE ?? 0.0};
            } else {
                ensembleGSInfo = $"\nEnsemble GS energy for 0/1/2 particles:\n {p0GSE} {p1GSE} {p2GSE}";
                // NnDQDJEnergies = new List<double>{p0GSE, p1GSE, p2GSE};
            }

            string gsCoulombInfo = 
                $"\n1PGS Coulomb energy (LR, LL, RR, t(LR/RR)):\n {cHamAP[orderLD,orderLD].Real} {cHamAP[0,0].Real} {cHamAP[orderLU*orderD+orderLD,orderLU*orderD+orderLD].Real} {cHamAP[orderLD,orderLU*orderD+orderLD].Magnitude}";
            string coulombInfo = 
                $"\nCoulomb energy (11#0, 11#1, 02#0, 02#1):\n {cHamAPdiag[0,0].Real} {cHamAPdiag[1,1].Real} {cHamAPdiag[oob.Count,oob.Count].Real} {cHamAPdiag[oob.Count + 1,oob.Count + 1].Real}";
            string hamAPdiagInfo = 
                $"\nCoulomb energy + Eig. (11#0, 11#1, 02#0, 02#1):\n {hamAPdiag[0,0].Real} {hamAPdiag[1,1].Real} {hamAPdiag[oob.Count,oob.Count].Real} {hamAPdiag[oob.Count + 1,oob.Count + 1].Real}";
            
            var t00 = cHamAPdiag[0,oob.Count  ].Magnitude;
            var t01 = cHamAPdiag[0,oob.Count+1].Magnitude;
            var t10 = cHamAPdiag[1,oob.Count  ].Magnitude;
            var t11 = cHamAPdiag[1,oob.Count+1].Magnitude;

            string tunnelingInfo = 
                $"\nTunneling energy (rms, avg, t00, t01, t10, t11):\n" + 
                $" {Math.Sqrt(0.25 * (t00*t00 + t01*t01 + t10*t10 + t11*t11))} {0.25 * (t00 + t01 + t10 + t11)}\n" + 
                $" {t00} {t01} {t10} {t11}";

            StringBuilder  repulsionInfoBuilder = new StringBuilder();
            StringBuilder cRepulsionInfoBuilder = new StringBuilder();

            foreach (var (info, ham, comment) in new [] {
                (repulsionInfoBuilder, hamAPdiag, "(w/ Eig.)"),
                (cRepulsionInfoBuilder, cHamAPdiag, "")
            })
            {   
                var u00 = (ham[oob.Count,oob.Count] - ham[0,0]).Real;
                var u01 = (ham[oob.Count,oob.Count] - ham[1,1]).Real;
                var u10 = (ham[oob.Count+1,oob.Count+1] - ham[0,0]).Real;
                var u11 = (ham[oob.Count+1,oob.Count+1] - ham[1,1]).Real;
                
                info.Append( 
                    $"\nRepulsion energy {comment} (rms, avg, u00, u01, u10, u11):\n" + 
                    $" {Math.Sqrt(0.25 * (u00*u00 + u01*u01 + u10*u10 + u11*u11))} {0.25 * (u00 + u01 + u10 + u11)}\n" + 
                    $" {u00} {u01} {u10} {u11}");
            }

            verbalReport +=
                uu0Info + "\n" +
                dd0Info + "\n" + "\n" +
                zt0Info + "\n" +
                oo1Info + "\n" +
                oo0Info + "\n" + "\n" +
                ap1Info + "\n" +
                ap0Info + "\n" + "\n" +

                ensembleGSInfo +
                // apGSinfo + 
                gsCoulombInfo +
                coulombInfo +
                hamAPdiagInfo +
                tunnelingInfo +
                repulsionInfoBuilder + 
                cRepulsionInfoBuilder;

            File.WriteAllText(NnDQDJResultPath, $"\n{J}");
            File.WriteAllText(NnDQDJReportPath, verbalReport);
            File.WriteAllText(NnDQDJPath.SubPath($"_Report{order}{do3Particle}.txt"), verbalReport);

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