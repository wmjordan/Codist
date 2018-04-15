using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.CodeAnalysis;

namespace Codist.Views
{
	static class QuickInfoOverrider
	{
		/// <summary>Hack into the default QuickInfo panel and provides click and go feature for symbols.</summary>
		public static void ApplyClickAndGoFeature(IList<object> qiContent, ISymbol symbol) {
			if (symbol == null || symbol.DeclaringSyntaxReferences.Length <= 0 || symbol.DeclaringSyntaxReferences[0].SyntaxTree.FilePath == null) {
				return;
			}
			for (int i = 0; i < qiContent.Count; i++) {
				var o = qiContent[i] as Panel;
				if (o == null || o.GetType().Name != "QuickInfoDisplayPanel") {
					continue;
				}
				//(o.GetType().GetProperty("Documentation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(o) as TextBlock)
				//		.TextAlignment = TextAlignment.Justify;
				foreach (var item in o.Children) {
					var title = item as DockPanel;
					if (title == null) {
						continue;
					}
					title.ToolTip = symbol.Name + " is defined in " + System.IO.Path.GetFileName(symbol.DeclaringSyntaxReferences[0].SyntaxTree.FilePath);
					title.Cursor = Cursors.Hand;
					title.MouseEnter += (s, args) => title.Background = SystemColors.HighlightBrush.Alpha(0.3);
					title.MouseLeave += (s, args) => title.Background = Brushes.Transparent;
					title.MouseLeftButtonUp += (s, args) => symbol.GoToSymbol();
					return;
				}
			}
		}

		/// <summary>
		/// Limits the displaying size of the quick info items.
		/// </summary>
		public static void LimitQuickInfoItemSize(IList<object> qiContent) {
			if (Config.Instance.QuickInfoMaxHeight <= 0 && Config.Instance.QuickInfoMaxWidth <= 0 || qiContent.Count == 0) {
				return;
			}
			for (int i = 0; i < qiContent.Count; i++) {
				var item = qiContent[i];
				var p = item as Panel;
				if (p != null) {
					foreach (var pi in p.Children) {
						LimitSize(pi as FrameworkElement);
					}
					continue;
				}
				if (LimitSize(item as FrameworkElement)) {
					continue;
				}
				var s = item as string;
				if (s != null) {
					qiContent[i] = new TextBlock { Text = s, TextWrapping = TextWrapping.Wrap }.LimitSize();
					continue;
				}
			}
			//const string CodistQuickInfo = nameof(CodistQuickInfo);
			//var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto }.LimitSize();
			//var stack = new QuickInfoContainer() { Name = CodistQuickInfo };
			//foreach (var item in qiContent) {
			//	if (stack.Children.Count > 0) {
			//		stack.Children.Add(new System.Windows.Shapes.Rectangle { Height = 10 });
			//	}
			//	var e = item as UIElement ?? new TextBlock { Text = item.ToString() };
			//	var t = e as TextBlock;
			//	if (t != null && t.TextWrapping == TextWrapping.NoWrap) {
			//		t.TextWrapping = TextWrapping.Wrap;
			//	}
			//	stack.Children.Add(e);
			//}
			//scrollViewer.Content = stack;
			//qiContent.Clear();
			//qiContent.Add(scrollViewer);
		}

		static bool LimitSize(FrameworkElement item) {
			if (item != null) {
				item.LimitSize();
				var t = item as TextBlock;
				if (t != null && t.TextWrapping == TextWrapping.NoWrap) {
					t.TextWrapping = TextWrapping.Wrap;
				}
				return true;
			}
			return false;
		}
	}
}
