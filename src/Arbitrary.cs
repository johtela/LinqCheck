/*
# Generating and Shrinking Arbitrary Values

![Yin and yang](https://upload.wikimedia.org/wikipedia/commons/thumb/1/17/Yin_yang.svg/145px-Yin_yang.svg.png)

In implementing a property based testing library random data generation takes 
us only half way there. The full power of the technique is attained by 
implementing shrinking of values as well. If random generation is the _yin_ of
property based testing, then the _yang_ is shrinking.

Shrinking, in practical terms, stands for the act of producing simpler versions 
of a generated value, which causes a property to fail. For example, assuming 
that number -123 causes our property to fail, shrinking first tries the value 
zero, which is arguably the simplest of all number. Then it tries positive 1 

number, and then tries to make the number smaller. If all the 

smaller numbers produced still keep our property failing, finally, shrinking 
will return . The procedure 
is similar for compound data structures such as strings and collections; 
shrinking first tries to remove elemenents from them, and then shrink each 
remaining element individually.

The point of all this is to create a simpler versions of the failing input data
without loosing the characteristic that makes the property fail. In the same 
manner as random generation is used to find test cases that violate the 
specified properties, shrinking tries to systematically simplify the input to 
its most basic form.

We define the random generation and shrinking operations in the interface 
`IArbitrary<T>` which represents an arbitrary value of type `T`. An 
implementation of this interface needs to be provided for all the types we wish 
to generate in our properties.
*/
namespace LinqCheck
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	public interface IArbitrary<T>
	{
		/*
		The `Generate` property returns the generator for the type.
		*/
		Gen<T> Generate { get; }
		/*
		The `Shrink` method provides shrunk versions of a value given as 
		parameter. The list of alternatives is returned as an IEnumerable. The 
		first alternative is the simplest form, and the following ones are each
		one step closer to the original. The original value is automatically 
		appended at	the end of the result when QuickCheck is calling the method. 
		So, there is no need to return it when implementing the IArbitrary 
		interface.
		*/
		IEnumerable<T> Shrink (T value);
	}
	/*
	## Base Class for IArbitrary Implementations
	We define also a base class that implements the `IArbitrary<T>` interface. 
	This class mainly used internally in the LinqCheck library.
	*/
	public abstract class ArbitraryBase<T> : IArbitrary<T>
	{
		public abstract Gen<T> Generate { get; }
		/*
		The default implementation of `Shrink` returns an empty set of values.
		So, it actually performs no shrinking at all.
		*/
		public virtual IEnumerable<T> Shrink (T value)
		{
			return Enumerable.Empty<T> ();
		}
	}
	/*
	## Wrapper Class for Arbitrary Values
	The `Arbitrary<T>` class is meant to be used as a quick template for 
	implementing the `IArbitary<T>` interface. It takes the generator and
	shrinker as parameters, and stores them in properties.
	*/
	public class Arbitrary<T> : ArbitraryBase<T>
	{
		public readonly Gen<T> Generator;
		public readonly Func<T, IEnumerable<T>> Shrinker;
		/*
		The simpler version of the constructor takes just the generator, and 
		leaves the shrinker unassigned. No shrinking is thus performed for the 
		generated values.
		*/
		public Arbitrary (Gen<T> generator)
		{
			Generator = generator;
		}
		/*
		The preferred version of the constructor takes both generator and 
		shrinker.
		*/
		public Arbitrary (Gen<T> generator, Func<T, IEnumerable<T>> shrinker)
		{
			Generator = generator;
			Shrinker = shrinker;
		}
		/*
		The implementation of `Generate` is trivial.
		*/
		public override Gen<T> Generate
		{
			get { return Generator; }
		}
		/*
		The overridden implementation of the `Shrink` method reverts to the 
		inherited (no shrinking) method, if no shrinker has been given.
		*/
		public override IEnumerable<T> Shrink (T value)
		{
			return Shrinker == null ?
				base.Shrink (value) :
				Shrinker (value);
		}
	}
	/*
	## Extension Methods for IArbitrary
	We define some additional operations for the IArbitrary interface as 
	extension methods. Most of these methods concern registering and 
	instantiating `IArbitrary<T>` implementations for different types. We use
	a simple [Container](Container.html) to manage the registered types at 
	runtime.
	*/
	public static class Arbitrary
	{
		/*
		The container is stored in a private, static variable. It is created
		in the static constructor. The built-in IArbitrary instances defined in
		the [DefaultArbitrary](DefaultArbitrary.html) class are registered, 
		once the container is created.
		*/
		private static Container _container;

		static Arbitrary ()
		{
			_container = new Container (typeof (IArbitrary<>));
			DefaultArbitrary.Register ();
		}
		/*
		### Registering New IArbitrary Implementations
		The following two methods register an implementation of IArbitrary for
		a given type. The type can be specified as a generic argument or as an 
		instance of the Type class.
		*/
		public static void Register<T> (IArbitrary<T> arbitrary)
		{
			_container.Register (arbitrary);
		}

		public static void Register (Type type)
		{
			_container.Register (type);
		}
		/*
		### Getting the Registered Implementation
		To retrieve a previously registered IArbitrary implementation, you can 
		call the `Get` method. It gets the implementation from the container.
		*/
		public static IArbitrary<T> Get<T> ()
		{
			return (IArbitrary<T>)_container.GetImplementation (typeof (T));
		}
		/*
		### Helper Methods
		The three methods below are defined for convenience. They are helper
		methods that can be used to generate or shrink an instance of a given 
		type. The methods assume that an implementation of IArbitrary is 
		registered for the type.
		*/
		public static Gen<T> Gen<T> ()
		{
			return Get<T> ().Generate;
		}

		public static T Generate<T> (Random rnd, int size)
		{
			return Get<T> ().Generate (rnd, size);
		}

		public static IEnumerable<T> Shrink<T> (T value)
		{
			return Get<T> ().Shrink (value);
		}
		/*
		### Combinators
		Lastly, we define some combinators which compose new IArbitrary 
		implementations from existing ones. The `SuchThat` combinator restricts
		generated values by filtering out the ones that do not match a given 
		predicate. The method tries 100 times to generate a value before giving
		up and throwing an exception. The reason for this is to avoid entering 
		an infinite loop, if the predicate is too restrictrive
		*/
		public static IArbitrary<T> SuchThat<T> (this IArbitrary<T> arbitrary, 
			Func<T, bool> predicate)
		{
			return new Arbitrary<T> (
				(rnd, size) =>
				{
					T res;
					var tries = 0;
					do
						res = arbitrary.Generate (rnd, size);
					while (!predicate (res) && ++tries < 100);
					if (tries >= 100)
						throw new ArgumentException ("Could not generate a " +
							"random value which satisfies the predicate.");
					return res;
				},
				val => arbitrary.Shrink (val).Where (predicate));
		}
		/*
		The `Convert` combinator creates an implementation of IArbitary for a 
		new type `U` given an existing implementation for type `T`. For this to 
		work, we need to specify conversion functions from `T` to `U` and vice 
		versa. 
		*/
		public static IArbitrary<U> Convert<T, U> (this IArbitrary<T> arbitrary,
			Func<T, U> convertTtoU, Func<U, T> convertUtoT)
		{
			return new Arbitrary<U> (
				from a in arbitrary.Generate
				select convertTtoU (a),
				a => from b in arbitrary.Shrink (convertUtoT (a))
					 select convertTtoU (b));
		}
	}
}
