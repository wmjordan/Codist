using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CLR;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.NaviBar;

/// <summary>
/// Overrides default navigator to editor.
/// </summary>
[Export(typeof(IWpfTextViewCreationListener))]
[ContentType(Constants.CodeTypes.Code)]
[ContentType(Constants.CodeTypes.Markdown)]
[ContentType(Constants.CodeTypes.VsMarkdown)]
[TextViewRole(PredefinedTextViewRoles.Document)]
sealed class NaviBarFactory : IWpfTextViewCreationListener
{
	public void TextViewCreated(IWpfTextView textView) {
		if (Config.Instance.Features.MatchFlags(Features.NaviBar)
			&& textView.Roles.Contains("DIFF") == false
			&& textView.TextBuffer.MayBeEditor()) {
			if (textView.TextBuffer.ContentType.IsOfType(Constants.CodeTypes.CSharp)
				|| textView.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown)) {
				SemanticContext.GetOrCreateSingletonInstance(textView);
				new Override(textView);
			}
		}
	}


	sealed class Override
	{
		IWpfTextView _View;

		public Override(IWpfTextView view) {
			_View = view;
			view.VisualElement.Loaded += AddNaviBar;
			view.Closed += View_Closed;
		}

		void AddNaviBar(object sender, RoutedEventArgs e) {
			var view = sender as IWpfTextView;
			// don't add duplicated NaviBar
			if (view.Properties.ContainsProperty(nameof(NaviBar))) {
				return;
			}
			var cp = view.VisualElement?.GetParent<Border>(b => b.Name == "PART_ContentPanel");
			if (cp == null) {
				return;
			}
			var bar = cp.GetFirstVisualChild<NaviBar>();
			if (bar != null) {
				// splitting a document window will go to here
				// we already have a NaviBar
				bar.AssociateExtraView(view);
				return;
			}

			var barHolder = cp.GetFirstVisualChild<Border>(b => b.Name == "DropDownBarMargin");
			if (barHolder == null) {
				var viewHost = view.VisualElement.GetParent<Panel>(b => b.GetType().Name == "WpfMultiViewHost");
				if (viewHost != null && view.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown)) {
					var b = new MarkdownBar(_View);
					DockPanel.SetDock(b, Dock.Top);
					if (viewHost.Children.Count == 1) {
						viewHost.Children.Insert(0, b);
					}
					else if (viewHost.Children[0] is ContentControl c && c.Content == null) {
						c.Content = b;
					}
				}
				return;
			}
			var dropDown1 = barHolder.GetFirstVisualChild<ComboBox>(c => c.Name == "DropDown1");
			var dropDown2 = barHolder.GetFirstVisualChild<ComboBox>(c => c.Name == "DropDown2");
			if (dropDown1 == null || dropDown2 == null) {
				return;
			}
			var container = dropDown1.GetParent<Grid>();
			if (container == null) {
				return;
			}
			if (_View?.IsClosed == false) {
				var b = new CSharpBar(_View) {
					MinWidth = 200
				};
				b.SetCurrentValue(Grid.ColumnProperty, 2);
				b.SetCurrentValue(Grid.ColumnSpanProperty, 3);
				container.Children.Add(b);
				dropDown1.Visibility = Visibility.Hidden;
				dropDown2.Visibility = Visibility.Hidden;
			}
		}

		void View_Closed(object sender, EventArgs e) {
			if (_View != null) {
				_View.VisualElement.Loaded -= AddNaviBar;
				_View.Closed -= View_Closed;
				_View.Properties.RemoveProperty(nameof(NaviBar));
				_View = null;
			}
		}
	}
}
