namespace Examples.UITests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Diagnostics;
	using System.Threading;
	using System.Windows.Automation;

	class Win10Calculator : Automated, ICalculator
	{
		private AutomationElement _result;

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

		public void Dispose ()
		{
			PushButton ("Close Calculator");
		}

		public void PushDigitButton (byte digit)
		{
			GetInvokePattern (GetDigitButton (digit)).Invoke ();
		}

		private readonly string[] _digitButtons = { "Zero", "One", "Two",
			"Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };

		public AutomationElement GetDigitButton (byte number)
		{
			if ((number < 0) || (number > 9))
				throw new ArgumentException ("mumber must be a digit 0-9");
			return GetButton (_digitButtons[number]);
		}

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

		public void Digit (byte number)
		{
			if (number > 9)
				throw new ArgumentException ("Invlalid digit", "number");
			PushDigitButton (number);
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
