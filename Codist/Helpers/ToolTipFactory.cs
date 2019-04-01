using System;
using System.Windows.Controls;
using AppHelpers;
using Microsoft.CodeAnalysis;

namespace Codist
{
	static class ToolTipFactory
	{
		public static Controls.ThemedToolTip CreateToolTip(ISymbol symbol, Compilation compilation) {
			var tip = new Controls.ThemedToolTip();
			if ((Config.Instance.DisplayOptimizations & DisplayOptimizations.CodeWindow) != 0) {
				WpfHelper.SetUITextRenderOptions(tip, true);
			}
			tip.Title
				.Append(symbol.GetAccessibility() + symbol.GetAbstractionModifier() + symbol.GetSymbolKindName() + " ")
				.Append(symbol.Name, true)
				.Append(symbol.GetParameterString());
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
				content.Append("\nconst: " + f.ConstantValue?.ToString()); // sometimes the const value could be null
			}
			foreach (var attr in symbol.GetAttributes()) {
				SymbolFormatter.Empty.ToUIText(content.AppendLine().Inlines, attr);
			}
			var doc = new XmlDoc(symbol, compilation);
			var summary = doc.Summary ?? (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DocumentationFromInheritDoc) ? doc.ExplicitInheritDoc?.Summary : null);
			if (summary != null) {
				content.AppendLine(true);
				new XmlDocRenderer(compilation, SymbolFormatter.Empty, symbol).Render(summary, content.Inlines);
				tip.MaxWidth = Config.Instance.QuickInfoMaxWidth;
			}
			return tip;
		}

		internal static TTarget SetTipOptions<TTarget>(this TTarget target)
			where TTarget : System.Windows.DependencyObject {
			ToolTipService.SetBetweenShowDelay(target, 0);
			ToolTipService.SetInitialShowDelay(target, 1000);
			ToolTipService.SetShowDuration(target, 15000);
			return target;
		}
	}
}
