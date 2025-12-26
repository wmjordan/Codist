using System;
using Microsoft.CodeAnalysis;
using R = Codist.Properties.Resources;

namespace Codist.SymbolCommands
{
	sealed class OptionDescriptor(int imageId, CommandOptions options, string description, CommandOptions exclusiveOptions = default, Predicate<ISymbol> applicationFilter = null)
	{
		public int ImageId { get; } = imageId;
		public CommandOptions Options { get; } = options;
		public CommandOptions ExclusiveOptions { get; } = exclusiveOptions;
		public string Description { get; } = description;
		public Predicate<ISymbol> ApplicationFilter { get; } = applicationFilter;
	}

	static class PredefinedOptionDescriptors
	{
		public readonly static OptionDescriptor DirectDerive = new(IconIds.DirectDerive, CommandOptions.DirectDerive, R.CMDT_FindDirectlyDerived);
		public readonly static OptionDescriptor MatchTypeArgument = new(IconIds.MatchTypeArgument, CommandOptions.MatchTypeArgument, R.CMDT_MatchTypeArgument, default, MayHaveMatchTypeArgumentOption);
		public readonly static OptionDescriptor ExtractMatch = new(IconIds.CurrentSymbolOnly, CommandOptions.ExtractMatch, R.CMDT_FindExtract, default, MayHaveExtractMatchOption);
		public readonly static OptionDescriptor CurrentFileScope = new(IconIds.File, CommandOptions.CurrentFile, R.CMDT_ScopeToCurrentFile, CommandOptions.CurrentFile | CommandOptions.CurrentProject | CommandOptions.RelatedProjects);
		public readonly static OptionDescriptor CurrentProjectScope = new(IconIds.Project, CommandOptions.CurrentProject, R.CMDT_ScopeToCurrentProject, CommandOptions.CurrentFile | CommandOptions.CurrentProject | CommandOptions.RelatedProjects);
		public readonly static OptionDescriptor RelatedProjectsScope = new(IconIds.RelatedProjects, CommandOptions.RelatedProjects, R.CMDT_ScopeToRelatedProjects, CommandOptions.CurrentFile | CommandOptions.CurrentProject | CommandOptions.RelatedProjects);
		public readonly static OptionDescriptor SourceCodeScope = new(IconIds.SourceCode, CommandOptions.SourceCode, R.CMDT_ScopeToSourceCode, CommandOptions.SourceCode | CommandOptions.External);
		public readonly static OptionDescriptor ExternalScope = new(IconIds.ExternalSymbol, CommandOptions.External, R.CMDT_ScopeToExternal, CommandOptions.SourceCode | CommandOptions.External);

		static bool MayHaveExtractMatchOption(ISymbol symbol) {
			if (symbol == null
				|| symbol.IsStatic
				|| symbol is IMethodSymbol m && m.MethodKind != MethodKind.Ordinary
				|| symbol is INamedTypeSymbol nt && nt.IsGenericType && !nt.IsBoundedGenericType()) {
				return false;
			}
			var t = symbol.GetReturnType() ?? symbol as INamedTypeSymbol;
			return t?.IsAnyKind(TypeKind.Class, TypeKind.Structure, TypeKind.Interface, TypeKind.Delegate) == true;
		}
		static bool MayHaveMatchTypeArgumentOption(ISymbol symbol) {
			return symbol != null && (symbol is IMethodSymbol m && m.IsBoundedGenericMethod()
					|| symbol is INamedTypeSymbol t && t.IsBoundedGenericType()
					|| symbol.ContainingType?.IsBoundedGenericType() == true);
		}
	}
}
