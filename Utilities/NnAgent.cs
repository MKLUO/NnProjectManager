using System;
using System.IO;
using System.Threading;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public static class NnAgent {
        // TODO: Check for NN exe 
        readonly static string nnMainPath = "C:\\Program Files (x86)\\nextnano\\2015_08_19\\";
        readonly public static string inputFileName = "nnInput.in";
        readonly public static string logFileName = "nnInput.log";
        readonly public static string projFileName = "nnProj";
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
    }
}