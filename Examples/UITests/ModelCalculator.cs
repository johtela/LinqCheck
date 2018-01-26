namespace Examples.UITests
{
	using System;

	public class ModelCalculator : ICalculator
	{
		private enum Oper { None, Add, Sub, Mul, Div }

		private double _accum;
		private double? _display;
		private double _lastVal;
		private Oper _lastOper;
		private bool _lastEqual;

		public ModelCalculator ()
		{
			Clear ();
		}

		public void Dispose () { }

		public double Display => _display ?? _accum;

		private void PerformLastOperation (double value)
		{
			switch (_lastOper)
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

		private void Operation (Oper oper)
		{
			if (!ResultAvailable)
				return;
			if (_display.HasValue)
				PerformLastOperation (_display.Value);
			_display = null;
			_lastVal = _accum;
			_lastOper = oper;
			_lastEqual = false;
		}

		public void Add ()
		{
			Operation (Oper.Add);
		}

		public void Clear ()
		{
			_accum = 0;
			_display = 0;
			_lastVal = 0;
			_lastOper = Oper.None;
			_lastEqual = false;
		}

		public void Digit (byte number)
		{
			if (!ResultAvailable)
				return;
			if (_lastEqual)
				Clear ();
			_display = _display.HasValue ?
				_display * 10 + number :
				number;
		}

		public bool ResultAvailable => 
			!(double.IsNaN (_accum) || double.IsInfinity (_accum));

		public void Divide ()
		{
			Operation (Oper.Div);
		}

		public void Equals ()
		{
			if (_display.HasValue)
			{
				PerformLastOperation (_display.Value);
				_lastVal = _display.Value;
			}
			else if (_lastOper != Oper.None)
				PerformLastOperation (_lastVal);
			_display = null;
			_lastEqual = true;
		}

		public void Multiply ()
		{
			Operation (Oper.Mul);
		}

		public void Subtract ()
		{
			Operation (Oper.Sub);
		}
	}
}