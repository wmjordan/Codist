using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Codist.Controls
{
	sealed class ThemedTipDocument : Border
	{
		const int PlaceHolderSize = WpfHelper.IconRightMargin + ThemeHelper.DefaultIconSize;
		readonly Grid _Container;
		int _RowCount;

		public ThemedTipDocument() {
			Child = _Container = new Grid {
				ColumnDefinitions = {
					new ColumnDefinition { Width = new GridLength(PlaceHolderSize) },
					new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
				},
				Margin = WpfHelper.SmallVerticalMargin
			};
		}

		public IEnumerable<TextBlock> Paragraphs => _Container.Children.OfType<TextBlock>();
		public int ParagraphCount => _RowCount;

		public ThemedTipDocument Append(ThemedTipParagraph block) {
			return AppendParagraph(block.Icon, block.Content);
		}
		public ThemedTipDocument AppendTitle(int imageId, string text) {
			return AppendParagraph(imageId, new ThemedTipText(text, true));
		}
		public ThemedTipDocument AppendLine() {
			_Container.RowDefinitions.Add(new RowDefinition());
			_Container.Children.Add(new Border {
				Height = 1,
				BorderThickness = WpfHelper.TinyMargin,
				BorderBrush = ThemeHelper.DocumentTextBrush,
				Margin = WpfHelper.SmallVerticalMargin
			}.SetValue(Grid.SetRow, _RowCount).SetValue(Grid.SetColumnSpan, 2));
			_RowCount++;
			return this;
		}
		public ThemedTipDocument AppendParagraph(int iconId, TextBlock content) {
			_Container.RowDefinitions.Add(new RowDefinition());
			FrameworkElement icon;
			if (iconId == 0) {
				icon = new Border { Height = WpfHelper.IconRightMargin, Width = PlaceHolderSize };
			}
			else {
				icon = ThemeHelper.GetImage(iconId).WrapMargin(WpfHelper.GlyphMargin);
				icon.SetValue(VerticalAlignmentProperty, VerticalAlignment.Top);
			}
			icon.SetValue(Grid.RowProperty, _RowCount);
			_Container.Children.Add(icon);
			content.SetValue(Grid.RowProperty, _RowCount);
			content.SetValue(Grid.ColumnProperty, 1);
			content.Margin = WpfHelper.TinyMargin;
			_Container.Children.Add(content);
			_RowCount++;
			return this;
		}
	}
}
