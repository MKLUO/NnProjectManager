using System.Diagnostics;

namespace Utilities
{
    public static class NnAgent {
        public static void RunNn(string name) {
            // TODO: Input file shoin be in this directory(name). 
            // TODO: Check if there's no multiple instances of NN running on same directory
            // TODO: build process argument string.

            string cmd = "";
            try {
                using (Process process = new Process()) {
                    process.StartInfo.FileName  = ""; // TODO: Nn Executable path
                    process.StartInfo.Arguments = ""; 

                    process.StartInfo.UseShellExecute = false;

                    process.Start();
                    process.WaitForExit();
                }
            } catch {
                throw new System.Exception("Exception encountered in RunNn (NnAgent)!");
            }
        }
    }
}