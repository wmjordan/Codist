using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	static class SymbolCommands
	{
		internal static async Task FindReferrersAsync(this SemanticContext context, ISymbol symbol, bool strict, Predicate<ISymbol> definitionFilter = null, Predicate<SyntaxNode> nodeFilter = null) {
			var filter = strict ? (s => s == symbol) : definitionFilter;
			var referrers = await symbol.FindReferrersAsync(context.Document.Project, filter, nodeFilter);
			await SyncHelper.SwitchToMainThreadAsync(default);
			var m = new SymbolMenu(context, SymbolListType.SymbolReferrers);
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
				.Append(R.T_Referrers);
			if (referrers != null) {
				var containerType = symbol.ContainingType;
				foreach (var (referrer, occurrence) in referrers) {
					var i = m.Add(referrer, false);
					i.Location = occurrence.FirstOrDefault().Item2.Location;
					foreach (var item in occurrence) {
						i.Usage |= item.Item1;
					}
					if (referrer.ContainingType != containerType) {
						i.Hint = (referrer.ContainingType ?? referrer).ToDisplayString(CodeAnalysisHelper.MemberNameFormat);
					}
					if (occurrence.Count > 1) {
						i.Hint += " @" + occurrence.Count;
					}
				}
			}
			m.ExtIconProvider = ExtIconProvider.Default.GetExtIconsWithUsage;
			m.Show();
		}

		internal static async Task FindDerivedClassesAsync(this SemanticContext context, ISymbol symbol, bool directDerive, bool orderByHierarchy) {
			var type = (symbol as INamedTypeSymbol).OriginalDefinition;
			var classes = await SymbolFinder.FindDerivedClassesAsync(type, context.Document.Project.Solution).ConfigureAwait(false);
			await SyncHelper.SwitchToMainThreadAsync(default);
			if (directDerive) {
				ShowSymbolMenuForResult(symbol, context, classes.Where(c => c.BaseType.OriginalDefinition.MatchWith(type)).ToList(), R.T_DirectlyDerivedClasses, false);
				return;
			}
			if (orderByHierarchy == false) {
				ShowSymbolMenuForResult(symbol, context, classes.ToList(), R.T_DerivedClasses, false);
				return;
			}
			var hierarchies = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(CodeAnalysisHelper.GetNamedTypeComparer()) {
					{ type.OriginalDefinition, new List<INamedTypeSymbol>() }
				};
			INamedTypeSymbol t, bt;
			foreach (var c in classes) {
				t = c.OriginalDefinition;
				bt = t.BaseType.OriginalDefinition;
				if (hierarchies.TryGetValue(bt, out var children)) {
					children.Add(t);
				}
				else {

					hierarchies.Add(bt, new List<INamedTypeSymbol> { t });
				}
			}
			ShowHierarchicalSymbolMenuForResult(symbol, context, type, hierarchies, R.T_DerivedClasses);
		}

		internal static async Task FindSubInterfacesAsync(this SemanticContext context, ISymbol symbol, bool directDerive) {
			var interfaces = await (symbol as INamedTypeSymbol).FindDerivedInterfacesAsync(context.Document.Project, directDerive, default).ConfigureAwait(false);
			await SyncHelper.SwitchToMainThreadAsync(default);
			ShowSymbolMenuForResult(symbol, context, interfaces, directDerive ? R.T_DirectlyDerivedInterfaces : R.T_DerivedInterfaces, false);
		}

		internal static async Task FindOverridesAsync(this SemanticContext context, ISymbol symbol) {
			var ovs = ImmutableArray.CreateBuilder<ISymbol>();
			ovs.AddRange(await SymbolFinder.FindOverridesAsync(symbol, context.Document.Project.Solution).ConfigureAwait(false));
			int c = ovs.Count;
			await SyncHelper.SwitchToMainThreadAsync(default);
			var m = new SymbolMenu(context);
			foreach (var item in ovs) {
				m.Add(item, item.ContainingType);
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
				.Append(R.T_Overrides)
				.Append(c.ToString());
			m.Show();
		}

		internal static async Task FindImplementationsAsync(this SemanticContext context, ISymbol symbol, CancellationToken cancellationToken = default) {
			var s = symbol;
			INamedTypeSymbol st;
			// workaround for a bug in Roslyn which keeps generic types from returning any result
			if (symbol.Kind == SymbolKind.NamedType && (st = (INamedTypeSymbol)symbol).IsGenericType) {
				s = st.OriginalDefinition;
			}
			var implementations = new List<ISymbol>(await SymbolFinder.FindImplementationsAsync(s, context.Document.Project.Solution, null, cancellationToken).ConfigureAwait(false));
			implementations.Sort(CodeAnalysisHelper.CompareSymbol);
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			var m = new SymbolMenu(context);
			var d = new SourceSymbolDeduper();
			if (symbol.Kind == SymbolKind.NamedType) {
				st = (INamedTypeSymbol)symbol;
				if (st.ConstructedFrom == st) {
					foreach (var impl in implementations) {
						if (d.TryAdd(impl)) {
							m.Add(impl, false);
						}
					}
				}
				else {
					foreach (INamedTypeSymbol impl in implementations) {
						if (impl.IsGenericType || impl.CanConvertTo(st)) {
							if (d.TryAdd(impl)) {
								m.Add(impl, false);
							}
						}
					}
				}
			}
			else {
				foreach (var impl in implementations) {
					if (d.TryAdd(impl)) {
						m.Add(impl, impl.ContainingSymbol);
					}
				}
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
				.Append(R.T_Implementations)
				.Append(implementations.Count.ToString());
			m.Show();
		}

		internal static async Task FindMembersAsync(this SemanticContext context, ISymbol symbol, UIElement positionElement = null) {
			SymbolMenu m;
			string countLabel;
			if (symbol.Kind == SymbolKind.Namespace) {
				var items = await context.GetNamespacesAndTypesAsync(symbol as INamespaceSymbol, default).ConfigureAwait(false);
				await SyncHelper.SwitchToMainThreadAsync();
				m = new SymbolMenu(context, SymbolListType.TypeList);
				m.AddRange(items.Select(s => new SymbolItem(s, m, false)));
				countLabel = R.T_NamespaceMembers.Replace("{count}", items.Length.ToString());
			}
			else {
				var members = symbol.FindMembers();
				int count = members[0].members.Count;
				var items = ImmutableArray.CreateBuilder<SymbolItem>(members.Sum(i => i.members.Count));
				await SyncHelper.SwitchToMainThreadAsync();
				m = new SymbolMenu(context, symbol.Kind == SymbolKind.Namespace ? SymbolListType.TypeList : SymbolListType.None);
				foreach (var item in members) {
					var t = item.type;
					items.AddRange(item.members.Select(s => new SymbolItem(s, m, false) { Hint = t }));
				}
				m.SetupForSpecialTypes(symbol as ITypeSymbol);
				m.AddRange(items);
				countLabel = R.T_Members.Replace("{count}", count.ToString()).Replace("{inherited}", (items.Count - count).ToString());
			}
			if (m.IconProvider == null) {
				if (symbol.Kind == SymbolKind.NamedType) {
					switch (((INamedTypeSymbol)symbol).TypeKind) {
						case TypeKind.Interface:
							m.ExtIconProvider = ExtIconProvider.InterfaceMembers.GetExtIcons; break;
						case TypeKind.Class:
						case TypeKind.Struct:
							m.ExtIconProvider = ExtIconProvider.Default.GetExtIcons; break;
					}
				}
				else {
					m.ExtIconProvider = ExtIconProvider.Default.GetExtIcons;
				}
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
				.Append(countLabel);
			m.Show(positionElement);
		}

		internal static async Task FindInstanceAsParameterAsync(this SemanticContext context, ISymbol symbol, bool strict) {
			var members = await(symbol as ITypeSymbol).FindInstanceAsParameterAsync(context.Document.Project, strict, default);
			await SyncHelper.SwitchToMainThreadAsync(default);
			ShowSymbolMenuForResult(symbol, context, members, R.T_AsParameter, true);
		}

		internal static async Task FindInstanceProducerAsync(this SemanticContext context, ISymbol symbol, bool strict) {
			var members = await(symbol as ITypeSymbol).FindSymbolInstanceProducerAsync(context.Document.Project, strict, default);
			await SyncHelper.SwitchToMainThreadAsync(default);
			ShowSymbolMenuForResult(symbol, context, members, R.T_Producers, true);
		}

		internal static async Task FindExtensionMethodsAsync(this SemanticContext context, ISymbol symbol, bool strict) {
			var members = await(symbol as ITypeSymbol).FindExtensionMethodsAsync(context.Document.Project, strict, default);
			await SyncHelper.SwitchToMainThreadAsync(default);
			ShowSymbolMenuForResult(symbol, context, members, R.T_Extensions, true);
		}

		internal static async Task FindSymbolWithNameAsync(this SemanticContext context, ISymbol symbol, bool fullMatch) {
			var results = await Task.Run(() => new List<ISymbol>(context.SemanticModel.Compilation.FindDeclarationMatchName(symbol.Name, fullMatch, true, default)));
			await SyncHelper.SwitchToMainThreadAsync(default);
			ShowSymbolMenuForResult(symbol, context, results, R.T_NameAlike, true);
		}

		internal static async Task FindMethodsBySignatureAsync(this SemanticContext context, ISymbol symbol, bool myCodeOnly) {
			var methods = await Task.Run(() => new List<ISymbol>(context.SemanticModel.Compilation.FindMethodBySignature(symbol, myCodeOnly, default)));
			await SyncHelper.SwitchToMainThreadAsync(default);
			ShowSymbolMenuForResult(symbol, context, methods, R.T_SignatureMatch, true);
		}

		internal static void ShowLocations(this SemanticContext context, ISymbol symbol, ImmutableArray<SyntaxReference> locations, UIElement positionElement = null) {
			var m = new SymbolMenu(context, SymbolListType.Locations);
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance);
			if (locations.Length != 0) {
				m.Title.Append(R.T_SourceLocations).Append(locations.Length);
				var locs = new SortedList<(string, string, int), Location>();
				foreach (var item in locations) {
					locs[(System.IO.Path.GetDirectoryName(item.SyntaxTree.FilePath), System.IO.Path.GetFileName(item.SyntaxTree.FilePath), item.Span.Start)] = item.ToLocation();
				}
				// add locations in source code
				foreach (var loc in locs) {
					m.Add(loc.Value);
				}
			}
			// add locations in meta data
			foreach (var loc in symbol.Locations.Where(l => l.IsInMetadata).OrderBy(x => x.MetadataModule.Name)) {
				m.Add(loc);
			}
			m.Show(positionElement);
		}

		static void ShowSymbolMenuForResult<TSymbol>(ISymbol symbol, SemanticContext context, List<TSymbol> members, string suffix, bool groupByType) where TSymbol : ISymbol {
			members.Sort(CodeAnalysisHelper.CompareSymbol);
			var m = new SymbolMenu(context);
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
				.Append(suffix);
			INamedTypeSymbol containingType = null;
			foreach (var item in members) {
				if (groupByType && item.ContainingType != containingType) {
					m.Add((ISymbol)(containingType = item.ContainingType) ?? item.ContainingNamespace, false)
						.Usage = SymbolUsageKind.Container;
					if (containingType?.TypeKind == TypeKind.Delegate) {
						continue; // skip Invoke method in Delegates, for results from FindMethodBySignature
					}
				}
				m.Add(item, false);
			}
			m.ExtIconProvider = ExtIconProvider.Default.GetExtIcons;
			m.Show();
		}

		static void ShowHierarchicalSymbolMenuForResult<TSymbol>(ISymbol symbol, SemanticContext context, TSymbol root, Dictionary<TSymbol, List<TSymbol>> hierarchies, string suffix) where TSymbol : ISymbol {
			if (hierarchies.TryGetValue(root, out var members) == false) {
				return;
			}
			members.Sort(CodeAnalysisHelper.CompareSymbol);
			var m = new SymbolMenu(context);
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
				.Append(suffix);
			foreach (var item in members) {
				m.Add(item, false);
				AddChildren(hierarchies, m, 0, item);
			}
			m.ExtIconProvider = ExtIconProvider.Default.GetExtIcons;
			m.Show();
		}

		static void AddChildren<TSymbol>(Dictionary<TSymbol, List<TSymbol>> hierarchies, SymbolMenu m, byte indentLevel, TSymbol item) where TSymbol : ISymbol {
			indentLevel++;
			if (hierarchies.TryGetValue(item, out var children)) {
				foreach (var child in children) {
					m.Add(child, false).IndentLevel = indentLevel;
					AddChildren(hierarchies, m, indentLevel, child);
				}
			}
		}

		internal static Task<ISymbol[]> GetNamespacesAndTypesAsync(this SemanticContext context, INamespaceSymbol symbol, CancellationToken cancellationToken) {
			return symbol == null
				? Task.FromResult(Array.Empty<ISymbol>())
				: GetNamespacesAndTypesUncheckedAsync(context, symbol, cancellationToken);

			async Task<ISymbol[]> GetNamespacesAndTypesUncheckedAsync(SemanticContext ctx, INamespaceSymbol s, CancellationToken ct) {
				var ss = new HashSet<(Microsoft.CodeAnalysis.Text.TextSpan, string)>();
				var a = new HashSet<ISymbol>(CodeAnalysisHelper.GetSymbolNameComparer());
				var defOrRefMembers = new HashSet<INamespaceOrTypeSymbol>(s.GetMembers());
				var nb = ImmutableArray.CreateBuilder<INamespaceOrTypeSymbol>();
				var tb = ImmutableArray.CreateBuilder<INamespaceOrTypeSymbol>();
				foreach (var ns in await s.FindSimilarNamespacesAsync(ctx.Document.Project, ct).ConfigureAwait(false)) {
					foreach (var m in ns.GetMembers()) {
						if (m.CanBeReferencedByName && m.IsImplicitlyDeclared == false && a.Add(m)) {
							(m.IsNamespace ? nb : tb).Add(m);
						}
					}
				}
				var r = new ISymbol[nb.Count + tb.Count];
				var i = -1;
				foreach (var item in nb.OrderBy(n => n.Name)
					.Concat(tb.OrderBy(n => n.Name).ThenBy(t => (t as INamedTypeSymbol)?.Arity ?? 0))
					) {
					r[++i] = item;
				}
				return r;
			}
		}
	}
}
