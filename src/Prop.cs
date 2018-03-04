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

	We define the monadic operations in a static class named Prop.
	*/
	public static class Prop
	{
		/*
		## Transforming a Value to Property
		The most basic monadic operation _return_, is named as `ToProp`. It 
		takes a value and wraps it in the Prop delegate. The method cannot
		fail, so it always returns `Succeeded`.
		*/
		public static Prop<T> ToProp<T> (this T value)
		{
			return state => Tuple.Create (TestResult.Succeeded, value);
		}
		/*
		A reverse operation of `ToProp` is `Fail`, which throws an exception. 
		It can be used to explicitly fail a property.
		*/
		public static Prop<T> Fail<T> (this T value)
		{
			return state => throw new PropertyFailed<T> (state.Label, value);
		}
		/*
		The third way to transform a value into a property is to discard it.
		*/
		public static Prop<T> Discard<T> (this T value)
		{
			return state => Tuple.Create (TestResult.Discarded, value);
		}
		/*
		## Implementing _bind_
		Now we have three implementations for the _return_ operation, so next
		we need to implment the monadic _bind_. It calls the first property and 
		checks that it succeeds. If it does, _bind_ passes the produced state 
		to the next property and returns it result. 
		
		The implementation is a slightly complicated version of
		[maybe monad](https://en.wikibooks.org/wiki/Haskell/Understanding_monads/Maybe),
		one of the simplest examples in the world of monads.
		*/
		public static Prop<U> Bind<T, U> (this Prop<T> prop, Func<T, Prop<U>> func)
		{
			return state =>
			{
				var res = prop (state);
				if (res.Item1 == TestResult.Succeeded)
					return func (res.Item2) (state);
				return Tuple.Create (TestResult.Discarded, default (U));
			};
		}
		/*
		## Generating an Arbitrary Value
		Usually we begin a property by generating an arbitrary value. The 
		`ForAll` method takes an instance of the IArbitrary interface and
		returns a value of type `T`. How the value is computed depends on the
		phase we are in. 
		
		If we are in the generation phase, then we use the generator embedded 
		in the IArbitrary interface to generate a new random value. If we are 
		in the shrinking phase, however, then we use the value stored in the 
		TestState object. 
		
		If we are just starting to shrink the values, then we call the `Shrink` 
		method in the arbitrary object to produce the alternative values. Note
		that we automatically append the original (failed) value to the 
		enumerable, so IArbitrary implementations do not have to do that 
		themselves.
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
		/*
		The other version of the ForAll method can be called without specifying 
		the IArbitrary instance. The method uses the default arbitrary 
		implementation that is registered for the given type.
		*/
		public static Prop<T> ForAll<T> ()
		{
			return ForAll (Arbitrary.Get<T> ());
		}
		/*
		## Generating a Dependent Random Value
		Sometimes we need to generate a random value which depends on an 
		already generated variable. For example, if we generate a random list 
		and want to choose an item inside the list in random. In that case we 
		cannot shrink the item, since the shrunk version will probably not be
		an item in the list.
		
		What we need is a method which, for a given TestState, always generates 
		the same (pseudorandom) value. The `Any` method does just that. It 
		takes a generator and calls it with an instance of Random class, which 
		is initialized with the seed stored in the test state. Thus it produces 
		the same value in the generation and shrinking phase. This ensures that 
		the shrinking works deterministically, and that the failing test case 
		is not lost.
		*/
		public static Prop<T> Any<T> (this Gen<T> gen)
		{
			return state => Tuple.Create (TestResult.Succeeded, gen (
				new Random (state.Seed), state.Size));
		}
		/*
		## Adding LINQ Support
		Implementations of LINQ's `Select` and `SelectMany` methods are 
		essentially identical to the ones in the [Gen](Gen.html) class. As 
		mentioned in the chapter describing generators, when these methods are 
		implemented in terms of _bind_ operation, they are always the same.
		*/
		public static Prop<U> Select<T, U> (this Prop<T> prop, Func<T, U> select)
		{
			return prop.Bind (a => select (a).ToProp ());
		}

		public static Prop<V> SelectMany<T, U, V> (this Prop<T> prop,
			Func<T, Prop<U>> project, Func<T, U, V> select)
		{
			return prop.Bind (a => project (a).Bind (b => select (a, b).ToProp ()));
		}
		/*
		## Filtering Test Cases
		LINQ's `where` clause is used to discard test cases. If the predicate 
		returns false, we stop the evaluation and return back to beginning to
		try the next test case.
		*/
		public static Prop<T> Where<T> (this Prop<T> prop, Func<T, bool> predicate)
		{
			return prop.Bind (value => predicate (value) ? 
				value.ToProp () : 
				value.Discard ());
		}
		/*
		The following combinator is mostly useful as an internal helper method. 
		The combinator fails the property, if the specified predicate does not 
		hold.
		*/
		public static Prop<T> FailIf<T> (this Prop<T> prop, Func<T, bool> predicate)
		{
			return prop.Bind (value => predicate (value) ?
				value.ToProp () :
				value.Fail ());
		}
		/*
		## Classifying Test Cases
		We exploit LINQ's `orderby` clause to give the users a way to classify 
		test cases. Although the `group by` clause would have had more relevant 
		name for this operation, its signature does not fit our purposes. So, 
		we use a somewhat misleading syntax, but otherwise the signature of 
		`OrderBy` matches our requirements perfectly.

		The `classify` parameter specifies a function that maps a test case to
		a value of type `U`. We don't care what the type `U` is, since we 
		always convert the result of the function to string. We put the result 
		to the dictionary stored in the test state, and accumulate the number 
		of cases there.
		*/
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
		/*
		## Restricting and Extending Range of Generated Values
		If you want to temporarily narrow or widen the range of values that the
		generators return, you can use the `Restrict` combinator. It stores the
		old value of the size parameter passed to generators, and sets a new 
		value to the test state. After the inner property has been evaluated,
		it restores the old value.
		*/
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
		/*
		## Private Helpers
		The following methods are private methods, not intended to be used in
		user code. They are used by the `Check` combinator, which is described
		at the end of this chapter.

		### Executing a Property
		The `Test` method runs the specified property multiple times (by 
		default 100 times) and returns true, if all the test iterations pass.
		In case any round throws a PropertyFailed exception, the execution
		immediately halts, and we return false. The method also counts the 
		number of passed and discarded tests and maintains these numbers in 
		the test state.
		*/
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
		/*
		### Enumerating Shrunk Test Cases
		When we have found a failed test case (`Test` method returned false),
		we shrink all the generated arbitrary values. This happens in the 
		`StartShrinking` phase. After that we move on the actual `Shrink` phase
		where we try to find the simplest combination of variables which fails
		the propery.

		The arbitrary implementations for each type should return the shrunk
		values in the order of increasing complexity. We can stop right	away 
		when we encounter the first value that causes our property to fail. 
		The problem is that we usually have more than one arbitrary value in a 
		property. So, we actually have an exponential number of combinations to 
		try, if we want to test all the solutions.

		To make the shrinking process practical, we enumerate the solution
		candidates in a simplified manner. We basically iterate through each
		variable at a time, fixing the others. This means that our solution is
		not necessarily the simplest one. How close to the best (simplest)
		solution it is, depends on how much dependencies there are among the
		variables in the property.

		The `Candidates` method returns an enumerable of shrunk variable 
		assignments starting from the simplest and ending at the original, 
		failed test case. Since we generate the shrunk values lazily, no 
		unnecessary work is done in advance.

		The implementation maintains a list of IEnumerator objects, which
		point to the current values in the enumerables returning the shrunk 
		variables. We advance one enumerator at a time until we exhaust it, and 
		then move on to the next variable.
		*/
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
		/*
		### Finding the Simplest Failing Test Case
		The `Optimize` method enumerates the candidates in order and calls
		`Test` to check whether they fail. The first candidate that fails
		is returned to the caller.

		Note that we also catch all exceptions in the loop since shrunk values 
		might cause properties to fail in unexpected ways. We ignore those
		errors in the context of shrinking.
		*/
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
		/*
		## Checking Properties
		Finally, we provide a way to define the conditions that should hold for
		all the generated test cases, i.e. the actual properties. The `Check` 
		extension method takes a property, a condition to be checked, number of
		test cases generated, and an optional label. Since this method captures
		the actual process of property based testing, let's examine it 
		step-by-step.
		*/
		public static Prop<T> Check<T> (this Prop<T> prop, 
			Expression<Func<T, bool>> condition, int tries = 100, string label = null)
		{
			/*
			First we initialize the random seed, and the size (range) passed to
			generators.
			*/
			var seed = DateTime.Now.Millisecond;
			var size = 10;
			/*
			If no label is specified, we use the string representation of the 
			condition expression as a label. We utilize the 
			[`Expression<T>`](https://msdn.microsoft.com/en-us/library/bb335710(v=vs.110).aspx)
			class and C# compiler's feature that provides the 
			[_abstract syntax tree_](https://en.wikipedia.org/wiki/Abstract_syntax_tree) 
			for a lambda expression.
			*/
			label = label ?? condition.Body.ToString ();
			/*
			Next we compile the expression and wrap it in the `FailIf` 
			combinator. Now we have a property which will fail, if the 
			specified condition is not true.
			*/
			var testProp = prop.FailIf (condition.Compile ());
			/*
			### Generation Phase
			We initialize the test state, and begin the `Generate` phase by 
			calling the `Test` helper method.
			*/
			var state = new TestState (TestPhase.Generate, seed, size, label);

			if (!Test<T> (testProp, tries, state))
			{
				/*
				### Shrinking Phase
				If `Test` returns false, we found a failing test case. That 
				test case is stored in the test state. We report the failure to
				the user, and move on the the `StartShrink` phase. Again we call
				`Test`, but this time it shrinks the values instead of 
				generating new ones.
				*/
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Write ("Falsifiable after {0} tests. Shrinking input.", 
					state.SuccessfulTests + 1);
				state = new TestState (TestPhase.StartShrink, seed, size, label,
					state.Values, new List<IEnumerable<object>> ());
				Test (testProp, 1, state);
				/*
				We make sure that the number of shrunk is correct. This is done
				merely to verify that there are no issues with the shrinking
				implementations. Then we move on the the `Shrink` phase by 
				calling `Optimize`, which starts going through the shrunk test
				cases.
				*/
				Debug.Assert (state.Values.Count == state.ShrunkValues.Count);
				var optimized = Optimize (testProp, state);
				/*
				Finally we fail one more time and let the exception escalate to
				the user code. Now we have the shrunk test case, and debugger
				will stop at the expression that caused the property to fail.
				If we are not in debugging mode, the exception is probably 
				caught by the unit testing framework.
				*/
				Console.ResetColor ();
				state = new TestState (TestPhase.Shrink, seed, size, label, 
					optimized, null);
				testProp (state);
				/*
				If we end up here, something went wrong, because the calling 
				the property did not trigger the exception. This is usually a 
				symptom of an undeterinistic property. Probably the property 
				has some side-effect which hides the failure depending on 
				external state. The only thing we can do, is to report this to 
				the user as an exception, and try to hint at what might be 
				wrong.
				*/
				throw new TestFailed (
					"The failed property was re-evaluated, but the error did " +
					" not reoccur. This probably means that the property has " + 
					" side effects which supress the error under some conditions " +
					"and make the test case undeterministic.");
			}
			/*
			If all generated test cases passed, we end up here. We report the 
			successful property and number of passed vs. discarded cases to the
			user.
			*/
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine ("'{0}' passed {1} tests. Discarded: {2}", 
				state.Label, state.SuccessfulTests, state.DiscardedTests);
			/*
			If `OrderBy` combinator was used in the property, we have some data
			in the `Classes` dictionary. We report the test case distribution to
			the user in that case.
			*/
			if (state.Classes.Count > 0)
			{
				Console.ForegroundColor = ConsoleColor.DarkGray;
				Console.WriteLine ("Test case distribution:");
				foreach (var cl in state.Classes)
					Console.WriteLine ("{0}: {1:p}", cl.Key, (double)cl.Value / tries);
			}
			Console.ResetColor ();
			/*
			Finally we return the original property, so that it can be chained 
			to additional Check calls.
			*/
			return prop;
		}
	}
}
/*
## Concluding Remarks
We covered all the facilities required for defining and testing properties; all
the machinery needed for performing property based testing is now introduced. 
The last thing to be presented is the built-in test runner, which is discussed 
in the last chapter.
*/