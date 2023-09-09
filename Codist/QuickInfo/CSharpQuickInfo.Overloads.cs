using System;
using System.Linq;
using System.Threading;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	partial class CSharpQuickInfo
	{
		static void ShowOverloadsInfo(InfoContainer qiContent, SyntaxNode node, IMethodSymbol method, SemanticModel semanticModel, CancellationToken cancellationToken) {
			var overloads = node.Kind().CeqAny(SyntaxKind.MethodDeclaration, SyntaxKind.ConstructorDeclaration)
				? method.ContainingType.GetMembers(method.Name)
				: semanticModel.GetMemberGroup(node, cancellationToken);
			if (overloads.Length < 2) {
				return;
			}
			var re = method.MethodKind == MethodKind.ReducedExtension;
			method = method.OriginalDefinition;
			if (re) {
				method = method.ReducedFrom;
			}
			var mst = method.IsStatic;
			var mmod = method.GetSpecialMethodModifier();
			var rt = method.ReturnType;
			var mps = method.Parameters;
			var ct = method.ContainingType;
			var overloadInfo = new ThemedTipDocument().AppendTitle(IconIds.MethodOverloads, R.T_MethodOverload);
			foreach (var overload in overloads) {
				var om = overload.OriginalDefinition as IMethodSymbol;
				if (om == null) {
					continue;
				}
				var ore = re && om.MethodKind == MethodKind.ReducedExtension;
				if (ore) {
					if (method.Equals(om.ReducedFrom)) {
						continue;
					}
				}
				else if (om.ReducedFrom != null) {
					om = om.ReducedFrom;
				}
				if (om.Equals(method)) {
					continue;
				}
				var t = new ThemedTipText();
				var st = om.IsStatic;
				if (st) {
					t.Append("static ".Render((st == mst ? SymbolFormatter.SemiTransparent : SymbolFormatter.Instance).Keyword));
				}
				var mod = om.GetSpecialMethodModifier();
				if (mod != null) {
					t.Append(mod.Render((mod == mmod ? SymbolFormatter.SemiTransparent : SymbolFormatter.Instance).Keyword));
				}
				if (om.MethodKind != MethodKind.Constructor) {
					t.AddSymbol(om.ReturnType, false, CodeAnalysisHelper.AreEqual(om.ReturnType, rt, false) ? SymbolFormatter.SemiTransparent : __SymbolFormatter).Append(" ");
				}
				if (ore) {
					t.AddSymbol(om.ReceiverType, "this", (om.ContainingType != ct ? __SymbolFormatter : SymbolFormatter.SemiTransparent).Keyword).Append(".", SymbolFormatter.SemiTransparent.PlainText);
				}
				else if (om.ContainingType != ct) {
					t.AddSymbol(om.ContainingType, false, __SymbolFormatter).Append(".", SymbolFormatter.SemiTransparent.PlainText);
				}
				t.AddSymbol(om, true, SymbolFormatter.SemiTransparent);
				t.Append("(", SymbolFormatter.SemiTransparent.PlainText);
				foreach (var op in om.Parameters) {
					var mp = mps.FirstOrDefault(p => p.Name == op.Name);
					if (op.Ordinal == 0) {
						if (ore == false && om.IsExtensionMethod) {
							t.Append("this ", __SymbolFormatter.Keyword);
						}
					}
					else {
						t.Append(", ", SymbolFormatter.SemiTransparent.PlainText);
					}
					if (mp != null) {
						if (mp.RefKind != op.RefKind
							|| CodeAnalysisHelper.AreEqual(mp.Type, op.Type, false) == false
							|| mp.IsParams != op.IsParams
							|| mp.IsOptional != op.IsOptional
							|| mp.HasExplicitDefaultValue != op.HasExplicitDefaultValue) {
							mp = null;
						}
					}
					t.AddSymbolDisplayParts(op.ToDisplayParts(CodeAnalysisHelper.InTypeOverloadDisplayFormat), mp == null ? __SymbolFormatter : SymbolFormatter.SemiTransparent, -1);
				}
				t.Append(")", SymbolFormatter.SemiTransparent.PlainText);
				overloadInfo.Append(new ThemedTipParagraph(overload.GetImageId(), t));
			}
			if (overloadInfo.ParagraphCount > 1) {
				qiContent.Add(overloadInfo);
			}
		}
	}
}
