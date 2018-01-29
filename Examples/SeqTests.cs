/*
# Adding New Arbitrary Types

LinqCheck generates test input automatically for the common data types defined 
in the .NET framework. This includes primitive types such as `bool`, `char`, 
`int`, `long`, `float`, and `double` as well as compound data sructures like 
arrays and IEnumerables. But often these are not enough. Your test data might 
contain user-defined classes, structures, or interfaces.

The random data generation in LinqCheck is based on the idea of composing 
complex data from simpler types. To demonstrate this idea we will generate some
tests for the [Seq](https://johtela.github.io/ExtensionCord/src/Seq.html) 
class defined in the [ExtensionCord](https://johtela.github.io/ExtensionCord/)
library. This is an immutable, singly-linked list designed to be as simple as
possible, so it serves as a good example. The Seq class is also generic, which
allows us to show how tests can be written generically too.

First let's import all the required libraries and namespaces.
*/
namespace Examples
{
	using System.Linq;
	using ExtensionCord;
	using LinqCheck;

	public class SeqTests
	{
		/*
		## Implementing IArbitrary<T>
		There needs to be an implementation of `IArbitrary<T>` interface for 
		each type `T` we wish to use in our tests. This implementation generates 
		random values of type `T`, and shrinks them when LinqCheck is looking 
		for a minimal failing test case.

		In this case we need to define an implementation of `IArbitrary<Seq<T>>`.
		The easiest way to do that is to create an instance `Arbitrary<T>` class,
		which stores the generator and shrinker for type `T`. Let's define a 
		method that does that.
		*/
		public static IArbitrary<Seq<T>> ArbitrarySeq<T> ()
		{
			/*
			First we need to define the generator for `Seq<T>` type. Generators
			have the type `Gen<T>` where T is the type of data produced. They 
			are defined as Linq expressions where complex types are constructed
			with built-in or user-defined combinators. In this case we can 
			utilize the combinator that creates an `Gen<IEnumerable<T>>` given 
			a generator of type `Gen<T>`.

			We don't know yet what is the item type `T` which will be contained
			in the seqeuence. Luckily we don't have to. We can assume that the 
			generator for `T` is already registered with LinqCheck, and get it
			by calling `Arbitrary.Gen<T> ()`.
			*/
			return new Arbitrary<Seq<T>> (
				from e in Arbitrary.Gen<T> ().EnumerableOf ()
				select e.ToSeq (),
				/*
				We also need to provide a way to shrink the failed sequence
				to simpler versions. Again, we take advantage of the fact
				that `Seq<T>` implements `IEnumerable<T>`, so we can use
				the built-in combinator to do the shrinking. Lastly, we
				convert the shrinked IEnumerables back to Seqs.
				*/
				seq =>
					from e in seq.ShrinkEnumerable ()
					select e.ToSeq ()
				);
		}
		/*
		It is that simple! Now we have a generic method that can generate
		arbitrary sequences of any type `T` provided that there is an
		implementation for `IArbitrary<T>`.

		## Testing Properties of Sequences
		So, what properties should all sequences have? Let's start with the 
		easy ones. First we'll check that the First and Rest properties
		are correct. Note that our test method is generic, it can basically
		test this property for any sequences of any item type.
		*/
		public void CheckFirstAndRest<T>()
		{
			/*
			We get a random sequence by calling the method defined above.
			*/
			(from seq in Prop.ForAll (ArbitrarySeq<T> ())
			 /*
			 There is no first item, if the sequence is empty, so we filter 
			 out all such sequences.
			 */
			 where !seq.IsEmpty ()
			 /*
			 Now we can return the test sequence and its first and rest
			 fields.
			 */
			 select new { seq, seq.First, seq.Rest })
			 /*
			 `Enumerable.First ()` extension method gives us the first item of 
			 an IEnumerable which should correspond to the value of the First
			 property.
			 */
			.Check (t => t.First.Equals (t.seq.First ()))
			/*
			We use the `Enumerable.Skip ()` method to skip the first item and
			compare the remaining items to the sequence pointed by the Rest 
			field. We use the helper method defined in the ExtensionCord library 
			to check that the two IEnumerables contain the same items. Note 
			that an empty sequence is represented by `null`, which would crash 
			the `SequenceEqual` method. So, we have to test that the sequence 
			to which the Rest property points to is not empty.
			*/
			.Check (t => t.Rest.IsEmpty () || t.Rest.SequenceEqual (t.seq.Skip (1)));
		}
		/*
		It does not really matter which item type we use to test the sequence, 
		since the it should work the same way with any type. However, it is not 
		possible to run a generic test method, so let's use `int`, `double`, 
		and `string` as example item types. You can use any type for which there
		is an IArbitrary implementation registered.
		*/
		[Test]
		public void TestFirstAndRest ()
		{
			CheckFirstAndRest<int> ();
			CheckFirstAndRest<double> ();
			CheckFirstAndRest<string> ();
		}
		/*
		When we run this test, we should get the following output:
		```
		Executing tests for fixture: SeqTests
		't.First.Equals(Convert(t.seq.First()))' passed 86 tests. Discarded: 14
		'(t.Rest.IsEmpty() OrElse t.Rest.SequenceEqual(t.seq.Skip(1)))' passed 89 tests. Discarded: 11
		't.First.Equals(Convert(t.seq.First()))' passed 95 tests. Discarded: 5
		'(t.Rest.IsEmpty() OrElse t.Rest.SequenceEqual(t.seq.Skip(1)))' passed 90 tests. Discarded: 10
		't.First.Equals(Convert(t.seq.First()))' passed 88 tests. Discarded: 12
		'(t.Rest.IsEmpty() OrElse t.Rest.SequenceEqual(t.seq.Skip(1)))' passed 88 tests. Discarded: 12
		00:00:00.0335073 - TestFirstAndRest

		All tests passed. 3 tests run in 00:00:00.0677541.
		```
		Note that some test cases are now discarded. This happens because 
		we specified that we only want to use sequences that are not empty. The
		where clause essentially throws away inputs which do not satisfy this 
		condition. So, our test set is a bit smaller. 
		
		There are two ways you can make the test set bigger: You can increase 
		the number of test cases generated by providing the second argument to 
		the `Check` method. The other option is to implement new `ArbitrarySeq` 
		so, that it never creates an empty sequence. This would not be desireable 
		in all tests, though.

		## Testing Addition
		So, now we are convinced that an arbitrary sequence generated from 
		IEnumerable is well-formed. What happens if we add an item to it? 
		Let's find out.
		*/
		public void CheckAddition<T> ()
		{
			/*
			First we generate an arbitrary sequence the same way as in the
			previous example.
			*/
			(from seq in Prop.ForAll (ArbitrarySeq<T> ())
			 /*
			 Then we construct a new sequence by adding an arbitrary item to it.
			 */
			 from item in Prop.ForAll<T> ()
			 let newSeq = item | seq
			 /*
			 Then we return all three in the test case.
			 */
			 select new { seq, item, newSeq })
			/*
			Let's first check that the new sequence is one item longer than
			the original. As before, we need to prepare for the possibility
			that the sequence might be `null`. In that case, we cannot use
			the `Count` extension method.
			*/
			.Check (t => t.newSeq.Count () == 
				(t.seq.IsEmpty () ? 0 : t.seq.Count ()) + 1)
			/*
			Then we should verify that the added item is the first item of
			the new sequence.
			*/
			.Check (t => t.newSeq.First.Equals (t.item))
			/*
			Last we should check that the rest of the new sequence is the
			same as the original one. Empty sequences need to be accounted
			for in this check too.
			*/
			.Check (t => t.newSeq.Rest.IsEmpty () || 
				t.newSeq.Rest.SequenceEqual (t.seq));
		}
		/*
		As before, we need to use instantiate few sequences to run the checks.
		*/
		[Test]
		public void TestAddition ()
		{
			CheckAddition<int> ();
			CheckAddition<string> ();
		}
		/*
		## Testing Removal
		The last feature we test is removing an item.
		*/
		public void CheckRemoval<T> ()
		{
			/*
			Let's again generate an arbitrary sequence and make sure it is not
			empty. This time we use the `SuchThat` combinator defined for the 
			`IArbitrary` interface, which filters out all the randomly generated
			values that do not match a given predicate. This has the benefit 
			that no test cases are discarded, but it also makes the test run
			a bit longer. Also, if the predicate is too strict, `SuchThat` 
			might not find a suitable value. In this case the generator will
			fail and throw an exception.
			*/
			(from seq in Prop.ForAll (ArbitrarySeq<T> ().SuchThat (
				s => !s.IsEmpty ()))
			 /*
			 Next we need to select from the sequence an arbitrary item that we
			 can remove. We do this by calling `Prop.Any`. It differs from
			 `Prop.ForAll` in that it takes `Gen<T>` as an argument instead
			 of `IArbitrary<T>`. This means that the random values generated
			 by `Any` are not shrunk, if the test fails. 
			 
			 Also, the values produced by `Any` might depend on the other 
			 generated values. As in this case, the chosen element must be 
			 inside the sequence that we generated previously. It would not 
			 make sense to shrink this value, because then we would probably 
			 loose the failing test case. 
			 
			 In general, we need to make sure that the same input data 
			 provides always the same result, and that our test case is 
			 deterministic. Given the same parameters, `Any` produces always 
			 the same result, whereas `ForAll` will produce a different value 
			 every time it is called.
			 */
			 from item in Prop.Any (Gen.ElementOf (seq))
			 /*
			 Now we can remove the chosen item and return the test case.
			 */
			 let newSeq = seq.Remove (item)
			 select new { seq, item, newSeq })
			/*
			The new sequence should be one item shorter than the original one.
			*/
			.Check (t => t.newSeq.IsEmpty ().Implies (t.seq.Count () == 1) ||
				t.newSeq.Count () == t.seq.Count () - 1)
			/*
			If we still find the same item that was removed in the new sequence,
			it must be a duplicate, and thus appear at least twice.
			*/
			.Check (t => !t.newSeq.Contains (t.item) ||
				t.seq.Count (i => i.Equals (t.item)) > 1);
		}
		/*
		Let's call our check with a few different item types.
		*/
		[Test]
		public void TestRemoval ()
		{
			CheckRemoval<char> ();
			CheckRemoval<int> ();
		}
	}
}
/*
## Next Steps
We went through some simple but powerfull constructs of LinqCheck. Next we 
will show how you can test code which has mutable state, and review few special
constructs provided by the library.
*/
