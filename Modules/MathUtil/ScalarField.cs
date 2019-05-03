using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

using FFTW.NET;

using Complex = System.Numerics.Complex;
// using ExComplex = Exocortex.DSP.Complex;

#nullable enable

namespace NnManager {

// FIXME: Temporally implemented scalarfield before DQDSystem is shipped here.
    public class ScalarField {
        public enum Dim {X, Y, Z}

        // FIXME: Public data should be immutable
        Complex[,,] Data { get; }
        ImmutableDictionary<Dim, ImmutableList<double>> Coords { get; }

        public ScalarField(Complex[,,] array, ImmutableDictionary<Dim, ImmutableList<double>> coords) {
            Data = array;
            Coords = new Dictionary<Dim, ImmutableList<double>>{
                { Dim.X, coords[Dim.X] },
                { Dim.Y, coords[Dim.Y] },
                { Dim.Z, coords[Dim.Z] }
            }.ToImmutableDictionary();
        }

        static (double[,,], ImmutableDictionary<Dim, List<double>>) NnDatAndCoordToField(
            string data, string coord
        ) { 
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

            return (array, coords);
        }

        // FIXME: WET!
        public static ScalarField FromNnDatAndCoord(
            string dataR, string coordR
        ) {
            var (arrayR, coordsR) = NnDatAndCoordToField(dataR, coordR);

            Complex[,,] array = new Complex[
                coordsR[Dim.X].Count,
                coordsR[Dim.Y].Count,
                coordsR[Dim.Z].Count
            ];

            foreach (var x in Enumerable.Range(0, coordsR[Dim.X].Count))
            foreach (var y in Enumerable.Range(0, coordsR[Dim.Y].Count))
            foreach (var z in Enumerable.Range(0, coordsR[Dim.Z].Count))
                array[x, y, z] = new Complex(arrayR[x, y, z], 0.0);

            return new ScalarField(
                array,
                coordsR.ToImmutableDictionary(
                    x => x.Key,
                    x => x.Value.ToImmutableList()
                ));
        }

