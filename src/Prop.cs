/*
# Properties

Finally we can present the properties themselves. We define properties as 
another incarnation of monads. They are generic functions that take the test 
state as an argument and return a value of the generic type and information 
about whether the execution was succesful or not.

A property is essentially an abstract concept, which can be implemented in 
various ways, and composed using combinators. Monadic operators provide us the 
tools that allow easily extending the concept in whatever direction we choose.
*/
namespace LinqCheck
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Diagnostics;
	using ExtensionCord;
	/*
	## Test Result
	If a property returns a value without throwing an exception, it has either 
	succeeded or discarded the test case (when a specified precondition was 
	not met). The `TestResult` enumeration specifies which case is in question. 
	*/
	public enum TestResult	{ Succeeded, Discarded };
	/*
	When a property fails, it throws an exception. The exception contains the
	necessary information about the reason of the failure.

	## The Prop Delegate
	Properties have the type `Prop<T>`, which is declared below. It is a 
	monadic	delegate type that represents an arbitrarily complex expression. 
	The expression is composed of various parts that generate random data, 
	constrain it, compute derived variables, and classify test cases.

	As with all monads we begin with simple constructs and build more complex
	ones using the combinators. The recommended way of writing a property
	expression is to use LINQ syntax, which makes the expresion easier to read
	and write.
	*/
	public delegate Tuple<TestResult, T> Prop<T> (TestState state);
	/*
	A property returns a pair of values. The first one tells if the property is
	passed or discarded, the second one contains the actual value that property
	produced. The state of the test is passed along in the parameter. Monadic
	operations hide the state from the user. So in practice, the user does not 
	have to care about the state at all.

	We define the monadic operations in a static class named `Prop`.
	*/
	public static class Prop
	{
		/*
		## Transforming a Value to Property
		The most basic monadic operation _return_, is named as `ToProp`. It 
		takes a value and wraps it to the Prop delegate. The method cannot
		fail, so it always returns `Succeeded`.
		*/
		public static Prop<T> ToProp<T> (this T value)
		{
			return state => Tuple.Create (TestResult.Succeeded, value);
		}
		/*
		A reverse operation of `ToProp` is `Fail`, which throws a 
		`PropertyFailed<T>` exception. It can be used to explicitly fail a 
		property.
		*/
		public static Prop<T> Fail<T> (this T value)
		{
			return state => throw new PropertyFailed<T> (state.Label, value);
		}
		/*
		The third way to transform a value to property is to discard it.
		*/
		public static Prop<T> Discard<T> (this T value)
		{
			return state => Tuple.Create (TestResult.Discarded, value);
		}
		/*
		## Generating an Arbitrary Value
		*/
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
							arbitrary.Shrink (value).Append (value).Cast<object> ());
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

		private static IEnumerable<List<object>> Candidates (
			List<IEnumerable<object>> shrunkValues)
		{
			var current = shrunkValues.Select (e =>
				{
					var res = e.GetEnumerator ();
					res.MoveNext ();
					return res;
				})
				.ToList ();
			List<object> GetCurrentValues () => 
				current.Select (e => e.Current).ToList ();
			yield return GetCurrentValues ();

			for (int i = 0; i < current.Count; i++)
				while (current[i].MoveNext ())
					yield return GetCurrentValues ();
		}

		private static List<object> Optimize<T> (Prop<T> prop, 
			TestState state)
		{
			foreach (var values in Candidates (state.ShrunkValues))
			{
				try
				{
					if (Test (prop, 1, new TestState (TestPhase.Shrink,
						state.Seed, state.Size, state.Label, values,
						state.ShrunkValues)))
						Console.Write (".");
					else
						return values;
				}
				catch (Exception) { }
			}
			return state.Values;
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
					state.Values, new List<IEnumerable<object>> ());
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
