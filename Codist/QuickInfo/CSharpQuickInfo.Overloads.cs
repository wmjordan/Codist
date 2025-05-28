using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	partial class CSharpQuickInfo
	{
		static void ShowOverloadsInfo(Context context, IMethodSymbol method) {
			var overloads = method.ContainingType.GetMembers(method.Name);
			if (overloads.Length < 2) {
				return;
			}
			ShowOverloadsInfo(context.Container, method, overloads);
		}

		static void ShowOverloadsInfo(InfoContainer container, IMethodSymbol method, System.Collections.Immutable.ImmutableArray<ISymbol> overloads) {
			const int MaxOverloadCount = 64;
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
			var overloadInfo = new GeneralInfoBlock(IconIds.MethodOverloads, R.T_MethodOverload);
			var count = 0;
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
				if (++count > MaxOverloadCount) {
					overloadInfo.Add(new BlockItem().Append(R.T_TooManyOverloads.Replace("<N>", overloads.Length.ToText())));
					break;
				}
				var t = new BlockItem() { IconId = overload.GetImageId() };
				var st = om.IsStatic;
				if (st) {
					t.Append("static ", (st == mst ? SymbolFormatter.SemiTransparent : SymbolFormatter.Instance).Keyword);
				}
				var mod = om.GetSpecialMethodModifier();
				if (mod != null) {
					t.Append(mod, (mod == mmod ? SymbolFormatter.SemiTransparent : SymbolFormatter.Instance).Keyword);
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
					t.AddSymbolDisplayParts(op.ToDisplayParts(CodeAnalysisHelper.InTypeOverloadDisplayFormat), mp == null ? __SymbolFormatter : SymbolFormatter.SemiTransparent);
				}
				t.Append(")", SymbolFormatter.SemiTransparent.PlainText);
				overloadInfo.Add(t);
			}
			if (overloadInfo.HasItem) {
				container.Add(overloadInfo);
			}
		}
	}
}
