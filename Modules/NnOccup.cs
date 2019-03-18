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

        RPath NnOccupPath => FSPath.SubPath("NnOccup");
        RPath NnOccupResultPath => NnOccupPath.SubPath("occup.txt");

        NnModule NnOccup(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "NN Occupation",
                NnOccupCanExecute, 
                NnOccupIsDone, 
                NnOccupExecute, 
                NnOccupGetResult, 
                NnOccupDefaultOption.ToImmutableDictionary(),
                options);

        // FIXME:
        public static ImmutableDictionary<string, string> NnOccupDefaultOption = 
            new Dictionary<string, string>{
                {"portion", "0.7"},
                {"x0", "-"},
                {"y0", "-"},
                {"z0", "-"},
                {"x1", "-"},
                {"y1", "-"},
                {"z1", "-"}
            }.ToImmutableDictionary();

        bool NnOccupCanExecute() {
            return NnMainIsDone();
        }

        bool NnOccupIsDone() {
            return File.Exists(NnOccupResultPath);
        }

        string NnOccupGetResult() {
            return File.ReadAllText(NnOccupResultPath);
        }

        bool NnOccupExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {
            // FIXME: HACKING HERE! Should be generalized in future.
            
            RPath? spectrumFile = NnAgent.GetQSpectrumPath(FSPath, NnAgent.BandType.X1);
            if (spectrumFile?.Content == null) 
                return false;

            Dictionary<int, double>? spectrum = NnAgent.NXY(spectrumFile.Content, 1);
            if (spectrum == null) return false;

            double result = 0.0;
            foreach ((RPath data, RPath coord, int id) in 
                NnAgent.GetQProbPaths(FSPath, NnAgent.BandType.X1)
            ) {
                if ((data.Content == null) || (coord.Content == null))
                    continue;

                ScalarField field = ScalarField.FromNnDatAndCoord(data.Content, coord.Content);

                options.TryGetValue("portion", out string? portion);
                options.TryGetValue("x0", out string? x0);
                options.TryGetValue("x1", out string? x1);
                options.TryGetValue("y0", out string? y0);
                options.TryGetValue("y1", out string? y1);
                options.TryGetValue("z0", out string? z0);
                options.TryGetValue("z1", out string? z1);

                // FIXME: 0.75?
                if (field.GetPortionInRange(
                        (
                            x0 != "-" ? Convert.ToDouble(x0) : (double?)null, 
                            y0 != "-" ? Convert.ToDouble(y0) : (double?)null, 
                            z0 != "-" ? Convert.ToDouble(z0) : (double?)null
                        ), 
                        (
                            x1 != "-" ? Convert.ToDouble(x1) : (double?)null, 
                            y1 != "-" ? Convert.ToDouble(y1) : (double?)null, 
                            z1 != "-" ? Convert.ToDouble(z1) : (double?)null
                        )
                    ) > Convert.ToDouble(portion ?? "0.75"))
                    result += spectrum[id];
            }

            // TODO: Spectrum annotation
            File.WriteAllText(NnOccupResultPath, result.ToString());
            return true;
        }

        // FIXME: Temporally implemented scalarfield before DQDSystem is shipped here.
        class ScalarField {
            public enum Dim {X, Y, Z}

            double[,,] Data { get; }
            ImmutableDictionary<Dim, ImmutableList<double>> Coords { get; }

            public ScalarField(double[,,] array, ImmutableDictionary<Dim, ImmutableList<double>> coords) {
                Data = array;
                Coords = new Dictionary<Dim, ImmutableList<double>>{
                    { Dim.X, coords[Dim.X] },
                    { Dim.Y, coords[Dim.Y] },
                    { Dim.Z, coords[Dim.Z] }
                }.ToImmutableDictionary();
            }

            public static ScalarField FromNnDatAndCoord(string data, string coord) {
                string[] coordLines =
                    coord.Splitter("[\r\n|\r|\n]+");

                ImmutableDictionary<Dim, List<double>> coords = 
                    new Dictionary<Dim, List<double>>{
                        { Dim.X, new List<double>() },
                        { Dim.Y, new List<double>() },
                        { Dim.Z, new List<double>() }
                    }.ToImmutableDictionary();

                Dim currentCoord = Dim.X;
                foreach (var line in coordLines)
                {
                    double newValue = Double.Parse(line, System.Globalization.NumberStyles.Float);
                    if (coords[currentCoord].Count != 0)
                        if (newValue < coords[currentCoord].LastOrDefault())
                            switch(currentCoord) {
                                case Dim.X: currentCoord = Dim.Y; break;
                                case Dim.Y: currentCoord = Dim.Z; break;
                                case Dim.Z: throw new Exception();
                            }
                    coords[currentCoord].Add(newValue);
                }

                string[] dataLines =
                    data.Splitter("[\r\n|\r|\n]+");

                double[,,] array = new double[
                    coords[Dim.X].Count,
                    coords[Dim.Y].Count,
                    coords[Dim.Z].Count
                ];
                int idx = 0, idy = 0, idz = 0;
                foreach (var line in dataLines)
                {
                    double value = Double.Parse(line, System.Globalization.NumberStyles.Float);
                    array[idx,idy,idz] = value;
                    if (++idx >= coords[Dim.X].Count) {
                        idx = 0;
                        if (++idy >= coords[Dim.Y].Count) {
                            idy = 0;
                            if (++idz >= coords[Dim.Z].Count)
                                break;
                        }
                    }
                }

                return new ScalarField(
                    array, 
                    coords.ToImmutableDictionary(
                        x => x.Key,
                        x => x.Value.ToImmutableList()
                    ));
            }

            bool IsWithinRange(double? x0, double? x1, double x) {
                if ((x0 == null) || (x1 == null)) return true;
                return (x - x0) * (x - x1) <= 0.0;
            }

            (int x1, int x2) GrabRange(ImmutableList<double> data, double? r0, double? r1) {
                int? x1 = null;
                int? x2 = null;
                foreach (var x in Enumerable.Range(0, data.Count))
                    if (IsWithinRange(r0, r1, data[x]))
                        x1 = x;
                foreach (var x in Enumerable.Range(0, data.Count).Reverse())
                    if (IsWithinRange(r0, r1, data[x]))
                        x2 = x;

                if (x1 == null) return (0, 0);
                if (x2 == null) return (0, 0);

                if (x1 > x2)
                    (x1, x2) = (x2, x1);

                return (
                    x1 ?? throw new Exception(), 
                    x2 ?? throw new Exception()
                );
            }

            double DxDyDz(int x, int y, int z) {
                double D(int x, ImmutableList<double> data) {
                    if ((x < 0) || (x >= data.Count)) 
                        return 0.0;
                    if (x == 0) 
                        return data[1] - data[0];
                    if (x == data.Count - 1) 
                        return data[data.Count - 1] - data[data.Count - 2];                    
                    return (data[x + 1] - data[x - 1]) * 0.5;
                }
                return D(x, Coords[Dim.X]) * D(y, Coords[Dim.Y]) * D(z, Coords[Dim.Z]);
            }

            public double GetPortionInRange(
                (double? x, double? y, double? z) range0, 
                (double? x, double? y, double? z) range1
            ) {
                (int x, int y, int z) idx0, idx1;          

                (idx0.x, idx1.x) = GrabRange(Coords[Dim.X], range0.x, range1.x);
                (idx0.y, idx1.y) = GrabRange(Coords[Dim.Y], range0.y, range1.y);
                (idx0.z, idx1.z) = GrabRange(Coords[Dim.Z], range0.z, range1.z);    

                double result = 0.0;
                foreach (var x in Enumerable.Range(idx0.x, idx1.x - idx0.x + 1))
                foreach (var y in Enumerable.Range(idx0.y, idx1.y - idx0.y + 1))
                foreach (var z in Enumerable.Range(idx0.z, idx1.z - idx0.z + 1))
                    result += Data[x, y, z] * DxDyDz(x, y, z);

                return result;
            }
        } 
    }
}