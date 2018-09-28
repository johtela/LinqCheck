/*
# Test Runner

In essence, a unit testing framework is quite a simple apparatus. Its core
function is to discover and execute user-defined test methods. Testing 
libraries typically contain a bunch of auxiliary methods that can be used to
check various conditions inside tests. Another common feature is to provide 
an application or Visual Studio add-in  which runs tests and reports their 
results.

If you need a simple way to define and run your tests, LinqCheck comes 
"batteries included" with a simple test runner. The test runner uses the same
conventions as other testing frameworks, so migrating to a more complete 
solution later on is easy.

We don't write a full application but rather include minimal set of methods
required to build a custom test bench.
*/
namespace LinqCheck
{
	using System;
	using System.Diagnostics;
	using System.Linq;
	using System.Reflection;
	/*
	## Exception Classes
	We use two exception classes to recognize failed tests. The TestFailed
	class is a more general one that indicates any kind of failure. The 
	`PropertyFailed<T>` class inherits from `TestFailed` and is thrown by the
	`Prop.Check` method whenever a property of type `Prop<T>` fails.
	*/
	public class TestFailed : Exception
	{
		public TestFailed (string message) : base (message)
		{
		}
	}

	public class PropertyFailed<T> : TestFailed
	{
		public readonly T Input;

		public PropertyFailed (string property, T input)
			: base (string.Format (
				"Property '{0}' failed for input:\n{1}", property, input))
		{
			Input = input;
		}
	}
	/*
	## Test Attribute
	We define a single attribute `[Test]` to denote a test method. If you 
	decorate a method with that attribute the test runner will find and
	run it.
	*/
	[AttributeUsage (AttributeTargets.Method)]
	public class TestAttribute : Attribute
	{
		public TestAttribute ()
		{
		}
	}
	/*
	Instead of defining another attribute to designate a test class or 
	fixture, we take the instances of the test classes as arguments to the 
	_RunTest*_ methods.
	
	## Checking Test Conditions
	The static Check class contains few helper methods that check specified
	conditions inside tests. Since these methods do not return anything, they 
	cannot be used in property expressions. They are designed to be used in 
	traditional unit test methods, in case you want to include those in your 
	tests.
	*/
	public static class Check
	{
		/*
		`IsTrue` and `IsFalse` methods throw a TestFailed exception if the
		specified condition is true of false respectively.
		*/
		public static void IsTrue (bool condition)
		{
			if (!condition)
				throw new TestFailed ("Expected condition to be true.");
		}
		
		public static void IsFalse (bool condition)
		{
			if (condition)
				throw new TestFailed ("Expected condition to be false.");
		}
		/*
		`AreEqual` and `AreNotEqual` test if two values are equal or not equal. 
		They use the `Object.Equals` method to determine the equality.
		*/
		public static void AreEqual<T> (T x, T y)
		{
			if (!x.Equals (y))
				throw new TestFailed (string.Format (
					"'{0}' and '{1}' should be equal.", x, y));
		}

		public static void AreNotEqual<T> (T x, T y)
		{
			if (x.Equals (y))
				throw new TestFailed (string.Format (
					"'{0}' and '{1}' should not be equal.", x, y));
		}
		/*
		The following method checks that an object has a specified type.
		*/
		public static void IsOfType<T> (object x)
		{
			if (!(x is T))
				throw new TestFailed (string.Format (
					"'{0}' should be of type '{1}'.", x, typeof(T)));
		}
		/*
		The names of `IsNull` and `IsNotNull` methods precisely describe what they do.
		*/
		public static void IsNull (object x)
		{
			if (x != null)
				throw new TestFailed (string.Format ("'{0}' should be null'.", x));
		}

