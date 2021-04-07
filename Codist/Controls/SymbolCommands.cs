using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	static class SymbolCommands
	{
		internal static void FindReferrers(this SemanticContext context, ISymbol symbol, Predicate<ISymbol> definitionFilter = null, Predicate<SyntaxNode> nodeFilter = null) {
			var referrers = SyncHelper.RunSync(() => symbol.FindReferrersAsync(context.Document.Project, definitionFilter, nodeFilter));
			if (referrers == null) {
				return;
			}
			var m = new SymbolMenu(context, SymbolListType.SymbolReferrers);
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(R.T_Referrers);
			var containerType = symbol.ContainingType;
			foreach (var (referrer, occurance) in referrers) {
				var s = referrer;
				var i = m.Menu.Add(s, false);
				i.Location = occurance.FirstOrDefault().Item2.Location;
				foreach (var item in occurance) {
					i.Usage |= item.Item1;
				}
				if (s.ContainingType != containerType) {
					i.Hint = (s.ContainingType ?? s).ToDisplayString(CodeAnalysisHelper.MemberNameFormat);
				}
			}
			m.Menu.ExtIconProvider = ExtIconProvider.Default.GetExtIconsWithUsage;
			m.Show();
		}

		internal static void FindDerivedClasses(this SemanticContext context, ISymbol symbol) {
			var classes = SyncHelper.RunSync(() => SymbolFinder.FindDerivedClassesAsync(symbol as INamedTypeSymbol, context.Document.Project.Solution, null, default)).ToList();
			ShowSymbolMenuForResult(symbol, context, classes, R.T_DerivedClasses, false);
		}

		internal static void FindSubInterfaces(this SemanticContext context, ISymbol symbol) {
			var interfaces = SyncHelper.RunSync(() => (symbol as INamedTypeSymbol).FindDerivedInterfacesAsync(context.Document.Project, default)).ToList();
			ShowSymbolMenuForResult(symbol, context, interfaces, R.T_DerivedInterfaces, false);
		}

		internal static void FindOverrides(this SemanticContext context, ISymbol symbol) {
			var m = new SymbolMenu(context);
			int c = 0;
			foreach (var ov in SyncHelper.RunSync(() => SymbolFinder.FindOverridesAsync(symbol, context.Document.Project.Solution, null, default))) {
				m.Menu.Add(ov, ov.ContainingType);
				++c;
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(R.T_Overrides)
				.Append(c.ToString());
			m.Show();
		}

		internal static void FindImplementations(this SemanticContext context, ISymbol symbol) {
			var implementations = new List<ISymbol>(SyncHelper.RunSync(() => SymbolFinder.FindImplementationsAsync(symbol, context.Document.Project.Solution, null, default)));
			implementations.Sort((a, b) => a.Name.CompareTo(b.Name));
			var m = new SymbolMenu(context);
			if (symbol.Kind == SymbolKind.NamedType) {
				foreach (var impl in implementations) {
					m.Menu.Add(impl, false);
				}
			}
			else {
				foreach (var impl in implementations) {
					m.Menu.Add(impl, impl.ContainingSymbol);
				}
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(R.T_Implementations)
				.Append(implementations.Count.ToString());
			m.Show();
		}

		internal static void FindMembers(this SemanticContext context, ISymbol symbol, UIElement positionElement = null) {
			var m = new SymbolMenu(context, symbol.Kind == SymbolKind.Namespace ? SymbolListType.TypeList : SymbolListType.None);
			var (count, external) = m.Menu.AddSymbolMembers(symbol);
			if (m.Menu.IconProvider == null) {
				if (symbol.Kind == SymbolKind.NamedType) {
					switch (((INamedTypeSymbol)symbol).TypeKind) {
						case TypeKind.Interface:
							m.Menu.ExtIconProvider = ExtIconProvider.InterfaceMembers.GetExtIcons; break;
						case TypeKind.Class:
						case TypeKind.Struct:
							m.Menu.ExtIconProvider = ExtIconProvider.Default.GetExtIcons; break;
					}
				}
				else {
					m.Menu.ExtIconProvider = ExtIconProvider.Default.GetExtIcons;
				}
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true);
			if (symbol.Kind != SymbolKind.Namespace) {
				m.Title.Append(R.T_Members.Replace("{count}", count.ToString()).Replace("{inherited}", external.ToString()));
			}
			else {
				m.Title.Append(R.T_NamespaceMembers.Replace("{count}", count.ToString()));
			}
			m.Show(positionElement);
		}

		internal static void FindInstanceAsParameter(this SemanticContext context, ISymbol symbol) {
			var members = SyncHelper.RunSync(() => (symbol as ITypeSymbol).FindInstanceAsParameterAsync(context.Document.Project, default));
			ShowSymbolMenuForResult(symbol, context, members, R.T_AsParameter, true);
		}

		internal static void FindInstanceProducer(this SemanticContext context, ISymbol symbol) {
			var members = SyncHelper.RunSync(() => (symbol as ITypeSymbol).FindSymbolInstanceProducerAsync(context.Document.Project, default));
			ShowSymbolMenuForResult(symbol, context, members, R.T_Producers, true);
		}

		internal static void FindExtensionMethods(this SemanticContext context, ISymbol symbol) {
			var members = SyncHelper.RunSync(() => (symbol as ITypeSymbol).FindExtensionMethodsAsync(context.Document.Project, Keyboard.Modifiers == ModifierKeys.Control, default));
			ShowSymbolMenuForResult(symbol, context, members, R.T_Extensions, true);
		}

		internal static void FindSymbolWithName(this SemanticContext context, ISymbol symbol) {
			var result = context.SemanticModel.Compilation.FindDeclarationMatchName(symbol.Name, Keyboard.Modifiers == ModifierKeys.Control, true, default);
			ShowSymbolMenuForResult(symbol, context, new List<ISymbol>(result), R.T_NameAlike, true);
		}

		internal static void FindMethodsBySignature(this SemanticContext context, ISymbol symbol) {
			var result = context.SemanticModel.Compilation.FindMethodBySignature(symbol, Keyboard.Modifiers == ModifierKeys.Control, default);
			ShowSymbolMenuForResult(symbol, context, new List<ISymbol>(result), R.T_SignatureMatch, true);
		}

		internal static void ShowLocations(this SemanticContext context, ISymbol symbol, ICollection<SyntaxReference> locations, UIElement positionElement = null) {
			var m = new SymbolMenu(context, SymbolListType.Locations);
			var locs = new SortedList<(string, string, int), Location>();
			foreach (var item in locations) {
				locs[(System.IO.Path.GetDirectoryName(item.SyntaxTree.FilePath), System.IO.Path.GetFileName(item.SyntaxTree.FilePath), item.Span.Start)] = item.ToLocation();
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(R.T_SourceLocations)
				.Append(locs.Count);
			// add locations in source code
			foreach (var loc in locs) {
				m.Menu.Add(loc.Value);
			}
			// add locations in meta data
			foreach (var loc in symbol.Locations.Where(l => l.IsInMetadata).OrderBy(x => x.MetadataModule.Name)) {
				m.Menu.Add(loc);
			}
			m.Show(positionElement);
		}

		static void ShowSymbolMenuForResult<TSymbol>(ISymbol source, SemanticContext context, List<TSymbol> members, string suffix, bool groupByType) where TSymbol : ISymbol {
			members.Sort(CodeAnalysisHelper.CompareSymbol);
			var m = new SymbolMenu(context);
			m.Title.SetGlyph(ThemeHelper.GetImage(source.GetImageId()))
				.Append(source.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(suffix);
			INamedTypeSymbol containingType = null;
			foreach (var item in members) {
				if (groupByType && item.ContainingType != containingType) {
					m.Menu.Add((ISymbol)(containingType = item.ContainingType) ?? item.ContainingNamespace, false)
						.Usage = SymbolUsageKind.Container;
					if (containingType?.TypeKind == TypeKind.Delegate) {
						continue; // skip Invoke method in Delegates, for results from FindMethodBySignature
					}
				}
				m.Menu.Add(item, false);
			}
			m.Menu.ExtIconProvider = ExtIconProvider.Default.GetExtIcons;
			m.Show();
		}

		internal static async Task<int> AddNamespacesAndTypesAsync(SemanticContext context, INamespaceSymbol s, SymbolList symbolList, CancellationToken cancellationToken) {
			if (s == null) {
				return 0;
			}
			var ss = new HashSet<(Microsoft.CodeAnalysis.Text.TextSpan, string)>();
			var a = new HashSet<ISymbol>(CodeAnalysisHelper.GetSymbolNameComparer());
			var defOrRefMembers = new HashSet<INamespaceOrTypeSymbol>(s.GetMembers());
			var nb = ImmutableArray.CreateBuilder<INamespaceOrTypeSymbol>();
			var tb = ImmutableArray.CreateBuilder<INamespaceOrTypeSymbol>();
			int defOrRef = 0;
			foreach (var ns in await s.FindSimilarNamespacesAsync(context.Document.Project, cancellationToken)) {
				foreach (var m in ns.GetMembers()) {
					if (m.CanBeReferencedByName && m.IsImplicitlyDeclared == false && a.Add(m)) {
						(m.IsNamespace ? nb : tb).Add(m);
					}
					continue;
				}
			}
			foreach (var item in nb.OrderBy(n => n.Name)
									.Concat(tb.OrderBy(n => n.Name))) {
				symbolList.Add(item, false);
				defOrRef++;
			}
			return defOrRef;
		}
	}
}
