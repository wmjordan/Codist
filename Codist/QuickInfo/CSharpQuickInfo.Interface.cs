using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	partial class CSharpQuickInfo
	{
		static void ShowInterfaceImplementation<TSymbol>(InfoContainer qiContent, TSymbol symbol, IReadOnlyList<TSymbol> explicitImplementations)
			where TSymbol : class, ISymbol {
			if (symbol.DeclaredAccessibility != Accessibility.Public
				&& explicitImplementations.Count == 0
				|| symbol.ContainingType is null) {
				return;
			}
			var interfaces = symbol.ContainingType.AllInterfaces;
			if (interfaces.Length == 0) {
				return;
			}
			var implementedIntfs = ImmutableArray.CreateBuilder<ITypeSymbol>(3);
			ThemedTipDocument info = null;
			var refKind = symbol.GetRefKind();
			var returnType = symbol.GetReturnType();
			var parameters = symbol.GetParameters();
			var typeParams = symbol.GetTypeParameters();
			foreach (var intf in interfaces) {
				foreach (var member in intf.GetMembers(symbol.Name)) {
					if (member.Kind == symbol.Kind
						&& member.DeclaredAccessibility == Accessibility.Public
						&& member.GetRefKind() == refKind
						&& member.MatchSignature(symbol.Kind, returnType, parameters, typeParams)) {
						implementedIntfs.Add(intf);
					}
				}
			}
			if (implementedIntfs.Count > 0) {
				info = new ThemedTipDocument().AppendTitle(IconIds.InterfaceImplementation, R.T_Implements);
				foreach (var item in implementedIntfs) {
					info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
				}
			}
			if (explicitImplementations != null) {
				implementedIntfs.Clear();
				implementedIntfs.AddRange(explicitImplementations.Select(i => i.ContainingType));
				if (implementedIntfs.Count > 0) {
					(info ?? (info = new ThemedTipDocument()))
						.AppendTitle(IconIds.InterfaceImplementation, R.T_ExplicitImplements);
					foreach (var item in implementedIntfs) {
						info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
					}
				}
			}
			if (info != null) {
				qiContent.Add(info);
			}
		}

		static void ShowInterfaceMembers(InfoContainer qiContent, INamedTypeSymbol type, INamedTypeSymbol declaredClass) {
			var doc = new ThemedTipDocument();
			doc.AppendTitle(IconIds.ListMembers, declaredClass != null ? R.T_MemberImplementation : R.T_Member);
			ShowInterfaceMembers(type, declaredClass, doc, false);
			foreach (var item in type.AllInterfaces) {
				ShowInterfaceMembers(item, declaredClass, doc, true);
			}
			if (doc.ParagraphCount > 1) {
				qiContent.Add(doc);
			}
		}

		static void ShowInterfaceMembers(INamedTypeSymbol type, INamedTypeSymbol declaredClass, ThemedTipDocument doc, bool isInherit) {
			var members = ImmutableArray.CreateBuilder<ISymbol>();
			members.AddRange(type.FindMembers());
			members.Sort(CodeAnalysisHelper.CompareByAccessibilityKindName);
			var isInterface = type.TypeKind == TypeKind.Interface;
			foreach (var member in members) {
				var t = new ThemedTipText();
				if (isInherit) {
					t.AddSymbol(type, false, SymbolFormatter.SemiTransparent).Append(".");
				}
				if (declaredClass != null && member.IsAbstract) {
					var implementation = declaredClass.FindImplementationForInterfaceMember(member);
					if (implementation != null) {
						doc.Append(new ThemedTipParagraph(implementation.GetImageId(), t.AddSymbol(implementation, member.GetOriginalName(), false, SymbolFormatter.Instance)));
						continue;
					}
					t.AddSymbol(member, false, SymbolFormatter.Instance)
						.Append(VsImageHelper.GetImage(IconIds.MissingImplementation).WrapMargin(WpfHelper.SmallHorizontalMargin).SetOpacity(WpfHelper.DimmedOpacity));
				}
				else {
					t.AddSymbol(member, false, SymbolFormatter.Instance);
				}
				if (member.Kind == SymbolKind.Method) {
					t.AddParameters(((IMethodSymbol)member).Parameters, SymbolFormatter.Instance);
					if (isInterface && member.IsStatic == false && member.IsAbstract == false) {
						t.Append(" ").AddImage(IconIds.DefaultInterfaceImplementation);
					}
				}
				if (member.IsStatic) {
					t.Append(" ").AddImage(IconIds.StaticMember);
				}
				doc.Append(new ThemedTipParagraph(member.GetImageId(), t));
			}
		}

		static void ShowInterfaces(InfoContainer qiContent, ITypeSymbol type) {
			type = type.OriginalDefinition;
			var interfaces = type.Interfaces;
			var declaredInterfaces = ImmutableArray.CreateBuilder<INamedTypeSymbol>(interfaces.Length);
			var inheritedInterfaces = ImmutableArray.CreateBuilder<(INamedTypeSymbol intf, ITypeSymbol baseType)>(5);
			foreach (var item in interfaces) {
				if (item.DeclaredAccessibility == Accessibility.Public || item.Locations.Any(l => l.IsInSource)) {
					declaredInterfaces.Add(item);
				}
			}
			HashSet<ITypeSymbol> all;
			switch (type.TypeKind) {
				case TypeKind.Class:
					all = new HashSet<ITypeSymbol>(interfaces);
					while ((type = type.BaseType) != null) {
						FindInterfacesForType(type, true, type.Interfaces, inheritedInterfaces, all);
					}
					foreach (var item in interfaces) {
						FindInterfacesForType(item, true, item.Interfaces, inheritedInterfaces, all);
					}
					break;
				case TypeKind.Interface:
					all = new HashSet<ITypeSymbol>(interfaces);
					foreach (var item in interfaces) {
						FindInterfacesForType(item, false, item.Interfaces, inheritedInterfaces, all);
					}
					FindInterfacesForType(type, false, type.Interfaces, inheritedInterfaces, all);
					break;
				case TypeKind.Struct:
					all = new HashSet<ITypeSymbol>(interfaces);
					foreach (var item in interfaces) {
						FindInterfacesForType(item, true, item.Interfaces, inheritedInterfaces, all);
					}
					break;
			}
			if (declaredInterfaces.Count == 0 && inheritedInterfaces.Count == 0) {
				return;
			}
			var info = new ThemedTipDocument().AppendTitle(IconIds.Interface, R.T_Interface);
			//ListInterfacesSortedByNamespace(info, declaredInterfaces, inheritedInterfaces);
			ListInterfacesInLogicalOrder(info, declaredInterfaces, inheritedInterfaces);
			qiContent.Add(info);

			void ListInterfacesSortedByNamespace(ThemedTipDocument d, ImmutableArray<INamedTypeSymbol>.Builder di, ImmutableArray<(INamedTypeSymbol intf, ITypeSymbol baseType)>.Builder ii) {
				var allInterfaces = new List<(string, ThemedTipParagraph)>(di.Count + ii.Count);
				foreach (var item in di) {
					allInterfaces.Add((item.ToDisplayString(CodeAnalysisHelper.QualifiedTypeNameFormat), new ThemedTipParagraph(item.GetImageId(), ToUIText(item))));
				}
				foreach (var (intf, baseType) in ii) {
					allInterfaces.Add((intf.ToDisplayString(CodeAnalysisHelper.QualifiedTypeNameFormat), new ThemedTipParagraph(
						intf.GetImageId(),
						ToUIText(intf)
							.Append(" : ", SymbolFormatter.SemiTransparent.PlainText)
							.Append(VsImageHelper.GetImage(baseType.GetImageId()).WrapMargin(WpfHelper.GlyphMargin).SetOpacity(SymbolFormatter.TransparentLevel))
							.AddSymbol(baseType, false, SymbolFormatter.SemiTransparent))));
				}
				allInterfaces.Sort((x, y) => x.Item1.CompareTo(y.Item1));
				foreach (var item in allInterfaces) {
					d.Append(item.Item2);
				}
			}

			void ListInterfacesInLogicalOrder(ThemedTipDocument d, ImmutableArray<INamedTypeSymbol>.Builder di, ImmutableArray<(INamedTypeSymbol intf, ITypeSymbol baseType)>.Builder ii) {
				foreach (var item in di) {
					d.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
				}
				foreach (var (intf, baseType) in ii) {
					d.Append(new ThemedTipParagraph(
						intf.IsDisposable() ? IconIds.Disposable : intf.GetImageId(),
						ToUIText(intf)
							.Append(" : ", SymbolFormatter.SemiTransparent.PlainText)
							.Append(VsImageHelper.GetImage(baseType.GetImageId()).WrapMargin(WpfHelper.GlyphMargin).SetOpacity(SymbolFormatter.TransparentLevel))
							.AddSymbol(baseType, false, SymbolFormatter.SemiTransparent)));
				}
			}
		}

		static void FindInterfacesForType(ITypeSymbol type, bool useType, ImmutableArray<INamedTypeSymbol> interfaces, ImmutableArray<(INamedTypeSymbol, ITypeSymbol)>.Builder inheritedInterfaces, HashSet<ITypeSymbol> all) {
			foreach (var item in interfaces) {
				if (all.Add(item) && IsAccessibleInterface(item)) {
					inheritedInterfaces.Add((item, type));
					FindInterfacesForType(useType ? type : item, useType, item.Interfaces, inheritedInterfaces, all);
				}
			}
		}

		static bool IsAccessibleInterface(INamedTypeSymbol type) {
			return type.DeclaredAccessibility == Accessibility.Public || type.Locations.Any(l => l.IsInSource);
		}
	}
}
