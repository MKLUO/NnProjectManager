using System;
using System.IO;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public static class NnAgent {
        // TODO: Check for NN exe
        readonly static string nnMainPath = "C:\\Program Files (x86)\\nextnano\\2015_08_19\\";
        readonly static string inputFileName = "nnInput.in";
        readonly static string inputFileNameBase = "nnInput.txt";
        readonly public static string projFileName = "nnProj";
        readonly public static string taskFileName = "nnTask";

        public static void InitNnFolder(RPath path, string content) {
            // TODO: Backup is created here. Proceeding NN runs copy yhis backup as their input.
            try {
                Directory.CreateDirectory(path);
                File.WriteAllText(
                    path.SubPath(inputFileNameBase),
                    content);
            } catch {
                throw new Exception("Exception encountered in InitNnFolder (NnAgent)!");
            }
        }

        public static void RunNnStructure(RPath path) {
            try {
                File.Copy(
                    path.SubPath(inputFileNameBase),
                    path.SubPath(inputFileName));

                Util.StartAndWaitProcess(
                    nnMainPath + "nextnano++\\bin 64bit\\nextnano++_Intel_64bit.exe",
                    " -s --license \"" + nnMainPath + "License\\license.txt\"" +
                    " --database \"" + nnMainPath + "nextnano++\\Syntax\\database_nnp.in\"" +
                    " --outputdirectory \"" + path + "\"" +
                    " --noautooutdir -log \"" + path.SubPath(inputFileName)
                );

                // TODO: parse log to look for syntax error
            } catch {
                throw new Exception("Exception encountered in RunNnStructure (NnAgent)!");
            }
        }

        public static void RunNn(RPath path) {
            // TODO: Input file should be in this directory(name). 
            // TODO: Check if there's no multiple instances of NN running on same directory
            try {
                File.Copy(
                    path.SubPath(inputFileNameBase),
                    path.SubPath(inputFileName));

                Util.StartAndWaitProcess(
                    nnMainPath + "nextnano++\\bin 64bit\\nextnano++_Intel_64bit.exe",
                    " --license \"" + nnMainPath + "License\\license.txt\"" +
                    " --database \"" + nnMainPath + "nextnano++\\Syntax\\database_nnp.in\"" +
                    " --outputdirectory \"" + path + "\"" +
                    " --noautooutdir -log \"" + path.SubPath(inputFileName)
                );

                // TODO: parse log to look for error
            } catch {
                throw new Exception("Exception encountered in RunNn (NnAgent)!");
            }
        }
    }
}