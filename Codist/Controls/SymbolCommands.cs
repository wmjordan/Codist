using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	// FIXME break commands and parameters into individual implementation types,
	//   so they can be repeated (refresh)
	static class SymbolCommands
	{
		internal static async Task FindReferrersAsync(this SemanticContext context, ISymbol symbol, bool strict, bool matchTypeArgument, IEnumerable<Document> documents, Predicate<ISymbol> definitionFilter = null, Predicate<SyntaxNode> nodeFilter = null) {
			var df = strict ? (symbol is IMethodSymbol ms && ms.MethodKind == MethodKind.ReducedExtension ? ms.ReducedFrom : symbol).Equals : definitionFilter;
			Predicate<ISymbol> of = matchTypeArgument ? symbol.Equals : null;
			var referrers = await symbol.FindReferrersAsync(context.Document.Project, documents, df, of, nodeFilter);
			await SyncHelper.SwitchToMainThreadAsync(default);
			var m = new SymbolMenu(context, SymbolListType.SymbolReferrers);
			m.Title.SetGlyph(symbol.GetImageId())
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

		internal static async Task FindDerivedClassesAsync(this SemanticContext context, INamedTypeSymbol symbol, bool directDerive, IEnumerable<Project> projects, SymbolSourceFilter source, bool orderByHierarchy) {
			var original = symbol.OriginalDefinition;
#if LOG || DEBUG
			var typeName = original.GetTypeName();
#endif
			var classes = await SymbolFinder.FindDerivedClassesAsync(original, context.Document.Project.Solution, projects.MakeImmutableSet()).ConfigureAwait(false);
			classes = source.Filter(classes);
			if (symbol.IsBoundedGenericType()) {
				classes = classes.Where(i => !original.Equals(i.BaseType.OriginalDefinition) || symbol.Equals(i.BaseType));
			}
			if (directDerive) {
				await ShowSymbolMenuForResultAsync(symbol, context, classes.Where(c => c.BaseType.OriginalDefinition.MatchWith(original)).MakeSortedSymbolArray(), R.T_DirectlyDerivedClasses, false).ConfigureAwait(false);
				return;
			}
			if (orderByHierarchy == false) {
				await ShowSymbolMenuForResultAsync(symbol, context, classes.MakeSortedSymbolArray(), R.T_DerivedClasses, false).ConfigureAwait(false);
				return;
			}
			var members = new List<INamedTypeSymbol>();
			var hierarchies = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(7, CodeAnalysisHelper.GetNamedTypeComparer()) {
					{ original, members }
				};
			INamedTypeSymbol t, bt;
			foreach (var c in classes) {
				t = c.OriginalDefinition;
				bt = t.BaseType.OriginalDefinition;
				if (hierarchies.TryGetValue(bt, out var children)) {
					children.Add(t);
					continue;
				}
#if LOG || DEBUG
				var btName = bt.GetTypeName();
				if (btName == typeName) {
					$"Found same name type, but not directly derived for {typeName}".Log();
					if (original.HasSource()) {
						$"  File: {original.GetSourceReferences()[0].SyntaxTree.FilePath}".Log();
					}
					if (original.ContainingAssembly.Equals(bt.ContainingAssembly) == false) {
						$"  Assembly: {bt.ContainingAssembly?.ToDisplayString()}".Log();
					}
				}
#endif
				hierarchies.Add(bt, [t]);
				if (source == SymbolSourceFilter.RequiresSource) {
					// add excluded base types from referenced types
					while (!bt.HasSource() && (t = bt.BaseType) != null) {
						if (hierarchies.TryGetValue(t, out children)) {
							if (!children.Contains(bt)) {
								children.Add(bt);
							}
						}
						else {
							hierarchies.Add(t, [bt]);
						}
						if (original.Equals(t)) {
							break;
						}
						bt = t;
					}
				}
			}
			await ShowHierarchicalSymbolMenuForResultAsync(symbol, context, hierarchies, members, R.T_DerivedClasses).ConfigureAwait(false);
		}

		internal static async Task FindSubInterfacesAsync(this SemanticContext context, ISymbol symbol, bool directDerive, SymbolSourceFilter source) {
			var interfaces = await (symbol as INamedTypeSymbol).FindDerivedInterfacesAsync(context.Document.Project, directDerive, source, default).ConfigureAwait(false);
			await ShowSymbolMenuForResultAsync(symbol, context, interfaces, directDerive ? R.T_DirectlyDerivedInterfaces : R.T_DerivedInterfaces, false).ConfigureAwait(false);
		}

		internal static async Task FindOverridesAsync(this SemanticContext context, ISymbol symbol, IEnumerable<Project> projects, SymbolSourceFilter source) {
			var overrides = (await symbol.FindOverridesAsync(context.Document.Project.Solution, source, projects).ConfigureAwait(false)).MakeSortedSymbolArray();
			await SyncHelper.SwitchToMainThreadAsync(default);
			var m = new SymbolMenu(context);
			foreach (var item in overrides) {
				m.Add(item, item.ContainingType);
			}
			m.Title.SetGlyph(symbol.GetImageId())
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
				.Append(R.T_Overrides)
				.Append(overrides.Length.ToString());
			m.Show();
		}

		internal static async Task FindImplementationsAsync(this SemanticContext context, ISymbol symbol, bool directImplementation, IEnumerable<Project> projects, SymbolSourceFilter source, CancellationToken cancellationToken = default) {
			var implementations = await symbol.FindImplementationsAsync(context.Document.Project.Solution, directImplementation, source, projects, cancellationToken).ConfigureAwait(false);
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			var m = new SymbolMenu(context);
			if (symbol.Kind == SymbolKind.NamedType) {
				foreach (var item in implementations) {
					m.Add(item, false);
				}
				m.ExtIconProvider = i => {
					var p = ExtIconProvider.Default.GetExtIcons(i);
					if (i.Symbol.IsDirectImplementationOf(symbol)) {
						if (p == null) {
							p = new System.Windows.Controls.StackPanel().MakeHorizontal();
						}
						p.Children.Add(VsImageHelper.GetImage(IconIds.InterfaceImplementation));
					}
					return p;
				};
			}
			else {
				foreach (var item in implementations) {
					m.Add(item, item.ContainingType);
				}
			}
			m.Title.SetGlyph(symbol.GetImageId())
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
				.Append(R.T_Implementations)
				.Append(implementations.Length.ToString());
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
				var members = symbol.ListMembers();
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
			m.Title.SetGlyph(symbol.GetImageId())
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
				.Append(countLabel);
			m.Show(positionElement);
		}

		internal static async Task FindInstanceAsParameterAsync(this SemanticContext context, ISymbol symbol, bool strict, bool sourceCode) {
			var members = await(symbol as ITypeSymbol).FindInstanceAsParameterAsync(context.Document.Project, strict, sourceCode, default);
			await ShowSymbolMenuForResultAsync(symbol, context, members, R.T_AsParameter, true);
		}

		internal static async Task FindInstanceProducerAsync(this SemanticContext context, ISymbol symbol, bool strict, bool sourceCode) {
			var members = await(symbol as ITypeSymbol).FindSymbolInstanceProducerAsync(context.Document.Project, strict, sourceCode, default);
			await ShowSymbolMenuForResultAsync(symbol, context, members, R.T_Producers, true);
		}

		internal static async Task FindExtensionMethodsAsync(this SemanticContext context, ISymbol symbol, bool strict, SymbolSourceFilter source) {
			var members = await(symbol as ITypeSymbol).FindExtensionMethodsAsync(context.Document.Project, strict, source, default).ConfigureAwait(false);
			await ShowSymbolMenuForResultAsync(symbol, context, members, R.T_Extensions, true);
		}

		internal static async Task FindSymbolWithNameAsync(this SemanticContext context, ISymbol symbol, bool fullMatch, bool matchCase, SymbolSourceFilter source) {
			var results = await Task.Run(() => ImmutableArray.CreateRange(context.SemanticModel.Compilation.FindDeclarationMatchName(symbol.Name, fullMatch, matchCase, source, default))).ConfigureAwait(false);
			await ShowSymbolMenuForResultAsync(symbol, context, results, R.T_NameAlike, true);
		}

		internal static async Task FindMethodsBySignatureAsync(this SemanticContext context, ISymbol symbol, bool excludeGenerics, SymbolSourceFilter source) {
			var methods = await Task.Run(() => (context.SemanticModel.Compilation.FindMethodBySignature(symbol, excludeGenerics, source, default)).MakeSortedSymbolArray());
			await ShowSymbolMenuForResultAsync(symbol, context, methods, R.T_SignatureMatch, true);
		}

		internal static async Task FindParameterAssignmentsAsync(this SemanticContext context, IParameterSymbol parameter, IEnumerable<Document> documents, bool strict = false, ArgumentAssignmentFilter assignmentFilter = ArgumentAssignmentFilter.Undefined) {
			var assignments = await parameter.FindParameterAssignmentsAsync(context.Document.Project, documents, strict, assignmentFilter, default);
			var c = 0;
			foreach (var item in assignments) {
				c += item.Value.Count;
			}
			await SyncHelper.SwitchToMainThreadAsync(default);
			var m = new SymbolMenu(context, SymbolListType.SymbolReferrers);
			m.Title.SetGlyph(IconIds.Argument)
				.AddSymbol(parameter, null,true, SymbolFormatter.Instance)
				.Append(R.T_AssignmentLocations).Append(c);
			if (c != 0) {
				if (parameter.HasExplicitDefaultValue) {
					if (assignmentFilter == ArgumentAssignmentFilter.ExplicitValue) {
						m.Title.AppendInfo("Explicit value only");
					}
					else if (assignmentFilter == ArgumentAssignmentFilter.DefaultValue) {
						m.Title.AppendInfo("Default value only");
					}
					m.Title.AppendLine().Append(VsImageHelper.GetImage(IconIds.FindParameterAssignment).WrapMargin(WpfHelper.GlyphMargin)).Append(R.T_Default).Append(" = ").Append(parameter.ExplicitDefaultValue?.ToString() ?? "null");
				}
				foreach (var site in assignments) {
					for (int i = 0; i < site.Value.Count; i++) {
						var location = site.Value[i];
						var symItem = m.Add(site.Key, false);
						symItem.Location = location.location ?? location.expression.GetLocation();
						symItem.Hint = location.assignment == ArgumentAssignment.Default ? "(default)" : location.expression.NormalizeWhitespace().ToString();
						if (location.assignment.CeqAny(ArgumentAssignment.ImplicitlyConverted, ArgumentAssignment.ImplicitlyConvertedNameValue)) {
							symItem.Usage = SymbolUsageKind.TypeCast;
						}
						if (i != 0) {
							symItem.IndentLevel = 1;
						}
					}
				}
			}
			m.ExtIconProvider = ExtIconProvider.Default.GetExtIconsWithUsage;
			m.Show();
		}

		internal static void ShowLocations(this SemanticContext context, ISymbol symbol, ImmutableArray<SyntaxReference> locations, UIElement positionElement = null) {
			var m = new SymbolMenu(context, SymbolListType.Locations);
			m.Title.SetGlyph(symbol.GetImageId())
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

		static async Task ShowSymbolMenuForResultAsync<TSymbol>(ISymbol symbol, SemanticContext context, ImmutableArray<TSymbol> members, string suffix, bool groupByType, string info = null) where TSymbol : ISymbol {
			List<(SymbolUsageKind usage, ISymbol memberOrContainer)> grouped = null;
			if (groupByType) {
				grouped = GroupSymbolsByContainingType(members);
			}
			await SyncHelper.SwitchToMainThreadAsync();
			var m = new SymbolMenu(context);
			m.Title.SetGlyph(symbol.GetImageId())
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
				.Append(suffix);
			if (info != null) {
				m.Title.AppendInfo(info);
			}
			if (groupByType) {
				foreach (var item in grouped) {
					m.Add(item.memberOrContainer, item.usage == SymbolUsageKind.Container).Usage = item.usage;
				}
			}
			else {
				foreach (var item in members) {
					m.Add(item, false);
				}
			}
			m.ExtIconProvider = ExtIconProvider.Default.GetExtIcons;
			m.Show();
		}

		static List<(SymbolUsageKind usage, ISymbol memberOrContainer)> GroupSymbolsByContainingType<TSymbol>(ImmutableArray<TSymbol> members) where TSymbol : ISymbol {
			INamedTypeSymbol containingType = null;
			var grouped = new List<(SymbolUsageKind, ISymbol)>(members.Length * 5 / 4);
			foreach (var member in members) {
				if (member.ContainingType != containingType) {
					grouped.Add((SymbolUsageKind.Container, containingType = member.ContainingType));
					if (containingType?.TypeKind == TypeKind.Delegate) {
						continue; // skip Invoke method in Delegates, for results from FindMethodBySignature
					}
				}
				grouped.Add((SymbolUsageKind.Normal, member));
			}

			return grouped;
		}

		static async Task ShowHierarchicalSymbolMenuForResultAsync<TSymbol>(ISymbol symbol, SemanticContext context, Dictionary<TSymbol, List<TSymbol>> hierarchies, List<TSymbol> members, string suffix) where TSymbol : ISymbol {
			members.Sort(CodeAnalysisHelper.CompareSymbol);
			await SyncHelper.SwitchToMainThreadAsync();
			var m = new SymbolMenu(context);
			m.Title.SetGlyph(symbol.GetImageId())
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

		static TTextBlock AppendInfo<TTextBlock>(this TTextBlock text, string filterInfo)
			where TTextBlock : System.Windows.Controls.TextBlock {
			return text.AppendLine().Append(VsImageHelper.GetImage(IconIds.Info).WrapMargin(WpfHelper.GlyphMargin)).Append(filterInfo);
		}
	}
}
