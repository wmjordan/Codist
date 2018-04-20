using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.CodeAnalysis;

namespace Codist.QuickInfo
{
	static class QuickInfoOverrider
	{
		/// <summary>Hack into the default QuickInfo panel and provides click and go feature for symbols.</summary>
		public static void ApplyClickAndGoFeature(IList<object> qiContent, ISymbol symbol) {
			if (symbol == null || symbol.DeclaringSyntaxReferences.Length == 0 || symbol.DeclaringSyntaxReferences[0].SyntaxTree == null) {
				return;
			}
			foreach (var item in qiContent) {
				var o = item as Panel;
				if (o == null || o.GetType().Name != "QuickInfoDisplayPanel") {
					continue;
				}
				var description = (o.GetType().GetProperty("MainDescription", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(o) as TextBlock);
				if (description == null) {
					return;
				}
				description.ToolTip = symbol.Name + " is defined in " + System.IO.Path.GetFileName(symbol.DeclaringSyntaxReferences[0].SyntaxTree.FilePath);
				description.Cursor = Cursors.Hand;
				description.MouseEnter += (s, args) => (s as TextBlock).Background = SystemColors.HighlightBrush.Alpha(0.3);
				description.MouseLeave += (s, args) => (s as TextBlock).Background = Brushes.Transparent;
				description.MouseLeftButtonUp += (s, args) => symbol.GoToSymbol();
				return;
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
				// finds out the default quick info panel
				if (p != null && p.GetType().Name == "QuickInfoDisplayPanel") {
					// adds a dummy control to hack into the default quick info panel
					qiContent.Add(new QuickInfoSizer(p));
					continue;
				}
				var s = item as string;
				if (s != null) {
					qiContent[i] = new TextBlock { Text = s, TextWrapping = TextWrapping.Wrap }.Scrollable().LimitSize();
					continue;
				}
				if ((item as FrameworkElement).LimitSize() == null) {
					continue;
				}
			}
		}

		static void EnhanceDefaultQuickInfoPanel(Panel quickInfoPanel) {
			// make documentation scrollable
			var doc = (quickInfoPanel.GetType().GetProperty("Documentation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(quickInfoPanel) as TextBlock);
			//	doc.TextAlignment = TextAlignment.Justify;
			if (doc != null) {
				var p = quickInfoPanel.Children.IndexOf(doc);
				if (p != -1) {
					quickInfoPanel.Children.RemoveAt(p);
					quickInfoPanel.Children.Insert(p, doc.Scrollable());
				}
			}
			// make description scrollable
			var description = (quickInfoPanel.GetType().GetProperty("MainDescription", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(quickInfoPanel) as TextBlock);
			if (description == null) {
				return;
			}
			var parent = description.GetVisualParent() as Border;
			parent.Child = null;
			parent.Child = description.Scrollable();
		}

		sealed class QuickInfoSizer : UIElement
		{
			readonly Panel _QuickInfoPanel;

			public QuickInfoSizer(Panel quickInfoPanel) {
				_QuickInfoPanel = quickInfoPanel;
			}
			protected override void OnVisualParentChanged(DependencyObject oldParent) {
				base.OnVisualParentChanged(oldParent);
				// makes the default quick info panel scrollable and size limited
				var p = _QuickInfoPanel.GetVisualParent() as ContentPresenter;
				if (p != null) {
					p.Content = null;
					p.Content = _QuickInfoPanel.Scrollable().LimitSize();
				}
				// hides the parent container from taking excessive space in the quick info window
				var c = this.GetVisualParent<Border>();
				if (c != null) {
					c.Visibility = Visibility.Collapsed;
				}
			}
		}
	}
}
