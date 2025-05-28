using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject
{
	unsafe class PunctuationsAndOperators
	{
		int[] _array, _array2;
		event EventHandler _event;

		public int this[int x] => _array[x];
		public int this[int x, int y] {
			get => _array[x] * _array2[y];
			set {
				throw new NotSupportedException();
			}
		}
		public event EventHandler MyEvent {
			add => _event += value;
			remove { _event -= value; }
		}

		/// <summary>
		/// Test method for brackets
		/// </summary>
		/// <seealso cref="CS7_3.C"/>
		/// <seealso cref="CS7_3.S"/>
		internal void Brackets(int[] input) {
			var a = new int[1];
			a = new[] { 1 };
			ref var a0 = ref a[0];
			var b = new int[][] { new[] { 1 }, new int[] { 2 } };
			var b0 = b[0];
			var x = "x"[0];
			var i = this[1] + this[2, 3] + input[0];
			var m = new int[2, 3];
		}

		internal void ParenthesesAndBraces<T>((int a, int b) n) {
			int i = 0;
			var tuple = (1, "2");
			var (id, value) = n;
			var tuple2 = (id: 1, value: "2");
			if (nameof(MyEvent).Length != 0
				&& (typeof(T) == tuple.Item1.GetType() || (DayOfWeek)id == DayOfWeek.Sunday)) {
			}
			switch ((DayOfWeek)n.a) {
				case DayOfWeek.Sunday: {
						var x = 2;
					}
					break;
				case DayOfWeek.Monday: {
						var x = 3;
					}
					break;
			}
			{ { /* do nothing block */ } }
			foreach (var x in _array) {
				Console.Write((IConvertible)x);
			}
			for (i = 0; i < 1; i++) {
			}
			while (DateTime.Now.Hour == 1) {
			}
			using (null) {
			}
			try {
				n.a = (unchecked(1 + 2) + checked(1 + 2));
			}
			catch (Exception) {
				throw;
			}
			finally {
				var x = 2;
			}
		}
	}
}
