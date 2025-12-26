using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using R = Codist.Properties.Resources;

namespace Codist.SymbolCommands
{
	sealed class FindInstanceProducersCommand : CommonListCommand<ISymbol>
	{
		static readonly OptionDescriptor[] __Options = [
			PredefinedOptionDescriptors.ExtractMatch,
			PredefinedOptionDescriptors.SourceCodeScope, PredefinedOptionDescriptors.ExternalScope
		];

		public override int ImageId => IconIds.InstanceProducer;
		public override string Title => R.CMD_FindInstanceProducer;
		public override string Description => R.CMDT_FindInstanceProducer;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override string ResultLabel => R.T_Producers;

		public override Task<ImmutableArray<ISymbol>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var source = MakeSourceFilterFromOption(Options);
			return (Symbol as ITypeSymbol).FindSymbolInstanceProducerAsync(Context.Document.Project, IsStrictMatch, source == SymbolSourceFilter.RequiresSource, cancellationToken);
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<ISymbol> data) {
			SetupSymbolMenuForResult(resultList, data, true);
		}
	}

	sealed class FindInstanceConsumersCommand : CommonListCommand<ISymbol>
	{
		static readonly OptionDescriptor[] __Options = [
			PredefinedOptionDescriptors.ExtractMatch,
			PredefinedOptionDescriptors.SourceCodeScope, PredefinedOptionDescriptors.ExternalScope
		];

		public override int ImageId => IconIds.Argument;
		public override string Title => R.CMD_FindInstanceAsParameter;
		public override string Description => R.CMDT_FindInstanceAsParameter;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override string ResultLabel => R.T_AsParameter;

		public override Task<ImmutableArray<ISymbol>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var source = MakeSourceFilterFromOption(Options);
			return (Symbol as ITypeSymbol).FindInstanceAsParameterAsync(Context.Document.Project, IsStrictMatch, source == SymbolSourceFilter.RequiresSource, cancellationToken);
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<ISymbol> data) {
			SetupSymbolMenuForResult(resultList, data, true);
		}
	}

	sealed class FindContainingTypeInstanceProducersCommand : CommonListCommand<ISymbol>
	{
		static readonly OptionDescriptor[] __Options = [
			PredefinedOptionDescriptors.ExtractMatch,
			PredefinedOptionDescriptors.SourceCodeScope, PredefinedOptionDescriptors.ExternalScope
		];

		public override int ImageId => IconIds.InstanceProducer;
		public override string Title => R.CMD_FindInstanceProducer;
		public override string Description => R.CMDT_FindContainingTypeInstanceProducer;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override ISymbol ResultSymbol => Symbol.ContainingType;
		protected override string ResultLabel => R.T_Producers;

		public override Task<ImmutableArray<ISymbol>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var source = MakeSourceFilterFromOption(Options);
			return Symbol.ContainingType.FindSymbolInstanceProducerAsync(Context.Document.Project, IsStrictMatch, source == SymbolSourceFilter.RequiresSource, cancellationToken);
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<ISymbol> data) {
			SetupSymbolMenuForResult(resultList, data, true);
		}
	}

	sealed class FindContainingTypeInstanceConsumersCommand : CommonListCommand<ISymbol>
	{
		static readonly OptionDescriptor[] __Options = [
			PredefinedOptionDescriptors.ExtractMatch,
			PredefinedOptionDescriptors.SourceCodeScope, PredefinedOptionDescriptors.ExternalScope
		];

		public override int ImageId => IconIds.Argument;
		public override string Title => R.CMD_FindInstanceAsParameter;
		public override string Description => R.CMDT_FindContainingTypeInstanceAsParameter;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override ISymbol ResultSymbol => Symbol.ContainingType;
		protected override string ResultLabel => R.T_AsParameter;

		public override Task<ImmutableArray<ISymbol>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var source = MakeSourceFilterFromOption(Options);
			return Symbol.ContainingType.FindInstanceAsParameterAsync(Context.Document.Project, IsStrictMatch, source == SymbolSourceFilter.RequiresSource, cancellationToken);
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<ISymbol> data) {
			SetupSymbolMenuForResult(resultList, data, true);
		}
	}
}
