using System;
using System.Collections.Generic;

namespace Codist
{
	readonly struct SelectedSyntax<TNode>
	{
		public readonly TNode Preceding, Following;
		public readonly List<TNode> Items;

		public SelectedSyntax(TNode preceding, List<TNode> items, TNode following) {
			Preceding = preceding;
			Items = items;
			Following = following;
		}
	}
}
