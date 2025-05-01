using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using Codist.Controls;

namespace Codist.QuickInfo
{
	sealed class InfoContainer : InfoBlock
	{
		readonly ImmutableArray<object>.Builder _List = ImmutableArray.CreateBuilder<object>();

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

		public override UIElement ToUI() {
			if (_List.Count == 1 && _List[0] is InfoBlock b) {
				return b.ToUI();
			}
			var s = new StackPanel();
			foreach (var item in _List) {
				s.Add(MakeUIElement(item));
			}
			return s;
		}

		static UIElement MakeUIElement(object item) {
			if (item is InfoBlock b) {
				return b.ToUI();
			}
			else if (item is UIElement u) {
				return u;
			}
			else if (item is string t) {
				return new ThemedTipText(t);
			}
			else {
				return item != null
					? new ThemedTipText(item.ToString())
					: (UIElement)new ContentPresenter();
			}
		}
	}
}
