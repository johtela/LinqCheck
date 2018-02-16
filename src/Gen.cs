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
	to _compose_ instances of a generic type together by providing two simple 
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
			`(Random, int) => U`. So, we need to return a lambda expression
			with two parameters `rnd` and `size`.
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
		Note that the process was almost mechanical. Think for a while, if you 
		could implement the method differently. You will notice that if you try
		to change the implementation in any way, the code will not compile 
		anymore; the types do not match.

		This is the reason, why we usually do not have to concern ourselves
		with the monad laws. If your monad type is a pure function, it is 
		impossible to define the _return_ and _bind_ operation in a way that 
		violates the monad laws. There is literally only one way to implement 
		them. Be warned that this realization can easily blow your mind...

		### What Have We Achieved?
		Before going further, let's step back and contemplate what we just 
		implemented. We defined two operations: `ToGen` converts a value into 
		a generator, and `Bind` turns a generator for `T`'s into generator for 
		`U`'s when given a function that maps a value of type `T` to a 
		generator of type `Gen<U>`.

		Essentially we defined a way to create generators and chain them 
		together while hiding the two parameters of the `Gen<T>` delegate. 
		This does not sound like a big deal, but it is. 
		
		We can construct arbitrary complex generators and implement almost 
		all the functionality we need using just these two operations. The 
		instance of the Random class and the size parameter are passed along
		transparently in the background. This simplifies our code and makes it 
		_composable_. With monads we achieve true code reusability.

		## Relation to Linq 
		In the introduction there was a vague claim that Linq and monads are
		somehow related. Now we will show exactly how. We will implement Linq's 
		core operations `Select` and `SelectMany` using `ToGen` and `Bind`. The 
		`Select*` methods enable the syntactic sugaring that allows us to write 
		generators as Linq expressions. 
		
		With syntactic sugaring we can define our generators with a
		[domain specific language](https://en.wikipedia.org/wiki/Domain-specific_language).
		This DSL just happens to have the same syntax as Linq. Haskell provides
		syntactic sugaring for monads too, although its syntax resembles an 
		imperative program rather than a SQL query. Nevertheless, the idea 
		is exactly the same.

		### Implementing Select and SelectMany

		The signature of the Linq's `Select` method is almost the same as for 
		`Bind`. The only difference is the type of the function given as the 
		second argument. Instead of `Func<T, Gen<U>>` it is `Func<T, U>`. 
		Converting the function type to one expected by `Bind` is trivial using
		`ToGen`.
		*/
		public static Gen<U> Select<T, U> (this Gen<T> gen, Func<T, U> select)
		{
			return gen.Bind (a => select (a).ToGen ());
		}
		/*
		The `SelectMany` operation is called _flatMap_ in some functional 
		languages. It performs a double `Bind` as it combines a generator for 
		`T`'s and a generator for `U`'s, and maps them to a generator for `V`'s.
		Whenever we have more than one select clause in a Linq expression this 
		method is called.
		*/
		public static Gen<V> SelectMany<T, U, V> (this Gen<T> gen,
			Func<T, Gen<U>> project, Func<T, U, V> select)
		{
			/*
			As for `Select` the correct implementation reveals itself by 
			following the types. Note that the types force us to arrange
			the `Bind` operations in a specific order.
			*/
			return gen.Bind (a => project (a).Bind (b => select (a, b).ToGen ()));
		}
		/*
		Using `Bind` and `ToGen` makes the implementation of `Select` and 
		`SelectMany` completely generic. This means that these methods are
		implemented exactly the same way for _any_ monad we might come up with. 
		Unfortunately it is not possible to reuse these implementations for 
		other monads like in Haskell since the C# type system lacks 
		[type classes](https://en.wikipedia.org/wiki/Type_class), but copying 
		and pasting will work as well. 
		
		There is another monad type in LinqCheck: `Prop<T>`. Check for yourself 
		that its implementation of `Select` and `SelectMany` is same as above.

		### Implementing Where
		Linq's `Where` combinator is such that we cannot define it in terms of 
		`ToGen` and `Bind`. `Where` filters out generated values which do not 
		match a specified predicate. It might need to call the generator 
		repeatedly to obtain a value that satisfies the predicate. In the 
		worst case, it might not find a matching value at all. The combinator 
		gives up after 100 tries and throw an exception. It is the caller's
		responsibility to ensure that the predicate is not too strict.
		*/
		public static Gen<T> Where<T> (this Gen<T> gen, Func<T, bool> predicate)
		{
			return (rnd, size) =>
			{
				T res;
				var tries = 0;
				do
					res = gen (rnd, size);
				while (!predicate (res) && ++tries < 100);
				if (tries >= 100)
					throw new ArgumentException ("Could not generate a " +
						"random value which satisfies the predicate.");
				return res;
			};
		}
		/*
		## Other Combinators
		New generators can be now defined as Linq expressions. As an example, 
		let's define two new combinators which combine two generators into a 
		generator of tuples.
		*/
		public static Gen<Tuple<T, U>> Plus<T, U> (this Gen<T> gen1, Gen<U> gen2)
		{
			return from a in gen1
				   from b in gen2
				   select Tuple.Create (a, b);
		}

		public static Gen<Tuple<T, U, V>> Plus<T, U, V> (this Gen<T> gen1, 
			Gen<U> gen2, Gen<V> gen3)
		{
			return from a in gen1
				   from b in gen2
				   from c in gen3
				   select Tuple.Create (a, b, c);
		}
		/*
		## Number Generators
		In addition to combinators we naturally need generators for the 
		number types `int` and `double`. These generators exploit the
		functionality of the Random class. The `size` parameter is used to 
		limit the generated values, if no explicit limit is specified.
		*/
        public static Gen<int> ChooseInt ()
        {
            return (rnd, size) => rnd.Next (-size / 2, size / 2);
        }

        public static Gen<int> ChooseInt (int min)
		{
			return (rnd, size) => rnd.Next (min, min + size);
		}

		public static Gen<int> ChooseInt (int min, int max)
		{
			return (rnd, size) => rnd.Next (min, max);
		}

        public static Gen<double> ChooseDouble ()
        {
            return (rnd, size) => ((rnd.NextDouble () - 0.5) * size);
        }

        public static Gen<double> ChooseDouble (double min)
        {
            return (rnd, size) => (rnd.NextDouble () * size) + min;
        }

        public static Gen<double> ChooseDouble (double min, double max)
		{
			return (rnd, size) => (rnd.NextDouble () * (max - min)) + min;
		}
		/*
		## Choosing a Value from a Predefined Set
		Another way to generate a random value is to select it from a predefined
		set. This set can be given as an array or as an IEnumerable.
		*/
		public static Gen<T> ChooseFrom<T> (params T[] values)
		{
			return (rnd, size) => values[rnd.Next (values.Length)];
		}

		public static Gen<T> ElementOf<T> (IEnumerable<T> enumerable)
		{
			return ChooseFrom (enumerable.ToArray ());
		}
		/*
		## Converting Generators
		We need also to do type conversions between the generators. The 
		following methods are defined mostly for convenience, to avoid 
		boilerplate code when constructing generators.
		*/
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
		/*
		## Generating Collections
		Once we can generate primitive types, the next step is to generate 
		collections of them. Since practically all collections implement the
		IEnumerable interface, we can use it as a basis to generate any 
		collection type.

		First let's define an infinite sequence of values. This is the most 
		general collection generator from which the more restricted versions
		are derived.
		*/
        private static IEnumerable<T> InfiniteEnumerable<T> (Gen<T> gen, 
			Random rnd, int size)
        {
            while (true) yield return gen (rnd, size);
        }
		/*
		The second variant returns the specified number of random values.
		*/
        private static IEnumerable<T> FixedEnumerable<T> (Gen<T> gen, 
			Random rnd, int size, int length)
        {
            return InfiniteEnumerable (gen, rnd, size).Take (length);
        }
		/*
		And the third variant returns a finite but random number of elements.
		*/
		private static IEnumerable<T> RandomEnumerable<T> (Gen<T> gen, 
			Random rnd, int size)
		{
            return FixedEnumerable (gen, rnd, size, rnd.Next (size));
		}
		/*
		The following version takes the limits from the generator's arguments.
		It is the easiest way to convert a generator into a collection 
		generator.
		*/
		public static Gen<IEnumerable<T>> EnumerableOf<T> (this Gen<T> gen)
		{
			return (rnd, size) => RandomEnumerable (gen, rnd, size);
		}
		/*
		Below are the corresponding generators for arrays. They utilize the 
		IEnumerable generators and the extension methods defined in 
		[System.Linq.Enumerable](https://msdn.microsoft.com/en-us/library/system.linq.enumerable(v=vs.110).aspx)
		and [ExtensionCord](https://johtela.github.io/ExtensionCord/) library.
		*/
		public static Gen<T[]> ArrayOf<T> (this Gen<T> gen)
		{
			return (rnd, size) => RandomEnumerable (gen, rnd, size).ToArray ();
		}

        public static Gen<T[]> FixedArrayOf<T> (this Gen<T> gen, int length)
        {
            return (rnd, size) => FixedEnumerable (gen, rnd, size, length)
				.ToArray ();
        }

		public static Gen<T[,]> Fixed2DArrayOf<T> (this Gen<T> gen, 
			int dimension1, int dimension2)
        {
            return (rnd, size) => InfiniteEnumerable (gen, rnd, size)
				.To2DArray (dimension1, dimension2);
        }
		/*
		## Choosing between Generators
		Sometimes we have more than one generator for a type, and we want to
		randomly choose one of them. The `OneOf` combinator will choose a 
		generator from an array using even distribution. I.e. each choice
		has an equal chance to be selected.
		*/
		public static Gen<T> OneOf<T> (params Gen<T>[] gens)
		{
			return ChooseInt (0, gens.Length).Bind (i => gens[i]);
		}
		/*
		If you want a skewed distribution of the choices, you can use the 
		`Frequency` method. It allows you to specify the frequency for each 
		choice. The frequencies are defined as integers with relative ranges. 
		For example, if choice _A_ has a frequency of 3 and choice _B_ has 12, 
		choice _B_ is 4 times more likely to be selected than _A_.
		*/
		public static Gen<T> Frequency<T> (params Tuple<int, Gen<T>>[] freqGens)
		{
			var sum = 0;
			for (int i = 0; i < freqGens.Length; i++)
				freqGens[i] = Tuple.Create (sum += freqGens[0].Item1, freqGens[i].Item2);

			return ChooseInt (1, sum).Bind (x => freqGens.First (fg => fg.Item1 >= x).Item2);
		}
	}
}
/*
## Generators Are Deterministic
Before wrapping up the chapter, let's discuss the requirement of determinism. 
Generators are not truly random; their output depends on the seed provided to 
the instance of the Random class that we pass around. To put it differently, 
they produce [_pseudorandom_](https://en.wikipedia.org/wiki/Pseudorandom_number_generator) 
values. 

This is important because we want to be able to generate a same sequence 
of values multiple times when we are generating test data. All of the 
combinators work deterministically. They extract random numbers from the `rnd` 
object and pass it around in the same manner every time they are called. This 
ensures that whatever generator we compose, given the same seed, it will 
generate the same value.
*/
