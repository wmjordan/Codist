using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using R = Codist.Properties.Resources;

namespace Codist.SymbolCommands
{
	sealed class FindDerivedClassesCommand
		: HierarchicalListCommand<INamedTypeSymbol>
	{
		static readonly OptionDescriptor[] __Options = [
			PredefinedOptionDescriptors.DirectDerive,
			PredefinedOptionDescriptors.CurrentProjectScope,
			PredefinedOptionDescriptors.RelatedProjectsScope,
			PredefinedOptionDescriptors.SourceCodeScope,
			PredefinedOptionDescriptors.ExternalScope
		];
		string _ResultLabel;

		public override int ImageId => IconIds.FindDerivedTypes;
		public override string Title => R.CMD_FindDerivedClasses;
		public override string Description => R.CMDT_FindDerivedClasses;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override string ResultLabel => _ResultLabel;

		public override async Task<(Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>, IReadOnlyList<INamedTypeSymbol>)> PrepareListDataAsync(CancellationToken cancellationToken) {
			var projects = MakeProjectListFromOption(Options);
			var source = MakeSourceFilterFromOption(Options);
			var orderByHierarchy = !UIHelper.IsShiftDown;
			var type = ((INamedTypeSymbol)Symbol);
			var original = type.OriginalDefinition;
#if LOG || DEBUG
			var typeName = original.GetTypeName();
#endif
			var classes = await SymbolFinder.FindDerivedClassesAsync(original, Context.Document.Project.Solution, projects.MakeImmutableSet(), cancellationToken).ConfigureAwait(false);
			classes = source.Filter(classes);
			if (type.IsBoundedGenericType()) {
				classes = classes.Where(i => !original.Equals(i.BaseType.OriginalDefinition) || type.Equals(i.BaseType));
			}
			if (DirectDerive) {
				_ResultLabel = R.T_DirectlyDerivedClasses;
				return (default, classes.Where(c => c.BaseType.OriginalDefinition.MatchWith(original)).MakeSortedSymbolList());
			}
			_ResultLabel = R.T_DerivedClasses;
			if (!orderByHierarchy) {
				return (default, classes.MakeSortedSymbolList());
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
			return (hierarchies, classes.MakeSortedSymbolList());
		}

		public override void UpdateList(SymbolMenu resultList, (Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> hierarchicalData, IReadOnlyList<INamedTypeSymbol> topList) data) {
			SetupList(data, resultList);
		}
	}

	sealed class FindImplementationsCommand
		: CommonListCommand<ISymbol>
	{
		static readonly OptionDescriptor[] __Options = [new OptionDescriptor(IconIds.DirectDerive, CommandOptions.DirectDerive, R.CMDT_FindDirectImplementations), PredefinedOptionDescriptors.CurrentProjectScope, PredefinedOptionDescriptors.RelatedProjectsScope, PredefinedOptionDescriptors.SourceCodeScope, PredefinedOptionDescriptors.ExternalScope];

		string _ResultLabel;

		public override int ImageId => IconIds.FindImplementations;
		public override string Title => R.CMD_FindImplementations;
		public override string Description => R.CMDT_FindImplementations;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override string ResultLabel => _ResultLabel;

		public override async Task<ImmutableArray<ISymbol>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var directImplementation = DirectDerive;
			var projects = MakeProjectListFromOption(Options);
			var source = MakeSourceFilterFromOption(Options);
			return await (Symbol as INamedTypeSymbol).FindImplementationsAsync(Context.Document.Project.Solution, directImplementation, source, projects, cancellationToken).ConfigureAwait(false);
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<ISymbol> data) {
			if (Symbol.Kind == SymbolKind.NamedType) {
				foreach (var item in data) {
					resultList.Add(item, false);
				}
				resultList.ExtIconProvider = GetExtIconProviderForNamedType;
			}
			else {
				foreach (var item in data) {
					resultList.Add(item, item.ContainingType);
				}
			}
			_ResultLabel = R.T_Implementations + data.Length.ToText();
		}

		StackPanel GetExtIconProviderForNamedType(SymbolItem i) {
			var p = ExtIconProvider.Default.GetExtIcons(i);
			if (i.Symbol.IsDirectImplementationOf(Symbol)) {
				(p ??= new StackPanel().MakeHorizontal())
					.Children
					.Add(VsImageHelper.GetImage(IconIds.InterfaceImplementation));
			}
			return p;
		}
	}

	sealed class FindSubInterfacesCommand
		: CommonListCommand<INamedTypeSymbol>
	{
		static readonly OptionDescriptor[] __Options = [PredefinedOptionDescriptors.DirectDerive, PredefinedOptionDescriptors.SourceCodeScope, PredefinedOptionDescriptors.ExternalScope];
		string _ResultLabel;

		public override int ImageId => IconIds.FindDerivedTypes;
		public override string Title => R.CMD_FindInheritedInterfaces;
		public override string Description => R.CMDT_FindInheritedInterfaces;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override string ResultLabel => _ResultLabel;

		public override async Task<ImmutableArray<INamedTypeSymbol>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var directDerive = DirectDerive;
			var source = MakeSourceFilterFromOption(Options);
			_ResultLabel = directDerive ? R.T_DirectlyDerivedInterfaces : R.T_DerivedInterfaces;
			return await (Symbol as INamedTypeSymbol).FindDerivedInterfacesAsync(Context.Document.Project, directDerive, source, cancellationToken).ConfigureAwait(false);
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<INamedTypeSymbol> data) {
			SetupSymbolMenuForResult(resultList, data, false);
		}
	}

	sealed class FindOverridesCommand
		: AnalysisListCommandBase<ImmutableArray<ISymbol>>
	{
		static readonly OptionDescriptor[] __Options = [
			PredefinedOptionDescriptors.CurrentProjectScope, PredefinedOptionDescriptors.RelatedProjectsScope, PredefinedOptionDescriptors.SourceCodeScope, PredefinedOptionDescriptors.ExternalScope
			];
		string _ResultLabel;

		public override int ImageId => IconIds.FindOverloads;
		public override string Title => R.CMD_FindOverrides;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override string ResultLabel => _ResultLabel;

		public override async Task<ImmutableArray<ISymbol>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var projects = MakeProjectListFromOption(Options);
			var source = MakeSourceFilterFromOption(Options);
			return (await Symbol.FindOverridesAsync(Context.Document.Project.Solution, source, projects, cancellationToken).ConfigureAwait(false)).MakeSortedSymbolArray();
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<ISymbol> data) {
			foreach (var item in data) {
				resultList.Add(item, item.ContainingType);
			}
			_ResultLabel = R.T_Overrides + data.Length.ToText();
		}
	}
}
