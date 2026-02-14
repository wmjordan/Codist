using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using R = Codist.Properties.Resources;

namespace Codist.SymbolCommands
{
	sealed class FindMethodsBySignatureCommand
		: CommonListCommand<ISymbol>
	{
		static readonly OptionDescriptor[] __Options = [
			new OptionDescriptor(IconIds.ExcludeGeneric, CommandOptions.NoTypeArgument, R.CMDT_ExcludeGenerics),
			PredefinedOptionDescriptors.SourceCodeScope,
			PredefinedOptionDescriptors.ExternalScope
		];

		public override int ImageId => IconIds.FindMethodsMatchingSignature;
		public override string Title => R.CMD_FindMethodsSameSignature;
		public override string Description => R.CMDT_FindMethodsSameSignature;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override string ResultLabel => R.T_SignatureMatch;

		public override Task<ImmutableArray<ISymbol>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var excludeGenerics = Options.MatchFlags(CommandOptions.NoTypeArgument);
			var source = MakeSourceFilterFromOption(Options);
			return Task.Run(() => (Context.SemanticModel.Compilation.FindMethodBySignature(Symbol, excludeGenerics, source, cancellationToken)).MakeSortedSymbolArray());
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<ISymbol> data) {
			SetupSymbolMenuForResult(resultList, data, true);
		}
	}

	sealed class FindExtensionMethodsCommand
		: CommonListCommand<IMethodSymbol>
	{
		static readonly OptionDescriptor[] __Options = [
			PredefinedOptionDescriptors.ExtractMatch,
			new OptionDescriptor(IconIds.MatchTypeArgument, CommandOptions.MatchTypeArgument, R.CMDT_MatchTypeArgument),
			PredefinedOptionDescriptors.CurrentFileScope,
			PredefinedOptionDescriptors.CurrentProjectScope,
			PredefinedOptionDescriptors.RelatedProjectsScope
		];

		public override int ImageId => IconIds.ExtensionMethod;
		public override string Title => R.CMD_FindExtensions;
		public override string Description => R.CMDT_FindExtensions;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override string ResultLabel => R.T_Extensions;
		protected override bool UseCtrlRestriction => true;

		public override async Task<ImmutableArray<IMethodSymbol>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var source = MakeSourceFilterFromOption(Options);
			return await (Symbol as ITypeSymbol).FindExtensionMethodsAsync(Context.Document.Project, StrictMatch, MatchTypeArgument, source, cancellationToken).ConfigureAwait(false);
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<IMethodSymbol> data) {
			SetupSymbolMenuForResult(resultList, data, true);
		}
	}

	sealed class FindReturnTypeExtensionMethodsCommand
		: CommonListCommand<IMethodSymbol>
	{
		static readonly OptionDescriptor[] __Options = [
			PredefinedOptionDescriptors.ExtractMatch,
			PredefinedOptionDescriptors.CurrentFileScope,
			PredefinedOptionDescriptors.CurrentProjectScope,
			PredefinedOptionDescriptors.RelatedProjectsScope
		];


		public override int ImageId => IconIds.ExtensionMethod;
		public override string Title => R.CMD_FindExtensionsFor;
		public override string Description => R.CMDT_FindTypeExtensionMethods;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override string ResultLabel => R.T_Extensions;
		protected override ISymbol ResultSymbol => Symbol.GetReturnType();
		protected override bool UseCtrlRestriction => true;

		public override async Task<ImmutableArray<IMethodSymbol>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var source = MakeSourceFilterFromOption(Options);
			return await Symbol.GetReturnType().FindExtensionMethodsAsync(Context.Document.Project, StrictMatch, MatchTypeArgument, source, cancellationToken).ConfigureAwait(false);
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<IMethodSymbol> data) {
			SetupSymbolMenuForResult(resultList, data, true);
		}
	}

	class FindParameterAssignmentsCommand : AnalysisListCommandBase<IReadOnlyCollection<KeyValuePair<ISymbol, List<(ArgumentAssignment assignment, Location location, ExpressionSyntax expression)>>>>
	{
		public override int ImageId => IconIds.FindParameterAssignment;
		public override string Title => R.CMD_FindAssignmentsFor;
		public override string Description => R.CMDT_FindAssignmentsFor;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => null;
		protected override string ResultLabel => null;
		protected override bool UseCtrlRestriction => true;

		public override Task<IReadOnlyCollection<KeyValuePair<ISymbol, List<(ArgumentAssignment assignment, Location location, ExpressionSyntax expression)>>>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var docs = MakeDocumentListFromOption(Options);
			return (Symbol as IParameterSymbol).FindParameterAssignmentsAsync(Context.Document.Project, docs, StrictMatch, ArgumentAssignmentFilter.Undefined, cancellationToken);
		}

		protected override void SetupListTitle(SymbolMenu resultList, IReadOnlyCollection<KeyValuePair<ISymbol, List<(ArgumentAssignment assignment, Location location, ExpressionSyntax expression)>>> data) {
			int totalCount = data.Sum(d => d.Value.Count);
			resultList.Title.SetGlyph(IconIds.Argument)
				.AddSymbol(Symbol, null, true, SymbolFormatter.Instance)
				.Append(R.T_AssignmentLocations + totalCount);
		}

		public override void UpdateList(SymbolMenu resultList, IReadOnlyCollection<KeyValuePair<ISymbol, List<(ArgumentAssignment assignment, Location location, ExpressionSyntax expression)>>> data) {
			resultList.ContainerType = SymbolListType.SymbolReferrers;
			resultList.ExtIconProvider = ExtIconProvider.Default.GetExtIconsWithUsage;

			foreach (var site in data) {
				for (int i = 0; i < site.Value.Count; i++) {
					var location = site.Value[i];
					var symItem = resultList.Add(site.Key, false);
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
	}

	sealed class FindOptionalParameterAssignmentsCommand : FindParameterAssignmentsCommand
	{
		static readonly OptionDescriptor[] __Options = [
			new OptionDescriptor(IconIds.ExplicitAssignment, CommandOptions.Explicit, R.CMDT_ExplicitAssignment, CommandOptions.Explicit | CommandOptions.Implicit),
			new OptionDescriptor(IconIds.DefaultAssignment, CommandOptions.Implicit, R.CMDT_DefaultAssignment, CommandOptions.Explicit | CommandOptions.Implicit)
		];

		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;

		public override Task<IReadOnlyCollection<KeyValuePair<ISymbol, List<(ArgumentAssignment assignment, Location location, ExpressionSyntax expression)>>>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var docs = MakeDocumentListFromOption(Options);
			var filter = Options.MatchFlags(CommandOptions.Explicit) ? ArgumentAssignmentFilter.ExplicitValue
				: Options.MatchFlags(CommandOptions.Implicit) ? ArgumentAssignmentFilter.DefaultValue
				: ArgumentAssignmentFilter.Undefined;
			return (Symbol as IParameterSymbol).FindParameterAssignmentsAsync(Context.Document.Project, docs, false, filter, cancellationToken);
		}
	}
}
