namespace Examples.UITests
{
	using LinqCheck;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using ExtensionCord;

	public class CalculatorTests
	{
		[Test]
		public void TestRandomSequence ()
		{
			Command.Register ();
			using (var wincalc = new Win7Calculator ())
			{
				(from commands in Prop.ForAll<List<Command>> ()
				 let result = Command.Execute (wincalc, commands)
				 select new { commands = commands.AsPrintable (), result })
				.Check (t => t.commands.Count () < 5);
			}
		}
	}
}
