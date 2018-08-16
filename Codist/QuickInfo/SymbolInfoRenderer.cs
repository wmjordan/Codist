using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.CodeAnalysis;

namespace Codist.QuickInfo
{
	static class SymbolInfoRenderer
	{
		public static TextBlock ShowSymbolDeclaration(TextBlock info, ISymbol symbol, SymbolFormatter formatter, bool defaultPublic, bool hideTypeKind) {
			if (defaultPublic == false || symbol.DeclaredAccessibility != Accessibility.Public) {
				info.Append(symbol.GetAccessibility(), formatter.Keyword);
			}
			if (symbol.Kind == SymbolKind.Field) {
				ShowFieldDeclaration(symbol as IFieldSymbol, formatter, info);
			}
			else {
				ShowSymbolDeclaration(symbol, formatter, info);
			}
			if (hideTypeKind == false) {
				info.Append(symbol.GetSymbolKindName(), symbol.Kind == SymbolKind.NamedType ? formatter.Keyword : null).Append(" ");
			}
			return info;
		}

		static void ShowFieldDeclaration(IFieldSymbol field, SymbolFormatter formatter, TextBlock info) {
			if (field.IsConst) {
				info.Append("const ", formatter.Keyword);
			}
			else {
				if (field.IsStatic) {
					info.Append("static ", formatter.Keyword);
				}
				if (field.IsReadOnly) {
					info.Append("readonly ", formatter.Keyword);
				}
				else if (field.IsVolatile) {
					info.Append("volatile ", formatter.Keyword);
				}
			}
		}

		static void ShowSymbolDeclaration(ISymbol symbol, SymbolFormatter formatter, TextBlock info) {
			if (symbol.IsAbstract) {
				info.Append("abstract ", formatter.Keyword);
			}
			else if (symbol.IsStatic) {
				info.Append("static ", formatter.Keyword);
			}
			else if (symbol.IsVirtual) {
				info.Append("virtual ", formatter.Keyword);
			}
			else if (symbol.IsOverride) {
				info.Append(symbol.IsSealed ? "sealed override " : "override ", formatter.Keyword);
				ISymbol o = null;
				switch (symbol.Kind) {
					case SymbolKind.Method: o = ((IMethodSymbol)symbol).OverriddenMethod; break;
					case SymbolKind.Property: o = ((IPropertySymbol)symbol).OverriddenProperty; break;
					case SymbolKind.Event: o = ((IEventSymbol)symbol).OverriddenEvent; break;
				}
				if (o != null) {
					INamedTypeSymbol t = o.ContainingType;
					if (t != null && t.IsCommonClass() == false) {
						info.AddSymbol(t, null, formatter).Append(".").AddSymbol(o, null, formatter).Append(" ");
					}
				}
			}
			else if (symbol.IsSealed && (symbol.Kind == SymbolKind.NamedType && (symbol as INamedTypeSymbol).TypeKind == TypeKind.Class || symbol.Kind == SymbolKind.Method)) {
				info.Append("sealed ", formatter.Keyword);
			}
			if (symbol.IsExtern) {
				info.Append("extern ", formatter.Keyword);
			}
		}
	}
}
