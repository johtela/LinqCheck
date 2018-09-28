/*
# Using UI Automation
It is a little known fact that .NET framework includes an ample UI automation 
library. This library resides under the System.Windows.Automation namespace. 
Through the facilities provided by the library you can control applications 
with Windows Forms, WPF, or plain old Win32 GUIs. So, essentially all types of 
Windows applications are supported. For a good introduction about the 
automation framework refer to 
[this article](https://www.codeproject.com/Articles/141842/Automate-your-UI-using-Microsoft-Automation-Framew)

Classes under the Automation namespace can also be used to write automated 
tests that run the full application as the user sees it. Very few people seem 
to use them for this purpose, though. Instead, a lot of effort is spent writing 
mocks and layering the UI code to make it more testable. In many cases using UI 
automation could actually be simpler and faster approach. It saves us from the 
burden of complicating the UI and maintaining the mock code. It also tests the 
actual production code and covers all the parts of the application stack, not 
just the UI.

We define a class called Automated which provides basic functionality for 
discovering UI elements and interacting with them. By inheriting the class, you 
can create a controller for a specific application.
*/
namespace Examples.UITests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Windows.Automation;

	public class Automated
	{
		/*
		## Information About the Application
		The fields in the class store information about the application we are
		automating. The first piece of information is the process handle.
		*/
		protected Process _process;
		/*
		Logically an application is composed of automation elements which are
		structured as a tree. The root of the tree is stored in the following
		field.
		*/
		protected AutomationElement _root;
		/*
		The elements which we want to use are stored in a dictionary with 
		string as keys.
		*/
		private Dictionary<string, AutomationElement> _elements =
			new Dictionary<string, AutomationElement> ();
		/*
		The stopwatch is needed for implementing timeout functionality.
		*/
		private Stopwatch stopwatch = new Stopwatch ();
		/*
		## Starting and Stopping the Application
		To start the application a subclass can call the `StartApp` method. It
		takes the application name and the name of the root element as 
		arguments. If the application could not be started, or the root element 
		is not found, an exception is thrown. If everything goes ok, the 
		`_process` and `_root` fields should be set after the method call.
		*/
		protected void StartApp (string appName, string rootElem, 
			int timeoutInMs = 5000)
		{
			_process = Process.Start (appName);
			stopwatch.Reset ();
			stopwatch.Start ();
			do
			{
				_root = AutomationElement.RootElement.FindFirst (
					TreeScope.Children, new PropertyCondition (
						AutomationElement.NameProperty, rootElem));
				Thread.Sleep (100);
			}
			while (_root == null && 
				stopwatch.ElapsedMilliseconds < timeoutInMs);
			if (_root == null)
				throw new TimeoutException (appName + " could not be started");
		}
		/*
		The automated application is stopped by closing its main window. 
		Sometimes this does not quit the application, as we will see with the
		Windows 10 calculator. We could forcefully kill the process in that
		case, but luckily we have other options too.
		*/
		protected void QuitApp ()
		{
			_process.CloseMainWindow ();
			_process.Dispose ();
		}
		/*
		## Controlling UI Elements
		A common operation we want to perform is pushing a button in the UI	of 
		the application. Each UI element has a unique name which identifies it.
		If we know the name of a button, we can push it	programmatically.
		*/
		protected void PushButton (string name)
		{
			GetInvokePattern (GetButton (name)).Invoke ();
		}
		/*
		Another common task is checking if an UI element is enabled. This can
		be done by calling the following method.
		*/
		protected bool IsEnabled (AutomationElement element)
		{
			return (bool)element.GetCurrentPropertyValue (
				AutomationElement.IsEnabledProperty);
		}
		/*
		We can wait that a button becomes enabled by using the method below. It
		will throw an exception, if the element is not enabled until the 
		specified time has elapsed.
		*/
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
		/*
		To be able to interact with an UI element (e.g. push a button), we 
		need to get its invoke pattern. The following method returns that. It
		waits a second, if needed, that the button becomes enabled. This is
		needed since in many cases there is a slight delay before elements
		get enabled after some operation is performed.
		*/
		protected InvokePattern GetInvokePattern (AutomationElement element)
		{
			WaitEnabled (element, 1000);

			return element.GetCurrentPattern (InvokePattern.Pattern) as
				InvokePattern;
		}
		/*
		You can get the automation element corresponding a button with the
		following method. It searches the tree of elements for the given name. 
		If it finds it, the method stores the element reference in the 
		`_elements`	dictionary. So, the next time you ask for the same 
		button it can be returned more quickly from the dictionary.
		*/
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
/*
## Next Steps
Now we have all the pieces of the puzzle to build the automation controllers to
the Windows calculators. Those will be implemented next.
*/