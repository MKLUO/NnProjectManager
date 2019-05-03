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

        RPath NnDQDJPath       => FSPath.SubPath("NnDQDJ");
        RPath NnDQDJResultPath   => NnDQDJPath.SubPath("_Result.txt");

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
                {"order", "2"}, 
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


            
            //// Read in spectrum & info (from DQD Report) ////
            SetStatus("Reading spectrum ...");
            if (NnDQDReportEnergyPath?.Content == null) 
                return false;
            if (NnDQDReportIdPath?.Content == null) 
                return false;

            var specLU = new List<(int Id, double Energy)>();
            var specLD = new List<(int Id, double Energy)>();
            var specRU = new List<(int Id, double Energy)>();
            var specRD = new List<(int Id, double Energy)>();

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
            options.TryGetValue("order", out string? orders);
            int order = orders != "-" ? Convert.ToUInt16(orders) : 2;

            if (order < 1) 
                return false;
            if (order > new []{specLU.Count(), specLD.Count(), specRU.Count(), specRD.Count()}.Min()) 
                return false;

            
            var lUWF = new List<(double energy, ScalarField wf)>();
            var lDWF = new List<(double energy, ScalarField wf)>();
            var rUWF = new List<(double energy, ScalarField wf)>();
            var rDWF = new List<(double energy, ScalarField wf)>();

            for (int i = 0; i < order; i++) {
                foreach ((var wf, var spec, var spin) in new [] {
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

                    // Truncate WFs
                    wf.Add((
                        spec[i].Energy,
                        ScalarField.FromNnDatAndCoord(
                            wfRealData.Content, wfRealCoord.Content, 
                            wfImagData.Content, wfImagCoord.Content
                        ).Truncate((x0, y0, z0), (x1, y1, z1))
                    ));
                }
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
            }
            


            //// 2-particle basis (notation: (spin-up WF, spin-down WF)) (ordering: left-GS, left-1stEX, ... , right-GS, right-1stEX, ...)
            //// Expected AP (1, 1) GSs: apb[order] (0, order), apb[2*order*order]       (order, 0)
            //// Expected AP (2, 0) GSs: apb[0]     (0, 0),     apb[2*order*order+order] (order, order)
            //// Expected P  (1, 1) GSs: pb[order]  (0, order)
            var apb = new List<(int i, int j)>();
            for (int i = 0; i < order * 2; i++)
            for (int j = 0; j < order * 2; j++)
                apb.Add((i, j));

            var pb = new List<(int i, int j)>();
            for (int i = 0; i < order * 2; i++)
            for (int j = i + 1; j < order * 2; j++)
                pb.Add((i, j));



            //// Evaluate Hamiltonians & diagonalize
            ////// energy = bound state energy + coulomb repulsion
            SetStatus("Evaluating Hamiltonians ...");

            var hamAP = new Complex[apb.Count(), apb.Count()];
            var hamUU = new Complex[ pb.Count(),  pb.Count()];
            var hamDD = new Complex[ pb.Count(),  pb.Count()];

            var cHamAP = new Complex[apb.Count(), apb.Count()];
            var cHamUU = new Complex[ pb.Count(),  pb.Count()];
            var cHamDD = new Complex[ pb.Count(),  pb.Count()];

            foreach (var (ham, cHam, basis, selCoef, denSet1, denSet2, spb1, spb2) in new []{
                (hamAP, cHamAP, apb,  0.0, denUp,   denDown, uWF, dWF),
                (hamUU, cHamUU,  pb, -1.0, denUp,   denUp,   uWF, uWF),
                (hamDD, cHamDD,  pb, -1.0, denDown, denDown, dWF, dWF)
            }) {
                for (int i = 0; i < basis.Count(); i++)
                for (int j = i; j < basis.Count(); j++) {

                    var den1 = denSet1[basis[i].i, basis[j].i];                
                    var den2 = denSet2[basis[i].j, basis[j].j];

                    var den3 = denSet1[basis[i].i, basis[j].j];
                    var den4 = denSet2[basis[i].j, basis[j].i];                    

                    cHam[i, j] = ScalarField.Coulomb(den1, den2);      
                    //// In parallel spin states, conjugate term of orbital WF also contributes to ham.
                    if (selCoef != 0.0)
                        cHam[i, j] += selCoef * ScalarField.Coulomb(den3, den4);                    

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


            //// Create Report (Verbal & NXY data)
            SetStatus("Writing Report ...");

            ////// Pickup eigenstates (candidates), label them by their leading components.
            var J = apEigen[0].val + apEigen[1].val - uuEigen[0].val - ddEigen[0].val;

            File.WriteAllText(NnDQDJResultPath, $"{J}");


            return true;
        }
    }
}