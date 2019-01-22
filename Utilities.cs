using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace NnManager
{
    public static class NnAgent {

        // TODO: Check for NN exe
        static string nnMainPath = @"C:\Program Files (x86)\nextnano\2015_08_19\";

        public static void InitNnFolder(string path, string content) {
            // TODO: 

        }

        public static void CheckNn(string path) {
            try {
                Util.StartAndWaitProcess(
                    nnMainPath + @"nextnano++\bin 64bit\nextnano++_Intel_64bit.exe",
                    " --license \""    + nnMainPath + "License\\license.txt\"" +
                    " --database \""   + nnMainPath + "nextnano++\\Syntax\\database_nnp.in\"" +
                    " --outputdirectory \"" + path + "\"" +
                    " --noautooutdir -log -s \"" + path + "\\input.in\""
                );
                //System.Threading.Thread.Sleep(10000);
            } catch {
                throw new System.Exception("Exception encountered in RunNn (NnAgent)!");
            }
        }

        public static void RunNn(string path) {
            // TODO: Input file should be in this directory(name). 
            // TODO: Check if there's no multiple instances of NN running on same directory
            // TODO: build process argument string.
            // TODO: Path check (check for safety!)

            // TODO: Should input be path or content??????

            try {
                Util.StartAndWaitProcess(
                    nnMainPath + "nextnano++\\bin 64bit\\nextnano++_Intel_64bit.exe",
                    " --license \""    + nnMainPath + "License\\license.txt\"" +
                    " --database \""   + nnMainPath + "nextnano++\\Syntax\\database_nnp.in\"" +
                    " --outputdirectory \"" + path + "\"" +
                    " --noautooutdir -log \"" + path + "\\input.in\""
                );
                //System.Threading.Thread.Sleep(10000);
            } catch {
                throw new System.Exception("Exception encountered in RunNn (NnAgent)!");
            }
        }
    }

    public static class Util {

        public static void StartAndWaitProcess(string fileName, string arguments) {
            using (Process process = new Process()) {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;

                process.Start();
                process.WaitForExit();
            }
        }

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

        // public static string Path(string path) {
        //     // TODO: verify path (make sure it is safe?)
        //     return path;
        // }

        // public static string SubPath(string root, string path) {
        //     // TODO: 
        //     return root + "\\" + path;
        // }

        public static void SerializeToFile(Object obj, string filePath) {
            // TODO: File mode?
            using (Stream stream = File.Open(filePath, FileMode.Create)) {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, obj);
            }
        }

        public static Object DeserializeFromFile(string filePath) {
            // TODO: 
            using (Stream stream = File.Open(filePath, FileMode.Open)) {
                BinaryFormatter formatter = new BinaryFormatter();
                return formatter.Deserialize(stream);
            }
        }
    }
}