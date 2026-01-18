using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using R = Codist.Properties.Resources;

namespace Codist.SymbolCommands
{
	abstract class SemanticCommandBase
	{
		public ISymbol Symbol { get; set; }
		public SyntaxNode Node { get; set; }
		public SemanticContext Context { get; set; }
		public CommandOptions Options { get; set; }

		protected bool StrictMatch => Options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
		protected bool MatchTypeArgument => Options.MatchFlags(CommandOptions.MatchTypeArgument) || UIHelper.IsCtrlDown;
		protected bool DirectDerive => Options.MatchFlags(CommandOptions.DirectDerive) || UIHelper.IsCtrlDown;

		public abstract int ImageId { get; }
		/// <summary>
		/// Command title in user interface.
		/// </summary>
		public abstract string Title { get; }
		/// <summary>
		/// The placeholder for <see cref="Symbol"/> in <see cref="Title"/>.
		/// </summary>
		public virtual string TitlePlaceHolderSubstitution { get; set; }
		/// <summary>
		/// Description in generating ToolTip.
		/// </summary>
		public virtual string Description => null;
		/// <summary>
		/// Denotes user can use Ctrl key to restrict execution results.
		/// </summary>
		protected virtual bool UseCtrlRestriction => false;

		public virtual bool CanRefresh {
			get {
				SymbolKind k;
				ISymbol s;
				return Context != null
					&& (s = Symbol) != null
					&& ((k = s.Kind).IsBetween(SymbolKind.Event, SymbolKind.Parameter)
						&& (k != SymbolKind.NamedType
							|| ((INamedTypeSymbol)s).TypeKind.CeqAny(TypeKind.Class, TypeKind.Struct, TypeKind.Interface, TypeKind.Enum, TypeKind.Delegate))
						|| k == SymbolKind.Property);
			}
		}

		public abstract Task ExecuteAsync(CancellationToken cancellationToken);

		public virtual IEnumerable<OptionDescriptor> OptionDescriptors => null;

		public virtual IEnumerable<SemanticCommandBase> GetSubCommands() => null;

		public virtual async Task RefreshSymbolAsync(CancellationToken cancellationToken) {
			Symbol = await Context.RelocateSymbolAsync(Symbol, cancellationToken);
		}

		protected IEnumerable<Document> MakeDocumentListFromOption(CommandOptions options) {
			return options.MatchFlags(CommandOptions.CurrentProject)
				? Context.Document.Project.Documents
				: options.MatchFlags(CommandOptions.CurrentFile)
				? [Context.Document]
				: options.MatchFlags(CommandOptions.RelatedProjects)
				? Context.Document.Project.GetRelatedProjectDocuments()
				: null;
		}
		protected IEnumerable<Project> MakeProjectListFromOption(CommandOptions options) {
			return options.MatchFlags(CommandOptions.CurrentProject)
				? [Context.Document.Project]
				: options.MatchFlags(CommandOptions.RelatedProjects)
				? Context.Document.Project.GetRelatedProjects()
				: null;
		}

		protected static SymbolSourceFilter MakeSourceFilterFromOption(CommandOptions options) {
			return options.MatchFlags(CommandOptions.SourceCode) ? SymbolSourceFilter.RequiresSource
				: options.MatchFlags(CommandOptions.External) ? SymbolSourceFilter.ExcludesSource
				: default;
		}

		internal CommandToolTip CreteToolTip() {
			return new CommandToolTip(ImageId,
				TitlePlaceHolderSubstitution != null ? GetTipHeaderText() : Title,
				new ThemedTipText((UseCtrlRestriction ? Description + Environment.NewLine + R.CMDT_SemanticCommandCtrlTip : Description) ?? String.Empty));
		}

		string GetTipHeaderText() {
			var title = Title;
			string sub = TitlePlaceHolderSubstitution;
			var i = title.IndexOf('<');
			if (i < 0) {
				goto FALLBACK;
			}
			var i2 = title.IndexOf('>', i);
			if (i2 < 0) {
				goto FALLBACK;
			}
			return title.Substring(0, i)
				+ (String.IsNullOrEmpty(sub) ? "?" : sub)
				+ title.Substring(i2 + 1);
		FALLBACK:
			return title;
		}
	}

	abstract class AnalysisListCommandBase<TListData> : SemanticCommandBase
	{
		protected virtual ISymbol ResultSymbol => Symbol;
		protected abstract string ResultLabel { get; }

		public override sealed async Task ExecuteAsync(CancellationToken cancellationToken) {
			var data = await PrepareListDataAsync(cancellationToken).ConfigureAwait(false);
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			var m = new SymbolMenu(Context);
			UpdateList(m, data);
			SetupListTitle(m, data);
			m.Show();
		}

		protected virtual void SetupListTitle(SymbolMenu resultList, TListData data) {
			var symbol = ResultSymbol;
			var title = resultList.Title
				.SetGlyph(symbol.GetImageId())
				.AddSymbol(symbol, null, true, SymbolFormatter.Instance);
			var label = ResultLabel;
			if (label != null) {
				title.Append(label);
			}
		}

		public async Task RefreshAsync(SymbolMenu resultList, CancellationToken cancellationToken) {
			if (await Context.UpdateAsync(cancellationToken)) {
				var data = await PrepareListDataAsync(cancellationToken).ConfigureAwait(false);
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				resultList.Title.Inlines.Clear();
				UpdateList(resultList, data);
				SetupListTitle(resultList, data);
			}
		}
		public abstract Task<TListData> PrepareListDataAsync(CancellationToken cancellationToken);
		public abstract void UpdateList(SymbolMenu resultList, TListData data);
	}

