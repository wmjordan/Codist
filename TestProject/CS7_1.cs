using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.CS7_1
{
	class Misc
	{
		static async Task Main() {
			await Task.Delay(1);
		}

		void M() {
			int count = 5;
			string label = "Colors used in the map";
			var pair = (count: count, label: label);
			pair = (count, label);

			Func<string, bool> whereClause = default;
		}
	}
}
