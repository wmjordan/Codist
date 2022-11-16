using System;

namespace Codist.Refactorings
{
	interface IRefactoring
	{
		int IconId { get; }
		string Title { get; }
		void Refactor(SemanticContext context);
	}

	interface IRefactoring<TSyntax> : IRefactoring
	{
		bool Accept(TSyntax node);
	}
}
