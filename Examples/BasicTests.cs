/*
# Writing Simple Tests

So, how does one begin property based testing with LinqCheck. Let's look at
few very basic tests first. Suppose we want to test the System.Math class. It 
is unlikely we find any bugs in it, but it is a simple static class that 
everyone knows. 

First we need to reference the code we are testing, in this case the System
namespace, as well as the LinqCheck library itself. The third namespace we
reference is [ExtensionCord](https://johtela.github.io/ExtensionCord/) 
which is a general helper library that contains useful extensions for the 
.NET types. That library is optional, but helps writing the properties in
a more compact way.
*/
namespace Examples
{
	using System;
	using System.Linq;
	using ExtensionCord;
	using LinqCheck;

	/*
	We can use any unit testing framework such as [NUnit](http://nunit.org/), 
	[xUnit](https://xunit.github.io/), 
	[MSTest](https://msdn.microsoft.com/en-us/library/hh694602.aspx), etc. to 
	run our tests. In this case, we are using the built-in command line test 
	runner included in LinqCheck. This is the simplest and fastest way to run
	your tests, but it does not scale up very well. For larger test suites a
	more complete framework is recommended.
	*/
	public class BasicTests
	{
		/*
		As in other unit testing tools, the tests are put in a class which 
		methods	are annotated with a special attribute indicating that they 
		are tests. In LinqCheck the attribute is simply `Test`.
		*/
		[Test]
		public void TestMin ()
		{
			/* 
			The first method we test is the `Math.Min`. It returns the 
			minimum of two numbers. So, let's generate two random integers
			to test it. We do this by writing a LINQ expression which 
			calls the `Prop.ForAll` method twice. The random integers are 
			assigned to variables `x` and `y`.
			*/
			(from x in Prop.ForAll<int> ()
			 from y in Prop.ForAll<int> ()
			 /*
			 Now we can call the `Math.Min` to calculate the minimum of `x` 
			 and `y`. We assign the result to variable `minxy`. Notice that 
			 this time we are not generating a random value but just evaluating 
			 an expression, so we use the `let` clause instead of `from`.
			 */
			 let minxy = Math.Min (x, y)
			 /*
			 Let's still generate a thrird arbitrary integer `z` and calculate 
			 the minimum of `x`, `y`, and `z`.
			 */
			 from z in Prop.ForAll<int> ()
			 let minxyz = Math.Min (minxy, z)
			 /*
			 We now got all the values we need, so we complete the LINQ 
			 expression with the `select` clause that returns our values to the 
			 next phase.
			 */
			 select new { x, y, z, minxy, minxyz })
			 /*
			 Essentially, we have now a test case dispenser that produces 
			 three random integers and calculates their minima. Now we can 
			 start thinking about what properties these values should have. 
			 The first property is obvious: `minxy` should contain the minimum 
			 of `x` and `y`. Let's check that this is true.
			 */
			.Check (t => t.minxy == (t.x < t.y ? t.x : t.y))
			/*
			The previous line defines a property that should hold for all input
			values. The `Prop.Check` extension method takes a lambda expression 
			to which the randomly generated test cases are fed, and the expression 
			should evaluate to true if the property holds. If it returns false or
			throws an exception, the property fails. The input values are 
			wrapped in anonymous object that we assign to parameter `t`. The 
			actual values are in the fields of this object.

			Naturally we should not use the `Math.Min` function to check the
			property, since that is the function we are testing. Instead we
			use the ternary `?` operator to do it. 
			
			We don't need to stop here. Let's check that the Min method is
			symmetric, that is, the order of the arguments should not matter.
			*/
			.Check (t => t.minxy == Math.Min (t.y, t.x))
			/*
			Lastly we check that the minimum operation is transitive as the
			`<=` operator is. For this we need the third random value `z`. We
			check that the minimum of `x`, `y`, `z` is the smallest of them. For
			that we utilize the `Enumerable.Min` function and a helper function
			from the ExtensionCord library.
			*/
			.Check (t => t.minxyz == EnumerableExt.Enumerate (t.x, t.y, t.z).Min ());
		}
		/*
		If we now run the tests with the built-in test runner, we should get an
		output similar to this:
		```
		Executing tests for fixture: BasicTests
		'(t.minxy == IIF((t.x < t.y), t.x, t.y))' passed 100 tests. Discarded: 0
		'(t.minxy == Min(t.y, t.x))' passed 100 tests. Discarded: 0
		'(t.minxyz == Enumerate(new [] {t.x, t.y, t.z}).Min())' passed 100 tests. Discarded: 0
		00:00:00.0275867 - TestMin

		All tests passed. 1 tests run in 00:00:00.0307409.		
		```
		The output tells that each property was tested with hundred random 
		inputs. So, we basically covered 300 test cases in about 0.03 seconds.
		
		## Failing Example

		Let's now look at what happen's when a property fails. We test the 
		`Math.Sin` function in a similar manner as above. 
		*/
		[Test]
		public void TestSin ()
		{
			(from x in Prop.ForAll<double> ()
			 let sinx = Math.Sin (x)
			 select new { x, sinx })
			.Check (t => t.sinx.IsBetween (-1.0, 1.0))
			.Check (t => t.sinx.ApproxEquals (Math.Cos (-Math.PI / 2 + t.x)));
		}
		/*
		In this case we generate random values of type `double`, and pass that
		value to the `Math.Sin` method. Then we test that the following 
		properties are true:

		* $\sin (x)$ is in range $[-1, 1]$.
		* Since cosine function is out-of-phase by $\pi \over 2$ with sine
		  function, we test that $\sin (x) = \cos ({\pi \over 2} + x)$.

		However, when we run this test we get an error:
		```
		't.sinx.IsBetween(-1, 1)' passed 100 tests. Discarded: 0
		Falsifiable after 1 tests. Shrinking input..
		Test 'TestSin' failed.
		Reason: Property 't.sinx.ApproxEquals(Cos((1,5707963267949 + t.x)))' failed for input:
		{ x = 3, sinx = 0,141120008059867 }
		   at LinqCheck.Prop.<>c__DisplayClass7_0`2.<Bind>b__0(TestState state) in ...
		```
		When LinqCheck finds a failing input, it first shrinks the test case 
		into a minimal example that still fails. This means that if we remove
		anything from the input, the test will pass. Then it reports the
		faling property, and the input which made the property false.

		In this case the problem is not in the function we are testing, but our
		property is wrong. Cosine is out of phase with sine not by 
		$\pi \over 2$ but $-\pi \over 2$. So, if we change the term to
		`-Math.PI / 2 + t.x` the test should pass.

		It is quite typical that we sometimes get the conditions wrong. However,
		the mistake is quickly pointed out to us by LinqCheck, and it is usually
		easy to spot the error once you see a failing example.
		*/
	}
}
/*
## Next Steps

The basic principle of LinqCheck is quite well captured by these few examples. 
If you are testing pure functions such as the methods defined in the `System.Math`
class, writing properties is usually very straightforward.

However, when testing your own code it is often necessary to define your own
random value generators, or otherwise restrict what values LinqCheck will 
generate. In the following sections we will look at more complex scenarios and 
additional features. 
*/