        public static ScalarField FromNnDatAndCoord(
            string dataR, string coordR,
            string dataI, string coordI
        ) {
            var (arrayR, coordsR) = NnDatAndCoordToField(dataR, coordR);
            var (arrayI, coordsI) = NnDatAndCoordToField(dataI, coordI);

            Complex[,,] array = new Complex[
                coordsR[Dim.X].Count,
                coordsR[Dim.Y].Count,
                coordsR[Dim.Z].Count
            ];

            foreach (var x in Enumerable.Range(0, coordsR[Dim.X].Count))
            foreach (var y in Enumerable.Range(0, coordsR[Dim.Y].Count))
            foreach (var z in Enumerable.Range(0, coordsR[Dim.Z].Count))
                array[x, y, z] = new Complex(arrayR[x, y, z], arrayI[x, y, z]);

            // FIXME: coordsR and coordsI are assumed to be same
            return new ScalarField(
                array,
                coordsR.ToImmutableDictionary(
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

            x2 = x2 + 1;

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

        public double Norm() {
            double result = 0.0;
            foreach (var x in Enumerable.Range(0, Coords[Dim.X].Count))
            foreach (var y in Enumerable.Range(0, Coords[Dim.Y].Count))
            foreach (var z in Enumerable.Range(0, Coords[Dim.Z].Count))
                result += Math.Pow(Data[x, y, z].Magnitude, 2) * DxDyDz(x, y, z);

            return result;
        }

        public Complex Sum() {
            Complex result = 0.0;
            foreach (var x in Enumerable.Range(0, Coords[Dim.X].Count))
            foreach (var y in Enumerable.Range(0, Coords[Dim.Y].Count))
            foreach (var z in Enumerable.Range(0, Coords[Dim.Z].Count))
                result += Data[x, y, z] * DxDyDz(x, y, z);

            return result;
        }

        public static ScalarField operator*(ScalarField left, ScalarField right) {
            var Coords = left.Coords;

            Complex[,,] array = new Complex[
                Coords[Dim.X].Count,
                Coords[Dim.Y].Count,
                Coords[Dim.Z].Count
            ];

            foreach (var x in Enumerable.Range(0, Coords[Dim.X].Count))
            foreach (var y in Enumerable.Range(0, Coords[Dim.Y].Count))
            foreach (var z in Enumerable.Range(0, Coords[Dim.Z].Count))
                array[x, y, z] = new Complex(
                    left.Data[x, y, z].Real * right.Data[x, y, z].Real - 
                    left.Data[x, y, z].Imaginary * right.Data[x, y, z].Imaginary, 
                    left.Data[x, y, z].Real * right.Data[x, y, z].Imaginary + 
                    left.Data[x, y, z].Imaginary * right.Data[x, y, z].Real);

            return new ScalarField(array, Coords);
        }

        public ScalarField Conj() {
            Complex[,,] array = new Complex[
                Coords[Dim.X].Count,
                Coords[Dim.Y].Count,
                Coords[Dim.Z].Count
            ];

            foreach (var x in Enumerable.Range(0, Coords[Dim.X].Count))
            foreach (var y in Enumerable.Range(0, Coords[Dim.Y].Count))
            foreach (var z in Enumerable.Range(0, Coords[Dim.Z].Count))
                array[x, y, z] = new Complex(Data[x, y, z].Real, - Data[x, y, z].Imaginary);

            return new ScalarField(array, Coords);
        }

        public ScalarField Truncate(
            (double? x, double? y, double? z) range0, 
            (double? x, double? y, double? z) range1
        ) { 
            (int x, int y, int z) idx0, idx1;          

            (idx0.x, idx1.x) = GrabRange(Coords[Dim.X], range0.x, range1.x);
            (idx0.y, idx1.y) = GrabRange(Coords[Dim.Y], range0.y, range1.y);
            (idx0.z, idx1.z) = GrabRange(Coords[Dim.Z], range0.z, range1.z);

            return Truncate(idx0, idx1);
        }

        public ScalarField Truncate(
            (int x, int y, int z) idx0, 
            (int x, int y, int z) idx1
        ) {
            var newArray = new Complex[idx1.x - idx0.x, idx1.y - idx0.y, idx1.z - idx0.z];
            List<double> coordX = new List<double>();
            List<double> coordY = new List<double>();
            List<double> coordZ = new List<double>();

            foreach (var x in Enumerable.Range(idx0.x, idx1.x - idx0.x))
            foreach (var y in Enumerable.Range(idx0.y, idx1.y - idx0.y))
            foreach (var z in Enumerable.Range(idx0.z, idx1.z - idx0.z))
                newArray[x - idx0.x, y - idx0.y, z - idx0.z] = Data[x, y, z];
            
            foreach (var x in Enumerable.Range(idx0.x, idx1.x - idx0.x))
                coordX.Add(Coords[Dim.X][x]);
            foreach (var y in Enumerable.Range(idx0.y, idx1.y - idx0.y))
                coordY.Add(Coords[Dim.Y][y]);
            foreach (var z in Enumerable.Range(idx0.z, idx1.z - idx0.z))
                coordZ.Add(Coords[Dim.Z][z]);

            return new ScalarField(
                newArray, 
                new Dictionary<Dim, ImmutableList<double>>{
                    {Dim.X, coordX.ToImmutableList()}, 
                    {Dim.Y, coordY.ToImmutableList()}, 
                    {Dim.Z, coordZ.ToImmutableList()}
                }.ToImmutableDictionary()
            );
        }

        public Complex IntegrateInRange(
            (double? x, double? y, double? z) range0, 
            (double? x, double? y, double? z) range1
        ) {
            (int x, int y, int z) idx0, idx1;          

            (idx0.x, idx1.x) = GrabRange(Coords[Dim.X], range0.x, range1.x);
            (idx0.y, idx1.y) = GrabRange(Coords[Dim.Y], range0.y, range1.y);
            (idx0.z, idx1.z) = GrabRange(Coords[Dim.Z], range0.z, range1.z);    

            Complex result = new Complex(0.0, 0.0);
            foreach (var x in Enumerable.Range(idx0.x, idx1.x - idx0.x))
            foreach (var y in Enumerable.Range(idx0.y, idx1.y - idx0.y))
            foreach (var z in Enumerable.Range(idx0.z, idx1.z - idx0.z))
                result += Data[x, y, z] * DxDyDz(x, y, z);

            return result;
        }

        public double PortionInRange(
            (double? x, double? y, double? z) range0, 
            (double? x, double? y, double? z) range1
        ) {
            return 
                IntegrateInRange(range0, range1).Real / 
                IntegrateInRange((null, null, null), (null, null, null)).Real;
        }

        public static Complex Coulomb(ScalarField f1, ScalarField f2) {
            int dimX = Math.Max(f1.Coords[Dim.X].Count, f2.Coords[Dim.X].Count);
            int dimY = Math.Max(f1.Coords[Dim.Y].Count, f2.Coords[Dim.Y].Count);
            int dimZ = Math.Max(f1.Coords[Dim.Z].Count, f2.Coords[Dim.Z].Count);

            var xC = f1.Coords[Dim.X].Count / 2;
            var yC = f1.Coords[Dim.Y].Count / 2;
            var zC = f1.Coords[Dim.Z].Count / 2;
            double gX = f1.Coords[Dim.X][xC] - f1.Coords[Dim.X][xC-1];
            double gY = f1.Coords[Dim.Y][yC] - f1.Coords[Dim.Y][yC-1];
            double gZ = f1.Coords[Dim.Z][zC] - f1.Coords[Dim.Z][zC-1];
            
            // // Prepare Coulomb field
            // Complex[,,] coulomb = new Complex[
            //     dimX * 2,
            //     dimY * 2,
            //     dimZ * 2
            // ];

            // //// Coulomb at singular point (r = 0)
            // var r = Math.Sqrt(gX*gX + gY*gY + gZ*gZ);
            // var sing = 
            //     (2.0/gX)*Math.Log((r+gX)/Math.Sqrt(gY*gY+gZ*gZ)) + 
            //     (2.0/gY)*Math.Log((r+gY)/Math.Sqrt(gX*gX+gZ*gZ)) +
            //     (4.0/gZ)*Math.Log((r+gZ)/Math.Sqrt(gX*gX+gY*gY)) -
            //     (2.0*gX/gY/gZ)*Math.Atan(gY*gZ/gX/r) - 
            //     (2.0*gY/gX/gZ)*Math.Atan(gX*gZ/gY/r) - 
            //     (gZ/gX/gY);


            // foreach (var x in Enumerable.Range(0, dimX * 2))
            // foreach (var y in Enumerable.Range(0, dimY * 2))
            // foreach (var z in Enumerable.Range(0, dimZ * 2))
            //     if ((x == dimX) && (y == dimY) && (z == dimZ))
            //         coulomb[x, y, z] = sing;
            //     else 
            //         coulomb[x, y, z] = 
            //             1.0 / Math.Sqrt(
            //                 Math.Pow((x - dimX)*gX, 2) + 
            //                 Math.Pow((y - dimY)*gY, 2) + 
            //                 Math.Pow((z - dimZ)*gZ, 2));

            // var conv = ConvolutionSym(
            //     coulomb, 
            //     f2.Data, 
            //     (dimX, dimY, dimZ), 
            //     (dimX, dimY, dimZ)
            // );

            /***
                To evaluate coulomb integral between 2 scalar field, here I resampled f2 
                onto half-grids to avoid singular point (delta r = 0) in coulomb kernel.
             */

            Complex[,,] coulombSplit = new Complex[
                dimX * 2 + 1,
                dimY * 2 + 1,
                dimZ * 2 + 1
            ];

            foreach (var x in Enumerable.Range(0, dimX * 2 + 1))
            foreach (var y in Enumerable.Range(0, dimY * 2 + 1))
            foreach (var z in Enumerable.Range(0, dimZ * 2 + 1))
                coulombSplit[x, y, z] = 
                    1.0 / Math.Sqrt(
                        Math.Pow((x - dimX - 0.5)*gX, 2) + 
                        Math.Pow((y - dimY - 0.5)*gY, 2) + 
                        Math.Pow((z - dimZ - 0.5)*gZ, 2));

            Complex[,,] f2Split = new Complex[
                dimX + 1,
                dimY + 1,
                dimZ + 1
            ];

            foreach (var x in Enumerable.Range(0, dimX))
            foreach (var y in Enumerable.Range(0, dimY))
            foreach (var z in Enumerable.Range(0, dimZ))
            foreach (var dx in new []{0, 1})
            foreach (var dy in new []{0, 1})
            foreach (var dz in new []{0, 1})
                f2Split[x + dx, y + dy, z + dz] += f2.Data[x, y, z] * 0.125;

            var conv = ConvolutionSym(
                coulombSplit, 
                f2Split, 
                (dimX, dimY, dimZ), 
                (dimX, dimY, dimZ)
            );

            Complex result = 0.0;
            foreach (var x in Enumerable.Range(0, dimX))
            foreach (var y in Enumerable.Range(0, dimY))
            foreach (var z in Enumerable.Range(0, dimZ))
                result += conv[x, y, z] * f1.Data[x, y, z];

            return result * gX * gY * gZ * gX * gY * gZ * CoulombConstant();
        }

        // Unit: eV * nm (Coulomb constant * e)
        static double CoulombConstant() =>
            // 1.43996454;
            1.43996454 / 11.7;
            // 0.0;

        public static Complex[] Flatten(Complex[,] input) {
            
            var dimX = input.GetLength(0);
            var dimY = input.GetLength(1);
            
            var result = new Complex[dimX * dimY];

            foreach (var x in Enumerable.Range(0, dimX))
            foreach (var y in Enumerable.Range(0, dimY))
                result[x * dimY + y] = input[x, y];

            return result;
        }

        public static Complex[] Flatten(Complex[,,] input) {
            
            var dimX = input.GetLength(0);
            var dimY = input.GetLength(1);
            var dimZ = input.GetLength(2);
            
            var result = new Complex[dimX * dimY * dimZ];

            foreach (var x in Enumerable.Range(0, dimX))
            foreach (var y in Enumerable.Range(0, dimY))
            foreach (var z in Enumerable.Range(0, dimZ))
                result[x * dimY * dimZ + y * dimZ + z] = input[x, y, z];

            return result;
        }

        public static Complex[,,] Unflatten(Complex[] input, (int x, int y, int z) dim) {
            var result = new Complex[dim.x, dim.y, dim.z];

            foreach (var x in Enumerable.Range(0, dim.x))
            foreach (var y in Enumerable.Range(0, dim.y))
            foreach (var z in Enumerable.Range(0, dim.z))
                result[x, y, z] = input[x * dim.y * dim.z + y * dim.z + z];

            return result;
        }

        static Complex[,,] Pad(Complex[,,] input, (int x, int y, int z) dim) {
            var result = new Complex[dim.x, dim.y, dim.z];
            foreach (var x in Enumerable.Range(0, input.GetLength(0)))
            foreach (var y in Enumerable.Range(0, input.GetLength(1)))
            foreach (var z in Enumerable.Range(0, input.GetLength(2)))
                result[x, y, z] = input[x, y, z];

            return result;
        }

        static Complex[,,] Truncate(
            Complex[,,] input, 
            (int x, int y, int z) dim,
            (int x, int y, int z) offset
        ) {
            var result = new Complex[dim.x, dim.y, dim.z];
            foreach (var x in Enumerable.Range(0, dim.x))
            foreach (var y in Enumerable.Range(0, dim.y))
            foreach (var z in Enumerable.Range(0, dim.z))
                result[x, y, z] = 
                    input[
                        x + offset.x, 
                        y + offset.y, 
                        z + offset.z
                    ];

            return result;
        }

        static Complex[,,] Reverse(Complex[,,] input) {
            int dimX = input.GetLength(0);
            int dimY = input.GetLength(1);
            int dimZ = input.GetLength(2);
            var result = new Complex[dimX, dimY, dimZ];
            foreach (var x in Enumerable.Range(0, dimX))
            foreach (var y in Enumerable.Range(0, dimY))
            foreach (var z in Enumerable.Range(0, dimZ))
                result[x, y, z] = input[
                    dimX - x - 1, 
                    dimY - y - 1, 
                    dimZ - z - 1];

            return result;
        }

        
        public static Complex[,,] ConvolutionSym(
            Complex[,,] target, 
            Complex[,,] mask,
            (int x, int y, int z) trunStart,
            (int x, int y, int z) trunLength
        ) {        
            
            int dimX = target.GetLength(0);
            int dimY = target.GetLength(1);
            int dimZ = target.GetLength(2);

            var maskPad = Pad(mask, (dimX, dimY, dimZ));
            // var maskPadRev = Reverse(maskPad);

            //// FFT
            var maskFT = FFT(maskPad);
            var targetFT = FFT(target);

            //// Compose convolution result in k-space
            var convFT = new Complex[
                dimX,
                dimY,
                dimZ
            ];

            foreach (var kx in Enumerable.Range(0, dimX))
            foreach (var ky in Enumerable.Range(0, dimY))
            foreach (var kz in Enumerable.Range(0, dimZ))
                convFT[kx, ky, kz] = 
                    maskFT[kx, ky, kz] * 
                    targetFT[kx, ky, kz];

            //// IFFT
            var conv = IFFT(convFT);

            return Truncate(conv, 
                (
                    trunLength.x,
                    trunLength.y,
                    trunLength.z
                ),
                (                    
                    trunStart.x,  
                    trunStart.y, 
                    trunStart.z
                )
            );

            // return conv;
        }

        public static Complex[,,] FFT(Complex[,,] input) {            

            var dimX = input.GetLength(0);
            var dimY = input.GetLength(1);
            var dimZ = input.GetLength(2);

            var flat = Flatten(input);
            var flatOut = new Complex[dimX * dimY * dimZ];

            PinnedArray<Complex> pinnedFlat  = new PinnedArray<Complex>(flat);
            PinnedArray<Complex> pinnedFlatOut = new PinnedArray<Complex>(flatOut);

            DFT.FFT(pinnedFlat, pinnedFlatOut);

            var result = Unflatten(
                flatOut,  
                (dimX, dimY, dimZ)
            );

            pinnedFlat.Dispose();
            pinnedFlatOut.Dispose();

            return result;
        }

        // public static Complex[,,] FFTaf(Complex[,,] input) {

        //     var dimX = input.GetLength(0);
        //     var dimY = input.GetLength(1);
        //     var dimZ = input.GetLength(2);

        //     var flat = Flatten(input);

        //     ArrayFire.Interop.AFArray.af_create_array(
        //         out IntPtr flatAF, 
        //         flat, 
        //         3, new long[]{dimX, dimY, dimZ},
        //         ArrayFire.Interop.af_dtype.c64);

        //     ArrayFire.Interop.AFSignal.af_fft3(
        //         out IntPtr flatOutAF, 
        //         flatAF,
        //         1.0, dimX, dimY, dimZ);
            
        //     var resultAF = new Complex[dimX * dimY * dimZ];
        //     ArrayFire.Interop.AFArray.af_get_data_ptr(resultAF, flatOutAF);

        //     var result = Unflatten(
        //         resultAF,
        //         (dimX, dimY, dimZ)
        //     );
            
        //     return result;
        // }

        public static Complex[,,] IFFT(Complex[,,] input) {

            var dimX = input.GetLength(0);
            var dimY = input.GetLength(1);
            var dimZ = input.GetLength(2);

            var flat = Flatten(input);
            var flatOut = new Complex[dimX * dimY * dimZ];

            PinnedArray<Complex> pinnedFlat  = new PinnedArray<Complex>(flat);
            PinnedArray<Complex> pinnedFlatOut = new PinnedArray<Complex>(flatOut);

            DFT.IFFT(pinnedFlat, pinnedFlatOut);

            var result = Unflatten(
                flatOut.Select(r => r / (dimX * dimY * dimZ)).ToArray(),
                (dimX, dimY, dimZ)
            );
            
            pinnedFlat.Dispose();
            pinnedFlatOut.Dispose();
            
            return result;
        }
    } 
}
