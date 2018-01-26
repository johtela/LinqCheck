﻿namespace Examples.UITests
{
	using System;
	using System.Windows.Automation;

	class Win7Calculator : Automated, ICalculator
	{
		private AutomationElement _result;

		public Win7Calculator ()
		{
			StartApp ("Calc.exe", "Calculator");
			_result = _root.FindFirst (TreeScope.Descendants, 
				new PropertyCondition (AutomationElement.AutomationIdProperty, "150"));
			if (_result == null)
				throw new Exception ("Could not find result box");
			Clear ();
		}

		public void Dispose ()
		{
			QuitApp ();
		}

		public void PushDigitButton (byte digit)
		{
			GetInvokePattern (GetDigitButton (digit)).Invoke ();
		}

		public AutomationElement GetDigitButton (byte number)
		{
			if ((number < 0) || (number > 9))
				throw new ArgumentException ("mumber must be a digit 0-9");
			return GetButton (number.ToString ());
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

		public void Digit (byte number)
		{
			if (number > 9)
				throw new ArgumentException ("Invlalid digit", "number");
			PushDigitButton (number);
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
