namespace LinqCheck
{
	using System;
	using System.Diagnostics;
	using System.Linq;
	using System.Reflection;
	
	/// <summary>
	/// Exception type for test failure.
	/// </summary>
	public class TestFailed : Exception
	{
		public TestFailed (string message) : base(message)
		{
		}
	}
	
	/// <summary>
	/// Attribute to mark test cases.
	/// </summary>
	public class TestAttribute : Attribute
	{
		public TestAttribute ()
		{
		}
	}

	/// <summary>
	/// Methods to check conditions in tests.
	/// </summary>
	public static class Check
	{
		/// <summary>
		/// Check that a condition is true.
		/// </summary>
		public static void IsTrue (bool condition)
		{
			if (!condition)
				throw new TestFailed ("Expected condition to be true.");
		}
		
		/// <summary>
		/// Check that a condition is false. 
		/// </summary>
		public static void IsFalse (bool condition)
		{
			if (condition)
				throw new TestFailed ("Expected condition to be false.");
		}
		
		/// <summary>
		///  Check that two values are equal.
		/// </summary>
		public static void AreEqual<T> (T x, T y)
		{
			if (!x.Equals (y))
				throw new TestFailed (string.Format ("'{0}' and '{1}' should be equal.", x, y));
		}
		
		/// <summary>
		/// Check that two values are not equal.
		/// </summary>
		public static void AreNotEqual<T> (T x, T y)
		{
			if (x.Equals (y))
				throw new TestFailed (string.Format ("'{0}' and '{1}' should not be equal.", x, y));
		}
		
		public static void IsOfType<T> (object x)
		{
			if (!(x is T))
				throw new TestFailed (string.Format ("'{0}' should be of type '{1}'.", x, typeof(T)));
		}

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

		/// <summary>
		/// Check that an action throws a specified exception.
		/// </summary>
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
				var msg = string.Format ("Expected exception {0} to be thrown, but got {1}", typeof(E).Name,
					caught == null ? "no exception" : caught.GetType ().Name);
				throw new TestFailed (msg);
			}
		}
	}
	
	/// <summary>
	/// Tester class contains methods for running tests.
	/// </summary>
	public static class Tester
	{
		/// <summary>
		/// Runs the tests in fixtures.
		/// </summary>
		public static void RunTests (params object[] fixtures)
		{
			RunTests (fixtures, false);
		}
		
		/// <summary>
		/// Runs the tests in fixtures outputting the duration each test takes.
		/// </summary>
		public static void RunTestsTimed (params object[] fixtures)
		{
			RunTests (fixtures, true);
		}
		
		/// <summary>
		/// Private mehtod to run the fixtures.
		/// </summary>
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
					System.Console.WriteLine ("All tests passed. {0} tests run in {1}.", run, stopWatch.Elapsed);
				else
					System.Console.WriteLine ("All tests passed. {0} tests run.", run);
			}
			Console.ResetColor ();
			GC.Collect ();
		}

		private static bool IsTest (this MethodInfo mi)
		{
			return mi.IsDefined (typeof (TestAttribute), false);
		}

		/// <summary>
		/// Run tests in a single fixture.
		/// </summary>
		private static void TestFixture (object fixture, bool timed, ref int run, ref int failed)
		{
			Console.ForegroundColor = ConsoleColor.Blue;
			Console.WriteLine ("Executing tests for fixture: " + fixture.GetType ().Name);

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

		/// <summary>
		/// Outputs the failure information.
		/// </summary>
		private static void OutputFailure (string test, Exception ex)
		{
			Console.WriteLine ();
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine ("Test '{0}' failed.", test);
			Console.ResetColor ();
			Console.Write ("Reason: ");
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine (ex.Message);
			Console.ResetColor ();
			var st = ex.StackTrace.Split ('\n');
			for (int i = 1; i < st.Length - 2; i++)
				Console.WriteLine (st [i]);
		}
	}
}

