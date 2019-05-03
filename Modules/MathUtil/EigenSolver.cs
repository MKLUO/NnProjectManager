using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

#nullable enable

namespace NnManager {

    static public class Eigen {

        public static List<(Complex[] vec, double val)> EVD(Complex[,] input, int evNum) {

            var dim  = input.GetLength(0);
            var dimY = input.GetLength(1);

            if (dim != dimY) return new List<(Complex[] vec, double val)>();

            var inputAlg = new alglib.complex[dim, dim];
            foreach (var i in Enumerable.Range(0, dim))
            foreach (var j in Enumerable.Range(0, dim))
                inputAlg[i, j] = new alglib.complex(
                    input[i, j].Real,
                    input[i, j].Imaginary
                );

            var w = new double[0];
            var z = new alglib.complex[0,0];
            alglib.evd.hmatrixevdi(
                inputAlg, 
                dim, 1, 
                false, 0, evNum > dim ? dim - 1 : evNum - 1, 
                ref w, ref z, new alglib.xparams(0)
            );

            var result = new List<(Complex[] vec, double val)>();
            foreach (var i in Enumerable.Range(0, evNum)) {
                var vec = new Complex[dim];
                foreach (var j in Enumerable.Range(0, dim))
                    vec[j] = new Complex(z[j,i].x, z[j,i].y);
                result.Add((vec, w[i]));
            }


            return result;
        }
    }
}