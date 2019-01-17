using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Utilities
{
    public static class NnAgent {

        static string nnMainPath = "C:\\Program Files (x86)\\nextnano\\2015_08_19\\";

        public static void RunNn(string path) {
            // TODO: Input file should be in this directory(name). 
            // TODO: Check if there's no multiple instances of NN running on same directory
            // TODO: build process argument string.
            // TODO: Path check (check for safety!)

            try {
                using (Process process = new Process()) {
                    process.StartInfo.FileName = 
                        nnMainPath + "nextnano++\\bin 64bit\\nextnano++_Intel_64bit.exe";
                    process.StartInfo.Arguments = 
                        " --license "    + nnMainPath + "License\\license.txt" +
                        " --database "   + nnMainPath + "nextnano++\\Syntax\\database_nnp.in" +
                        " --outputdirectory " + path +
                        " --noautooutdir " + path + "\\input.in";
                    process.StartInfo.UseShellExecute = false;

                    process.Start();
                    process.WaitForExit();
                }
            } catch {
                throw new System.Exception("Exception encountered in RunNn (NnAgent)!");
            }
        }
    }

    public static class Util {

        static HashSet<string> usedString = new HashSet<string>();
        public static string RandomString(int length)
        {
            Random random = new System.Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string str;
            do {
                str = new string(Enumerable.Repeat(chars, length)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
            } while (!usedString.Contains(str));
            usedString.Add(str);

            return str;
        }
    }
}