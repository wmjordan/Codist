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
using TH = Microsoft.VisualStudio.Shell.ThreadHelper;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	static class SymbolCommands
	{
		internal static async Task FindReferrersAsync(this SemanticContext context, ISymbol symbol, Predicate<ISymbol> definitionFilter = null, Predicate<SyntaxNode> nodeFilter = null) {
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			var filter = Keyboard.Modifiers == ModifierKeys.Control ? (s => s == symbol) : definitionFilter;
			var referrers = await symbol.FindReferrersAsync(context.Document.Project, filter, nodeFilter).ConfigureAwait(false);
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			var m = new SymbolMenu(context, SymbolListType.SymbolReferrers);
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(R.T_Referrers);
			if (referrers != null) {
				var containerType = symbol.ContainingType;
				foreach (var (referrer, occurance) in referrers) {
					var i = m.Menu.Add(referrer, false);
					i.Location = occurance.FirstOrDefault().Item2.Location;
					foreach (var item in occurance) {
						i.Usage |= item.Item1;
					}
					if (referrer.ContainingType != containerType) {
						i.Hint = (referrer.ContainingType ?? referrer).ToDisplayString(CodeAnalysisHelper.MemberNameFormat);
					}
				}
			}
			m.Menu.ExtIconProvider = ExtIconProvider.Default.GetExtIconsWithUsage;
			m.Show();
		}

		internal static async Task FindDerivedClassesAsync(this SemanticContext context, ISymbol symbol) {
			var classes = (await SymbolFinder.FindDerivedClassesAsync(symbol as INamedTypeSymbol, context.Document.Project.Solution, null, default).ConfigureAwait(false)).ToList();
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			ShowSymbolMenuForResult(symbol, context, classes, R.T_DerivedClasses, false);
		}

		internal static async Task FindSubInterfacesAsync(this SemanticContext context, ISymbol symbol) {
			var interfaces = (await (symbol as INamedTypeSymbol).FindDerivedInterfacesAsync(context.Document.Project, default).ConfigureAwait(false)).ToList();
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			ShowSymbolMenuForResult(symbol, context, interfaces, R.T_DerivedInterfaces, false);
		}

		internal static async Task FindOverridesAsync(this SemanticContext context, ISymbol symbol) {
			var ovs = ImmutableArray.CreateBuilder<ISymbol>();
			ovs.AddRange(await SymbolFinder.FindOverridesAsync(symbol, context.Document.Project.Solution, null, default).ConfigureAwait(false));
			int c = ovs.Count;
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			var m = new SymbolMenu(context);
			foreach (var item in ovs) {
				m.Menu.Add(item, item.ContainingType);
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(R.T_Overrides)
				.Append(c.ToString());
			m.Show();
		}

		internal static async Task FindImplementationsAsync(this SemanticContext context, ISymbol symbol) {
			var s = symbol;
			INamedTypeSymbol st;
			// workaround for a bug in Roslyn which keeps generic types from returning any result
			if (symbol.Kind == SymbolKind.NamedType && (st = (INamedTypeSymbol)symbol).IsGenericType) {
				s = st.OriginalDefinition;
			}
			var implementations = new List<ISymbol>(await SymbolFinder.FindImplementationsAsync(s, context.Document.Project.Solution, null, default).ConfigureAwait(false));
			implementations.Sort(CodeAnalysisHelper.CompareSymbol);
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			var m = new SymbolMenu(context);
			if (symbol.Kind == SymbolKind.NamedType) {
				st = (INamedTypeSymbol)symbol;
				if (st.ConstructedFrom == st) {
					foreach (var impl in implementations) {
						m.Menu.Add(impl, false);
					}
				}
				else {
					foreach (INamedTypeSymbol impl in implementations) {
						if (impl.IsGenericType || impl.CanConvertTo(st)) {
							m.Menu.Add(impl, false);
						}
					}
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

		internal static async Task FindMembersAsync(this SemanticContext context, ISymbol symbol, UIElement positionElement = null) {
			SymbolMenu m;
			SymbolList l;
			string countLabel;
			if (symbol.Kind == SymbolKind.Namespace) {
				var items = await context.GetNamespacesAndTypesAsync(symbol as INamespaceSymbol, default).ConfigureAwait(false);
				await TH.JoinableTaskFactory.SwitchToMainThreadAsync();
				m = new SymbolMenu(context, SymbolListType.TypeList);
				l = m.Menu;
				l.AddRange(items.Select(s => new SymbolItem(s, l, false)));
				countLabel = R.T_NamespaceMembers.Replace("{count}", items.Length.ToString());
			}
			else {
				var members = symbol.FindMembers();
				int count = members[0].members.Count;
				var items = ImmutableArray.CreateBuilder<SymbolItem>(members.Sum(i => i.members.Count));
				await TH.JoinableTaskFactory.SwitchToMainThreadAsync();
				m = new SymbolMenu(context, symbol.Kind == SymbolKind.Namespace ? SymbolListType.TypeList : SymbolListType.None);
				l = m.Menu;
				foreach (var item in members) {
					var t = item.type;
					items.AddRange(item.members.Select(s => new SymbolItem(s, l, false) { Hint = t }));
				}
				l.SetupForSpecialTypes(symbol as ITypeSymbol);
				l.AddRange(items);
				countLabel = R.T_Members.Replace("{count}", count.ToString()).Replace("{inherited}", (items.Count - count).ToString());
			}
			if (l.IconProvider == null) {
				if (symbol.Kind == SymbolKind.NamedType) {
					switch (((INamedTypeSymbol)symbol).TypeKind) {
						case TypeKind.Interface:
							l.ExtIconProvider = ExtIconProvider.InterfaceMembers.GetExtIcons; break;
						case TypeKind.Class:
						case TypeKind.Struct:
							l.ExtIconProvider = ExtIconProvider.Default.GetExtIcons; break;
					}
				}
				else {
					l.ExtIconProvider = ExtIconProvider.Default.GetExtIcons;
				}
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(countLabel);
			m.Show(positionElement);
		}

		internal static async Task FindInstanceAsParameterAsync(this SemanticContext context, ISymbol symbol) {
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			var strictMatch = Keyboard.Modifiers == ModifierKeys.Control;
			var members = await(symbol as ITypeSymbol).FindInstanceAsParameterAsync(context.Document.Project, strictMatch, default).ConfigureAwait(false);
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			ShowSymbolMenuForResult(symbol, context, members, R.T_AsParameter, true);
		}

		internal static async Task FindInstanceProducerAsync(this SemanticContext context, ISymbol symbol) {
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			var strict = Keyboard.Modifiers == ModifierKeys.Control;
			var members = await(symbol as ITypeSymbol).FindSymbolInstanceProducerAsync(context.Document.Project, strict, default).ConfigureAwait(false);
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			ShowSymbolMenuForResult(symbol, context, members, R.T_Producers, true);
		}

		internal static async Task FindExtensionMethodsAsync(this SemanticContext context, ISymbol symbol) {
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			var strict = Keyboard.Modifiers == ModifierKeys.Control;
			var members = await(symbol as ITypeSymbol).FindExtensionMethodsAsync(context.Document.Project, strict, default).ConfigureAwait(false);
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
			ShowSymbolMenuForResult(symbol, context, members, R.T_Extensions, true);
		}

		internal static void FindSymbolWithName(this SemanticContext context, ISymbol symbol) {
			var fullMatch = Keyboard.Modifiers == ModifierKeys.Control;
			var result = context.SemanticModel.Compilation.FindDeclarationMatchName(symbol.Name, fullMatch, true, default);
			ShowSymbolMenuForResult(symbol, context, new List<ISymbol>(result), R.T_NameAlike, true);
		}

		internal static void FindMethodsBySignature(this SemanticContext context, ISymbol symbol) {
			var myCodeOnly = Keyboard.Modifiers == ModifierKeys.Control;
			var result = context.SemanticModel.Compilation.FindMethodBySignature(symbol, myCodeOnly, default);
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

		internal static async Task<ISymbol[]> GetNamespacesAndTypesAsync(this SemanticContext context, INamespaceSymbol s, CancellationToken cancellationToken) {
			if (s == null) {
				return Array.Empty<ISymbol>();
			}
			var ss = new HashSet<(Microsoft.CodeAnalysis.Text.TextSpan, string)>();
			var a = new HashSet<ISymbol>(CodeAnalysisHelper.GetSymbolNameComparer());
			var defOrRefMembers = new HashSet<INamespaceOrTypeSymbol>(s.GetMembers());
			var nb = ImmutableArray.CreateBuilder<INamespaceOrTypeSymbol>();
			var tb = ImmutableArray.CreateBuilder<INamespaceOrTypeSymbol>();
			foreach (var ns in await s.FindSimilarNamespacesAsync(context.Document.Project, cancellationToken).ConfigureAwait(false)) {
				foreach (var m in ns.GetMembers()) {
					if (m.CanBeReferencedByName && m.IsImplicitlyDeclared == false && a.Add(m)) {
						(m.IsNamespace ? nb : tb).Add(m);
					}
					continue;
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
