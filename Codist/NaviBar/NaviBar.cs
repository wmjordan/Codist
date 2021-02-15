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
		protected NaviBar(IWpfTextView textView) {
			View = textView;
			ListContainer = View.Properties.GetOrCreateSingletonProperty(() => new ExternalAdornment(textView));
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			textView.Properties.AddProperty(nameof(NaviBar), this);
			Resources = SharedDictionaryManager.NavigationBar;
			SetResourceReference(BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientKey);
			SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextInactiveKey);
			Unloaded += NaviBar_Unloaded;
		}

		public abstract void ShowActiveItemMenu();
		public abstract void ShowRootItemMenu(int parameter);
		protected IWpfTextView View { get; }
		internal ExternalAdornment ListContainer { get; }

		protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e) {
			var h = WpfHelper.GetParentOrSelf<DependencyObject>(e.Source as DependencyObject, o => o is IContextMenuHost) as IContextMenuHost;
			if (h != null) {
				h.ShowContextMenu(e);
				e.Handled = true;
			}
		}

		void NaviBar_Unloaded(object sender, RoutedEventArgs e) {
			Unloaded -= NaviBar_Unloaded;
			View.Properties.RemoveProperty(nameof(NaviBar));

			if (View.IsClosed == false) {
				Loaded += NaviBar_Loaded;
			}
		}

		void NaviBar_Loaded(object sender, RoutedEventArgs e) {
			Loaded -= NaviBar_Loaded;
			View.Properties.AddProperty(nameof(NaviBar), this);
		}
	}
}
