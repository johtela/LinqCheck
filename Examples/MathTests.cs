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
			(from x in Prop.ForAll<int> ()
			 from y in Prop.ForAll<int> ()
			 from z in Prop.ForAll<int> ()
			 let minxy = Math.Min (x, y)
			 select new { x, y, z, minxy })
			.Check (t => t.minxy == t.x || t.minxy == t.y)
			.Check (t => t.minxy <= t.x && t.minxy <= t.y)
			.Check (t => t.minxy == Math.Min (t.y, t.x))
			.Check (t => (Math.Min (t.minxy, t.z) == t.z).Implies (t.z <= t.x && t.z <= t.y));
		}
	}
}
