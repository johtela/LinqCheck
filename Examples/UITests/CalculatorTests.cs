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
			using (var modcalc = new ModelCalculator ())
			{
				(from commands in Prop.ForAll<List<Command>> ()
				 let realres = Command.Execute (wincalc, commands)
				 let modres = Command.Execute (modcalc, commands)
				 select new
				 {
					 ok = wincalc.ResultAvailable &&  modcalc.ResultAvailable,
					 commands = commands.AsPrintable (),
					 realres,
					 modres
				 })
				.Check (t => !t.ok || t.realres.ApproxEquals (t.modres));
			}
		}
	}
}
