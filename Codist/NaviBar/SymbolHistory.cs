using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codist.CodeBar
{
	readonly struct SymbolHistory : IEquatable<SymbolHistory>
	{
		internal static List<SymbolHistory> Records { get; } = new List<SymbolHistory>();
		internal static void Add(string document) {
			for (int i = 0; i < Records.Count; i++) {
				if (String.Equals(Records[i].Document, document, StringComparison.OrdinalIgnoreCase)) {
					Records.RemoveAt(i);
					break;
				}
			}
			Records.Insert(0, new SymbolHistory(document, 1, 1));
			if (Records.Count > 10) {
				Records.RemoveAt(10);
			}
		}

		public readonly string Document;
		public readonly int Line, Column;

		public SymbolHistory(string document, int line, int column) {
			Document = document;
			Line = line;
			Column = column;
		}

		public override bool Equals(object obj) {
			return AppHelpers.ClrHacker.DirectCompare(this, (SymbolHistory)obj);
		}

		public bool Equals(SymbolHistory other) {
			return AppHelpers.ClrHacker.DirectCompare(this, other);
		}

		public override int GetHashCode() {
			var hashCode = 1439312346;
			hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Document);
			hashCode = hashCode * -1521134295 + Line.GetHashCode();
			hashCode = hashCode * -1521134295 + Column.GetHashCode();
			return hashCode;
		}

		public static bool operator ==(SymbolHistory history1, SymbolHistory history2) {
			return history1.Equals(history2);
		}

		public static bool operator !=(SymbolHistory history1, SymbolHistory history2) {
			return !(history1 == history2);
		}
	}
}
