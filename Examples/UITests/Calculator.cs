/*
# Testing Programs with Mutable State
Pure functions are easy to test with LinqCheck as we can call them 
directly from our properties. We can pass the generated input to our
functions, without needing to set up any additional state. But what about
programs which are not pure, which have mutable internal state? The majority
of code running in real world falls into this category.

It is possible to use property based testing with mutable state, but it 
incurs a bit more work than what is required for purely functional code. Let's
say that we have a mutable object with a set of public methods which might 
change its internal state. What we can do is generate random sequences of 
method calls. After the calls are executed we check that the object's state is 
what we expect. The question is, however, how can we know what the state should 
be after a random sequence of actions. What properties can we actually check?

In practice, the easiest solution to this problem is to create a simplified 
model of the program we want to test. The model captures the essential features 
of the program and serves as a 
[_reference implementation_](https://en.wikipedia.org/wiki/Reference_implementation).
We invoke the same sequence of methods on the model and on the actual program, 
and then check that their states are equivalent. John Hughes talks about the 
technique in many videos
[^1^](https://www.youtube.com/watch?v=zi0rHwfiX1Q)
[^2^](https://www.youtube.com/watch?v=hXnS_Xjwk2Y)
[^3^](https://www.youtube.com/watch?v=H18vxq-VsCk) available on YouTube.

This approach sounds like a lot of work at first, but in practice writing the 
model is usually quite straightforward. There are additional benefits we get 
from the model:

*	We gain insight into our program in the process of writing the model.

*	We get a formal specification of our program that can be exploited in 
	documenting and communicating its behaviour.
	
*	Once we have the situation that the model and the real program work exactly 
	the same way, we can start refactoring the program and changing its 
	implementation - drastically if we want to. The model can be used to ensure 
	that its behaviour is not changed, not even for the edge cases. Unit tests 
	provide similar assurances, but the level of confidence you get from property 
	based tests is much higher.

## Testing the Windows Calculator
To illustrate the approach, we use Windows Calculator as an example program 
that we would like to test. It is a nice example for a few reasons. First, 
everybody knows how the calculator program works. Its paragon, the pocket 
calculator, is still around with essentially the same user interface as in the 
1970's when it was introduced. Second, although it seems to be a very simple 
program with few possible states, it actually has more edge cases than one 
might guess. Third, we can demonstrate how property based testing can be
utilized in conjunction with UI automation.

![Adler 81S pocket calculator from the mid-1970s](https://upload.wikimedia.org/wikipedia/commons/thumb/c/c7/Calculator_Adler_81S.jpg/225px-Calculator_Adler_81S.jpg)
![Windows 7 Calculator](https://upload.wikimedia.org/wikipedia/en/thumb/a/ae/Windows_7_Calculator.png/225px-Windows_7_Calculator.png)
![Windows 10 Calculator](https://upload.wikimedia.org/wikipedia/en/2/26/Windows_10_Calculator.png)

## Modelling the Calculator
Before we begin, we create a new namespace for our tests, and import the 
required libraries.
*/
namespace Examples.UITests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using ExtensionCord;
	using LinqCheck;
	/*
	What kind of model could we build for the calculator? The obvious option 
	works fine for our purposes. So, let's encapsulate the calculator in a 
	simple interface with methods corresponding to the calculator buttons. 
	Calling one of the methods has the same effect as pushing the button.

	We extend the `IDisposable` interface. Creating the interface will open
	the calculator and calling `Dispose` will close it.
	*/
	public interface ICalculator : IDisposable
	{
		/* 
		`Clear` corresponds to the `[C]` button.
		*/
		void Clear ();
		/*
		Digit function only accepts numbers from 0 to 9, so its argument type
		is byte.
		*/
		void Digit (byte number);
		/*
		The following methods correspond to operator buttons `+`, `-`, `*`, and
		`/`.
		*/
		void Add ();
		void Subtract ();
		void Multiply ();
		void Divide ();
		/*
		`Equals` calculates the result and displays it. It can be also pressed
		multiple times to repeat the previous calculation.
		*/
		void Equals ();
		/*
		We also need to access the result currently displayed, and we need to
		be able to query, if the result is available. The calculator might be 
		in an error state where the result is not defined; for example, after 
		division by zero.
		*/
		double Display { get; }
		bool ResultAvailable { get; }
	}
	/*
	Now we just need to implement this interface in two places: in our model, 
	and in the automation code that controls the Windows calculator. Since the
	calculator application is different in Windows 7 and Windows 10 (nobody
	uses Windows 8, right?), we will provide actually three implementations. 
	By doing so we will be able also to compare, if the behaviour of the 
	calculator has changed in the different versions of Windows.

	## Input Data
	The second thing we need to decide, is how to encode the input data. What
	we need as an input is a sequence of method calls, and we could model that 
	in various ways. Whatever we choose, we need to wrap it under a single 
	type. This follows from the requirement that all input types need to have 
	an implementation of `IArbitrary<T>` interface where `T` is the input type.
	
	Let's first define a type representing a single method invokation or a push 
	of a button. We name this type as Command. After we have defined that, we 
	can exploit the built-in combinators in LinqCheck to generate a list of 
	commands. This simplifies our job significantly, and lets us leverage the 
	power of existing generators and shrinkers.

	The Command type is an abstract class with private inner classes 
	representing two kinds of buttons: digit buttons which are used enter 
	numbers and command buttons used to perform calculations. 
	*/
	public abstract class Command
	{
		/*
		The `Do` method calls the appropriate method in the ICalculator 
		instance given as an argument. This method is implemented slightly
		differently by the two private subclasses.
		*/
		public abstract void Do (ICalculator target);
		/*
		The _Digit subclass stores the number to be pressed in the `Value`
		field.
		*/
		private class _Digit : Command
		{
			public readonly int Value;

			public _Digit (int value)
			{
				Value = value;
			}
			/*
			When a digit command is executed, it calls the `Digit` method on
			the given calculator.
			*/
			public override void Do (ICalculator target)
			{
				target.Digit ((byte)Value);
			}
			/*
			The input data should be printable, so that it can be shown in
			error messages. That is why we need to implement the the `ToString`
			method as well.
			*/
			public override string ToString () => Value.ToString ();
		}
		/*
		The other types of commands are represented by the _Command subclass.
		It stores the command name and a delegate function to be called when
		the command is executed.
		*/
		private class _Command : Command
		{
			public Action<ICalculator> Action;
			public string Name;

			public _Command (Action<ICalculator> action, string name)
			{
				Action = action;
				Name = name;
			}
			/*
			The `Do` method simply calls the stored delegate...
			*/
			public override void Do (ICalculator target)
			{
				Action (target);
			}
			/*
			..and `ToString` returns the command name.
			*/
			public override string ToString () => Name;
		}
		/*
		Now we can define public functions and properties that create commands 
		corresponding methods we have in the ICalculator interface. We don't
		include the `Clear` method since we don't intend to use it in our 
		randomly generated sequences.
		*/
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
		/*
		The `Execute` method runs a sequence of commands on a given calculator.
		This is the method we will call in our property when testing the 
		calculator. It returns the number displayed after the sequences is
		executed.
		*/
		public static double Execute (ICalculator calculator, 
			IEnumerable<Command> commands)
		{
			calculator.Clear ();
			foreach (var command in commands)
				command.Do (calculator);
			return calculator.Display;
		}
		/*
		The only thing left is to define the implementation for 
		`IArbitrary<Command>`. We register it in the static constructor. We can
		use the `Arbitrary<T>` wrapper class as a helper.
		*/
		static Command ()
		{
			Arbitrary.Register (new Arbitrary<Command> (
				/*
				The generator consist of two branches. We will use the `OneOf`
				combinator to first randomly chooses whether we push a digit 
				button or an operator button.
				*/
				Gen.OneOf (
					/*
					If the digit button is selected, then we choose a random
					number from the range of 0 to 9, and create a `Digit` 
					command for the chosen number.
					*/
					from i in Gen.ChooseInt (0, 10)
					select Digit (i),
					/*
					If the winner was the other branch, then we choose a random
					command from the set of operators.
					*/
					Gen.ChooseFrom (Add, Subtract, Multiply, Divide, Equals)
				),
				/*
				How to implement shrinking is a bit harder to figure out. 
				Shrinking of operators is not something we can easily define, 
				so we will be content with shrinking just the digit command 
				utilizing the shrinking defined for integers. We achieve the 
				true power of shrinking by reducing the lists of commands to
				shorter examples when LinqCheck finds a failing sequence. This 
				functionality we get out-of-the-box without any additional code.
				*/
				com => com is _Digit n ?
					Arbitrary.Shrink (n.Value).Select (Digit) :
					EnumerableExt.Enumerate (com)
			));
		}
		/*
		Since static constructors are called quite undeterministically when a 
		class is first used, we define a dummy method that we can call before 
		generating any test data. This ensures that the implementation for 
		`IArbitrary<Command>` is registered.
		*/
		public static void Register () { }
	}
}
/*
## Next Steps
Now we have the basic building blocks to create tests for the calculator. Next
we need to connect our abstract API to the real world and implement the 
ICalculator interface first for the model and then for the Windows apps.
*/
