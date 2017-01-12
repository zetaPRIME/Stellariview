using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using System.Diagnostics;

using Fluent.IO;

namespace Stellariview {
    public class Program {
        public static string[] args;

        public static string tempModifier = "";

        public static void Main(string[] args) {
            Program.args = args;
            ProcessArguments(args);

            using (Core core = Core.instance = new Core()) {
                core.Run();
            }
        }
        static void ProcessArguments(string[] args) {
            if (args.Length > 0) Core.startingPath = new Path(args[0]);
            else Core.startingPath = new Path(Assembly.GetCallingAssembly().Location).Up(); // containing folder
        }
    }
}
