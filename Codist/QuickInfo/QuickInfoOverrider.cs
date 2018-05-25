using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.CodeAnalysis;

namespace Codist.QuickInfo
{
	sealed class DefaultQuickInfoPanelWrapper
	{
		static Func<StackPanel, TextBlock> __GetMainDescription;
		static Func<StackPanel, TextBlock> __GetDocumentation;
		static readonly SolidColorBrush __HighlightBrush = SystemColors.HighlightBrush.Alpha(0.3);

		public DefaultQuickInfoPanelWrapper(StackPanel panel) {
			Panel = panel;
			if (__GetMainDescription == null && panel != null) {
				var t = panel.GetType();
				__GetMainDescription = t.CreateGetPropertyMethod<StackPanel, TextBlock>("MainDescription");
				__GetDocumentation = t.CreateGetPropertyMethod<StackPanel, TextBlock>("Documentation");
			}
		}
		public StackPanel Panel { get; }
		public TextBlock MainDesciption => Panel != null ? __GetMainDescription(Panel) : null;
		public TextBlock Documentation => Panel != null ? __GetDocumentation(Panel) : null;

		/// <summary>Hack into the default QuickInfo panel and provides click and go feature for symbols.</summary>
		public void ApplyClickAndGo(ISymbol symbol) {
			if (symbol == null) {
				return;
			}
			var description = MainDesciption;
			if (description == null) {
				return;
			}
			var locs = symbol.GetSourceLocations();
			if (locs.Length == 0) {
				var asm = symbol.GetAssemblyModuleName();
				if (asm != null) {
					description.ToolTip = symbol.Name + " is defined in " + asm;
				}
				return;
			}
			description.ToolTip = symbol.Name + " is defined in " + System.IO.Path.GetFileName(symbol.DeclaringSyntaxReferences[0].SyntaxTree.FilePath);
			description.Cursor = Cursors.Hand;
			description.MouseEnter += (s, args) => (s as TextBlock).Background = __HighlightBrush;
			description.MouseLeave += (s, args) => (s as TextBlock).Background = Brushes.Transparent;
			if (locs.Length == 1) {
				description.MouseLeftButtonUp += (s, args) => symbol.GoToSource();
				return;
			}
			else {
				description.MouseLeftButtonUp += (s, args) => {
					var tb = (s as TextBlock);
					if (tb.ContextMenu == null) {
						tb.ContextMenu = UIHelper.CreateContextMenuForSourceLocations(symbol.MetadataName, locs);
					}
					tb.ContextMenu.IsOpen = true;
				};
			}
		}

		/// <summary>overrides default doc summary</summary>
		/// <param name="newDoc">The overriding doc element.</param>
		public void OverrideDocumentation(UIElement newDoc) {
			var doc = Documentation;
			if (doc != null) {
				doc.Visibility = Visibility.Collapsed;
				Panel.Children.Insert(Panel.Children.IndexOf(doc), newDoc);
			}
		}

	}
	static class QuickInfoOverrider
	{
		public static StackPanel FindDefaultQuickInfoPanel(IList<object> qiContent) {
			foreach (var item in qiContent) {
				var o = item as StackPanel;
				if (o != null && o.GetType().Name == "QuickInfoDisplayPanel") {
					return o;
				}
			}
			return null;
		}

		/// <summary>
		/// Limits the displaying size of the quick info items.
		/// </summary>
		public static void LimitQuickInfoItemSize(IList<object> qiContent, DefaultQuickInfoPanelWrapper quickInfoWrapper) {
			if (Config.Instance.QuickInfoMaxHeight <= 0 && Config.Instance.QuickInfoMaxWidth <= 0 || qiContent.Count == 0) {
				return;
			}
			for (int i = 0; i < qiContent.Count; i++) {
				var item = qiContent[i];
				var p = item as Panel;
				// finds out the default quick info panel
				if (p != null && p == quickInfoWrapper.Panel) {
					// adds a dummy control to hack into the default quick info panel
					qiContent.Add(new QuickInfoSizer(p));
					continue;
				}
				var s = item as string;
				if (s != null) {
					qiContent[i] = new TextBlock { Text = s, TextWrapping = TextWrapping.Wrap }.Scrollable().LimitSize();
					continue;
				}
				// todo: make other elements scrollable
				if ((item as FrameworkElement).LimitSize() == null) {
					continue;
				}
			}
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
