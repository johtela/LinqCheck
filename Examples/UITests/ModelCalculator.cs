/*
# Implementing a Model

The model implementation should work exactly as the application it's mimicking.
Since we don't have the source code for the Windows calculator, we have to 
reverse-engineer its operation. Luckily, calculator's functionality is quite 
simple. Although when developing the model, LinqCheck found many command 
sequences where the result of the calculation did not match my assumptions. 
This is why the model implementation went through quite many revisions before 
stabilizing into the current one.

We begin by inheriting the ICalculator interface and defining a private 
enumeration type which is used in recording the operation that was performed 
previously. 
*/
namespace Examples.UITests
{
	public class ModelCalculator : ICalculator
	{
		private enum Oper { None, Add, Sub, Mul, Div }
		/*
		## Calculator State

		The (mutable) state of the calculator is determined by the following 
		fields. The accumulator contains the current result of the calculation.
		*/
		private double _accum;
		/*
		What is shown on the display depends on the calculator's state. When we
		enter numbers, they are shown on the display. However, when we press
		the `=` button, the value stored in the accumulator is shown. We define 
		the	`_display` field to be nullable. If its value is `null`, it means 
		that the accumulator is shown. Otherwise it contains the current number 
		entered by the user.
		*/
		private double? _display;
		/*
		When the user presses `=` multiple times, then the previous operation 
		is repeated. So, we need to remember what the previous operation was. 
		We store the previously entered number in the `_prevVal` field and the 
		previously peformed operation in the `_prevOper` field. 
		*/
		private double _prevVal;
		private Oper _prevOper;
		/*
		We need to remember also if the previous operation was `=`. This 
		affects when we forget the previous operation.
		*/
		private bool _prevEqual;
		/*
		As you can see from above, we need surprisingly many fields to store 
		the calculator state. Although in the UI there is just one value 
		visible at any time, beneath the surface there is much more going on.
		For fun, let's define formally the set of possible states that our 
		model can be in. The set is the cartesian product of:
		$$
		\mathbb{R} \times \{ \mathbb{R} \cup \mathrm{null} \} \times \mathbb{R} 
		\times \{ \mathrm{None}, \mathrm{Add}, \mathrm{Sub}, \mathrm{Mul,} 
		\mathrm{Div} \} \times \{ \mathrm{true}, \mathrm{false} \}
		$$
		Even if we consider the set of real numbers to be finite (since 
		floating points essentially are) the cardinality of the set is still
		astronomical. 
		
		This exemplifies the inherent problem with mutable state. Even for the 
		simplest of programs the number of states quickly runs out of hand. If 
		we keep adding new features, we will inevitably end up in the situation 
		where it is impossible to reason about the program behavior.
		
		Purely functional programs get around this problem by eliminating the
		mutable state altogether, thus making it feasible to keep a mental 
		picture of the program in your head.

		## Creating And Disposing the Model

		The constructor initializes the model by calling the `Clear` method.
		No cleanup is necessary during the model disposal.
		*/
		public ModelCalculator ()
		{
			Clear ();
		}

		public void Dispose () { }
		/*
		## Calculator Display

		As mentioned above, the value on the Display depends on whether user
		has entered a number or not. When the `_display` field is `null`, it
		corresponds to a state where the user has pressed `=` or not entered
		anything yet. In that case, we return the value of the accumulator.
		Otherwise we return the value stored in the `_display` field.
		*/
		public double Display => _display ?? _accum;
		/*
		## Clearing the State

		The `Clear` method corresponds pressing the `[C]` button. It 
		initializes the state fields to zero, None, or false.
		*/
		public void Clear ()
		{
			_accum = 0;
			_display = 0;
			_prevVal = 0;
			_prevOper = Oper.None;
			_prevEqual = false;
		}
		/*
		## Operations

		Before we implement the arithmetic operations, we define a couple of
		helper functions. The first one performs the previous operation again.
		This function is actually used when an operation is performed the first
		time too. It checks the value of the `_prevOper` field and based on 
		that adds, subtracts, multiplies, or divides the accumulator with the 
		parameter `value`. If there is no previous operation set, then it 
		stores the `value` in the accumulator.
		*/
		private void PerformPreviousOperation (double value)
		{
			switch (_prevOper)
			{
				case Oper.Add:
					_accum += value;
					break;
				case Oper.Sub:
					_accum -= value;
					break;
				case Oper.Mul:
					_accum *= value;
					break;
				case Oper.Div:
					_accum /= value;
					break;
				default:
					_accum = value;
					break;
			}
		}
		/*
		The `Operation` method is called when a operator button is pressed. It
		checks whether the user has entered a number, and calculates the 
		intermediate result into the accumulator first. Then it clears the 
		display, and assigns new values to the `_prevVal` and `_prevOper` 
		fields.
		*/
		private void Operation (Oper oper)
		{
			if (!ResultAvailable)
				return;
			if (_display.HasValue)
				PerformPreviousOperation (_display.Value);
			_display = null;
			_prevVal = _accum;
			_prevOper = oper;
			_prevEqual = false;
		}
		/*
		The arithmetic operations of the ICalculator interface are 
		implemented by calling the `Operation` function with the appropriate 
		enumeration value.
		*/
		public void Add ()
		{
			Operation (Oper.Add);
		}

		public void Divide ()
		{
			Operation (Oper.Div);
		}

		public void Multiply ()
		{
			Operation (Oper.Mul);
		}

		public void Subtract ()
		{
			Operation (Oper.Sub);
		}
		/*
		The `Digit` method is called when the user pushes a digit button. It 
		multiplies the existing digit by ten, and adds a new digit to the 
		display. 
		
		A couple of special cases need to be handled:
		
		1.	If the display is locked, because the last operator returned 
			undefined, infinity or other error,	then we disallow adding numbers. 
		2.	If the user previously pressed the equals button, then we clear the 
			state before entering the digit.
		*/
		public void Digit (byte number)
		{
			if (!ResultAvailable)
				return;
			if (_prevEqual)
				Clear ();
			_display = _display.HasValue ?
				_display * 10 + number :
				number;
		}
		/*
		## Checking If a Result Is Available

		If the accumulator does not contain a valid number, then we have
		performed a calculation that has led to an undefined state. The
		`ResultAvailable` function will retrn `false` in that case.
		*/
		public bool ResultAvailable => 
			!(double.IsNaN (_accum) || double.IsInfinity (_accum));
		/*
		## Calculating the Result

		When the user presses the `=` button, then we calculate the result of 
		the currently previous operation and current value. If there are no 
		digits entered, however, then we recalculate the previous calculation
		with the previous value.
		*/
		public void Equals ()
		{
			if (_display.HasValue)
			{
				PerformPreviousOperation (_display.Value);
				_prevVal = _display.Value;
			}
			else if (_prevOper != Oper.None)
				PerformPreviousOperation (_prevVal);
			_display = null;
			_prevEqual = true;
		}
	}
}
/*
## About the Model Implementation

A word of warning to anyone considering writing a model for an imperative
program: The model probably looks as trivial as the one above once you finally 
get it working. However, there are many nuances in the implementation you have 
to deal with when developing the model. This is because the state of the model 
and the actual program must match at all times. If they do not match in some 
specific case, LinqCheck will almost certainly find that case and complain 
about it. So, prepare for finding these special cases and spending some time 
fixing your model.
*/