using System;
using System.Collections.Generic;

using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace NnManager {

    public partial class Project {

        [Serializable]
        class Template {

            public Template(
                string content
            ) {

            }
            public string generateContent(
                Dictionary<string, string> param) {
                return "";
            }
        }
    }
}