/*
# Test State and Phases

When properties are executed, an object representing the state of the test is 
supplied to them. This object contains all the necessary information required 
to evaluate the property. The state object is mutable; properties modify it as 
they are run.
*/
namespace LinqCheck
{
    using System;
    using System.Collections.Generic;
	/*
	## Test Phases
	```mermaid
	graph TD
		GEN[Generate test case]
		PASS{Conditions satisfied?}
		SHRINK[Produce shrunk test case]
		FAIL{Property failed?}
		DONE((Done))
		GEN --> PASS
		PASS -->|yes|GEN
		PASS -->|no|SHRINK
		PASS -->|all cases passed|DONE
		SHRINK --> FAIL
		FAIL --> |no|SHRINK
		FAIL --> |yes|DONE
	```
	Properties can be evaluated in three different contexts. These correspond 
	to the phases of the test execution. The whole process is outlined on the 
	figure on the right.

	At first when a property is executed, it produces new test cases and 
	evaluates that all specified conditions hold. If they do, a new case is
	generated until a predefined number of cases has passed.

	If, however, one of the cases fails, then we jump to the shrinking step.
	That step first produces a (lazy) list of shrunk versions of each input 
	parameter. Then it produces one combination of shrunk input parameters at
	a time, and checks if that combination makes the property fail. If it does,
	then that test case is reported to the user, and we are done. On the other
	hand, if the propery does not fail, we go back and produce a new shrunk
	alternative. 
	
	Eventually, if all the shrunk test cases pass, we use the case that caused 
	the property to fail in the first place. It is more probable, however, that 
	some of the shrunk alternative has already made the property to fail before 
	this happens.

	We define three test phases that control the property evaluation:
	* **Generate** - Property generates new random test values and evaluates
	  them normally.
	* **StartShrink** - Property produces an enumerable of shrunk alternatives
	  for each variable generated in the failing case.
	* **Shrink** - Property takes its input from the enumerable of shrunk 
	  variables, and checks if it fails with those.

	The enumeration of test phases is defined below.
	*/
	public enum TestPhase { Generate, StartShrink, Shrink }
	/*
	## Test State
	The phase is naturally the first piece of data we define for the state 
	object.	
	*/
    public class TestState
    {
        public readonly TestPhase Phase;
		/*
		### Input for Random Generation
		In the first phase we need the input for the random value generators. 
		The following three fields contain the random number generator, its 
		seed, and the current size used with generators.
		*/
        public readonly Random Random;
		public int Seed;
        public int Size;
		/*
		### Property Label
		The property label is used when outputting information to console. The
		label is stored along with the state object.
		*/
        public string Label;
		/*
		### Test Counters
		The two fields below contain counters how many tests have passed and
		have been discarded so far.
		*/
        public int SuccessfulTests;
        public int DiscardedTests;
		/*
		### Test Case Classification
		When the `orderby` clause is used in the property, the test cases are
		classified to groups based on the specified attribute. The groups are
		stored in the `Classes` dictionary defined below.
		*/
        public SortedDictionary<string, int> Classes = 
			new SortedDictionary<string,int> ();
		/*
		### Generated Values
		The generated variables are stored in the `Values` list. Note that all 
		the values are essentially untyped; they are stored as objects. Since 
		all properties have an associated type, we can cast the values to 
		appropriate type when the properties need them.
		*/
		public readonly List<object> Values;
		/*
		Another piece of information needed is the index of the item in the 
		`Values` list we are generating/using currently. It is stored int the 
		field below.
		*/
		public int CurrentValue;
		/*
		### Shrunk Test Data
		The shrunk test data is kept in the `ShrunkValues` list. It contains 
		all the shrunk values of generated variables. Note that they are stored 
		in enumerables, so that the actual values are generated lazily as the 
		enumerables are traversed.
		*/
		public readonly List<IEnumerable<object>> ShrunkValues;
		/*
		### Constructors
		We provide two simple constructors. They initialize all the fields 
		according to specified values or to their default values.
		*/
		public TestState (TestPhase phase, int seed, int size, string label) :
            this (phase, seed, size, label, null, null)
        {}

        public TestState(TestPhase phase, int seed, int size, string label, 
			List<object> values, List<IEnumerable<object>> shrunkValues)
        {
            Phase = phase;
            Random = new Random(seed);
			Seed = seed;
            Size = size;
			Label = label;
            Values = values ?? new List<object>();
            ShrunkValues = shrunkValues;
        }
		/*
		### Resetting State
		We need to reset the values between each property run in the generation
		phase. The following method clears the `Values` list and initializes 
		the index back to zero.
		*/
        public void ResetValues ()
        {
            if (Phase == TestPhase.Generate)
            {
                CurrentValue = 0;
                Values.Clear();
            }
        }
    }
}
/*
## Few Remarks about the State
One must be quite careful when updating the state in the properties. The state
is mutable and can cause problems if it is not behaving deterministically. Most
of the fields in the object do not change after the state is initialized, but
the random number generator, and generated values obviously do. So, we need to
manage their side-effects carefully.
*/