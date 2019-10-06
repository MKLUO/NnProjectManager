using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

#nullable enable

namespace NnManager {

    static public class Eigen {

        static alglib.complex[,] ToAlglibComplexMat(Complex[,] input) {
            var inputAlg = new alglib.complex[input.GetLength(0), input.GetLength(1)];
            foreach (var i in Enumerable.Range(0, input.GetLength(0)))
            foreach (var j in Enumerable.Range(0, input.GetLength(1)))
                inputAlg[i, j] = new alglib.complex(
                    input[i, j].Real,
                    input[i, j].Imaginary
                );

            return inputAlg;
        }

        static Complex[,] ToComplexMat(alglib.complex[,] input) {
            var inputOrd = new Complex[input.GetLength(0), input.GetLength(1)];
            foreach (var i in Enumerable.Range(0, input.GetLength(0)))
            foreach (var j in Enumerable.Range(0, input.GetLength(1)))
                inputOrd[i, j] = new Complex(
                    input[i, j].x,
                    input[i, j].y
                );

            return inputOrd;
        }

        static alglib.complex[,] AlglibIdMat(int dim) {
            var output = new alglib.complex[dim, dim];
            foreach (var i in Enumerable.Range(0, dim))
                output[i, i] = 1.0;
            return output;
        }

        public static Complex[] SingleEntryVec(int dim, int idx) {
            var output = new Complex[dim];
            output[idx] = 1.0;
            return output;
        }

        public static List<(Complex[] vec, double val)> EVD(Complex[,] input, int evNum = 0) {

            var dim  = input.GetLength(0);
            var dimY = input.GetLength(1);

            if (dim != dimY) return new List<(Complex[] vec, double val)>();

            evNum = (evNum == 0) ? dim : evNum;

            var w = new double[0];
            var z = new alglib.complex[0,0];
            alglib.evd.hmatrixevdi(
                ToAlglibComplexMat(input), 
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

        public static Complex[,] Multiply(Complex[,] a, Complex[,] b) {
            if (a.GetLength(1) != b.GetLength(0))
                return new Complex[0,0];

            var c = new alglib.complex[a.GetLength(0), b.GetLength(1)];

            alglib.cmatrixgemm(
                a.GetLength(0),
                b.GetLength(1),
                a.GetLength(1),
                1.0,
                ToAlglibComplexMat(a), 0, 0, 0,
                ToAlglibComplexMat(b), 0, 0, 0,
                0.0, 
                ref c, 0, 0
            );

            return ToComplexMat(c);
        }

        public static Complex[,] Multiply(Complex[,] a, Complex[] vec) {
            var b = new Complex[vec.GetLength(0), 1];
            foreach (var i in Enumerable.Range(0, vec.GetLength(0))) 
                b[i, 0] = vec[i];
            return Multiply(a, b);
        }

        public static Complex[,] Transpose(Complex[,] input) =>
            RorCTranspose(input, false);

        public static Complex[,] Adjoint(Complex[,] input) =>
            RorCTranspose(input, true);

        static Complex[,] RorCTranspose(Complex[,] input, bool conj) {
            if (input.GetLength(0) != input.GetLength(1))
                return new Complex[0,0];

            var dim = input.GetLength(0);

            var inputAdj = new alglib.complex[dim, dim];

            alglib.cmatrixgemm(
                dim, dim, dim,
                1.0,
                ToAlglibComplexMat(input), 0, 0, conj? 2: 1,
                AlglibIdMat(dim), 0, 0, 0,
                0.0, 
                ref inputAdj, 0, 0
            );

            return ToComplexMat(inputAdj);
        }

        public static bool Test() {
            var sigX = new Complex[2,2];
            var sigY = new Complex[2,2];

            sigX[0,1] = 1;
            sigX[1,0] = 1;

            sigY[0,1] = new Complex(0, -1);
            sigY[1,0] = new Complex(0, 1);

            var shouldBeSigZ = Multiply(sigX, sigY);

            return true;
        }
    }
}