using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using Codist.Controls;

namespace Codist.QuickInfo
{
	sealed class InfoContainer
	{
		readonly ImmutableArray<object>.Builder _List = ImmutableArray.CreateBuilder<object>();
		public readonly IQuickInfoOverride Override;

		public InfoContainer(IQuickInfoOverride quickInfoOverride) {
			Override = quickInfoOverride;
		}

		public int ItemCount => _List.Count;

		public void Insert(int index, object item) {
			if (item != null) {
				_List.Insert(index, item);
			}
		}
		public void Add(object item) {
			if (item != null) {
				_List.Add(item);
			}
		}

		public StackPanel ToUI() {
			var s = new StackPanel();
			foreach (var item in _List) {
				if (item is UIElement u) {
					s.Children.Add(u);
				}
				else if (item is string t) {
					s.Children.Add(new ThemedTipText(t));
				}
			}
			return s;
		}
	}
}
