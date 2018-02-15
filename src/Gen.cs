/*
# Generators and Monads

Most programming languages have a random number generator of some kind. The 
Random class in the System namespace provides the default implementation for
.NET. It can produce random integers and floating point numbers. That is all
well and good, but we need to generate many more types than just numbers. The 
question is: how can we generalize the Random class to provide values of any 
type?

The answer is that we need to turn the random value generator into an abstract 
concept, and define it as a function with multiple implementations. The function 
gets a random number generator and transforms its output to a specific type `T`. 
We define this abstract function as a delegate and call it `Gen<T>`.
*/
namespace LinqCheck
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using ExtensionCord;

	public delegate T Gen<T> (Random rnd, int size);
	/*
	The delegate takes as arguments an instance of the Random class and size, 
	which represents the range of the returned value. The bigger the size the 
	wider range of values should be returned. For integers, the size parameter
	limits the maximum number that the generator can return. For strings, it 
	specifies the maximum length. The interpretation of the argument is thus
	context-sensitive.

	We could now provide an implementation of `Gen<T>` for every type `T` that 
	we would like to generate, but that would result in a lot of boilerplate 
	code. Instead of taking that route, let's take our abstraction one step 
	further and make `Gen<T>` an instance of a
	[_monad_](https://en.wikipedia.org/wiki/Monad_(functional_programming)).

	## Monads
	The internet is littered with articles and blog posts explaining monads 
	through [category theory](https://en.wikipedia.org/wiki/Category_theory) or
	using some (obscure) analogy. Leaving theoretical and pedagogical aspects 
	aside, we will just show a real-world example of a monad implemented in C#. 
	Even if you are unfamiliar with the concept, hopefully you can grasp the 
	principle by following the code.

	To build a monad you first of all need a generic type. Monad defines a way 
	to _compose_ instances of generic types together by providing two simple 
	operations: _return_ and _bind_. Assuming our generic type is $M \langle T \rangle$, 
	these operations have the following signatures (we are using the 
	[_curried_](https://en.wikipedia.org/wiki/Currying) notation here):
	$$
	\begin{align}
	return & : T \to M \langle T \rangle \\
	bind & : M \langle T \rangle \to (T \to M \langle U \rangle) \to M \langle U \rangle
	\end{align}
	$$
	All monads share these two operations. They differ, however, in how the 
	operations are implemented. There are also [laws](https://wiki.haskell.org/Monad_laws) 
	which all monads must obey, but we can ignore them for now.
	
	To make things more concrete, let's implement the two operators for our 
	`Gen<T>` delegate. We put the operations into a static class called Gen.
	*/
	public static class Gen
	{
		/*
		### Implementing _return_
		The signature of the _return_ operation is:
		$$
		T \to M \langle T \rangle
		$$
		The operation takes a value of type $T$ and returns it wrapped in the 
		monad type $M \langle T \rangle$. Since _return_ is a reserved keyword 
		in C# it is bad practice to define a method with that name. Instead, 
		we name it `ToGen` to denote that the method transforms a value into
		the Gen monad. It is a generic, static, extension method whose
		signature in C# syntax looks like this:
		*/
		public static Gen<T> ToGen<T> (this T value)
		{
			/*
			To implement the method, we can use a functional programming 
			technique commonly called as _following the types_. It entails 
			realizing that we are implementing a pure method, so we can only 
			return a value which we can construct from our parameters. 
			
			The only parameter we get is a value of type `T`. We have to return 
			a value of type `Gen<T>`, which is an alias for a delegate 
			`(Random, int) => T`. There is practically only one way to implement 
			that delegate given the parameters we have at hand. 
			*/
			return (rnd, size) => value;
		}
		/*
		When we follow the types the implementation becomes obvious. 
		
		### Implementing _bind_
		The signature of _bind_ is somewhat more complicated:
		$$
		M \langle T \rangle \to (T \to M \langle U \rangle) \to M \langle U \rangle
		$$
		but it can be mapped to the C# syntax in a straightforward fashion. 
		Again, we define the operation as static, generic, extension method.
		*/
		public static Gen<U> Bind<T, U> (this Gen<T> gen, Func<T, Gen<U>> func)
		{
			/*
			Let's again follow the types to implement the method. We need to 
			return a value of type `Gen<U>` which corresponds to delegate 
			`(Random, int) => U`.
			*/
			return (rnd, size) =>
			{
				/*
				How should we implement this lambda expression? Well, we have 
				been given two parameters: a generator of type `Gen<T>` and a 
				function which maps `T` to `Gen<U>`. To get a value of type `T`
				we need to call `gen`. Its arguments `(Random, int)` we get 
				from the parameters of the lambda expression.
				*/
				var a = gen (rnd, size);
				/*
				Now we have a value of type `T` in variable `a`. The only thing
				we can do with that variable is to feed it to `func` to get a 
				generator of type `Gen<U>`. To get the expected result type `U`
				of our lambda expression, we call this generator with the same 
				`rnd` and `size` parameters as before.
				*/
				return func (a) (rnd, size);
			};
		}
		/*
		Note that the implementation was almost mechanical. Think for a while,
		if you could have implemented the method differently. You will notice
		that if you try to change the implementation in any way, the code will 
		not compile anymore; the types do not match.

		This is the reason, why we usually do not have to concern ourselves
		with the monad laws. If your monad type is a pure function, it is 
		impossible to define the _return_ and _bind_ operation in a way that 
		violates the monad laws. There is literally only one way to implement 
		them. Be warned that this realization can easily blow your mind...

		### What Have We Achieved?
		
		## Relation to Linq	
		*/
		public static Gen<U> Select<T, U> (this Gen<T> gen, Func<T, U> select)
		{
			return gen.Bind (a => select (a).ToGen ());
		}

		/// <summary>
		/// SelectMany extension method needed to enable Linq's syntactic sugaring.
		/// </summary>
		public static Gen<V> SelectMany<T, U, V> (this Gen<T> gen,
			Func<T, Gen<U>> project, Func<T, U, V> select)
		{
			return gen.Bind (a => project (a).Bind (b => select (a, b).ToGen ()));
		}

		/// <summary>
		/// Where extension method needed to enable Linq's syntactic sugaring.
		/// </summary>
		public static Gen<T> Where<T> (this Gen<T> gen, Func<T, bool> predicate)
		{
			return (rnd, size) =>
			{
				T result;
				do { result = gen (rnd, size); }
				while (!predicate (result));
				return result;
			};
		}

		/// <summary>
		/// Combine two Gen values into a tuple.
		/// </summary>
		public static Gen<Tuple<T, U>> Plus<T, U> (this Gen<T> gen1, Gen<U> gen2)
		{
			return from a in gen1
				   from b in gen2
				   select Tuple.Create (a, b);
		}

		/// <summary>
		/// Combine three Gen values into a tuple.
		/// </summary>
		public static Gen<Tuple<T, U, V>> Plus<T, U, V> (this Gen<T> gen1, Gen<U> gen2,
			Gen<V> gen3)
		{
			return from a in gen1
				   from b in gen2
				   from c in gen3
				   select Tuple.Create (a, b, c);
		}

        public static Gen<int> ChooseInt ()
        {
            return (rnd, size) => rnd.Next (-size / 2, size / 2);
        }

        /// <summary>
        /// Primitive generator to choose an integer.
        /// </summary>
        public static Gen<int> ChooseInt (int min)
		{
			return (rnd, size) => rnd.Next (min, min + size);
		}

		/// <summary>
		/// Primitive generator to choose an integer in the given range.
		/// </summary>
		public static Gen<int> ChooseInt (int min, int max)
		{
			return (rnd, size) => rnd.Next (min, max);
		}

        public static Gen<double> ChooseDouble ()
        {
            return (rnd, size) => ((rnd.NextDouble () - 0.5) * size);
        }

        /// <summary>
        /// Primitive generator to choose a double in the given range.
        /// </summary>
        public static Gen<double> ChooseDouble (double min)
        {
            return (rnd, size) => (rnd.NextDouble () * size) + min;
        }

        /// <summary>
        /// Primitive generator to choose a double.
        /// </summary>
        public static Gen<double> ChooseDouble (double min, double max)
		{
			return (rnd, size) => (rnd.NextDouble () * (max - min)) + min;
		}

		/// <summary>
		/// Randomly choose an value from an array.
		/// </summary>
		public static Gen<T> ChooseFrom<T> (params T[] values)
		{
			return (rnd, size) => values[rnd.Next (values.Length)];
		}

		public static Gen<T> ElementOf<T> (IEnumerable<T> enumerable)
		{
			return ChooseFrom (enumerable.ToArray ());
		}

		/// <summary>
		/// Cast the gen to its base type.
		/// </summary>
		public static Gen<U> Cast<T, U> (this Gen<T> gen) where T : U
		{
			return gen.Bind (x => ((U)x).ToGen ());
		}

		public static Gen<long> ToLong (this Gen<int> gen)
		{
			return gen.Bind (x => ((long)x).ToGen ());
		}

		public static Gen<float> ToFloat (this Gen<double> gen)
		{
			return gen.Bind (x => ((float)x).ToGen ());
		}

        /// <summary>
        /// <summary>
        /// Helper function that generates an infinite stream of values.
        /// </summary>
        private static IEnumerable<T> InfiniteEnumerable<T> (Gen<T> gen, Random rnd, int size)
        {
            while (true) yield return gen (rnd, size);
        }

        /// Helper function that generates a fixed number of values.
        /// </summary>
        private static IEnumerable<T> FixedEnumerable<T> (Gen<T> gen, Random rnd, int size, int length)
        {
            return InfiniteEnumerable (gen, rnd, size).Take (length);
        }

        /// <summary>
		/// Helper function that generates an random number of values.
		/// </summary>
		private static IEnumerable<T> RandomEnumerable<T> (Gen<T> gen, Random rnd, int size)
		{
            return FixedEnumerable (gen, rnd, size, rnd.Next (size));
		}

        /// <summary>
		/// Returns a list (enumeration) of generated values.
		/// </summary>
		public static Gen<IEnumerable<T>> EnumerableOf<T> (this Gen<T> gen)
		{
			return (rnd, size) => RandomEnumerable (gen, rnd, size);
		}

		/// <summary>
		/// Returns an array of generated values.
		/// </summary>
		public static Gen<T[]> ArrayOf<T> (this Gen<T> gen)
		{
			return (rnd, size) => RandomEnumerable (gen, rnd, size).ToArray ();
		}

        /// <summary>
        /// Returns an array of generated values.
        /// </summary>
        public static Gen<T[]> FixedArrayOf<T> (this Gen<T> gen, int length)
        {
            return (rnd, size) => FixedEnumerable (gen, rnd, size, length).ToArray ();
        }

        /// <summary>
        /// Returns an array of generated values.
        /// </summary>
        public static Gen<T[,]> Fixed2DArrayOf<T> (this Gen<T> gen, int dimension1, int dimension2)
        {
            return (rnd, size) => InfiniteEnumerable (gen, rnd, size).To2DArray (dimension1, dimension2);
        }

        /// <summary>
		/// Randomly chooses one of given generators.
		/// </summary>
		public static Gen<T> OneOf<T> (params Gen<T>[] gens)
		{
			return ChooseInt (0, gens.Length).Bind (i => gens[i]);
		}

		/// <summary>
		/// Choose a generator randomly from a list based on frequencies.
		/// </summary>
		public static Gen<T> Frequency<T> (params Tuple<int, Gen<T>>[] freqGens)
		{
			var sum = 0;
			for (int i = 0; i < freqGens.Length; i++)
				freqGens[i] = Tuple.Create (sum += freqGens[0].Item1, freqGens[i].Item2);

			return ChooseInt (1, sum).Bind (x => freqGens.First (fg => fg.Item1 >= x).Item2);
		}
	}
}