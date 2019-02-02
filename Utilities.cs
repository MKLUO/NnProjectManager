using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using System.Security.Cryptography;
using System.Text;

namespace NnManager
{
    using RPath = Util.RestrictedPath;

    public static class NnAgent
    {

        // TODO: Check for NN exe
        readonly static string nnMainPath = @"C:\Program Files (x86)\nextnano\2015_08_19\";
        readonly static string inputFileName = @"nnInput.in";
        readonly static string inputFileNameBackup = @"nnInput.txt";
        readonly public static string projFileName = @"nnProj";

        static void InitNnFolder(RPath path, string content)
        {
            // TODO: 
            Directory.CreateDirectory(path.ToString());
            File.WriteAllText(
                path.SubPath(inputFileName).ToString(),
                content);
            File.WriteAllText(
                path.SubPath(inputFileNameBackup).ToString(),
                content);
        }

        public static bool CheckNn(RPath path, string content, bool test = false)
        {

            // FIXME: test
            
            try
            {
                InitNnFolder(path, content);

                if (test)
                {
                    return true;
                }

                Util.StartAndWaitProcess(
                    nnMainPath + @"nextnano++\bin 64bit\nextnano++_Intel_64bit.exe",
                    " -s --license \"" + nnMainPath + "License\\license.txt\"" +
                    " --database \"" + nnMainPath + "nextnano++\\Syntax\\database_nnp.in\"" +
                    " --outputdirectory \"" + path.ToString() + "\"" +
                    " --noautooutdir -log \"" + path.SubPath(inputFileName).ToString()
                );
                //System.Threading.Thread.Sleep(10000);

                // TODO: parse log to look for syntax error

                return true;
            }
            catch
            {
                throw new Exception("Exception encountered in CheckNn (NnAgent)!");
            }
        }

        public static void RunNn(RPath path, string content, bool test = false)
        {
            // TODO: Input file should be in this directory(name). 
            // TODO: Check if there's no multiple instances of NN running on same directory
            // TODO: build process argument string.
            // TODO: Path check (check for safety!)

            // TODO: Should input be path or content??????

            // FIXME: test

            try
            {
                InitNnFolder(path, content);

                if (test)
                {
                    System.Threading.Thread.Sleep(10000);
                    File.Create(path.SubPath("done.txt").ToString()).Close();
                    return;
                }

                Util.StartAndWaitProcess(
                    nnMainPath + "nextnano++\\bin 64bit\\nextnano++_Intel_64bit.exe",
                    " --license \"" + nnMainPath + "License\\license.txt\"" +
                    " --database \"" + nnMainPath + "nextnano++\\Syntax\\database_nnp.in\"" +
                    " --outputdirectory \"" + path.ToString() + "\"" +
                    " --noautooutdir -log \"" + path.SubPath(inputFileName)
                );
            }
            catch
            {
                throw new Exception("Exception encountered in RunNn (NnAgent)!");
            }
        }
    }

    public static class Util
    {
        #region WarnAndDecide

        public class WarnAndDecideEventArgs
        {
            public WarnAndDecideEventArgs(string s) { Text = s; }
            public String Text { get; } // readonly
        }

        public delegate bool WarnAndDecideEventHandler(WarnAndDecideEventArgs e);

        static event WarnAndDecideEventHandler warning;
        public static event WarnAndDecideEventHandler Warning
        {
            add
            {
                warning = value;
            }
            remove
            {
                warning -= value;
            }
        }

        public static bool WarnAndDecide(string msg)
        {
            WarnAndDecideEventHandler handler = warning;
            if (handler != null)
                return handler(new WarnAndDecideEventArgs(msg));
            else
                return true;
        }

        #endregion

        #region Error

        public class ErrorEventArgs
        {
            public ErrorEventArgs(string s) { Text = s; }
            public String Text { get; } // readonly
        }

        public delegate void ErrorEventHandler(ErrorEventArgs e);

        public static event ErrorEventHandler Error;

        public static void ErrorHappend(string msg)
        {
            ErrorEventHandler handler = Error;
            if (handler != null)
                handler(new ErrorEventArgs(msg));
        }

        #endregion

        [Serializable]
        public class RestrictedPath
        {

            readonly string path;

            public static RestrictedPath InitRoot(string path)
            {
                string fullPath = Path.GetFullPath(path);

                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);

                return new RestrictedPath(fullPath);
            }

            RestrictedPath(string path)
            {
                this.path = path;
            }

            // public static implicit operator RestrictedPath(string path)
            // {
            //     return new RestrictedPath(path);
            // }

            // public RestrictedPath GetDir()
            // {
            //     return Directory.GetParent(path).ToString();
            // }

            public RestrictedPath SubPath(string folder)
            {
                // TODO: check for parent path
                string resultPath = Path.GetFullPath(path + "\\" + folder);

                if (Directory.GetParent(resultPath).ToString().ToLower() !=
                    new DirectoryInfo(path).ToString().ToLower())
                    throw new Exception("Exception occured in Util.SubPath! (input must be single-layered subfolder/file)");

                return new RestrictedPath(resultPath);
            }

            override
            public string ToString()
            {
                return path;
            }
        }

        #region log

        static string log = "";
        public static void Log(string msg)
        {
            log += msg + '\n';
        }
        public static string GetLog()
        {
            return log;
        }

        #endregion

        public static void StartAndWaitProcess(string fileName, string arguments)
        {
            using (Process process = new Process())
            {
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

        #region hashing

        public static string HashPath(string path)
        {
            // TODO:
            string result = "";
            List<string> entries = new List<string>();

            if (Directory.Exists(path.ToString()))
                entries = Directory.GetFileSystemEntries(path.ToString())
                    .OrderBy(p => p).ToList();
            else if (File.Exists(path.ToString()))
                entries.Add(path.ToString());
            else return "";

            foreach (string entry in entries)
            {
                string entryPath = Path.GetFileName(entry);
                result += Hash(entryPath);
                if (Directory.Exists(entry))
                    result += HashPath(entry);
                else
                    result += Hash(File.ReadAllText(entry));
            }

            return Hash(result);
        }

        static byte[] GetHash(string inputString)
        {
            HashAlgorithm algorithm = MD5.Create();  //or use SHA256.Create();
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }

        static string Hash(string input)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in GetHash(input))
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }

        #endregion

        #region serializing

        public static void SerializeToFile(Object obj, RPath filePath)
        {
            // TODO: File mode?
            using (Stream stream = File.Open(filePath.ToString(), FileMode.Create))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, obj);
            }
        }

        public static Object DeserializeFromFile(RPath filePath)
        {
            // TODO: 
            using (Stream stream = File.Open(filePath.ToString(), FileMode.Open))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return formatter.Deserialize(stream);
            }
        }

        #endregion

        public static string ParamToTag(Dictionary<string, (string, string)> param)
        {
            string result = "";

            foreach (var pair in param)
            {
                if (pair.Value.Item1 != null)
                    result += "_" + pair.Key + "-" + pair.Value.Item1;
            }

            return result;
        }        
    }
}