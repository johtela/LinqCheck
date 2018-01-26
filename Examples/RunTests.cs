namespace Examples
{
	using LinqCheck;
	using System.Diagnostics;
	using UITests;

	class RunTests
	{
		static void Main (string[] args)
		{
			Tester.RunTestsTimed (new CalculatorTests ());
			//RunCalcTest ();
		}

		private static void RunCalcTest ()
		{
			using (var calc = new ModelCalculator ())
			{
				calc.Digit (6);
				calc.Divide ();
				calc.Digit (0);
				calc.Divide ();
				calc.Digit (0);
				calc.Digit (4);
				calc.Digit (0);
			}
		}
	}
}
