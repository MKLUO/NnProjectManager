using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public static class NnAgent {
        // TODO: Check for NN exe 
        readonly static string nnMainPath = "C:\\Program Files (x86)\\nextnano\\2015_08_19\\";
        readonly public static string inputFileName = "nnInput.in";
        readonly public static string logFileName = "nnInput.log";
        readonly public static string projFileName = "nnProj";
        readonly public static string tempFileName = "nnTemp";
        readonly public static string planFileName = "nnPlan";
        readonly public static string taskFileName = "nnTask";

        public static void RunNnStructure(RPath path, string content, CancellationToken ct) {
            try {
                File.WriteAllText(
                    path.SubPath(inputFileName),
                    content);

                Util.StartAndWaitProcess(
                    nnMainPath + "nextnano++\\bin 64bit\\nextnano++_Intel_64bit.exe",
                    " -s --license \"" + nnMainPath + "License\\license.txt\"" +
                    " --database \"" + nnMainPath + "nextnano++\\Syntax\\database_nnp.in\"" +
                    " --outputdirectory \"" + path + "\"" +
                    " --noautooutdir -log \"" + path.SubPath(inputFileName),
                    ct
                );

                File.WriteAllText(
                    path.SubPath(inputFileName),
                    content);
            } catch {
                throw new Exception("Exception encountered in RunNnStructure (NnAgent)!");
            }
        }

        public static void RunNn(RPath path, string content, CancellationToken ct) {
            try {
                File.WriteAllText(
                    path.SubPath(inputFileName),
                    content);

                Util.StartAndWaitProcess(
                    nnMainPath + "nextnano++\\bin 64bit\\nextnano++_Intel_64bit.exe",
                    " --license \"" + nnMainPath + "License\\license.txt\"" +
                    " --database \"" + nnMainPath + "nextnano++\\Syntax\\database_nnp.in\"" +
                    " --outputdirectory \"" + path + "\"" +
                    " --noautooutdir -log \"" + path.SubPath(inputFileName),
                    ct
                );

                File.WriteAllText(
                    path.SubPath(inputFileName),
                    content);
            } catch {
                throw new Exception("Exception encountered in RunNn (NnAgent)!");
            }
        }

        public enum BandType {
            X1,
            X2,
            X3,
            Gamma,
            Delta
        }


        // FIXME: HACKING HERE! Should be generalized in future.

        public static IEnumerable<(RPath data, RPath coord, int num)> GetQProbPaths(RPath path, BandType band) {
            int counter = 1;
            do {
                string entry = NnAgent.GetQProbEntry(NnAgent.BandType.X1, counter);
                RPath coord = path.SubPath(entry + ".coord");
                RPath data = path.SubPath(entry + ".dat");

                if (!(File.Exists(coord) && File.Exists(data)))
                    yield break;
                
                yield return (data, coord, counter);

                counter++;
            } while (true);
        }

        static string GetQProbEntry(BandType band, int num) {
            return $"wf_probability_quantum_region_{band.ToString()}_0000_{num.ToString("0000")}";
        }

        public static RPath? GetQSpectrumPath(RPath path, BandType band) {
            string entry = $"wf_spectrum_quantum_region_{band.ToString()}";
            RPath data = path.SubPath(entry + ".dat");

            if (File.Exists(data))
                return data;
            else return null;
        }

        public static Dictionary<int, double>? NXY(string content, int ny)
        {
            string[] dataLines =
                content.Splitter("[\r\n|\r|\n]+");

            int n = dataLines[0].Splitter("([ |\t]+)").Length;
            if (ny >= n) return null;

            Dictionary<int, double> result = new Dictionary<int, double>();
            foreach (int i in Enumerable.Range(1, dataLines.Count() - 1)) {
                var data = dataLines[i].Splitter("[ |\t]+");
                result[int.Parse(data[0])] = Double.Parse(data[ny + 1], System.Globalization.NumberStyles.Float);
            }

            return result;
        }
    }
}