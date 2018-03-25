using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Codist.Views
{
	static class QuickInfoOverrider
	{
		/// <summary>
		/// Displays selection character count and line count in quick info.
		/// </summary>
		public static void ShowSelectionInfo(IQuickInfoSession session, IList<object> qiContent, SnapshotPoint point) {
			var selection = session.TextView.Selection;
			if (selection.IsEmpty != false) {
				return;
			}
			var p1 = selection.Start.Position;
			var p2 = selection.End.Position;
			if (p1.Snapshot != point.Snapshot // in the C# Interactive window, the snapshots could be different ones
				|| p1 > point || point > p2) {
				return;
			}
			var c = 0;
			foreach (var item in selection.SelectedSpans) {
				c += item.Length;
			}
			if (c < 2) {
				return;
			}
			var y1 = point.Snapshot.GetLineNumberFromPosition(p1);
			var y2 = point.Snapshot.GetLineNumberFromPosition(p2) + 1;
			var info = new TextBlock().AddText("Selection: ", true).AddText(c.ToString()).AddText(" characters");
			if (y2 - y1 > 1) {
				info.AddText(", " + (y2 - y1).ToString() + " lines");
			}
			qiContent.Add(info);
		}

		/// <summary>Provides click and go feature for symbols.</summary>
		public static void ApplyClickAndGoFeature(IList<object> qiContent, ISymbol symbol) {
			for (int i = 0; i < qiContent.Count; i++) {
				var o = qiContent[i] as Panel;
				if (o == null || o.GetType().Name != "QuickInfoDisplayPanel") {
					continue;
				}
				//(o.GetType().GetProperty("Documentation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(o) as TextBlock)
				//		.TextAlignment = TextAlignment.Justify;
				if (symbol == null || symbol.DeclaringSyntaxReferences.Length <= 0 || symbol.DeclaringSyntaxReferences[0].SyntaxTree.FilePath == null) {
					continue;
				}
				foreach (var item in o.Children) {
					var title = item as DockPanel;
					if (title == null) {
						continue;
					}
					title.ToolTip = symbol.Name + " is defined in " + System.IO.Path.GetFileName(symbol.DeclaringSyntaxReferences[0].SyntaxTree.FilePath);
					title.Cursor = Cursors.Hand;
					MouseButtonEventHandler m = (s, args) => symbol.GoToSymbol();
					MouseEventHandler hover = (s, args) => title.Background = SystemColors.HighlightBrush.Alpha(0.3);
					MouseEventHandler leave = (s, args) => title.Background = Brushes.Transparent;
					RoutedEventHandler unload = (s, args) => {
						title.MouseEnter -= hover;
						title.MouseLeave -= leave;
						title.MouseLeftButtonUp -= m;
					};
					title.MouseEnter += hover;
					title.MouseLeave += leave;
					title.MouseLeftButtonUp += m;
					title.Unloaded += unload;
					return;
				}
			}
		}

		/// <summary>
		/// Limits the displaying size of the quick info items by moving them into a <see cref="ScrollViewer"/>.
		/// </summary>
		public static void LimitQuickInfoSize(IList<object> qiContent) {
			if (Config.Instance.QuickInfoMaxHeight <= 0 && Config.Instance.QuickInfoMaxWidth <= 0 || qiContent.Count == 0) {
				return;
			}
			var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto }.LimitSize();
			var stack = new StackPanel();
			foreach (var item in qiContent) {
				if (stack.Children.Count > 0) {
					stack.Children.Add(new System.Windows.Shapes.Rectangle { Height = 10 });
				}
				var e = item as UIElement ?? new TextBlock { Text = item.ToString() };
				var t = e as TextBlock;
				if (t != null && t.TextWrapping == TextWrapping.NoWrap) {
					t.TextWrapping = TextWrapping.Wrap;
				}
				stack.Children.Add(e);
			}
			scrollViewer.Content = stack;
			qiContent.Clear();
			qiContent.Add(scrollViewer);
		}

	}
}
