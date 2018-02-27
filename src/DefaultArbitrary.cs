/*
# Built-In Arbitrary Types

LinqCheck provides the most importantant arbitrary types out-of-the-box. We 
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
			Integral types `int` and `long` use the same helperes to generate 
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

		Characters are selected from the English alphabet adding a few special
		characters. Consequently, no unprintable characters or strings should 
		be produced. If you want to generate characters from the whole ASCII 
		table, or even from the Unicode character set, you can do so by first 
		generating a random integer with appropriate range and using it as 
		a character code.
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
		The shrinking of characters is quite 
		*/
		private static IEnumerable<char> ShrinkChar (char c)
		{
			var candidates = new char[] 
				{ 'a', 'b', 'c', 'A', 'B', 'C', '1', '2', '3', char.ToLower (c),
				  ' ', '\n' };

			return candidates.Where (x => x.SimplerThan (c));
		}

		private static bool SimplerThan (this char x, char y)
		{
			Func<Func<char, bool>, bool> simpler = fun => fun (x) && !fun (y);

			return simpler (char.IsLower) || simpler (char.IsUpper) || simpler (char.IsDigit) ||
				simpler (c => c == ' ') || simpler (char.IsWhiteSpace) || x < y;
		}

		private static IEnumerable<int> ShrinkInteger (int x)
		{
			yield return 0;
			var absx = Math.Abs (x);
			for (var i = absx / 2; absx - i < absx; i = i / 2)
				yield return absx - i;
			if (x < 0) yield return -x;
		}

		private static IEnumerable<double> ShrinkDouble (double x)
		{
			if (x < 0) yield return -x;
			yield return Math.Floor (x);
		}

		public static IEnumerable<IEnumerable<T>> ShrinkEnumerable<T> (this IEnumerable<T> e)
		{
			return RemoveUntil (e).SelectMany (Fun.Identity).Concat (ShrinkOne (e)).Prepend (new T[0]);
		}

		private static IEnumerable<IEnumerable<IEnumerable<T>>> RemoveUntil<T> (IEnumerable<T> e)
		{
			var len = e.Count ();
			for (var k = len - 1; k > 0; k = k / 2)
				yield return RemoveK (e, k, len);
		}

		private static IEnumerable<IEnumerable<T>> RemoveK<T> (IEnumerable<T> e, int k, int len)
		{
			if (k > len) return new IEnumerable<T>[0];
			var xs1 = e.Take (k);
			var xs2 = e.Skip (k);
			return (from r in RemoveK (xs2, k, len - k)
					select xs1.Concat (r)).Append (xs2);
		}

		private static IEnumerable<IEnumerable<T>> ShrinkOne<T> (IEnumerable<T> e)
		{
			if (e.None ()) return new IEnumerable<T>[0];
			var first = e.First ();
			var rest = e.Skip (1);
			return (from x in Arbitrary.Get<T> ().Shrink (first)
					select rest.Append(x)).Concat (
					from xs in ShrinkOne (e.Skip (1))
					select xs.Append (first));
		}

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

		private class Array<T> : ArbitraryBase<T[]>
		{
			public override Gen<T[]> Generate
			{
				get { return Arbitrary.Gen<T> ().ArrayOf (); }
			}

			public override IEnumerable<T[]> Shrink (T[] value)
			{
				return ShrinkEnumerable (value).Select (i => i.ToArray ());
			}
		}

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
