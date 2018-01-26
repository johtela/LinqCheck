namespace Examples.UITests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Windows.Automation;

	public class Automated
	{
		protected Process _process;
		protected AutomationElement _root;
		private Dictionary<string, AutomationElement> _elements =
			new Dictionary<string, AutomationElement> ();
		private Stopwatch stopwatch = new Stopwatch ();

		protected void StartApp (string appName, string rootElem)
		{
			_process = Process.Start (appName);
			var cnt = 0;
			do
			{
				_root = AutomationElement.RootElement.FindFirst (
					TreeScope.Children, new PropertyCondition (
						AutomationElement.NameProperty, rootElem));
				Thread.Sleep (100);
			}
			while (_root == null && ++cnt < 50);
			if (_root == null)
				throw new Exception (appName + " could not be started");
		}

		protected void QuitApp ()
		{
			_process.CloseMainWindow ();
			_process.Dispose ();
		}

		protected void PushButton (string name)
		{
			GetInvokePattern (GetButton (name)).Invoke ();
		}

		protected bool IsEnabled (AutomationElement element)
		{
			return (bool)element.GetCurrentPropertyValue (
				AutomationElement.IsEnabledProperty);
		}

		protected void WaitEnabled (AutomationElement element, int timeoutInMs)
		{
			stopwatch.Reset ();
			stopwatch.Start ();
			while (!IsEnabled (element) &&
				stopwatch.ElapsedMilliseconds < timeoutInMs)
				Thread.Sleep (10);
			stopwatch.Stop ();
			if (!IsEnabled (element))
				throw new TimeoutException (
					"Timed out when waiting the element to get enabled.");
		}

		protected InvokePattern GetInvokePattern (AutomationElement element)
		{
			WaitEnabled (element, 1000);
			return element.GetCurrentPattern (InvokePattern.Pattern) as
				InvokePattern;
		}

		protected AutomationElement GetButton (string name)
		{
			if (_elements.TryGetValue (name, out AutomationElement elem))
				return elem;
			var result = _root.FindFirst (TreeScope.Descendants,
				new PropertyCondition (AutomationElement.NameProperty, name));
			if (result == null)
				throw new ArgumentException (
					"No function button found with name: " + name);
			_elements.Add (name, result);
			return result;
		}
	}
}