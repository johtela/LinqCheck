/*
# Windows 10 Calculator

![Windows 10 Calculator](https://upload.wikimedia.org/wikipedia/en/2/26/Windows_10_Calculator.png)

The calculator shipping with Windows 8 and 10 was completely rewritten. It has
a new look, but the basic functionality remained the same. So, from the
functional point of view it should work like before. Our automation controller 
implementation thus looks very similar to the Windows 7 version.

We inherit again the Automated class and implement the ICalculator interface.
*/
namespace Examples.UITests
{
	using System;
	using System.Threading;
	using System.Windows.Automation;

	class Win10Calculator : Automated, ICalculator
	{
		/*
		We store the automation element corresponding to the result text box 
		in the field below.
		*/
		private AutomationElement _result;
		/*
		## Opening and Closing the Calculator

		The calculator is launched with `StartApp` method. To locate the result
		text box we search an automation element with the Id "CalulatorResults".
		*/
		public Win10Calculator ()
		{
			StartApp ("calc.exe", "Calculator");
			Thread.Sleep (1000);
			_result = _root.FindFirst (TreeScope.Descendants,
				new PropertyCondition (
					AutomationElement.AutomationIdProperty,
					"CalculatorResults"));
			if (_result == null)
				throw new Exception ("Could not find result box");
			Clear ();
		}
		/*
		Because the Windows 10 calculator launches a separate process when it
		starts, it cannot be closed by closing the main window of the original
		process. That process does not have a main window, and it has already 
		terminated when we want to close the calculator. So instead, we just
		programmatically push the close button to close the calculator.
		*/
		public void Dispose ()
		{
			PushButton ("Close Calculator");
		}
		/*
		## Pushing Digit Buttons
		Windows 10 has different names for the digit buttons than Windows 7
		calculator, but the process of pushing the button is exactly same.
		*/
		private readonly string[] _digitButtons = { "Zero", "One", "Two",
			"Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };

		public void Digit (byte number)
		{
			if ((number < 0) || (number > 9))
				throw new ArgumentException ("mumber must be a digit 0-9");
			GetInvokePattern (GetButton (_digitButtons[number])).Invoke ();
		}
		/*
		## Reading the Result
		The code for getting the displayed result is also identical to Windows 
		7 implementation.
		*/
		public string GetResult ()
		{
			return _result.GetCurrentPropertyValue (
				AutomationElement.NameProperty).ToString ();
		}

		public double Display =>
			double.TryParse (GetResult (), out double result) ?
				result :
				double.NaN;

		public bool ResultAvailable =>
			double.TryParse (GetResult (), out double result);

		/*
		## Pushing Operator Buttons
		The operator buttons have also different names, but pushing them is no
		more difficult.
		*/
		public void Add ()
		{
			PushButton ("Plus");
		}

		public void Clear ()
		{
			PushButton ("Clear");
		}

		public void Divide ()
		{
			PushButton ("Divide by");
		}

		public void Multiply ()
		{
			PushButton ("Multiply by");
		}

		public void Subtract ()
		{
			PushButton ("Minus");
		}

		public void Equals ()
		{
			PushButton ("Equals");
		}
	}
}
/*
## Next Steps
Finally we have all the calculator controllers and their model implemented. Now
we can start testing them.
*/
