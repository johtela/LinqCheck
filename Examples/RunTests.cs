/*
# Running the Tests

The only thing left to do is to run all the test fixtures we built in the 
previous sections. As stated in the beginning, we have many options for a test
runner; practically any of the unit testing frameworks will do. The simplest
solution, though, is to use the built-in command line runner. It is typically 
called from the main method. The code below shows how to run the three test 
fixtures we defined previously.
*/
namespace Examples
{
	using LinqCheck;
	using UITests;

	class RunTests
	{
		static void Main (string[] args)
		{
			/*
			There are two variants for the test runner, the `RunTests` will 
			execute test methods without gathering any timing data. 
			`RunTestsTimed` will record how long each test takes and show the 
			elapsed time with the results. The output will appear in the 
			console window. If you want it somewhere else, 	use the standard 
			`>` directive in the command line to redirect the output to a file,
			for example.

			Both methods take a variable list of test fixtures as an argument. 
			Just create an instance of each fixture and pass it to the methods.
			*/
			Tester.RunTestsTimed (
				new BasicTests (),
				new SeqTests (),
				new CalculatorTests ());
		}
	}
}
/*
## Conclusion
Hopefully you got some idea about how property based testing is done with 
LinqCheck based on these few examples. Next we will jump to the implementation
and cover all the concepts needed in LinqCheck's design.
*/
