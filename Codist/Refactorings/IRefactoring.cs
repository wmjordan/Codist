using System;
using System.Collections.Generic;

namespace Codist.Refactorings
{
	interface IRefactoring
	{
		int IconId { get; }
		string Title { get; }
		bool Accept(RefactoringContext context);
		void Refactor(SemanticContext context);
	}
}
