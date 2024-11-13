using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.CS12_0;

class InlineArray
{
	[System.Runtime.CompilerServices.InlineArray(10)]
	public struct Buffer
	{
		private int _element0;
	}

	InlineArray() {
		var buffer = new Buffer();
		for (int i = 0; i < 10; i++) {
			buffer[i] = i;
		}
	}

}

