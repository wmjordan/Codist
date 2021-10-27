using System;
using System.Windows.Controls;
using System.Windows.Input;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System.Windows;

namespace Codist.NaviBar
{
	public abstract class NaviBar : ToolBar, INaviBar
	{
		IWpfTextView _View;

		protected NaviBar(IWpfTextView textView) {
			_View = textView;
			_View.Closed += View_Closed;
			ListContainer = ExternalAdornment.GetOrCreate(textView);
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			textView.Properties.AddProperty(nameof(NaviBar), this);
			Resources = SharedDictionaryManager.NavigationBar;
			SetResourceReference(BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientKey);
			SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextInactiveKey);
			Unloaded += NaviBar_Unloaded;
		}

		public abstract void ShowActiveItemMenu();
		public abstract void ShowRootItemMenu(int parameter);
		internal protected abstract void BindView();
		protected abstract void UnbindView();

		protected IWpfTextView View => _View;
		internal ExternalAdornment ListContainer { get; private set; }

		protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e) {
			var h = WpfHelper.GetParentOrSelf<DependencyObject>(e.Source as DependencyObject, o => o is IContextMenuHost) as IContextMenuHost;
			if (h != null) {
				h.ShowContextMenu(e);
				e.Handled = true;
			}
		}

		void NaviBar_Unloaded(object sender, RoutedEventArgs e) {
			Unloaded -= NaviBar_Unloaded;
			_View.Properties.RemoveProperty(nameof(NaviBar));

			if (_View.IsClosed == false) {
				Loaded += NaviBar_Loaded;
			}
		}

		void NaviBar_Loaded(object sender, RoutedEventArgs e) {
			Loaded -= NaviBar_Loaded;
			_View.Properties.AddProperty(nameof(NaviBar), this);
		}

		void View_Closed(object sender, EventArgs e) {
			if (_View != null) {
				_View.Closed -= View_Closed;
				Loaded -= NaviBar_Loaded;
				Unloaded -= NaviBar_Unloaded;
				UnbindView();
				var visualParent = this.GetParent<FrameworkElement>();
				if (visualParent is Panel p) {
					p.Children.Remove(this);
				}
				else if (visualParent is ContentControl c) {
					c.Content = null;
				}
				ListContainer = null;
				DataContext = null;
				this.DisposeCollection();
				_View.Properties.RemoveProperty(nameof(NaviBar));
				_View.Properties.RemoveProperty(typeof(ExternalAdornment));
				_View = null;
			}
		}
	}
}
