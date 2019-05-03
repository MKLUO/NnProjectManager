using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

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

        // FIXME: Option: Force
        public static bool WarnAndDecide(string msg) {
            WarnAndDecideEventHandler handler = warning;
            if (handler != null)
                return handler(new WarnAndDecideEventArgs(msg));
            else
                return false;
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

            public string? Content {
                get {
                    if (File.Exists(path))
                        using (StreamReader sr = new StreamReader(File.OpenRead(path)))
                            return sr.ReadToEnd();
                    else return null;
                }
            }

            public static RestrictedPath InitRoot(string path) {
                string fullPath = Path.GetFullPath(path);

                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);

                return new RestrictedPath(fullPath);
            }

            RestrictedPath(string path, bool create = true) {
                this.path = path;
                if (!create) return;

                string parent = Directory.GetParent(path).ToString();
                if (!Directory.Exists(parent))
                    Directory.CreateDirectory(parent);
            }

            public static implicit operator string(RestrictedPath rPath) {
                return rPath.path;
            }

            public RestrictedPath SubPath(string folder, bool create = true) {
                string resultPath = Path.GetFullPath(path + "\\" + folder);

                if (Directory.GetParent(resultPath).ToString().ToLower() !=
                    new DirectoryInfo(path).ToString().ToLower())
                    throw new Exception("Exception occured in Util.SubPath! (parameter must be single-layered subfolder/file)");

                return new RestrictedPath(resultPath, create);
            }

            public bool Exists() => Directory.Exists(path) || File.Exists(path);
        }

        #region log

        public static CancellationTokenSource StartLogParser<Log>(RPath logFilePath, CancellationToken ct, Action<string?> status)
            where Log : LogBase, new() {
            if (!File.Exists(logFilePath))
                File.Create(logFilePath).Close();

            var tsLog = CancellationTokenSource.CreateLinkedTokenSource(ct);

            Task logTask = Task.Run(
                () => LogParser<Log>(logFilePath, tsLog.Token, status),
                tsLog.Token
            );

            return tsLog;
        }

        public static void LogParser<Log>(RPath logFilePath, CancellationToken ct, Action<string?> status) 
            where Log : LogBase, new() {
            Log log = new Log();

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = Path.GetDirectoryName(logFilePath);
            watcher.Filter = Path.GetFileName(logFilePath);

            StreamReader sr = new StreamReader(
                new FileStream(
                    logFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.Read,
                    FileShare.ReadWrite
                )
            );
            while (!ct.IsCancellationRequested) {
                watcher.WaitForChanged(WatcherChangeTypes.All, 1000);
                while (!sr.EndOfStream) {
                    string? str = log.Push(sr.ReadLine());
                    if (str != null) status(str);
                }
            }
            while (!sr.EndOfStream) {
                string? str = log.Push(sr.ReadLine());
                if (str != null) status(str);
            }

            status(null);

            sr.Close();
        }

        public static void SaveLog(RPath path, string log) {
            // FIXME:
        }

        #endregion

        public static void StartAndWaitProcess(
            string fileName,
            string arguments,
            CancellationToken ct
        ) {
            using(Process process = new Process()) {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                var reg = ct.Register(() => {
                    if (!process.HasExited)
                        process.Kill();
                });

                process.Start();
                process.WaitForExit();

                reg.Dispose();
            }
        }

        public static int seed =
            BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);

        public static string RandomString(int length) {
            Random random = new System.Random(
                Interlocked.Increment(ref Util.seed)
            );
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }        

        #region hashing

        public static string HashPath(string path) {
            // TODO: better hashing
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
            using(Stream stream = File.Open(filePath, FileMode.Create)) {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, obj);
            }
        }

        public static Object DeserializeFromFile(RPath filePath) {
            using(Stream stream = File.Open(filePath, FileMode.Open)) {
                BinaryFormatter formatter = new BinaryFormatter();
                return formatter.Deserialize(stream);
            }
        }

        #endregion

        public static string ParamToTag(Dictionary < string, (string, string) > param) {
            if (param.Count == 0)
                return "";

            string result = " (";
            foreach (var pair in param)
                if (pair.Value.Item1 != null)
                    result += pair.Key + "-" + pair.Value.Item1 + ", ";
            return result.Substring(0, result.Length - 2) + ")";
        }

        public static string TrimSpaces(this string input) {
            return Regex.Replace(input, "[ |\t|\r|\n]+", "");
        }

        // public static string[] Splitter(this string input, string delim, bool excludeDelim = false) {
        public static string[] Splitter(this string input, string delim) {
            // if (excludeDelim)
            //     return Regex.Split(input, delim)
            //         .Where(s => s != String.Empty)
            //         .Where(s => !Regex.IsMatch(s, delim))
            //         .ToArray<string>();
            // else 
            return Regex.Split(input, delim)
                .Where(s => s != String.Empty)
                .ToArray<string>();
        }
    }
}