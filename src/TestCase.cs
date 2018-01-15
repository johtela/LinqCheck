namespace LinqCheck
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Diagnostics;
	using ExtensionCord;

	/// <summary>
	/// Result of a single test run. Test can either succeed, fail, or be discarded. When test
	/// fails an TestFailed exception is thrown. Discarded test means that the precondition of
	/// the test is not met.
	/// </summary>
	public enum TestResult	{ Succeeded, Discarded };

	/// <summary>
	/// The property monad wraps a function that tests some property. A property represents
	/// arbitrarily complex expression that describes how the code to be tested should behave.
	/// </summary>
	public delegate Tuple<TestResult, T> TestCase<T> (TestState state);

	/// <summary>
	/// The primitives and combinators dealing with properties.
	/// </summary>
	public static class TestCase
	{
		/// <summary>
		/// Wrap a value in the Property monad.
		/// </summary>
		public static TestCase<T> ToTestCase<T> (this T value)
		{
			return state => Tuple.Create (TestResult.Succeeded, value);
		}

		public static TestCase<T> Fail<T> (this T value)
		{
			return state =>
			{
				throw new TestFailed (string.Format ("Test case '{0}' failed for input {1}",
					state.Label, value)
				);
			};
		}

		public static TestCase<T> Discard<T> (this T value)
		{
			return state => Tuple.Create (TestResult.Discarded, value);
		}

		public static TestCase<T> ForAll<T> (this Gen<T> gen)
		{
			return ForAll (new Arbitrary<T> (gen));
		}

		public static TestCase<T> ForAll<T> (this IArbitrary<T> arbitrary)
		{
			return state => 
			{
				T value;

				if (state.Phase == TestPhase.Generate)
				{
					value = arbitrary.Generate (state.Random, state.Size);
					state.Values.Add (value);
				}
				else
				{
					value = (T)state.Values [state.CurrentValue++];
					if (state.Phase == TestPhase.StartShrink)
						state.ShrunkValues.Add (
							new List<object> (arbitrary.Shrink (value).Append (value).Cast<object> ()));
				}
				return Tuple.Create (TestResult.Succeeded, value);
			};
		}

		public static TestCase<T> ForAll<T> ()
		{
			return ForAll (Arbitrary.Get<T> ());
		}

		public static TestCase<T> Restrict<T> (this TestCase<T> testcase, int size)
		{
			return state =>
			{
				var oldSize = state.Size;
				state.Size = size;
				var res = testcase (state);
				state.Size = oldSize;
				return res;
			};
		}

		public static TestCase<U> Bind<T, U> (this TestCase<T> testcase, Func<T, TestCase<U>> func)
		{
			return state =>
			{
				var res = testcase (state);
				if (res.Item1 == TestResult.Succeeded)
					return func (res.Item2) (state);
				return Tuple.Create (res.Item1, default(U));
			};
		}

		public static TestCase<U> Select<T, U> (this TestCase<T> testcase, Func<T, U> select)
		{
			return testcase.Bind (a => select (a).ToTestCase ());
		}

		public static TestCase<V> SelectMany<T, U, V> (this TestCase<T> testcase,
			Func<T, TestCase<U>> project, Func<T, U, V> select)
		{
			return testcase.Bind (a => project (a).Bind (b => select (a, b).ToTestCase ()));
		}

		public static TestCase<T> Where<T> (this TestCase<T> testcase, Func<T, bool> predicate)
		{
			return testcase.Bind (value => predicate (value) ? value.ToTestCase () : value.Discard ());
		}

		public static TestCase<T> OrderBy<T, U> (this TestCase<T> testcase, Func<T, U> classify)
		{
			return state => 
			{
				var res = testcase (state);
				var cl = classify (res.Item2).ToString ();
                var cnt = 0;
                if (state.Classes.TryGetValue (cl, out cnt))
					state.Classes[cl] = cnt + 1;
                else
					state.Classes.Add (cl, 1);
				return res;
			};
		}

		public static TestCase<T> FailIf<T> (this TestCase<T> testcase, Func<T, bool> predicate)
		{
			return testcase.Bind (value => predicate (value) ? value.ToTestCase () : value.Fail ());
		}

		public static TestCase<T> Label<T> (this TestCase<T> testcase, string label)
		{
			return state =>
			{
				state.Label = label;
				return testcase (state);
			};
		}

        public static TestCase<T> Label<T> (this TestCase<T> testcase, string label, params object[] args)
        {
            return testcase.Label (string.Format (label, args));
        }

        private static bool Test<T> (TestCase<T> testcase, int tries, TestState state)
		{
			try
			{
				while (state.SuccessfulTests + state.DiscardedTests < tries)
				{
					state.ResetValues ();

					switch (testcase (state).Item1)
					{
						case TestResult.Succeeded:
							state.SuccessfulTests++;
							break;
						case TestResult.Discarded:
							state.DiscardedTests++;
							break;
					}
				}
			}
			catch (TestFailed) { return false; }
			return true;
		}

		private static List<object> GenerateValues (List<List<object>> shrunkValues, List<int> indices)
		{
			return new List<object> (shrunkValues.Select ((lst, i) => lst [indices [i]]));
		}

		private static bool NextCandidate (List<List<object>> shrunkValues, List<int> indices)
		{
			for (int i = 0; i < shrunkValues.Count; i++)
			{
				var ind = indices [i] - 1;
				if (ind >= 0)
				{
					indices [i] = ind;
					return true;
				}
				indices [i] = shrunkValues [i].Count - 1;
			}
			return false;
		}

		private static List<object> Optimize<T> (TestCase<T> testcase, List<List<object>> shrunkValues, 
			List<object> values)
		{
			var current = new List<int> (shrunkValues.Select (l => l.Count - 1));
			var best = values;
			var bestWeight = current.Sum ();

			while (NextCandidate (shrunkValues, current))
			{
				values = GenerateValues (shrunkValues, current);
				if (!Test (testcase, 1, new TestState (TestPhase.Shrink, 0, 0, values, shrunkValues)))
				{
					var weight = current.Sum ();
					if (weight <= bestWeight)
					{
						best = values;
						bestWeight = weight;
						Console.Write (".");
					}
				}
			}
			return best;
		}

		public static void Check<T> (this TestCase<T> testcase, Func<T, bool> prop, int tries = 100)
		{
			var seed = DateTime.Now.Millisecond;
			var size = 10;
			var testProp = testcase.FailIf (prop);
			var state = new TestState (TestPhase.Generate, seed, size);

			// Testing phase.
			if (!Test<T> (testProp, tries, state))
			{
				// Shrinking phase.
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Write ("Falsifiable after {0} tests. Shrinking input.", state.SuccessfulTests + 1);
				state = new TestState (TestPhase.StartShrink, seed, size, state.Values,
					new List<List<object>> ());
				Test (testProp, 1, state);
				Debug.Assert (state.Values.Count == state.ShrunkValues.Count);
				var optimized = Optimize (testProp, state.ShrunkValues, state.Values);
				Console.ResetColor ();
				state = new TestState (TestPhase.Shrink, 0, 0, optimized, null);
				// Fail again with optimized input without catching the exception.
				testProp (state);
				throw new TestFailed ("The failed propety was re-evaluated, but the error did not reoccur. "
					+ "This probably means that the property has side effects which supress the error "
					+ "under some conditions and make the test case undeterministic.");
			}
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine ("'{0}' passed {1} tests. Discarded: {2}", 
				state.Label, state.SuccessfulTests, state.DiscardedTests);
			if (state.Classes.Count > 0)
			{
				Console.ForegroundColor = ConsoleColor.DarkGray;
				Console.WriteLine ("Test case distribution:");
				foreach (var cl in state.Classes)
					Console.WriteLine ("{0}: {1:p}", cl.Key, (double)cl.Value / tries);
			}
			Console.ResetColor ();
		}
	}
}
