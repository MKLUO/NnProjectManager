using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;

namespace NnManager {
    using RPath = Util.RestrictedPath;

    public static class Util {
        #region WarnAndDecide

        public class WarnAndDecideEventArgs {
            public WarnAndDecideEventArgs(string s) { Text = s; }
            public String Text { get; } // readonly
        }

        public delegate bool WarnAndDecideEventHandler(WarnAndDecideEventArgs e);

        static event WarnAndDecideEventHandler warning;
        public static event WarnAndDecideEventHandler Warning {
            add {
                warning = value;
            }
            remove {
                warning -= value;
            }
        }

        public static bool WarnAndDecide(string msg) {
            WarnAndDecideEventHandler handler = warning;
            if (handler != null)
                return handler(new WarnAndDecideEventArgs(msg));
            else
                return true;
        }

        #endregion

        #region Error

        public class ErrorEventArgs {
            public ErrorEventArgs(string s) { Text = s; }
            public String Text { get; } // readonly
        }

        public delegate void ErrorEventHandler(ErrorEventArgs e);

        public static event ErrorEventHandler Error;

        public static void ErrorHappend(string msg) {
            ErrorEventHandler handler = Error;
            if (handler != null)
                handler(new ErrorEventArgs(msg));
        }

        #endregion

        [Serializable]
        public class RestrictedPath {
            readonly string path;

            public static RestrictedPath InitRoot(string path) {
                string fullPath = Path.GetFullPath(path);

                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);

                return new RestrictedPath(fullPath);
            }

            RestrictedPath(string path) {
                this.path = path;
            }

            public static implicit operator string(RestrictedPath rPath) {
                return rPath.path;
            }

            public RestrictedPath SubPath(string folder) {
                // TODO: check for parent path
                string resultPath = Path.GetFullPath(path + "\\" + folder);

                if (Directory.GetParent(resultPath).ToString().ToLower() !=
                    new DirectoryInfo(path).ToString().ToLower())
                    throw new Exception("Exception occured in Util.SubPath! (parameter must be single-layered subfolder/file)");

                return new RestrictedPath(resultPath);
            }
        }

        #region log

        static string log = "";
        public static void Log(string msg) {
            log += msg + '\n';
        }
        public static string GetLog() {
            return log;
        }

        #endregion

        public static void StartAndWaitProcess(string fileName, string arguments) {
            using(Process process = new Process()) {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;

                process.Start();
                process.WaitForExit();
            }
        }

        public static string RandomString(int length) {
            Random random = new System.Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        #region hashing

        public static string HashPath(string path) {
            // TODO:
            string result = "";
            List<string> entries = new List<string>();

            if (Directory.Exists(path))
                entries = Directory.GetFileSystemEntries(path)
                .OrderBy(p => p).ToList();
            else if (File.Exists(path))
                entries.Add(path);
            else return "";

            foreach (string entry in entries) {
                string entryPath = Path.GetFileName(entry);
                result += Hash(entryPath);
                if (Directory.Exists(entry))
                    result += HashPath(entry);
                else
                    result += Hash(File.ReadAllText(entry));
            }

            return Hash(result);
        }

        static byte[] GetHash(string inputString) {
            HashAlgorithm algorithm = MD5.Create(); //or use SHA256.Create();
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }

        static string Hash(string input) {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in GetHash(input))
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }

        #endregion

        #region serializing

        public static void SerializeToFile(Object obj, RPath filePath) {
            // TODO: File mode?
            using(Stream stream = File.Open(filePath, FileMode.Create)) {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, obj);
            }
        }

        public static Object DeserializeFromFile(RPath filePath) {
            // TODO: 
            using(Stream stream = File.Open(filePath, FileMode.Open)) {
                BinaryFormatter formatter = new BinaryFormatter();
                return formatter.Deserialize(stream);
            }
        }

        #endregion

        public static string ParamToTag(Dictionary < string, (string, string) > param) {
            string result = "";

            foreach (var pair in param) {
                if (pair.Value.Item1 != null)
                    result += "_" + pair.Key + "-" + pair.Value.Item1;
            }

            return result;
        }
    }
}