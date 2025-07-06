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
		static void ShowInterfaceImplementation(InfoContainer qiContent, ISymbol symbol, IReadOnlyList<ISymbol> explicitImplementations) {
			if ((symbol.DeclaredAccessibility != Accessibility.Public && explicitImplementations.Count == 0)
				|| symbol.ContainingType is null) {
				return;
			}
			var intfs = symbol.GetImplementedInterfaces();
			GeneralInfoBlock info = null;
			if (intfs.Length > 0) {
				info = new GeneralInfoBlock(IconIds.InterfaceImplementation, R.T_Implements);
				foreach (var item in intfs) {
					info.Add(new BlockItem { IconId = item.GetImageId() }.AddSymbolDisplayParts(item.ToDisplayParts(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat), __SymbolFormatter));
				}
			}
			if (explicitImplementations?.Count != 0) {
				intfs = intfs.Clear();
				intfs.AddRange(explicitImplementations.Select(i => i.ContainingType));
				if (intfs.Length > 0) {
					(info ?? (info = new GeneralInfoBlock()))
						.Add(new BlockItem(IconIds.InterfaceImplementation, R.T_ExplicitImplements, true));
					foreach (var item in intfs) {
						info.Add(new BlockItem { IconId = item.GetImageId() }.AddSymbolDisplayParts(item.ToDisplayParts(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat), __SymbolFormatter));
					}
				}
			}
			if (info != null) {
				qiContent.Add(info);
			}
		}

		static void ShowInterfaceMembers(InfoContainer qiContent, INamedTypeSymbol type, INamedTypeSymbol declaredClass) {
			var doc = new GeneralInfoBlock(IconIds.ListMembers, declaredClass != null ? R.T_MemberImplementation : R.T_Member);
			ShowInterfaceMembers(type, declaredClass, doc, false);
			foreach (var item in type.AllInterfaces) {
				ShowInterfaceMembers(item, declaredClass, doc, true);
			}
			if (doc.HasItem) {
				qiContent.Add(doc);
			}
		}

		static void ShowInterfaceMembers(INamedTypeSymbol type, INamedTypeSymbol declaredClass, GeneralInfoBlock doc, bool isInherit) {
			var members = ImmutableArray.CreateBuilder<ISymbol>();
			members.AddRange(type.FindMembers());
			members.Sort(CodeAnalysisHelper.CompareByAccessibilityKindName);
			var isInterface = type.TypeKind == TypeKind.Interface;
			foreach (var member in members) {
				var t = new BlockItem(member.GetImageId());
				if (isInherit) {
					t.AddSymbol(type, false, SymbolFormatter.SemiTransparent).Append(".");
				}
				if (declaredClass != null && member.IsAbstract) {
					var implementation = declaredClass.FindImplementationForInterfaceMember(member);
					if (implementation != null) {
						doc.Add(new BlockItem(implementation.GetImageId()).AddSymbol(implementation, member.GetOriginalName()));
						continue;
					}
					t.AddSymbol(member)
						.Append(new IconSegment(IconIds.MissingImplementation) { Margin = WpfHelper.SmallHorizontalMargin, Opacity = WpfHelper.DimmedOpacity });
				}
				else {
					t.AddSymbol(member);
				}
				if (member.Kind == SymbolKind.Method) {
					t.AddParameters(((IMethodSymbol)member).Parameters);
					if (isInterface && member.IsStatic == false && member.IsAbstract == false) {
						t.Append(" ").AppendIcon(IconIds.DefaultInterfaceImplementation);
					}
				}
				if (member.IsStatic) {
					t.Append(" ").AppendIcon(IconIds.StaticMember);
				}
				doc.Add(t);
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
			var all = new HashSet<ITypeSymbol>(interfaces);
			switch (type.TypeKind) {
				case TypeKind.Class:
					while ((type = type.BaseType) != null) {
						FindInterfacesForType(type, true, type.Interfaces, inheritedInterfaces, all);
					}
					foreach (var item in interfaces) {
						FindInterfacesForType(item, true, item.Interfaces, inheritedInterfaces, all);
					}
					break;
				case TypeKind.Interface:
					foreach (var item in interfaces) {
						FindInterfacesForType(item, false, item.Interfaces, inheritedInterfaces, all);
					}
					FindInterfacesForType(type, false, type.Interfaces, inheritedInterfaces, all);
					break;
				case TypeKind.Struct:
					foreach (var item in interfaces) {
						FindInterfacesForType(item, true, item.Interfaces, inheritedInterfaces, all);
					}
					break;
			}
			if (declaredInterfaces.Count == 0 && inheritedInterfaces.Count == 0) {
				return;
			}
			var info = new GeneralInfoBlock(IconIds.Interface, R.T_Interface);
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

			void ListInterfacesInLogicalOrder(GeneralInfoBlock d, ImmutableArray<INamedTypeSymbol>.Builder di, ImmutableArray<(INamedTypeSymbol intf, ITypeSymbol baseType)>.Builder ii) {
				foreach (var item in di) {
					d.Add(new BlockItem(item.GetImageId()).AddSymbolDisplayParts(item.ToDisplayParts(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat), __SymbolFormatter));
				}
				foreach (var (intf, baseType) in ii) {
					d.Add(new BlockItem(
						intf.IsDisposable() ? IconIds.Disposable : intf.GetImageId())
						.AddSymbolDisplayParts(intf.ToDisplayParts(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat), __SymbolFormatter)
						.Append(" : ", SymbolFormatter.SemiTransparent.PlainText)
						.Append(new IconSegment(baseType.GetImageId()) { Margin = WpfHelper.GlyphMargin, Opacity = SymbolFormatter.TransparentLevel })
						.AddSymbol(baseType, false, SymbolFormatter.SemiTransparent));
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