		public static void IsNotNull (object x)
		{
			if (x == null)
				throw new TestFailed (string.Format ("'{0}' should not be null'.", x));
		}
		/*
		Lastly, we define a method which takes an action and fails, if it does
		_not_ throw an exception of the specified type.
		*/
		public static void Throws<E> (Action action) where E: Exception
		{
			Exception caught = null;
			try
			{
				action ();
			}
			catch (Exception ex)
			{
				caught = ex;
			}
			if (caught == null || !(caught is E))
			{
				var msg = string.Format (
					"Expected exception {0} to be thrown, but got {1}", typeof(E).Name,
					caught == null ? "no exception" : caught.GetType ().Name);
				throw new TestFailed (msg);
			}
		}
	}
	/*
	## Running Tests
	The Tester class contains the last pieces of the puzzle. It defines two 
	methods which can be used to run test fixtures. A fixture can be any .NET
	object - it does not have to implement anything or have a specific type.
	*/
	public static class Tester
	{
		/*
		The basic version of the test runner takes an array of test fixtures
		and runs them without measuring the execution time
		*/
		public static void RunTests (params object[] fixtures)
		{
			RunTests (fixtures, false);
		}
		/*
		`RunTestsTimed` works exactly as the `RunTests`, but it also measures
		the time each test method or fixture takes to execute.
		*/
		public static void RunTestsTimed (params object[] fixtures)
		{
			RunTests (fixtures, true);
		}
		/*
		The actual implementation of the test runners is in the private method
		below. It is fairly simple; we set up the stopwatch to measure time,
		if needed, run the tests in each fixture, and finally report the 
		results to the user. 
		
		As a last step, we call `GC.Collect` to run the garbage collection. 
		This helps us verify that there are no major memory leaks in the code
		that was tested.
		*/
		private static void RunTests (object[] fixtures, bool timed)
		{
			int run = 0;
			int failed = 0;
			Stopwatch stopWatch = null;

			if (timed)
			{
				stopWatch = new Stopwatch ();
				stopWatch.Reset ();
				stopWatch.Start ();
			}
			foreach (object fixture in fixtures)
				TestFixture (fixture, timed, ref run, ref failed);
			if (timed) stopWatch.Stop ();
			
			if (failed > 0)
			{
				Console.ForegroundColor = ConsoleColor.DarkRed;
				System.Console.WriteLine ("{0} out of {1} tests failed.", failed, run);
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Green;
				if (timed)
					System.Console.WriteLine ("All tests passed. {0} tests run in {1}.", 
						run, stopWatch.Elapsed);
				else
					System.Console.WriteLine ("All tests passed. {0} tests run.", run);
			}
			Console.ResetColor ();
			GC.Collect ();
		}
		/*
		## Private Helpers
		The rest of the methods are helper functions used by `RunTests`. The 
		first method checks if a method has the `[Test]` attribute defined.
		*/
		private static bool IsTest (this MethodInfo mi)
		{
			return mi.IsDefined (typeof (TestAttribute), false);
		}
		/*
		The method below runs all tests in a test fixture, and reports back any
		failed tests. The only tricky part in the implementation is handling 
		the exceptions thrown by test methods. Since we are using reflection to
		locate and call the test methods, we need to unwrap the actual 
		exception thrown from a TargetInvocationException object.
		*/
		private static void TestFixture (object fixture, bool timed, ref int run, 
			ref int failed)
		{
			Console.ForegroundColor = ConsoleColor.Blue;
			Console.WriteLine ("Executing tests for fixture: {0}",
				fixture.GetType ().Name);

			var tests = from m in fixture.GetType ().GetMethods ()
						where m.IsTest ()
						select m;
			var stopWatch = timed ? new Stopwatch () : null;
			
			Console.ResetColor ();
			foreach (var test in tests)
			{
				try
				{
					if (timed)
					{
						stopWatch.Reset ();
						stopWatch.Start ();
					}
					test.Invoke (fixture, null);
					if (timed)
					{
						stopWatch.Stop ();
						Console.WriteLine ("{0} - {1}", stopWatch.Elapsed, test.Name);
					}
					else
						Console.Write (".");
				}
				catch (TargetInvocationException ex)
				{
					OutputFailure (test.Name, ex.InnerException);
					failed++;
				}
				run++;
			}
			Console.WriteLine ();
		}
		/*
		The following helper writes text to console in the specified color. It
		resets the color to default after that.
		*/
		private static void WriteInColor (ConsoleColor color, string output,
			params object[] args)
		{
			Console.ForegroundColor = color;
			if (args == null || args.Length == 0)
				Console.WriteLine (output);
			else
				Console.WriteLine (output, args);
			Console.ResetColor ();
		}
		/*
		The `OutputFailure` method prints the name of a failing test, the 
		exception message, and the stack trace. It skips the first and last 
		stack frame since those are always inside the test runner.
		*/
		private static void OutputFailure (string test, Exception ex)
		{
			Console.WriteLine ();
			WriteInColor (ConsoleColor.Red, "Test '{0}' failed.", test);
			Console.Write ("Reason: ");
			WriteInColor (ConsoleColor.Yellow, ex.Message);
			var st = ex.StackTrace.Split ('\n');
			for (int i = 1; i < st.Length - 2; i++)
				Console.WriteLine (st [i]);
		}
	}
}
/*
## Redirecting Output
If you want to get the output of the test runner to some other place than the
console window, use the `Console.SetOut` method to redirect it to a file, for
example.
*/