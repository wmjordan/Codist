using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Codist.Refactorings
{
	sealed class RefactoringContext
	{
		readonly SemanticContext _SemanticContext;
		SelectedStatementInfo? _SelectedStatementInfo;

		public RefactoringContext(SemanticContext semanticContext) {
			_SemanticContext = semanticContext;
		}

		public SemanticContext SemanticContext => _SemanticContext;
		public SelectedStatementInfo SelectedStatementInfo => (_SelectedStatementInfo ?? (_SelectedStatementInfo = _SemanticContext.Compilation.GetStatements(_SemanticContext.View.FirstSelectionSpan().ToTextSpan()))).Value;
		public SyntaxToken Token => _SemanticContext.Token;
		public SyntaxNode Node => _SemanticContext.Node;
		public SyntaxNode NodeIncludeTrivia => _SemanticContext.NodeIncludeTrivia;
		public OptionSet WorkspaceOptions => _SemanticContext.Workspace.Options;

		internal RefactoringAction[] Actions { get; set; }
		internal IRefactoring Refactoring { get; set; }
		internal SyntaxNode NewRoot { get; set; }

		public (SyntaxTriviaList indent, SyntaxTrivia newLine) GetIndentAndNewLine(SyntaxNode node) {
			return GetIndentAndNewLine((node.GetContainingStatementOrDeclaration() ?? node).SpanStart);
		}

		public (SyntaxTriviaList indent, SyntaxTrivia newLine) GetIndentAndNewLine(int position) {
			var options = WorkspaceOptions;
			return (SF.TriviaList(SF.Whitespace(SemanticContext.View.TextSnapshot.GetLinePrecedingWhitespaceAtPosition(position)))
				.Add(SF.Whitespace(options.GetIndentString(Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.DoubleIndentRefactoring) ? 2 : 1))),
				SF.Whitespace(options.GetNewLineString())
				);
		}
	}
}
