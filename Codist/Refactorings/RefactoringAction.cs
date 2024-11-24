using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Codist.Refactorings
{
	readonly struct RefactoringAction
	{
		// hack: We can not use SyntaxList here
		// there's a bug in that class which removes node positions of Original
		// when nodes are passed in from IEnumerable<SyntaxNode>
		public readonly List<SyntaxNode> Original;
		public readonly List<SyntaxNode> Insert;
		public readonly SyntaxAnnotation Annotation;
		public readonly ActionType ActionType;

		public RefactoringAction(ActionType action, List<SyntaxNode> delete, List<SyntaxNode> insert) {
			ActionType = action;
			Original = delete;
			Insert = insert;
			if (insert == null) {
				Annotation = null;
				return;
			}
			Annotation = new SyntaxAnnotation();
			for (int i = 0; i < insert.Count; i++) {
				insert[i] = insert[i].WithAdditionalAnnotations(Annotation);
			}
		}

		public SyntaxNode FirstOriginal => Original != null && Original.Count != 0
			? Original[0]
			: null;
		public TextSpan OriginalSpan => Original?.Count > 0
			? TextSpan.FromBounds(Original[0].FullSpan.Start, Original[Original.Count - 1].FullSpan.End)
			: default;

		public string GetInsertionString(SyntaxNode root) {
			return String.Concat(root.GetAnnotatedNodes(Annotation).Select(n => n.ToFullString()));
		}
	}
}
