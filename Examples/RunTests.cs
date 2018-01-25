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
		}

	}
}
