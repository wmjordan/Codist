using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using R = Codist.Properties.Resources;

namespace Codist.SymbolCommands
{
	sealed class DebugUnitTestCommand : SemanticCommandBase
	{
		public override int ImageId => IconIds.DebugTest;
		public override string Title => R.CMD_DebugUnitTest;

		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			if (Context.Node.FirstAncestorOrSelf<SyntaxNode>(n => n.IsAnyKind(SyntaxKind.ClassDeclaration, SyntaxKind.MethodDeclaration)) != Node) {
				Context.View.MoveCaret(Node.SpanStart);
			}
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			TextEditorHelper.ExecuteEditorCommand("TestExplorer.DebugAllTestsInContext");
		}
	}

	sealed class RunUnitTestCommand : SemanticCommandBase
	{
		public override int ImageId => IconIds.RunTest;
		public override string Title => R.CMD_RunUnitTest;

		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			if (Context.Node.FirstAncestorOrSelf<SyntaxNode>(n => n.IsAnyKind(SyntaxKind.ClassDeclaration, SyntaxKind.MethodDeclaration)) != Node) {
				Context.View.MoveCaret(Node.SpanStart);
			}
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			TextEditorHelper.ExecuteEditorCommand("TestExplorer.RunAllTestsInContext");
		}
	}

	sealed class GoToNodeCommand : SemanticCommandBase
	{
		public override int ImageId => IconIds.GoToDefinition;
		public override string Title => R.CMD_GoToDefinition;

		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			Node.GetReference().GoToSource();
		}
	}

	sealed class SelectNodeCommand : SemanticCommandBase
	{
		public override int ImageId => IconIds.SelectCode;
		public override string Title => R.CMD_SelectCode;

		public override IEnumerable<SemanticCommandBase> GetSubCommands() {
			yield return new SelectNodeWithoutTriviaCommand { Node = Node, Context = Context };
		}

		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			Node.SelectNode(true);
		}

		sealed class SelectNodeWithoutTriviaCommand : SemanticCommandBase
		{
			public override int ImageId => IconIds.SelectCodeWithoutTrivia;
			public override string Title => R.CMDT_SelectCodeWithoutTrivia;
			public override string Description => R.CMDT_SelectCodeWithoutTrivia;

			public override async Task ExecuteAsync(CancellationToken cancellationToken) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				Node.SelectNode(false);
			}
		}
	}

	sealed class SelectSymbolNodeCommand : SemanticCommandBase
	{
		public override int ImageId => IconIds.SelectCode;
		public override string Title => R.CMD_SelectCode;
		public override IEnumerable<SemanticCommandBase> GetSubCommands() {
			yield return new SelectNodeWithoutTriviaCommand { Symbol = Symbol, Context = Context };
		}

		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			Symbol.GetSyntaxNode(cancellationToken).SelectNode(true);
		}

		sealed class SelectNodeWithoutTriviaCommand : SemanticCommandBase
		{
			public override int ImageId => IconIds.SelectCodeWithoutTrivia;
			public override string Title => R.CMDT_SelectCodeWithoutTrivia;
			public override string Description => R.CMDT_SelectCodeWithoutTrivia;

			public override async Task ExecuteAsync(CancellationToken cancellationToken) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				Symbol.GetSyntaxNode(cancellationToken).SelectNode(false);
			}
		}
	}

	sealed class ListReferencedSymbolsCommand : AnalysisListCommandBase<KeyValuePair<ISymbol, int>[]>
	{
		string _ResultLabel;
		public override int ImageId => IconIds.ListReferencedSymbols;
		public override string Title => R.CMD_ListReferencedSymbols;
		public override string Description => R.CMDT_ListReferencedSymbols;
		protected override string ResultLabel => _ResultLabel;

		public override Task<KeyValuePair<ISymbol, int>[]> PrepareListDataAsync(CancellationToken cancellationToken) {
			var data = Node.FindReferencingSymbols(Context.SemanticModel, true, cancellationToken);
			_ResultLabel = R.T_ReferencedSymbols + data.Length.ToText();
			return Task.FromResult(data);
		}

		public override void UpdateList(SymbolMenu resultList, KeyValuePair<ISymbol, int>[] data) {
			var loc = Node.SyntaxTree.FilePath;
			var containerType = Symbol.ContainingType ?? Symbol;
			foreach (var sr in data) {
				var s = sr.Key;
				var sl = s.GetSourceReferences()[0];
				SymbolItem i;
				if (sl.SyntaxTree.FilePath != loc) {
					i = resultList.Add(sl.ToLocation());
					i.Content.FontWeight = FontWeights.Bold;
					i.Content.HorizontalAlignment = HorizontalAlignment.Center;
					loc = sl.SyntaxTree.FilePath;
				}
				i = resultList.Add(s, false);
				if (s.ContainingType.Equals(containerType) == false) {
					i.Hint = (s.ContainingType ?? s).ToDisplayString(CodeAnalysisHelper.MemberNameFormat);
				}
				if (sr.Value > 1) {
					i.Hint += " @" + sr.Value.ToText();
				}
			}
		}
	}
}
