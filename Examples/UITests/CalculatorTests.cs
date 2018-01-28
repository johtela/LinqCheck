namespace Examples.UITests
{
	using LinqCheck;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using ExtensionCord;

	public class CalculatorTests
	{
		private ICalculator GetWinCalculator ()
		{
			if (Environment.OSVersion.Version.Major == 6 &&
				Environment.OSVersion.Version.Minor == 2)
				return new Win10Calculator ();
			else
				return new Win7Calculator ();
		}

		[Test]
		public void TestRandomSequence ()
		{
			Command.Register ();
			using (var wincalc = GetWinCalculator ())
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
