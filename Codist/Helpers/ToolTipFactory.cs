using System;
using System.Linq;
using System.Windows.Controls;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Utilities;

namespace Codist
{
	static class ToolTipFactory
	{
		public static ThemedToolTip CreateToolTip(ISymbol symbol) {
			return CreateToolTip(symbol, false, null);
		}

		public static ThemedToolTip CreateToolTip(ISymbol symbol, bool forMemberList, Compilation compilation) {
			var tip = new ThemedToolTip();
			if ((Config.Instance.DisplayOptimizations & DisplayOptimizations.CodeWindow) != 0) {
				WpfHelper.SetUITextRenderOptions(tip, true);
			}
			if (forMemberList == false) {
				tip.Title.Append(ThemeHelper.GetImage(symbol.GetImageId()).WrapMargin(WpfHelper.GlyphMargin));
			}
			tip.Title
				.Append(symbol.GetAccessibility() + symbol.GetAbstractionModifier() + (symbol as IMethodSymbol).GetSpecialMethodModifier() + symbol.GetSymbolKindName() + " ")
				.Append(symbol.Name, true)
				.Append(symbol.GetParameterString());
			var content = tip.Content;
			var t = symbol.GetReturnType();
			if (t != null) {
				content.Append("member type: ")
					.Append(t.ToDisplayString(WpfHelper.MemberNameFormat), true);
			}
			else if (symbol.Kind == SymbolKind.TypeParameter) {
				content.Append("defined in: ")
					.Append(symbol.ContainingSymbol.ToDisplayString(WpfHelper.MemberNameFormat), true);
				var tp = symbol as ITypeParameterSymbol;
				if (tp.HasConstraint()) {
					content.AppendLine().Append("constraint: ");
					SymbolFormatter.Empty.ShowTypeConstaints(tp, content);
				}
			}
			t = symbol.ContainingType;
			if (t != null) {
				if (content.Inlines.FirstInline != null) {
					content.AppendLine();
				}
				content.Append(t.GetSymbolKindName() + ": ")
					.Append(t.ToDisplayString(WpfHelper.MemberNameFormat), true);
			}
			if (forMemberList == false) {
				if (content.Inlines.FirstInline != null) {
					content.AppendLine();
				}
				content.Append("namespace: " + symbol.ContainingNamespace?.ToString()).AppendLine();
				if (symbol.HasSource()) {
					content.Append("source file: " + String.Join(", ", symbol.GetSourceReferences().Select(r => System.IO.Path.GetFileName(r.SyntaxTree.FilePath))));
				}
				else {
					content.Append("assembly: " + symbol.GetAssemblyModuleName());
				}

				if (symbol.Kind == SymbolKind.NamedType
					&& ((INamedTypeSymbol)symbol).TypeKind == TypeKind.Delegate) {
					ShowDelegateSignature(content, (INamedTypeSymbol)symbol);
				}
			}
			ShowAttributes(symbol, content);
			if (compilation != null && Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.XmlDocSummary)) {
				ShowXmlDocSummary(symbol, compilation, tip);
			}
			ShowNumericForms(symbol, tip);
			return tip;
		}

