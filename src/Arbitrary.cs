﻿/*
# Generating and Shrinking Arbitrary Values

![Yin and yang](https://upload.wikimedia.org/wikipedia/commons/thumb/1/17/Yin_yang.svg/145px-Yin_yang.svg.png)

In the realm of property based testing generating random test data is just one
side of the story. If random value generation is the _yin_ then the _yang_ is 
shrinking these values.

We put these dual operations into a generic interface `IArbitrary<T>`. There 
must be an instance of this interface for all the types we want to use in our
properties.
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
		parameter. The more complex the generated value is, the more instances 
		shrinking typically returns.
		*/
		IEnumerable<T> Shrink (T value);
	}
	/*
	*/
	public abstract class ArbitraryBase<T> : IArbitrary<T>
	{
		public abstract Gen<T> Generate { get; }

		public virtual IEnumerable<T> Shrink (T value)
		{
			return new T[0];
		}
	}

	/// <summary>
	/// Concrete arbitrary for non-generic types. Takes a function to
	/// create an arbitrary value.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class Arbitrary<T> : ArbitraryBase<T>
	{
		public readonly Gen<T> Generator;
		public readonly Func<T, IEnumerable<T>> Shrinker;

		public Arbitrary (Gen<T> generator)
		{
			Generator = generator;
		}

		public Arbitrary (Gen<T> generator, Func<T, IEnumerable<T>> shrinker)
		{
			Generator = generator;
			Shrinker = shrinker;
		}

		public override Gen<T> Generate
		{
			get { return Generator; }
		}

		public override IEnumerable<T> Shrink (T value)
		{
			return Shrinker == null ?
				base.Shrink (value) :
				Shrinker (value);
		}
	}

	/// <summary>
	/// The basic infrastructure and extension methods for managing
	/// and composing IArbitrary[T] interfaces.
	/// </summary>
	public static class Arbitrary
	{
		private static Container _container;

		static Arbitrary ()
		{
			_container = new Container (typeof (IArbitrary<>));
			DefaultArbitrary.Register ();
		}

		public static void Register<T> (IArbitrary<T> arbitrary)
		{
			_container.Register (arbitrary);
		}

		public static void Register (Type type)
		{
			_container.Register (type);
		}

		public static IArbitrary<T> Get<T> ()
		{
			return (IArbitrary<T>)_container.GetImplementation (typeof (T));
		}

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
	}
}
