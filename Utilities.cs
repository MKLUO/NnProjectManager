using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Utilities
{
    public static class NnAgent {

        // TODO: Check for NN exe
        static string nnMainPath = "C:\\Program Files (x86)\\nextnano\\2015_08_19\\";

        public static void InitNnFolder(string path, string content) {
            // TODO: 
        }

        public static void RunNn(string path) {
            // TODO: Input file should be in this directory(name). 
            // TODO: Check if there's no multiple instances of NN running on same directory
            // TODO: build process argument string.
            // TODO: Path check (check for safety!)

            try {
                // using (Process process = new Process()) {
                //     process.StartInfo.FileName = 
                //         nnMainPath + "nextnano++\\bin 64bit\\nextnano++_Intel_64bit.exe";
                //     process.StartInfo.Arguments = 
                //         " --license \""    + nnMainPath + "License\\license.txt\"" +
                //         " --database \""   + nnMainPath + "nextnano++\\Syntax\\database_nnp.in\"" +
                //         " --outputdirectory \"" + path + "\"" +
                //         " --noautooutdir \"" + path + "\\input.in\"";
                //     process.StartInfo.UseShellExecute = false;

                //     process.Start();
                //     process.WaitForExit();
                // }
                System.Threading.Thread.Sleep(10000);
            } catch {
                throw new System.Exception("Exception encountered in RunNn (NnAgent)!");
            }
        }
    }

    public static class Util {

        public static string RandomString(int length)
        {
            Random random = new System.Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static string HashPath(string path)
        {
            // TODO: 
            return "";
        }

        public static string Path(string path) {
            // TODO: verify path (make sure it is safe?)
            return path;
        }

        public static string SubFolder(string root, string folder) {
            // TODO: 
            return "";
        }

        public static void SerializeToFile(Object obj, string filePath) {
            // TODO: 

        }

        public static Object DeserializeFromFile(string filePath) {
            // TODO: 
            return new Object();
        }
    }
}