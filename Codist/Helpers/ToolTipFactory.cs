using System;
using System.Windows.Controls;
using Microsoft.CodeAnalysis;

namespace Codist
{
	static class ToolTipFactory
	{
		public static Controls.ThemedToolTip CreateToolTip(ISymbol symbol, Compilation compilation) {
			var tip = new Controls.ThemedToolTip();
			tip.Title
				.Append(symbol.GetAccessibility() + symbol.GetAbstractionModifier() + symbol.GetSymbolKindName() + " ")
				.Append(symbol.GetSignatureString(), true);
			var content = tip.Content;
			var t = symbol.GetReturnType();
			if (t != null) {
				content.Append("member type: ")
					.Append(t.ToDisplayString(WpfHelper.MemberNameFormat), true);
			}
			t = symbol.ContainingType;
			if (t != null) {
				if (content.Inlines.FirstInline != null) {
					content.AppendLine();
				}
				content.Append(t.GetSymbolKindName() + ": ")
					.Append(t.ToDisplayString(WpfHelper.MemberNameFormat), true);
			}
			if (content.Inlines.FirstInline != null) {
				content.AppendLine();
			}
			content.Append("namespace: " + symbol.ContainingNamespace?.ToString())
				.Append("\nassembly: " + symbol.GetAssemblyModuleName());
			var f = symbol as IFieldSymbol;
			if (f != null && f.IsConst) {
				content.Append("\nconst: " + f.ConstantValue?.ToString());
			}
			var doc = symbol.GetXmlDocSummaryForSymbol();
			if (doc != null) {
				new XmlDocRenderer(compilation, SymbolFormatter.Empty, symbol).Render(doc, content.Append("\n\n").Inlines);
				tip.MaxWidth = Config.Instance.QuickInfoMaxWidth;
			}
			return tip;
		}

		internal static TTarget SetTipOptions<TTarget>(this TTarget target)
			where TTarget : System.Windows.DependencyObject {
			ToolTipService.SetBetweenShowDelay(target, 1000);
			ToolTipService.SetInitialShowDelay(target, 1000);
			ToolTipService.SetShowDuration(target, 15000);
			return target;
		}
	}
}
