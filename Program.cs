using System;
using System.Collections.Generic;

using NnManager;

namespace nnProjectManager
{
    class Program
    {
        static void Main(string[] args)
        {
            // Project projLoad = new Project(
            //     "C:\\Users\\MKLUO\\Documents\\NNTEST\\test.nnproj",
            //     load: true
            // );

            // projLoad.LaunchAllTask();
            // projLoad.WaitForAllTask();

            // projLoad.Save();

            Project projTest = new Project(
                "C:\\Users\\MKLUO\\Documents\\NNTEST\\test.nnproj"
            );            

            projTest.SetOutputPath(
                "C:\\Users\\MKLUO\\Documents\\NNTEST"
            );

            projTest.AddTemplate(
                "Temp01", 
                "\n\n\r\n @define   \t\t\tkkk_kk 1234sdasd__\n\n\n\n123123 @kkk_kk \t\t \t\t 1312 123"
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
            
            projTest.Save();
        }
    }
}