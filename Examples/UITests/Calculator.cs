namespace Examples.UITests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using ExtensionCord;
	using LinqCheck;

	public interface ICalculator : IDisposable
	{
		void Clear ();
		void Digit (byte number);
		void Add ();
		void Subtract ();
		void Multiply ();
		void Divide ();
		void Equals ();
		double Display { get; }
		bool ResultAvailable { get; }
	}

	public abstract class Command
	{
		public abstract void Do (ICalculator target);

		private class _Digit : Command
		{
			public readonly int Value;

			public _Digit (int value)
			{
				Value = value;
			}

			public override void Do (ICalculator target)
			{
				target.Digit ((byte)Value);
			}

			public override string ToString () => Value.ToString ();
		}

		private class _Command : Command
		{
			public Action<ICalculator> Action;
			public string Name;

			public _Command (Action<ICalculator> action, string name)
			{
				Action = action;
				Name = name;
			}

			public override void Do (ICalculator target)
			{
				Action (target);
			}

			public override string ToString () => Name;
		}

		public static Command Digit (int value) =>
			new _Digit (value);

		public static readonly Command Add =
			new _Command (c => c.Add (), "+");

		public static readonly Command Subtract =
			new _Command (c => c.Subtract (), "-");

		public static readonly Command Multiply =
			new _Command (c => c.Multiply (), "*");

		public static readonly Command Divide =
			new _Command (c => c.Divide (), "/");

		public static readonly new Command Equals =
			new _Command (c => c.Equals (), "=");

		public static double Execute (ICalculator calculator, 
			IEnumerable<Command> commands)
		{
			calculator.Clear ();
			foreach (var command in commands)
				command.Do (calculator);
			return calculator.Display;
		}

		public static void Register () { }

		static Command ()
		{
			Arbitrary.Register (new Arbitrary<Command> (
				Gen.OneOf (
					from i in Gen.ChooseInt (0, 10)
					select Digit (i),
					Gen.ChooseFrom (Add, Subtract, Multiply, Divide, Equals)
				),
				com => com is _Digit n ?
					Arbitrary.Shrink (n.Value).Select (Digit) :
					EnumerableExt.Enumerate (com)
			));
		}
	}
}