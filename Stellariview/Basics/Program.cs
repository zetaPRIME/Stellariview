using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Diagnostics;

using Fluent.IO;

namespace Stellariview
{
	public class Program
	{
		public static string[] args;

		public static string tempModifier = "";

		public static void Main(string[] args)
		{
			Program.args = args;
			ProcessArguments(args);

			using (Core core = Core.instance = new Core())
			{
				core.Run();
			}
		}
		static void ProcessArguments(string[] args)
		{
			if (args.Length > 0) Core.startingPath = new Path(args[0]);
			// debug: point to a place
			//else Core.startingPath = new Path("D:\\~cyr\\data\\new\\XFAV\\set\\!! YES !!\\qq\\8faf711072c98df42c227636ef65c6c2.jpg");
			else Core.startingPath = new Path("D:\\~cyr\\data\\new\\XFAV\\set\\!! YES !!\\qq\\1018421_FenFen_mbf_ych_1_lo.png");
		}
	}
}
