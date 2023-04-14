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

		/// <summary>
		/// Defines the adornment layer for syntax node range highlight.
		/// </summary>
		[Export(typeof(AdornmentLayerDefinition))]
		[Name(nameof(CSharpBar.SyntaxNodeRange))]
		[Order(After = PredefinedAdornmentLayers.CurrentLineHighlighter)]
		AdornmentLayerDefinition _SyntaxNodeRangeAdornmentLayer;

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
					new Overrider(textView, _TextSearchService);
				}
			}
		}


		sealed class Overrider
		{
			IWpfTextView _View;
			ITextSearchService2 _TextSearch;
			FrameworkElement _NaviBarHolder;

			public Overrider(IWpfTextView view, ITextSearchService2 textSearch) {
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
				var naviBar = cp.GetFirstVisualChild<NaviBar>();
				if (naviBar != null) {
					//naviBar.BindView();
					view.Properties.AddProperty(nameof(NaviBar), naviBar);
					return;
				}

				var naviBarHolder = _NaviBarHolder = cp.GetFirstVisualChild<Border>(b => b.Name == "DropDownBarMargin");
				if (naviBarHolder == null) {
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
				var dropDown1 = naviBarHolder.GetFirstVisualChild<ComboBox>(c => c.Name == "DropDown1");
				var dropDown2 = naviBarHolder.GetFirstVisualChild<ComboBox>(c => c.Name == "DropDown2");
				if (dropDown1 == null || dropDown2 == null) {
					return;
				}
				var container = dropDown1.GetParent<Grid>();
				if (container == null) {
					return;
				}
				if (_View?.IsClosed == false) {
					var bar = new CSharpBar(_View) {
						MinWidth = 200
					};
					bar.SetCurrentValue(Grid.ColumnProperty, 2);
					bar.SetCurrentValue(Grid.ColumnSpanProperty, 3);
					container.Children.Add(bar);
					dropDown1.Visibility = Visibility.Hidden;
					dropDown2.Visibility = Visibility.Hidden;
					RegisterResurrectionHandler(bar);
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
					await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(default);
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
