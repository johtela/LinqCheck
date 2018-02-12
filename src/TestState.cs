namespace LinqCheck
{
    using System;
    using System.Collections.Generic;

    public enum TestPhase { Generate, StartShrink, Shrink }

    public class TestState
    {
        public readonly TestPhase Phase;
        public readonly Random Random;
		public int Seed;
        public int Size;
        public string Label;
        public int SuccessfulTests;
        public int DiscardedTests;
        public SortedDictionary<string, int> Classes = 
			new SortedDictionary<string,int> ();
        public int CurrentValue;
        public readonly List<object> Values;
        public readonly List<List<object>> ShrunkValues;

        public TestState(TestPhase phase, int seed, int size, string label) :
            this (phase, seed, size, label, null, null)
        {}

        public TestState(TestPhase phase, int seed, int size, string label, 
			List<object> values, List<List<object>> shrunkValues)
        {
            Phase = phase;
            Random = new Random(seed);
			Seed = seed;
            Size = size;
			Label = label;
            Values = values ?? new List<object>();
            ShrunkValues = shrunkValues;
        }

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
