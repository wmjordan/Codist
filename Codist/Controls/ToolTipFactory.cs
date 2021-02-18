using System;
using System.Linq;
using System.Windows.Controls;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Utilities;
using R = Codist.Properties.Resources;

namespace Codist
{
	static class ToolTipFactory
	{
		public static ThemedToolTip CreateToolTip(ISymbol symbol, bool forMemberList, SemanticContext context) {
			var tip = new ThemedToolTip();
			if (Config.Instance.DisplayOptimizations.MatchFlags(DisplayOptimizations.CodeWindow)) {
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
				content.Append(R.T_MemberType)
					.Append(t.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true);
			}
			else if (symbol.Kind == SymbolKind.TypeParameter) {
				content.Append(R.T_DefinedInType)
					.Append(symbol.ContainingSymbol?.ToDisplayString(CodeAnalysisHelper.MemberNameFormat) ?? String.Empty, true);
				var tp = symbol as ITypeParameterSymbol;
				if (tp.HasConstraint()) {
					content.AppendLine().Append(R.T_Constraint);
					SymbolFormatter.Instance.ShowTypeConstaints(tp, content);
				}
			}
			t = symbol.ContainingType;
			if (t != null && t.TypeKind != TypeKind.Enum) {
				content.AppendLineBreak()
					.Append(t.GetSymbolKindName(), SymbolFormatter.Instance.Keyword).Append(": ")
					.Append(t.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true);
			}
			if (forMemberList == false) {
				content.AppendLineBreak()
					.Append(R.T_Namespace + symbol.ContainingNamespace?.ToString()).AppendLine();
				if (symbol.Kind == SymbolKind.Namespace) {
					var sol = context.Workspace.CurrentSolution;
					content.Append(R.T_Assembly).Append(String.Join(", ", ((INamespaceSymbol)symbol).ConstituentNamespaces.Select(n => n.GetAssemblyModuleName()).Distinct()))
						.AppendLine().Append(R.T_Project).Append(String.Join(", ", symbol.GetSourceReferences().Select(r => sol.GetDocument(r.SyntaxTree).Project).Distinct().Select(p => p.Name)))
						.AppendLine().Append(R.T_Location).Append(symbol.Locations.Length);
				}
				else {
					if (symbol.HasSource()) {
						content.Append(R.T_SourceFile).Append(String.Join(", ", symbol.GetSourceReferences().Select(r => System.IO.Path.GetFileName(r.SyntaxTree.FilePath))));
						var sol = context.Workspace.CurrentSolution;
						content.AppendLine().Append(R.T_Project).Append(String.Join(", ", symbol.GetSourceReferences().Select(r => sol.GetDocument(r.SyntaxTree).Project).Distinct().Select(p => p.Name)));
					}
					else {
						content.Append(R.T_Assembly).Append(symbol.GetAssemblyModuleName());
					}

					if (symbol.Kind == SymbolKind.NamedType) {
						switch (((INamedTypeSymbol)symbol).TypeKind) {
							case TypeKind.Delegate:
								ShowDelegateSignature(content, (INamedTypeSymbol)symbol);
								break;
							case TypeKind.Enum:
								ShowEnumType(content, symbol);
								break;
						}
					}
				}
			}
			ShowAttributes(symbol, content);
			if (context.SemanticModel?.Compilation != null && Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.XmlDocSummary)) {
				ShowXmlDocSummary(symbol, context.SemanticModel.Compilation, tip);
			}
			ShowNumericForms(symbol, tip);
			return tip;
		}

		static void ShowDelegateSignature(TextBlock content, INamedTypeSymbol type) {
			content.AppendLineBreak().Append(R.T_Signature);
			var invoke = type.OriginalDefinition.DelegateInvokeMethod;
			content.AddSymbol(invoke.ReturnType, false, SymbolFormatter.Instance)
				.Append(" ").AddSymbol(type, true, SymbolFormatter.Instance)
				.AddParameters(invoke.Parameters, SymbolFormatter.Instance);
		}

		static void ShowEnumType(TextBlock content, ISymbol symbol) {
			var t = ((INamedTypeSymbol)symbol).EnumUnderlyingType.ToDisplayString(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat);
			if (t != "int") {
				content.AppendLineBreak().Append(R.T_Type + t);
			}
		}

		static void ShowAttributes(ISymbol symbol, TextBlock content) {
			if (Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.Attributes)) {
				foreach (var attr in symbol.GetAttributes()) {
					SymbolFormatter.Instance.Format(content.AppendLine().Inlines, attr, false);
				}
				if (symbol.Kind == SymbolKind.Method) {
					foreach (var attr in ((IMethodSymbol)symbol).GetReturnTypeAttributes()) {
						SymbolFormatter.Instance.Format(content.AppendLine().Inlines, attr, true);
					}
				}
			}
		}

		static void ShowNumericForms(ISymbol symbol, ThemedToolTip tip) {
			if (Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.NumericValues)) {
				var f = symbol as IFieldSymbol;
				if (f != null && f.IsConst) {
					var p = ShowNumericForms(f);
					if (p != null) {
						tip.AddBorder().Child = p;
					}
				}
			}
		}

		static void ShowXmlDocSummary(ISymbol symbol, Compilation compilation, ThemedToolTip tip) {
			var doc = new XmlDoc(symbol, compilation);
			var summary = doc.GetDescription(symbol)
				?? (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DocumentationFromInheritDoc) ? doc.GetInheritedDescription(symbol, out doc) : null);
			if (summary != null) {
				var docContent = tip.AddTextBlock();
				new XmlDocRenderer(compilation, SymbolFormatter.Instance).Render(summary, docContent);
				if (Config.Instance.QuickInfoMaxWidth >= 100) {
					tip.MaxWidth = Config.Instance.QuickInfoMaxWidth;
				}
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
