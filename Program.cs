using System;
using System.Collections.Generic;

using NnManager;

namespace nnProjectManager
{
    class Program
    {
        static void Main(string[] args)
        {
            Project projTest = new Project();            

            projTest.AddTemplate(
                "Temp01", 
                ""
            );

            projTest.AddTask(
                "Task01",
                "Temp01",        
                new Dictionary<string, string>        
                {
                    {"What", "Yup"}
                }
            );

            projTest.LaunchAllTask();

            projTest.WaitForAllTask();
        }
    }
}