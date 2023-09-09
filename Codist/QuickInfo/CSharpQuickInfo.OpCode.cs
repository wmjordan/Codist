using System.Collections.Generic;
using System.Reflection.Emit;
using Codist.Controls;
using Microsoft.CodeAnalysis;

namespace Codist.QuickInfo
{
	partial class CSharpQuickInfo
	{
		static readonly Dictionary<string, OpCode> __OpCodes = LoadOpCodes();
		static readonly string[] __EmitNamespace = new[] { "Emit", "Reflection", "System" };

		static Dictionary<string, OpCode> LoadOpCodes() {
			var d = new Dictionary<string, OpCode>(226);
			foreach (var p in typeof(OpCodes).GetFields()) {
				if (p.FieldType == typeof(OpCode)) {
					d.Add(p.Name, (OpCode)p.GetValue(null));
				}
			}
			return d;
		}

		static void ShowOpCodeInfo(InfoContainer container, IFieldSymbol field) {
			if (field.ContainingType.MatchTypeName("OpCodes", __EmitNamespace)
				&& __OpCodes.TryGetValue(field.Name, out OpCode code)) {
				container.Add(new ThemedTipDocument()
					.AppendTitle(IconIds.OpCodes, $"{code.OpCodeType} {code.Name}")
					.Append(new ThemedTipParagraph(new ThemedTipText("Value: 0x" + code.Value.ToString("X2"))
						.AppendLine().Append($"Operand type: {code.OperandType}")
						.AppendLine().Append($"Stack behavior: {code.StackBehaviourPop}, {code.StackBehaviourPush}")
						.AppendLine().Append($"Flow control: {code.FlowControl}")
						))
				);
			}
		}
	}
}
