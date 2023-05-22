using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Codist.Refactorings
{
	sealed class RefactoringContext
	{
		readonly SemanticContext _SemanticContext;
		SelectedSyntax<StatementSyntax>? _SelectedStatementInfo;

		public RefactoringContext(SemanticContext semanticContext) {
			_SemanticContext = semanticContext;
		}

		public SemanticContext SemanticContext => _SemanticContext;
		public SelectedSyntax<StatementSyntax> SelectedStatementInfo => (_SelectedStatementInfo ?? (_SelectedStatementInfo = _SemanticContext.Compilation.GetStatements(_SemanticContext.View.FirstSelectionSpan().ToTextSpan()))).Value;
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

		public (SyntaxTriviaList indent, SyntaxTrivia newLine) GetIndentAndNewLine(int position, int indentUnit = -1) {
			var options = WorkspaceOptions;
			if (indentUnit < 0) {
				indentUnit = Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.DoubleIndentRefactoring) ? 2 : 1;
			}
			string indent = SemanticContext.View.TextSnapshot.GetLinePrecedingWhitespaceAtPosition(position)
				+ options.GetIndentString(indentUnit);
			return (SF.TriviaList(SF.Whitespace(indent)), SF.Whitespace(options.GetNewLineString()));
		}

		public bool AcceptAny(Refactorings.IRefactoring[] refactorings) {
			foreach (var item in refactorings) {
				if (item.Accept(this)) {
					return true;
				}
			}
			return false;
		}
	}
}
