/*
# Built-In Arbitrary Types

LinqCheck provides the most important arbitrary types out-of-the-box. We 
define the built-in implementations for the IArbitrary interface in the 
DefaultArbitrary static class. 
*/
namespace LinqCheck
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using ExtensionCord;

	public static class DefaultArbitrary
	{
		/*
		## Registering Default Arbitrary Types
		All of the default arbitrary types are registered automatically in the 
		static constructor. Most of these types rely on the helper methods, 
		which are introduced below. We will discuss the details of each type 
		in context of the helper methods.
		*/
		internal static void Register ()
		{
			/*
			### Character Type
			Characters are randomly selected from a predefined set. This 
			produces simpler and more readable characters than randomly 
			generating ASCII codes.
			*/
			Arbitrary.Register (new Arbitrary<char> (
				Gen.ChooseFrom (CharCandidates ().ToArray ()), 
				ShrinkChar));
			/*
			### Integral Types
			Integral types `int` and `long` use the same helpers to generate 
			and shrink the values. The maximum number generated depends on the
			size parameter used by the generators.
			*/
			Arbitrary.Register (new Arbitrary<int> (
				Gen.ChooseInt (),
				x => ShrinkInteger (x).Distinct ()));

			Arbitrary.Register (new Arbitrary<long> (
				Gen.ChooseInt ().ToLong (), 
				x => ShrinkInteger ((int)x).Distinct ().Select (i => (long)i)));
			/*
			### Floating Point Types
			`float` and `double` types are generated and shrunk with the same
			methods. The results are casted to lower precision when necessary.
			*/
			Arbitrary.Register (new Arbitrary<float> (
				Gen.ChooseDouble ().ToFloat (),
				x => ShrinkDouble (x).Select (d => (float)d)));

			Arbitrary.Register (new Arbitrary<double> (
				Gen.ChooseDouble (),
				ShrinkDouble));
			/*
			### Strings
			Strings are arrays of characters, so they can be composed from 
			arbitrary characters with appropriate combinators.
			*/
			Arbitrary.Register (new Arbitrary<string> (
				from a in Arbitrary.Gen<char> ().ArrayOf ()
				select new string (a),
				x => ShrinkEnumerable (x).Select (cs => new string (cs.ToArray ()))));
			/*
			### Collection Types
			The arbitrary implementations for collections are generic. 
			Therefore, they work with any item type. Their implementation is 
			discussed below.
			*/
			Arbitrary.Register (typeof (Enumerable<>));
			Arbitrary.Register (typeof (Array<>));
            Arbitrary.Register (typeof (AList<>));
		}
		/*
		## Helper Methods for Primitive Types
		The following methods are used in producing and shrinking the primitive
		types. First, let's examine how we generate and shrink characters.

		### Generating Characters
		Characters are selected from the printable part of the ASCII table. If 
		you want to generate characters from the whole ASCII table, or even 
		from the Unicode character set, you can do so by first generating a 
		random integer with appropriate range and using it as a character code.
		*/
		private static IEnumerable<char> CharCandidates ()
		{
			for (char c = 'A'; c <= '~'; c++)
				yield return c;
			for (char c = ' '; c < 'A'; c++)
				yield return c;
			yield return '\t';
			yield return '\n';
		}
		/*
		### Shrinking Characters
		The rules of character shrinking are a bit arbitrary but agreeable. We 
		prefer to have lowercase letters over uppercase ones, and letters over 
		numbers or whitespace.
		*/
		private static IEnumerable<char> ShrinkChar (char c)
		{
			var candidates = new char[] 
				{ 'a', 'b', 'A', 'B', '1', '2', char.ToLower (c), ' ' };

			return candidates.Where (x => x.SimplerThan (c));
		}

		private static bool SimplerThan (this char x, char y)
		{
			bool simpler (Func<char, bool> fun) => fun (x) && !fun (y);

			return simpler (char.IsLower) || simpler (char.IsUpper) || 
				simpler (char.IsDigit) || simpler (c => c == ' ') || 
				simpler (char.IsWhiteSpace) || x < y;
		}
		/*
		### Shrinking Integers
		When shrinking integers we prefer to have values close to zero and 
		positive numbers before negative ones.
		*/
		private static IEnumerable<int> ShrinkInteger (int x)
		{
			yield return 0;
			if (x < 0) yield return -x;
			for (var i = x / 2; Math.Abs (x - i) < Math.Abs (x); i = i / 2)
				yield return x - i;
		}
		/*
		### Shrinking Floating Point Numbers
		We simplify floating point numbers by trying first zero and then the 
		value truncated down or up. Finally we try to make the number positive,
		if it is negative.
		*/
		private static IEnumerable<double> ShrinkDouble (double x)
		{
			yield return 0.0;
			yield return Math.Floor (x);
			yield return Math.Ceiling (x);
			if (x < 0.0) yield return -x;
		}
		/*
		### Shrinking Enumerables
		The IEnumerable type has the most involved shrinking procedure. We 
		first try to remove as many items from the enumerable as we can, and 
		then we	shrink each individual element at a time. The simplest case, 
		and the first alternative returned, is the empty enumerable.
		*/
		public static IEnumerable<IEnumerable<T>> ShrinkEnumerable<T> (
			this IEnumerable<T> e)
		{
			return Shorten (e)
				.SelectMany (Fun.Identity)
				.Concat (ShrinkOne (e))
				.Prepend (new T[0]);
		}
		/*
		The shorter versions are produced by removing decreasing number of 
		elements from the enumerable. At first iteration we remove all but 
		one element. After each round we decrement the variable _k_, which 
		contains the number of elements removed, by one. Eventually we remove 
		only one element. The elements of the shorter enumerables are 
		simplified individually by the `ShrinkOne` method.
		*/
		private static IEnumerable<IEnumerable<IEnumerable<T>>> Shorten<T> (
			IEnumerable<T> e)
		{
			var len = e.Count ();
			for (var k = len - 1; k > 0; k--)
			{
				var shrunk = RemoveK (e, k, len);
				foreach (var s in shrunk)
					yield return ShrinkOne (s);
				yield return shrunk;
			}
		}
		/*
		The `RemoveK` method removes _k_ elements from different positions and 
		compiles a set of enumerables with the same length that have different 
		elements removed.
		*/
		private static IEnumerable<IEnumerable<T>> RemoveK<T> (IEnumerable<T> e, 
			int k, int len)
		{
			if (k > len) return Enumerable.Empty<IEnumerable<T>> ();
			var xs1 = e.Take (k);
			var xs2 = e.Skip (k);
			return (from r in RemoveK (xs2, k, len - k)
					select xs1.Concat (r))
				.Append (xs2);
		}
		/*
		The `ShrinkOne` method recursively shrinks each element at a time.
		*/
		private static IEnumerable<IEnumerable<T>> ShrinkOne<T> (IEnumerable<T> e)
		{
			if (e.None ())
				return Enumerable.Empty<IEnumerable<T>> ();
			var first = e.First ();
			var rest = e.Skip (1);
			return (from x in Arbitrary.Shrink (first)
					select rest.Prepend (x))
					.Concat (
					from xs in ShrinkOne (e.Skip (1))
					select xs.Prepend (first));
		}
		/*
		## Arbitrary Collections
		With the ability to generate and shrink enumerables we can implement 
		the IArbitrary interface for collection types. As the collections are
		generic, their arbitrary counterparts need to be also parameterized by
		the element type. Therefore we write separate arbitrary classes for 
		collections. All of the classes inherit from ArbitraryBase.

		### Arbitrary Enumerable
		The implementation of arbitrary enumerable is trivial using the 
		generator and shrinker we already defined.
		*/
		private class Enumerable<T> : ArbitraryBase<IEnumerable<T>>
		{
			public override Gen<IEnumerable<T>> Generate 
			{
				get { return Arbitrary.Gen<T> ().EnumerableOf (); }
			}

			public override IEnumerable<IEnumerable<T>> Shrink (IEnumerable<T> value)
			{
				return ShrinkEnumerable (value);
			}
		}
		/*
		### Arbitrary Array
		An arbitrary array is composed of arbitrary enumerable.
		*/
		private class Array<T> : ArbitraryBase<T[]>
		{
			public override Gen<T[]> Generate
			{
				get { return Arbitrary.Gen<T> ().ArrayOf (); }
			}

			public override IEnumerable<T[]> Shrink (T[] value)
			{
				return ShrinkEnumerable (value).Select (Enumerable.ToArray);
			}
		}
		/*
		### Arbitrary List
		Building an arbitrary list is equally simple using the existing 
		combinators.
		*/
        private class AList<T> : ArbitraryBase<List<T>>
        {
            public override Gen<List<T>> Generate
            {
                get
                {
                    return from e in Arbitrary.Gen<T> ().EnumerableOf ()
                           select new List<T> (e);
                }
            }

            public override IEnumerable<List<T>> Shrink (List<T> value)
            {
                return from e in ShrinkEnumerable (value)
                        select new List<T> (e);
            }
        }
	}
}
