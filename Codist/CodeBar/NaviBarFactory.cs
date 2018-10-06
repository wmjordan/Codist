using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using AppHelpers;

namespace Codist.CodeBar
{
	/// <summary>
	/// Overrides default navigator to editor.
	/// </summary>
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType(Constants.CodeTypes.CSharp)]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	sealed partial class NaviBarFactory : IWpfTextViewCreationListener
	{
		public void TextViewCreated(IWpfTextView textView) {
			if (Config.Instance.Features.MatchFlags(Features.Breadcrumb)) {
				var h = new Overrider(textView);
				textView.VisualElement.Loaded += h.FindNaviBar;
				textView.VisualElement.Unloaded += h.ViewUnloaded;
			}
		}

		sealed class Overrider
		{
			readonly IWpfTextView _View;

			public Overrider(IWpfTextView view) {
				_View = view;
			}

			public void ViewUnloaded(object sender, EventArgs e) {
				_View.VisualElement.Loaded -= FindNaviBar;
				_View.VisualElement.Unloaded -= ViewUnloaded;
			}

			public void FindNaviBar(object sender, RoutedEventArgs e) {
				var view = sender as FrameworkElement;
				var naviBar = view
					?.GetVisualParent<Border>(b => b.Name == "PART_ContentPanel")
					?.GetFirstVisualChild<Border>(b => b.Name == "DropDownBarMargin");
				if (naviBar == null) {
					return;
				}
				var dropDown1 = naviBar.GetFirstVisualChild<ComboBox>(c => c.Name == "DropDown1");
				var dropDown2 = naviBar.GetFirstVisualChild<ComboBox>(c => c.Name == "DropDown2");
				if (dropDown1 == null || dropDown2 == null) {
					return;
				}
				var container = dropDown1.GetVisualParent<Grid>();
				var bar = new CSharpNaviBar(_View) {
					MinWidth = 200
				};
				bar.SetCurrentValue(Grid.ColumnProperty, 2);
				bar.SetCurrentValue(Grid.ColumnSpanProperty, 3);
				container.Children.Add(bar);
				dropDown1.Visibility = Visibility.Hidden;
				dropDown2.Visibility = Visibility.Hidden;
				view.Loaded -= FindNaviBar;
			}
		}
	}
}
