using System;
using System.Collections.Generic;

using NnManager;

namespace nnProjectManager
{
    class Program
    {
        static void Main(string[] args)
        {
            Project projLoad = new Project(
                "C:\\Users\\MKLUO\\Documents\\NNTEST",
                load: true
            );

            projLoad.LaunchAllTask();
            projLoad.WaitForAllTask();

            projLoad.Save();

            // Project projTest = new Project();            

            // projTest.AddTemplate(
            //     "Temp01", 
            //     ""
            // );

            // projTest.AddTask(
            //     "Task01",
            //     "Temp01",        
            //     new Dictionary<string, string>        
            //     {
            //         {"What", "Yup"}
            //     }
            // );

            // projTest.Save(
            //     "C:\\Users\\MKLUO\\Documents\\NNTEST"
            // );

            // projTest.LaunchAllTask();
            // projTest.WaitForAllTask();
        }
    }
}