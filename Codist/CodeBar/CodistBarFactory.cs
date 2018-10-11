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
	sealed partial class CodistBarFactory : IWpfTextViewCreationListener
	{
#pragma warning disable 649, 169

		/// <summary>
		/// Defines the adornment layer for the item adornment.
		/// </summary>
		[Export(typeof(AdornmentLayerDefinition))]
		[Name(nameof(CSharpBar))]
		[Order(Before = PredefinedAdornmentLayers.TextMarker)]
		AdornmentLayerDefinition _EditorAdornmentLayer;

#pragma warning restore 649, 169

		public void TextViewCreated(IWpfTextView textView) {
			if (Config.Instance.Features.MatchFlags(Features.Breadcrumb)) {
				textView.Properties.GetOrCreateSingletonProperty(() => new SemanticContext(textView));
				new Overrider(textView);
			}
		}

		sealed class Overrider
		{
			readonly IWpfTextView _View;

			public Overrider(IWpfTextView view) {
				_View = view;
				view.VisualElement.Loaded += FindNaviBar;
				view.VisualElement.Unloaded += ViewUnloaded;
			}

			void FindNaviBar(object sender, RoutedEventArgs e) {
				var view = sender as FrameworkElement;
				var naviBar = view
					?.GetVisualParent<Border>(b => b.Name == "PART_ContentPanel")
					?.GetFirstVisualChild<Border>(b => b.Name == "DropDownBarMargin");
				if (naviBar == null) {
					goto EXIT;
				}
				var dropDown1 = naviBar.GetFirstVisualChild<ComboBox>(c => c.Name == "DropDown1");
				var dropDown2 = naviBar.GetFirstVisualChild<ComboBox>(c => c.Name == "DropDown2");
				if (dropDown1 == null || dropDown2 == null) {
					goto EXIT;
				}
				var container = dropDown1.GetVisualParent<Grid>();
				var bar = new CSharpBar(_View) {
					MinWidth = 200
				};
				bar.SetCurrentValue(Grid.ColumnProperty, 2);
				bar.SetCurrentValue(Grid.ColumnSpanProperty, 3);
				container.Children.Add(bar);
				dropDown1.Visibility = Visibility.Hidden;
				dropDown2.Visibility = Visibility.Hidden;
				EXIT:
				UnloadEvents();
			}

			void ViewUnloaded(object sender, EventArgs e) {
				UnloadEvents();
			}

			void UnloadEvents() {
				_View.VisualElement.Loaded -= FindNaviBar;
				_View.VisualElement.Unloaded -= ViewUnloaded;
			}
		}
	}
}
