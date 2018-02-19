/*
# Windows 7 Calculator

![Windows 7 Calculator](https://upload.wikimedia.org/wikipedia/en/thumb/a/ae/Windows_7_Calculator.png/225px-Windows_7_Calculator.png)

The calculator in Windows 7 is probably written in C++, and is a fairly old 
application. Nevertheless, it fully supports the automation facilities. 
Windows' accessibility features are also built upon automation, so all 
standard Windows applications have to support it.

So, let's implement the necessary plumbing to control the calculator. We do that
by inheriting the Automated class and implementing the ICalculator interface.
*/
namespace Examples.UITests
{
	using System;
	using System.Windows.Automation;

	class Win7Calculator : Automated, ICalculator
	{
		/*
		The only data we store is the automation element for the calculator's 
		display text box.
		*/
		private AutomationElement _result;
		/*
		## Opening and Closing the Calculator
		The calculator is launched when the class is created, so we start it in
		the constructor. After the calculator started successfully, we find the
		automation element for the text box representing the result display. We
		find the text box by it's automation ID property, since it does not 
		have a name.
		*/
		public Win7Calculator ()
		{
			StartApp ("Calc.exe", "Calculator");
			_result = _root.FindFirst (TreeScope.Descendants, 
				new PropertyCondition (AutomationElement.AutomationIdProperty, "150"));
			if (_result == null)
				throw new Exception ("Could not find result box");
			Clear ();
		}
		/*
		Disposing the object calls the inherited `QuitApp` method, which closes
		the calculator.
		*/
		public void Dispose ()
		{
			QuitApp ();
		}
		/*
		## Pushing Digit Buttons
		The digit buttons in Windows 7 calculator are named simply with numbers
		from 0 to 9. So we can locate the buttons easily and use the inherited
		helper methods to push them.
		*/
		public void Digit (byte number)
		{
			if ((number < 0) || (number > 9))
				throw new ArgumentException ("mumber must be between 0-9");
			GetInvokePattern (GetButton (number.ToString ())).Invoke ();
		}
		/*
		## Reading the Result
		To get the result displayed currently, we just read the name property
		of the automation element we stored previously. The following helper
		method returns the result as string.
		*/
		public string GetResult ()
		{
			return _result.GetCurrentPropertyValue (
				AutomationElement.NameProperty).ToString ();
		}
		/*
		We need to convert the result to a double to implement the ICalculator
		interface's `Display` property. We try to parse the result and if we
		fail, we return `NaN`.
		*/
		public double Display => 
			double.TryParse (GetResult (), out double result) ? 
				result : 
				double.NaN;
		/*
		To check if a result is available, we again just try to parse it.
		*/
		public bool ResultAvailable =>
			double.TryParse (GetResult (), out double result);
		/*
		## Pushing Operator Buttons

		The operator buttons are also named quite logically in the automation
		element tree, so pushing them is just a matter of calling the inherited
		`PushButton` method with the correct name.
		*/
		public void Add ()
		{
			PushButton ("Add");
		}

		public void Clear ()
		{
			PushButton ("Clear");
		}

		public void Divide ()
		{
			PushButton ("Divide");
		}

		public void Multiply ()
		{
			PushButton ("Multiply");
		}

		public void Subtract ()
		{
			PushButton ("Subtract");
		}

		public void Equals ()
		{
			PushButton ("Equals");
		}
	}
}
/*
## Next Steps
That was pretty easy. Now we can do the same drill for Windows 10 calculator.
*/
