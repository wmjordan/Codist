using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CLR;
using Microsoft.CodeAnalysis;
using R = Codist.Properties.Resources;

namespace Codist.SymbolCommands
{
	sealed class CopySymbolCommand : SemanticCommandBase
	{
		public override int ImageId => IconIds.Copy;
		public override string Title => R.CMD_CopySymbol;
		public override string Description => R.CMDT_CopySymbol;
		public override bool CanRefresh => false;

		public override IEnumerable<SemanticCommandBase> GetSubCommands() {
			if (Symbol is IFieldSymbol f && f.ConstantValue != null) {
				yield return new CopyConstantValueCommand { Value = f.ConstantValue };
			}
			yield return new CopyTypeQualifiedSymbolNameCommand { Symbol = Symbol, Context = Context };
			if (Symbol != null) {
				if (Symbol.IsQualifiable()) {
					yield return new CopyFullyQualifiedSymbolNameCommand { Symbol = Symbol, Context = Context };
				}
				if (Symbol.Kind != SymbolKind.Namespace) {
					yield return new CopySymbolDefinitionCommand { Symbol = Symbol, Context = Context };
				}
			}
		}

		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			if (Options.MatchFlags(CommandOptions.Alternative)) {
				CopyQualifiedSymbolName(Symbol, true);
			}
			else {
				TryCopy(Symbol.GetOriginalName());
			}
		}

		static void TryCopy(string content) {
			try {
				System.Windows.Clipboard.SetDataObject(content);
			}
			catch (SystemException) {
				// ignore failure
			}
		}

		static void CopyQualifiedSymbolName(ISymbol symbol, bool fullyQualified) {
			var s = symbol.OriginalDefinition;
			string t;
			switch (s.Kind) {
				case SymbolKind.Namespace:
				case SymbolKind.NamedType:
					t = s.ToDisplayString(CodeAnalysisHelper.QualifiedTypeNameFormat);
					break;
				case SymbolKind.Method:
					var m = s as IMethodSymbol;
					if (m.ReducedFrom != null) {
						s = m.ReducedFrom;
					}
					if (m.MethodKind == MethodKind.Constructor) {
						s = m.ContainingType;
						goto case SymbolKind.NamedType;
					}
					else if (m.MethodKind == MethodKind.ExplicitInterfaceImplementation) {
						t = m.Name;
						break;
					}
					goto default;
				default:
					t = s.ToDisplayString(fullyQualified ? CodeAnalysisHelper.QualifiedTypeNameFormat : CodeAnalysisHelper.TypeMemberNameFormat);
					break;
			}
			TryCopy(t);
		}

		sealed class CopyTypeQualifiedSymbolNameCommand : SemanticCommandBase
		{
			public override int ImageId => IconIds.Class;
			public override string Title => R.CMDT_CopyQualifiedName;
			public override string Description => R.CMDT_CopyQualifiedName;

			public override async Task ExecuteAsync(CancellationToken cancellationToken) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				CopyQualifiedSymbolName(Symbol, false);
			}
		}
		sealed class CopyFullyQualifiedSymbolNameCommand : SemanticCommandBase
		{
			public override int ImageId => IconIds.Namespace;
			public override string Title => R.CMDT_CopyFullyQualifiedName;
			public override string Description => R.CMDT_CopyFullyQualifiedName;

			public override async Task ExecuteAsync(CancellationToken cancellationToken) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				CopyQualifiedSymbolName(Symbol, true);
			}
		}
		sealed class CopySymbolDefinitionCommand : SemanticCommandBase
		{
			public override int ImageId => IconIds.Definition;
			public override string Title => R.CMDT_CopyDefinition;
			public override string Description => R.CMDT_CopyDefinition;
			public override bool CanRefresh => true;

			public override async Task ExecuteAsync(CancellationToken cancellationToken) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				var s = Symbol.OriginalDefinition;
				TryCopy(s.Kind == SymbolKind.NamedType
					? ((INamedTypeSymbol)s).GetDefinition(CodeAnalysisHelper.DefinitionNameFormat)
					: s.ToDisplayString(CodeAnalysisHelper.DefinitionNameFormat));
			}
		}
		sealed class CopyConstantValueCommand : SemanticCommandBase
		{
			public override int ImageId => IconIds.Constant;
			public override string Title => R.CMD_CopyConstantValue;
			public override string Description => R.CMD_CopyConstantValue;
			public object Value { get; set; }
			public override async Task ExecuteAsync(CancellationToken cancellationToken) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				TryCopy(Value?.ToString() ?? "null");
			}
		}
	}

	sealed class GotoDefinitionCommand : SemanticCommandBase
	{
		public override int ImageId => IconIds.GoToDefinition;
		public override string Title => R.CMD_GoToDefinition;

		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			var locs = Symbol.GetSourceReferences();
			if (locs.Length == 1) {
				locs[0].GoToSource();
			}
			else {
				new SymbolCommands.ListSymbolLocationsCommand { Symbol = Symbol, Context = Context }.Show(locs);
			}
		}
	}

	class GoToReturnTypeDefinitionCommand : SemanticCommandBase
	{
		public override int ImageId => IconIds.GoToReturnType;
		public override string Title => R.CMD_GoTo;
		public override string Description => R.CMDT_GoToTypeDefinition;

		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			Symbol.GetReturnType().ResolveElementType().GoToSource();
		}
	}

	sealed class GoToSpecialGenericSymbolReturnTypeCommand : GoToReturnTypeDefinitionCommand
	{
		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			Symbol.GetReturnType().ResolveElementType().ResolveSingleGenericTypeArgument().GoToSource();
		}
	}
}
