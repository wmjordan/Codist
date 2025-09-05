using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Options;

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
		public SelectedSyntax<StatementSyntax> SelectedStatementInfo {
			get {
				if (_SelectedStatementInfo.HasValue == false) {
					var ss = _SemanticContext.View.FirstSelectionSpan();
					if (_SemanticContext.IsSourceBufferInView == false) {
						ss = _SemanticContext.MapDownToSourceSpan(ss).FirstOrDefault();
						if (ss.Snapshot == null) {
							return default;
						}
					}
					return (_SelectedStatementInfo = _SemanticContext.Compilation.GetStatements(ss.ToTextSpan())).Value;
				}
				return _SelectedStatementInfo.Value;
			}
		}

		public SyntaxToken Token => _SemanticContext.Token;
		public SyntaxNode Node => _SemanticContext.Node;
		public SyntaxNode NodeIncludeTrivia => _SemanticContext.NodeIncludeTrivia;
		public OptionSet WorkspaceOptions => _SemanticContext.Workspace.Options;

		internal RefactoringAction[] Actions { get; set; }
		internal IRefactoring Refactoring { get; set; }
		internal SyntaxNode NewRoot { get; set; }

		public (SyntaxTriviaList indent, SyntaxTrivia newLine) GetIndentAndNewLine(int position, int indentUnit = -1) {
			return _SemanticContext.GetIndentAndNewLine(position, indentUnit);
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
