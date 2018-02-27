namespace LinqCheck
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Linq.Expressions;
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
	public delegate Tuple<TestResult, T> Prop<T> (TestState state);

	/// <summary>
	/// The primitives and combinators dealing with properties.
	/// </summary>
	public static class Prop
	{
		/// <summary>
		/// Wrap a value in the Property monad.
		/// </summary>
		public static Prop<T> ToProp<T> (this T value)
		{
			return state => Tuple.Create (TestResult.Succeeded, value);
		}

		public static Prop<T> Fail<T> (this T value)
		{
			return state => throw new PropertyFailed<T> (state.Label, value);
		}

		public static Prop<T> Discard<T> (this T value)
		{
			return state => Tuple.Create (TestResult.Discarded, value);
		}

		public static Prop<T> ForAll<T> (this IArbitrary<T> arbitrary)
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

		public static Prop<T> ForAll<T> ()
		{
			return ForAll (Arbitrary.Get<T> ());
		}

		public static Prop<T> Any<T> (this Gen<T> gen)
		{
			return state => Tuple.Create (TestResult.Succeeded, gen (new Random (state.Seed), state.Size));
		}

		public static Prop<T> Restrict<T> (this Prop<T> prop, int size)
		{
			return state =>
			{
				var oldSize = state.Size;
				state.Size = size;
				var res = prop (state);
				state.Size = oldSize;
				return res;
			};
		}

		public static Prop<U> Bind<T, U> (this Prop<T> prop, Func<T, Prop<U>> func)
		{
			return state =>
			{
				var res = prop (state);
				if (res.Item1 == TestResult.Succeeded)
					return func (res.Item2) (state);
				return Tuple.Create (res.Item1, default(U));
			};
		}

		public static Prop<U> Select<T, U> (this Prop<T> prop, Func<T, U> select)
		{
			return prop.Bind (a => select (a).ToProp ());
		}

		public static Prop<V> SelectMany<T, U, V> (this Prop<T> prop,
			Func<T, Prop<U>> project, Func<T, U, V> select)
		{
			return prop.Bind (a => project (a).Bind (b => select (a, b).ToProp ()));
		}

		public static Prop<T> Where<T> (this Prop<T> prop, Func<T, bool> predicate)
		{
			return prop.Bind (value => predicate (value) ? value.ToProp () : value.Discard ());
		}

		public static Prop<T> OrderBy<T, U> (this Prop<T> prop, Func<T, U> classify)
		{
			return state => 
			{
				var res = prop (state);
				var cl = classify (res.Item2).ToString ();
				if (state.Classes.TryGetValue (cl, out int cnt))
					state.Classes[cl] = cnt + 1;
				else
					state.Classes.Add (cl, 1);
				return res;
			};
		}

		public static Prop<T> FailIf<T> (this Prop<T> prop, Func<T, bool> predicate)
		{
			return prop.Bind (value => predicate (value) ? value.ToProp () : value.Fail ());
		}

        private static bool Test<T> (Prop<T> prop, int tries, TestState state)
		{
			try
			{
				while (state.SuccessfulTests + state.DiscardedTests < tries)
				{
					state.ResetValues ();

					switch (prop (state).Item1)
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
			catch (PropertyFailed<T>) { return false; }
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
				var ind = indices [i] + 1;
				if (ind < shrunkValues[i].Count)
				{
					indices [i] = ind;
					return true;
				}
			}
			return false;
		}

		private static List<object> Optimize<T> (Prop<T> prop, TestState state)
		{
			var current = new List<int> (
				Enumerable.Repeat (0, state.ShrunkValues.Count));
			var values = state.Values;

			while (NextCandidate (state.ShrunkValues, current))
			{
				values = GenerateValues (state.ShrunkValues, current);
				try
				{
					if (!Test (prop, 1, new TestState (TestPhase.Shrink,
						state.Seed, state.Size, state.Label, values,
						state.ShrunkValues)))
						break;
				}
				catch (Exception) { }
			}
			return values;
		}

		public static Prop<T> Check<T> (this Prop<T> prop, Expression<Func<T, bool>> condition, 
			int tries = 100, string label = null)
		{
			var seed = DateTime.Now.Millisecond;
			var size = 10;
			label = label ?? condition.Body.ToString ();
			var testProp = prop.FailIf (condition.Compile ());
			var state = new TestState (TestPhase.Generate, seed, size, label);

			// Testing phase.
			if (!Test<T> (testProp, tries, state))
			{
				// Shrinking phase.
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Write ("Falsifiable after {0} tests. Shrinking input.", state.SuccessfulTests + 1);
				state = new TestState (TestPhase.StartShrink, seed, size, label,
					state.Values, new List<List<object>> ());
				Test (testProp, 1, state);
				Debug.Assert (state.Values.Count == state.ShrunkValues.Count);
				var optimized = Optimize (testProp, state);
				Console.ResetColor ();
				state = new TestState (TestPhase.Shrink, seed, size, label, optimized, null);
				// Fail again with optimized input without catching the exception.
				testProp (state);
				throw new TestFailed (
					"The failed property was re-evaluated, but the error did " +
					" not reoccur. This probably means that the property has " + 
					" side effects which supress the error under some conditions " +
					"and make the test case undeterministic.");
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
			return prop;
		}
	}
}
