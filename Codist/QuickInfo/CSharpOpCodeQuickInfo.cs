using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Imaging;

namespace Codist.QuickInfo
{
	static class CSharpOpCodeQuickInfo
	{
		static readonly Dictionary<string, OpCode> _OpCodes = LoadOpCodes();

		static Dictionary<string, OpCode> LoadOpCodes() {
			var d = new Dictionary<string, OpCode>(226);
			foreach (var p in typeof(OpCodes).GetFields()) {
				if (p.FieldType == typeof(OpCode)) {
					d.Add(p.Name, (OpCode)p.GetValue(null));
				}
			}
			return d;
		}

		public static void ShowOpCodeInfo(this QiContainer container, IFieldSymbol property) {
			if (_OpCodes.TryGetValue(property.Name, out OpCode code)) {
				container.Add(new ThemedTipDocument()
					.AppendTitle(IconIds.OpCodes, code.OpCodeType.ToString() + " " + code.Name)
					.Append(new ThemedTipParagraph(new ThemedTipText("Value: 0x" + code.Value.ToString("X2"))
						.AppendLine().Append("Operand type: " + code.OperandType.ToString())
						.AppendLine().Append("Stack: " + code.StackBehaviourPop.ToString() + ", " + code.StackBehaviourPush.ToString())
						.AppendLine().Append("Flow control: " + code.FlowControl.ToString())
						))
				);
			}
		}
	}
}
