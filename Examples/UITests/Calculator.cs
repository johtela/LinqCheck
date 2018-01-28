/*
# Testing Programs with Mutable State

Pure functions are easy to test with LinqCheck since you can call them 
directly from your properties. You can just pass the generated input to your
functions, there is no need to set up any additional state. But what about
programs which are not pure, which have mutable internal state? The majority
of code running in real world falls into this category.

It is possible to use property based testing with mutable state, but it 
incurs a bit more work than what is required for purely functional code. Let's
say that we have a mutable object with a set of public methods which might 
change its internal state. What we can do is generate random sequences of 
method calls. After the calls are executed we check that the obect's state is 
as we expect. The question is, however, how can we know what the state should 
be after a random sequence of actions. What properties can we actually check?

In practice, the easiest solution to the problem is to create a simplified 
model of the program we want to test. The model captures the essential features 
of the imperative program and serves as a 
[reference implementation](https://en.wikipedia.org/wiki/Reference_implementation).
We run the same sequence of method invocations with the model and with the 
actual program, and then check is that their states are equivalent. John Hughes
talks about the technique in many videos
[^1^](https://www.youtube.com/watch?v=zi0rHwfiX1Q)
[^2^](https://www.youtube.com/watch?v=hXnS_Xjwk2Y)
[^3^](https://www.youtube.com/watch?v=H18vxq-VsCk) available on YouTube.

This approach sounds like a lot of work at first, but in practice writing the 
model is usually quite straightforward. There are also additional benefits we 
get from the model. We get essentially a specification for the program in the
form that is much easier to understand and document. Once we have the situation
that the model and the real program work exactly the same way, we can start 
refactoring the real program and changing its implementation - drastically if 
we want to. The model can be used to ensure that its behaviour is not changed, 
not even for the edge cases. Unit tests provide similar assurances, but the 
level of confidence you get from property based tests is much higher.

## Testing the Windows Calculator

To illustrate the approach, we use Windows Calculator as an example program 
that we would like to test. It is a nice example for a few reasons. First of 
all, everybody knows how the calculator program works. Its paragon is the 
pocket calculator which is still around with essentially the same user 
interface as in the 1970's when it was introduced. Secondly, although it seems
to be a very simple program with few possible states, it actually has more edge
cases than one might guess.

![Adler 81S pocket calculator from the mid-1970s](https://upload.wikimedia.org/wikipedia/commons/thumb/c/c7/Calculator_Adler_81S.jpg/225px-Calculator_Adler_81S.jpg)
![Windows 7 Calculator](https://upload.wikimedia.org/wikipedia/en/thumb/a/ae/Windows_7_Calculator.png/225px-Windows_7_Calculator.png)
![Windows 10 Calculator](https://upload.wikimedia.org/wikipedia/en/2/26/Windows_10_Calculator.png)
*/
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
/*

*/
