namespace Examples.UITests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Windows.Automation;

	class Win7Calculator : ICalculator
	{
		private Process _process;
		private AutomationElement _root;
		private AutomationElement _result;
		private Dictionary<string, AutomationElement> _buttons =
			new Dictionary<string, AutomationElement> ();

		public Win7Calculator ()
		{
			_process = Process.Start ("Calc.exe");
			var ct = 0;
			do
			{
				_root = AutomationElement.RootElement.FindFirst (
					TreeScope.Children, new PropertyCondition (
						AutomationElement.NameProperty, "Calculator"));
				Thread.Sleep (100);
			}
			while (_root == null && ++ct < 50);
			if (_root == null)
				throw new Exception ("Calculator could not be started");
			_result = _root.FindFirst (TreeScope.Descendants, 
				new PropertyCondition (AutomationElement.AutomationIdProperty, "150"));
			if (_result == null)
				throw new Exception ("Could not find result box");
			Clear ();
		}

		public void Dispose ()
		{
			_process.CloseMainWindow ();
			_process.Dispose ();
		}

		public void PushButton (string name)
		{
			GetInvokePattern (GetButton (name)).Invoke ();
		}

		public void PushDigitButton (byte digit)
		{
			GetInvokePattern (GetDigitButton (digit)).Invoke ();
		}

		public InvokePattern GetInvokePattern (AutomationElement element)
		{
			return element.GetCurrentPattern (InvokePattern.Pattern) as 
				InvokePattern;
		}

		public AutomationElement GetButton (string name)
		{
			if (_buttons.TryGetValue (name, out AutomationElement elem))
				return elem;
			var result = _root.FindFirst (TreeScope.Descendants, 
				new PropertyCondition (AutomationElement.NameProperty, name));
			if (result == null)
				throw new ArgumentException ("No function button found with name", 
					name);
			_buttons.Add (name, result);
			return result;
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
