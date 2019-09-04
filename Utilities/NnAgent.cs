using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;
    using Dim = ScalarField.Dim;

    public static class NnAgent {
        // TODO: Check for NN exe 
        readonly static string nnMainPath = "C:\\Program Files (x86)\\nextnano\\2015_08_19\\";
        readonly static string nnPPPath = "nextnano++\\bin 64bit\\nextnano++_Intel_64bit.exe";
        readonly static string nn3Path = "nextnano3\\Intel 64bit\\nextnano3_Intel_64bit.exe";
        readonly static string nnPPDBPath = "nextnano++\\Syntax\\database_nnp.in\"";
        readonly static string nn3DBPath = "nextnano3\\Syntax\\database_nn3.in\"";

        readonly public static string inputFileName = "nnInput.in";
        readonly public static string inputRefFileName = "_nnInput.in";
        readonly public static string logFileName = "nnInput.log";
        readonly public static string projFileName = "nnProj";
        readonly public static string tempFileName = "nnTemp";
        readonly public static string planFileName = "nnPlan";
        readonly public static string taskFileName = "nnTask";

        public static void RunNnStructure(RPath path, string content, CancellationToken ct, NnType type) {
            try {
                File.WriteAllText(
                    path.SubPath(inputFileName),
                    content);                    

                string exePath = "";
                string dBPath = "";
                switch (type) {
                    case NnType.Nn3: 
                        exePath = nn3Path; 
                        dBPath = nn3DBPath;
                        break;
                    case NnType.NnPP: 
                        exePath = nnPPPath;
                        dBPath = nnPPDBPath;
                        break;
                }
                if (exePath == "") throw new Exception();
                
                Util.StartAndWaitProcess(
                    nnMainPath + exePath,
                    " -s --license \"" + nnMainPath + "License\\license.txt\"" +
                    " --database \"" + nnMainPath + dBPath +
                    " --outputdirectory \"" + path + "\"" +
                    " --noautooutdir -log \"" + path.SubPath(inputFileName),
                    ct
                );
            } catch {
                Util.ErrorHappend("Exception encountered in RunNnStructure (NnAgent)!");
            }
        }

        public static void RunNn(RPath path, string content, CancellationToken ct, NnType type) {
            try {
                File.WriteAllText(
                    path.SubPath(inputFileName),
                    content);

                File.WriteAllText(
                    path.SubPath(inputRefFileName),
                    content);

                string exePath = "";
                string dBPath = "";
                switch (type) {
                    case NnType.Nn3: 
                        exePath = nn3Path; 
                        dBPath = nn3DBPath;
                        break;
                    case NnType.NnPP: 
                        exePath = nnPPPath;
                        dBPath = nnPPDBPath;
                        break;
                }
                if (exePath == "") throw new Exception();

                Util.StartAndWaitProcess(
                    nnMainPath + exePath,
                    " --license \"" + nnMainPath + "License\\license.txt\"" +
                    " --database \"" + nnMainPath + dBPath +
                    " --outputdirectory \"" + path + "\"" +
                    " --noautooutdir -log \"" + path.SubPath(inputFileName),
                    ct
                );
            } catch {
                Util.ErrorHappend("Exception encountered in RunNn (NnAgent)!");
            }
        }

        public static string? GenerateNnReport(RPath path, CancellationToken _, NnType type) {
            try {
                string exePath = "";
                switch (type) {
                    case NnType.Nn3: exePath = nn3Path; break;
                    case NnType.NnPP: exePath = nnPPPath; break;
                }

                string timeTotal = "", convergence = "";

                string content = File.ReadAllText(path.SubPath(logFileName));
                string[] lines = content.Splitter("([\r\n|\r|\n]+)");

                foreach (string line in lines)
                    if (Regex.IsMatch(line, "Simulator Run Time:"))
                        // timeTotal = Regex.Match(line, @"(\(.*\[h\]\))").Result("$1\n");
                        timeTotal = line + "\n";
                    else if (Regex.IsMatch(line, "OUTER-ITERATION failed to converge!!"))
                        convergence = "OUTER-ITERATION failed to converge!!\n";

                return timeTotal + convergence;
            } catch {
                return null;
            }
        }

        public enum BandType {
            X1,
            X2,
            X3,
            Gamma,
            Delta
        }

        public enum Spin {
            Up,
            Down
        }

        public static (string real, string imag) NnAmplFileEntry(BandType band, int id, Spin spin) {
            switch (spin) {
                case Spin.Down:
                    return (
                        $"wf_amplitude_real_quantum_region_{band.ToString()}_2_0000_{id.ToString("0000")}", 
                        $"wf_amplitude_imag_quantum_region_{band.ToString()}_2_0000_{id.ToString("0000")}");
                default:
                    return (
                        $"wf_amplitude_real_quantum_region_{band.ToString()}_1_0000_{id.ToString("0000")}", 
                        $"wf_amplitude_imag_quantum_region_{band.ToString()}_1_0000_{id.ToString("0000")}");
            }
        }
            

        public static string NnProbFileEntry(BandType band, int id) =>
            $"wf_probability_quantum_region_{band.ToString()}_0000_{id.ToString("0000")}";


        // FIXME: HACKING HERE! Should be generalized in future.

        public static (RPath? data, RPath? coord, RPath? fld, RPath? v) GetCoordAndDat(RPath path, string entry, bool ignoreExist = false) {
            RPath coord = path.SubPath(entry + ".coord");
            RPath data = path.SubPath(entry + ".dat");
            RPath fld = path.SubPath(entry + ".fld");
            RPath v = path.SubPath(entry + ".v");

            if (!ignoreExist)
                if (!(
                    File.Exists(coord) && 
                    File.Exists(data) && 
                    File.Exists(fld) && 
                    File.Exists(v)
                    ))
                    return (null, null, null, null);
            
            return (data, coord, fld, v);
        }

        public static List<(string, string)>? ReadNXY(string content, int ny)
        {
            string[] dataLines =
                content.Splitter("[\r\n|\r|\n]+");

            int n = dataLines[0].Splitter("[ |\t]+").Length;
            if (ny > n - 2) return null;

            var result = new List<(string, string)>();
            foreach (int i in Enumerable.Range(1, dataLines.Count() - 1)) {
                var data = dataLines[i].Splitter("[ |\t]+");
                if (ny > data.Length - 2) return null;
                
                result.Add((data[0], data[ny + 1]));
            }

            return result;
        }

        public static Dictionary<int, double>? ReadNXYIntDouble(string content, int ny)
        {
            var nxy = NnAgent.ReadNXY(content, ny) ?? 
                throw new Exception();
            if (nxy == null) return null;

            var result = new Dictionary<int, double>();

            foreach (var (id, value) in nxy)
                result[int.Parse(id)] = Double.Parse(value);
            
            return result;
        }

        public static Complex[,,] CoulombKernel(ScalarField refWf) {
            var refer = refWf;
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
            
            return coulomb;
        }
    }
}