	abstract class CommonListCommand<TSymbol>
		: AnalysisListCommandBase<ImmutableArray<TSymbol>> where TSymbol : ISymbol
	{
		protected void SetupSymbolMenuForResult(SymbolMenu menu, ImmutableArray<TSymbol> data, bool groupByType) {
			List<(SymbolUsageKind usage, ISymbol memberOrContainer)> grouped = null;
			if (groupByType) {
				grouped = GroupSymbolsByContainingType(data);
			}
			if (groupByType) {
				foreach (var item in grouped) {
					menu.Add(item.memberOrContainer, item.usage == SymbolUsageKind.Container).Usage = item.usage;
				}
			}
			else {
				foreach (var item in data) {
					menu.Add(item, false);
				}
			}
			menu.ExtIconProvider = ExtIconProvider.Default.GetExtIcons;
		}

		static List<(SymbolUsageKind usage, ISymbol memberOrContainer)> GroupSymbolsByContainingType(ImmutableArray<TSymbol> members) {
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

		protected static TTextBlock AppendInfo<TTextBlock>(TTextBlock text, string filterInfo)
			where TTextBlock : TextBlock {
			return text.AppendLine()
				.Append(VsImageHelper.GetImage(IconIds.Info).WrapMargin(WpfHelper.GlyphMargin))
				.Append(filterInfo);
		}
	}

	// displays a list with hierarchical structure of inheritance
	// first item in tuple is the hierarchical data, where each symbol in item.Value may refer to other item.Key
	// second item is the flat top-level list
	abstract class HierarchicalListCommand<TSymbol> : AnalysisListCommandBase<(SymbolRelations<TSymbol, TSymbol> hierarchicalData, IReadOnlyList<TSymbol> topList)> where TSymbol : ISymbol
	{
		protected void SetupList((SymbolRelations<TSymbol, TSymbol> hierarchicalData, IReadOnlyList<TSymbol> topList) data, SymbolMenu resultList) {
			var hierarchies = data.hierarchicalData;
			if (hierarchies != null) {
				foreach (var item in data.topList) {
					resultList.Add(item, false);
					AddChildren(hierarchies, resultList, 0, item);
				}
			}
			else {
				foreach (var item in data.topList) {
					resultList.Add(item, false);
				}
			}
		}

		static void AddChildren(SymbolRelations<TSymbol, TSymbol> hierarchies, SymbolMenu resultList, byte indentLevel, TSymbol item) {
			var children = hierarchies.GetRelations(item);
			if (children is null) {
				return;
			}
			indentLevel++;
			foreach (var child in children) {
				resultList.Add(child, false).IndentLevel = indentLevel;
				AddChildren(hierarchies, resultList, indentLevel, child);
			}
		}
	}

	sealed class FindSymbolsWithNameCommand
		: CommonListCommand<ISymbol>
	{
		static readonly OptionDescriptor[] __Options = [
			new OptionDescriptor(IconIds.SameName, CommandOptions.ExtractMatch, R.CMDT_FindSymbolWithFullName),
			new OptionDescriptor(IconIds.MatchCase, CommandOptions.MatchCase, R.CMDT_MatchCase),
			PredefinedOptionDescriptors.SourceCodeScope,
			PredefinedOptionDescriptors.ExternalScope
			];

		public override int ImageId => IconIds.FindSymbolsWithName;
		public override string Title => R.CMD_FindSymbolwithName;
		public override string Description => R.CMDT_FindSymbolwithName;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override string ResultLabel => R.T_NameAlike;
		protected override bool UseCtrlRestriction => true;

		public override async Task<ImmutableArray<ISymbol>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var matchCase = Options.MatchFlags(CommandOptions.MatchCase);
			var source = MakeSourceFilterFromOption(Options);
			return await Task.Run(() => ImmutableArray.CreateRange(Context.SemanticModel.Compilation.FindDeclarationMatchName(Symbol.Name, StrictMatch, matchCase, source, default))).ConfigureAwait(false);
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<ISymbol> data) {
			SetupSymbolMenuForResult(resultList, data, true);
		}
	}

	sealed class ListSymbolLocationsCommand : AnalysisListCommandBase<ImmutableArray<SyntaxReference>>
	{
		string _ResultLabel;
		public override int ImageId => IconIds.FileLocations;
		public override string Title => R.CMD_ListSymbolLocations;
		protected override string ResultLabel => _ResultLabel;

		public override async Task<ImmutableArray<SyntaxReference>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var symbol = Symbol;
			if (symbol is INamespaceSymbol ns) {
				symbol = ns.GetCompilationNamespace(Context.SemanticModel);
			}
			return symbol.GetSourceReferences();
		}

		internal void Show(ImmutableArray<SyntaxReference> locations, System.Windows.UIElement relativeElement = null) {
			var m = new SymbolMenu(Context, SymbolListType.Locations);
			UpdateList(m, locations);
			SetupListTitle(m, locations);
			m.Show(relativeElement);
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<SyntaxReference> data) {
			if (data.Length != 0) {
				// source locations
				_ResultLabel = R.T_SourceLocations + data.Length.ToText();
				var locs = new SortedList<(string, string, int), Location>();

				foreach (var item in data) {
					locs[(System.IO.Path.GetDirectoryName(item.SyntaxTree.FilePath),
						 System.IO.Path.GetFileName(item.SyntaxTree.FilePath),
						 item.Span.Start)] = item.ToLocation();
				}

				foreach (var loc in locs) {
					resultList.Add(loc.Value);
				}
			}

			// metadata locations
			foreach (var loc in Symbol.Locations.Where(l => l.IsInMetadata).OrderBy(x => x.MetadataModule.Name)) {
				resultList.Add(loc);
			}
		}
	}
}
