namespace LinqCheck
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Interface for generating random values.
	/// </summary>
	/// <typeparam name="T">The type of the arbitrary value created.</typeparam>
	public interface IArbitrary<T>
	{
		Gen<T> Generate { get; }
		IEnumerable<T> Shrink (T value);
	}

	/// <summary>
	/// Base class for creating instances and combinators for arbitrary values.
	/// </summary>
	/// <typeparam name="T"></typeparam>
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
	}
}
