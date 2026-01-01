using System;
using Microsoft.CodeAnalysis;

namespace Codist.SymbolCommands
{
	sealed class CommandFactory(SemanticContext context, ISymbol defaultSymbol, SyntaxNode syntaxNode = null)
	{
		public SemanticContext Context => context;
		public ISymbol Symbol => defaultSymbol;
		public SyntaxNode Node => syntaxNode;
		public SemanticCommandBase Create(CommandId id, string symbolNameSubstitution = null) {
			SemanticCommandBase cmd = id switch {
				CommandId.DebugUnitTest => new DebugUnitTestCommand(),
				CommandId.RunUnitTest => new RunUnitTestCommand(),
				CommandId.GoToNode => new GoToNodeCommand(),
				CommandId.SelectNode => new SelectNodeCommand(),
				CommandId.SelectSymbolNode => new SelectSymbolNodeCommand(),
				CommandId.GoToSymbolDefinition => new GotoDefinitionCommand(),
				CommandId.GoToSymbolReturnType => new GoToReturnTypeDefinitionCommand(),
				CommandId.GoToSpecialGenericSymbolReturnType => new GoToSpecialGenericSymbolReturnTypeCommand(),
				CommandId.CopySymbol => new CopySymbolCommand(),
				CommandId.FindExtensionMethods => new FindExtensionMethodsCommand(),
				CommandId.FindReturnTypeExtensionMethods => new FindReturnTypeExtensionMethodsCommand(),
				CommandId.FindSubInterfaces => new FindSubInterfacesCommand(),
				CommandId.FindImplementations => new FindImplementationsCommand(),
				CommandId.FindDerivedClasses => new FindDerivedClassesCommand(),
				CommandId.FindOverrides => new FindOverridesCommand(),
				CommandId.FindReferrers => new FindReferrersCommand(),
				CommandId.FindSymbolsWithName => new FindSymbolsWithNameCommand(),
				CommandId.FindMethodsBySignature => new FindMethodsBySignatureCommand(),
				CommandId.FindConstructorReferrers => new FindConstructorReferrersCommand(),
				CommandId.FindObjectInitializers => new FindObjectInitializersCommand(),
				CommandId.FindParameterAssignments => new FindParameterAssignmentsCommand(),
				CommandId.FindOptionalParameterAssignments => new FindOptionalParameterAssignmentsCommand(),
				CommandId.ListReferencedSymbols => new ListReferencedSymbolsCommand(),
				CommandId.ListSymbolMembers => defaultSymbol.Kind == SymbolKind.Namespace ? new ListNamespaceMembersCommand() : new ListTypeMembersCommand(),
				CommandId.ListReturnTypeMembers => new ListReturnTypeMembersCommand(),
				CommandId.ListSpecialGenericReturnTypeMembers => new ListSpecialGenericReturnTypeMembersCommand(),
				CommandId.FindInstanceProducers => new FindInstanceProducersCommand(),
				CommandId.FindInstanceConsumers => new FindInstanceConsumersCommand(),
				CommandId.FindContainingTypeInstanceProducers => new FindContainingTypeInstanceProducersCommand(),
				CommandId.FindContainingTypeInstanceConsumers => new FindContainingTypeInstanceConsumersCommand(),
				CommandId.FindTypeReferrers => new FindTypeReferrersCommand(),
				CommandId.ListSymbolLocations => new ListSymbolLocationsCommand(),
				CommandId.ListEventArgsMembers => new ListEventArgsMembersCommand(),
				_ => throw new NotImplementedException(),
			};
			cmd.Context = context;
			cmd.Symbol = defaultSymbol;
			cmd.Node = syntaxNode;
			cmd.TitlePlaceHolderSubstitution = symbolNameSubstitution;
			return cmd;
		}
	}
}
