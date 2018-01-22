using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqCheck;

namespace Examples
{
	class RunTests
	{
		static void Main (string[] args)
		{
			Tester.RunTestsTimed (new BasicTests (), new SeqTests ());
		}
	}
}
