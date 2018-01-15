namespace Examples
{
	using System;
	using ExtensionCord;
	using LinqCheck;

	public class MathTests
	{
		[Test]
		public void TestMin()
		{
			var tc = from x in TestCase.ForAll<int> ()
					 from y in TestCase.ForAll<int> ()
					 from z in TestCase.ForAll<int> ()
					 let minxy = Math.Min (x, y)
					 let minyx = Math.Min (y, x)
					 let min_minxy_z = Math.Min (minxy, z)
					 select new { x, y, z, minxy, minyx, min_minxy_z };

			tc.Label ("Minimum of x and y is either of the values")
				.Check (_ => 
					_.minxy == _.x || _.minxy == _.y);
			tc.Label ("Minimum of x and y is smaller of the values")
				.Check (_ => 
					_.minxy <= _.x && _.minxy <= _.y);
			tc.Label ("Minimum is symmetric: min (x, y) = min (y, x)")
				.Check (_ => 
					_.minxy == _.minyx);
			tc.Label ("min (min (x, y), z) = z ==> z <= x and z <= y")
				.Check (_ => 
					(_.min_minxy_z == _.z).Implies (_.z <= _.x && _.z < _.y));
		}
	}
}
