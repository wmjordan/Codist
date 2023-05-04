using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AppHelpers;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Codist.NaviBar
{
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
#pragma warning disable 649, 169
		[Import(typeof(ITextSearchService2))]
		ITextSearchService2 _TextSearchService;
#pragma warning restore 649, 169

		public void TextViewCreated(IWpfTextView textView) {
			if (Config.Instance.Features.MatchFlags(Features.NaviBar)
				&& textView.Roles.Contains("DIFF") == false
				&& textView.TextBuffer.MayBeEditor()) {
				if (textView.TextBuffer.ContentType.IsOfType(Constants.CodeTypes.CSharp)
					|| textView.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown)) {
					SemanticContext.GetOrCreateSingletonInstance(textView);
					new Override(textView, _TextSearchService);
				}
			}
		}


		sealed class Override
		{
			IWpfTextView _View;
			ITextSearchService2 _TextSearch;
			FrameworkElement _NaviBarHolder;

			public Override(IWpfTextView view, ITextSearchService2 textSearch) {
				_View = view;
				_TextSearch = textSearch;
				view.VisualElement.Loaded += AddNaviBar;
				view.Closed += View_Closed;
			}

			void AddNaviBar(object sender, RoutedEventArgs e) {
				var view = sender as IWpfTextView ?? _View;
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
					view.Properties.AddProperty(nameof(NaviBar), bar);
					return;
				}

				var barHolder = _NaviBarHolder = cp.GetFirstVisualChild<Border>(b => b.Name == "DropDownBarMargin");
				if (barHolder == null) {
					var viewHost = view.VisualElement.GetParent<Panel>(b => b.GetType().Name == "WpfMultiViewHost");
					if (viewHost != null && view.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown)) {
						var b = new MarkdownBar(_View, _TextSearch);
						DockPanel.SetDock(b, Dock.Top);
						if (viewHost.Children.Count == 1) {
							viewHost.Children.Insert(0, b);
						}
						else if (viewHost.Children[0] is ContentControl c && c.Content == null) {
							c.Content = b;
						}
						RegisterResurrectionHandler(b);
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
					RegisterResurrectionHandler(b);
				}
			}

			void RegisterResurrectionHandler(NaviBar bar) {
				bar.Unloaded += ResurrectNaviBar_OnUnloaded;
			}

			// Fixes https://github.com/wmjordan/Codist/issues/131
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void ResurrectNaviBar_OnUnloaded(object sender, RoutedEventArgs e) {
                var view = _View;
				if (view?.IsClosed == false) {
					view.Properties.RemoveProperty(nameof(NaviBar));
					await Task.Delay(1000).ConfigureAwait(false);
					await SyncHelper.SwitchToMainThreadAsync(default);
					if (view.VisualElement.IsVisible) {
						AddNaviBar(view, e);
					}
				}
			}

			void View_Closed(object sender, EventArgs e) {
				if (_View != null) {
					_View.VisualElement.Loaded -= AddNaviBar;
					_View.Closed -= View_Closed;
                    _View.Properties.RemoveProperty(nameof(NaviBar));
					if (_NaviBarHolder != null) {
						_NaviBarHolder.Unloaded -= ResurrectNaviBar_OnUnloaded;
						_NaviBarHolder = null;
					}
					_TextSearch = null;
					_View = null;
				}
			}
		}
	}
}