		static void ShowNumericForms(ISymbol symbol, ThemedToolTip tip) {
			if (Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.NumericValues)) {
				var f = symbol as IFieldSymbol;
				if (f != null && f.IsConst) {
					var p = ShowNumericForms(f);
					if (p != null) {
						tip.Children.Add(p);
					}
				}
			}
		}

		static void ShowDelegateSignature(TextBlock content, INamedTypeSymbol d) {
			content.Append("\nsignature: ");
			var invoke = d.OriginalDefinition.DelegateInvokeMethod;
			content.AddSymbol(invoke.ReturnType, false, SymbolFormatter.Empty)
				.Append(" ").AddSymbol(d, true, SymbolFormatter.Empty)
				.AddParameters(invoke.Parameters, SymbolFormatter.Empty);
		}

		static void ShowAttributes(ISymbol symbol, TextBlock content) {
			if (Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.Attributes)) {
				foreach (var attr in symbol.GetAttributes()) {
					SymbolFormatter.Empty.Format(content.AppendLine().Inlines, attr, false);
				}
				if (symbol.Kind == SymbolKind.Method) {
					foreach (var attr in ((IMethodSymbol)symbol).GetReturnTypeAttributes()) {
						SymbolFormatter.Empty.Format(content.AppendLine().Inlines, attr, true);
					}
				}
			}
		}

		static void ShowXmlDocSummary(ISymbol symbol, Compilation compilation, ThemedToolTip tip) {
			var doc = new XmlDoc(symbol, compilation);
			var summary = doc.Summary ?? (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DocumentationFromInheritDoc) ? doc.ExplicitInheritDoc?.Summary : null);
			if (summary != null) {
				var docContent = tip.AddTextBlock();
				new XmlDocRenderer(compilation, SymbolFormatter.Empty, symbol).Render(summary, docContent);
				tip.MaxWidth = Config.Instance.QuickInfoMaxWidth;
			}
		}

		internal static TTarget SetTipOptions<TTarget>(this TTarget target)
			where TTarget : System.Windows.DependencyObject {
			ToolTipService.SetBetweenShowDelay(target, 0);
			ToolTipService.SetInitialShowDelay(target, 1000);
			ToolTipService.SetShowDuration(target, 15000);
			return target;
		}

		internal static StackPanel ShowNumericForms(SyntaxNode node) {
			return ShowNumericForms(node.GetFirstToken().Value, node.Parent.Kind() == SyntaxKind.UnaryMinusExpression ? NumericForm.Negative : NumericForm.None);
		}

		internal static StackPanel ShowNumericForms(object value) {
			return ShowNumericForms(value, NumericForm.None);
		}

		internal static StackPanel ShowNumericForms(IFieldSymbol symbol) {
			return ShowNumericForms(symbol.ConstantValue, NumericForm.None);
		}

		static StackPanel ShowNumericForms(object value, NumericForm form) {
			if (value == null) {
				return null;
			}
			switch (Type.GetTypeCode(value.GetType())) {
				case TypeCode.Int32: {
					var v = (int)value;
					if (form == NumericForm.Negative) {
						v = -v;
					}
					return ShowNumericForms(
						form == NumericForm.Unsigned ? ((uint)v).ToString() : v.ToString(),
						new byte[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
				}
				case TypeCode.Int64: {
					var v = (long)value;
					if (form == NumericForm.Negative) {
						v = -v;
					}
					return ShowNumericForms(
						form == NumericForm.Unsigned ? ((ulong)v).ToString() : v.ToString(),
						new byte[] { (byte)(v >> 56), (byte)(v >> 48), (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
				}
				case TypeCode.Byte:
					return ShowNumericForms(((byte)value).ToString(), new byte[] { (byte)value });
				case TypeCode.Int16: {
					var v = (short)value;
					if (form == NumericForm.Negative) {
						v = (short)-v;
					}
					return ShowNumericForms(
						form == NumericForm.Unsigned ? ((ushort)v).ToString() : v.ToString(),
						new byte[] { (byte)(v >> 8), (byte)v });
				}
				case TypeCode.Char: {
					var v = (char)value;
					return ShowNumericForms(((ushort)v).ToString(), new byte[] { (byte)(v >> 8), (byte)v });
				}
				case TypeCode.UInt32:
					return ShowNumericForms((int)(uint)value, NumericForm.Unsigned);
				case TypeCode.UInt16:
					return ShowNumericForms((short)(ushort)value, NumericForm.Unsigned);
				case TypeCode.UInt64:
					return ShowNumericForms((long)(ulong)value, NumericForm.Unsigned);
				case TypeCode.SByte:
					return ShowNumericForms(((sbyte)value).ToString(), new byte[] { (byte)(sbyte)value });
			}
			return null;
		}

		static StackPanel ShowNumericForms(string dec, byte[] bytes) {
			return new StackPanel()
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(dec).Add(new ThemedTipText(" DEC", true)))
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(ToHexString(bytes)).Add(new ThemedTipText(" HEX", true)))
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(ToBinString(bytes)).Add(new ThemedTipText(" BIN", true)));
		}

		static string ToBinString(byte[] bytes) {
			using (var sbr = ReusableStringBuilder.AcquireDefault((bytes.Length << 3) + bytes.Length)) {
				var sb = sbr.Resource;
				for (int i = 0; i < bytes.Length; i++) {
					ref var b = ref bytes[i];
					if (b == 0 && sb.Length == 0) {
						continue;
					}
					if (sb.Length > 0) {
						sb.Append(' ');
					}
					sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
				}
				return sb.Length == 0 ? "00000000" : sb.ToString();
			}
		}

		static string ToHexString(byte[] bytes) {
			switch (bytes.Length) {
				case 1: return bytes[0].ToString("X2");
				case 2: return bytes[0].ToString("X2") + bytes[1].ToString("X2");
				case 4:
					return bytes[0].ToString("X2") + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + bytes[3].ToString("X2");
				case 8:
					return bytes[0].ToString("X2") + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + bytes[3].ToString("X2") + " "
						+ bytes[4].ToString("X2") + bytes[5].ToString("X2") + " " + bytes[6].ToString("X2") + bytes[7].ToString("X2");
				default:
					return string.Empty;
			}
		}

		enum NumericForm
		{
			None,
			Negative,
			Unsigned
		}

	}
}
