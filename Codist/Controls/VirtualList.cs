using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	class VirtualList : ListBox, IDisposable
	{
		public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register("Header", typeof(UIElement), typeof(VirtualList));
		public static readonly DependencyProperty HeaderButtonsProperty = DependencyProperty.Register("HeaderButtons", typeof(UIElement), typeof(VirtualList));
		public static readonly DependencyProperty FooterProperty = DependencyProperty.Register("Footer", typeof(UIElement), typeof(VirtualList));
		public static readonly DependencyProperty ItemsControlMaxHeightProperty = DependencyProperty.Register("ItemsControlMaxHeight", typeof(double), typeof(VirtualList));

		public VirtualList() {
			SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
			SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
			ItemsControlMaxHeight = 500;
			HorizontalContentAlignment = HorizontalAlignment.Stretch;
			Resources = SharedDictionaryManager.VirtualList;
			this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
		}

		public UIElement Header {
			get => GetValue(HeaderProperty) as UIElement;
			set => SetValue(HeaderProperty, value);
		}
		public UIElement HeaderButtons {
			get => GetValue(HeaderButtonsProperty) as UIElement;
			set => SetValue(HeaderButtonsProperty, value);
		}
		public UIElement Footer {
			get => GetValue(FooterProperty) as UIElement;
			set => SetValue(FooterProperty, value);
		}
		public double ItemsControlMaxHeight {
			get => (double)GetValue(ItemsControlMaxHeightProperty);
			set => SetValue(ItemsControlMaxHeightProperty, value);
		}
		public ListCollectionView FilteredItems { get; set; }
		public FrameworkElement Container { get; set; }
		public FrameworkElement Owner { get; set; }
		public bool NeedsRefresh { get; set; }
		public bool EnableVirtualMode {
			get => ScrollViewer.GetCanContentScroll(this);
			set => ScrollViewer.SetCanContentScroll(this, value);
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e) {
			base.OnPreviewKeyDown(e);
			if (e.Key == Key.Tab) {
				e.Handled = true;
				return;
			}
			if (e.OriginalSource is TextBox == false) {
				return;
			}
			switch (e.Key) {
				case Key.Enter:
					break;
				case Key.Up:
					if (SelectedIndex > 0) {
						SelectedIndex--;
						this.ScrollToSelectedItem();
					}
					else if (HasItems) {
						SelectedIndex = this.ItemCount() - 1;
						this.ScrollToSelectedItem();
					}
					e.Handled = true;
					break;
				case Key.Down:
					if (SelectedIndex < this.ItemCount() - 1) {
						SelectedIndex++;
						this.ScrollToSelectedItem();
					}
					else if (HasItems) {
						SelectedIndex = 0;
						this.ScrollToSelectedItem();
					}
					e.Handled = true;
					break;
				case Key.PageUp:
					if (SelectedIndex >= 10) {
						SelectedIndex -= 10;
						this.ScrollToSelectedItem();
					}
					else if (HasItems) {
						SelectedIndex = 0;
						this.ScrollToSelectedItem();
					}
					e.Handled = true;
					break;
				case Key.PageDown:
					if (SelectedIndex >= this.ItemCount() - 10) {
						SelectedIndex = this.ItemCount() - 1;
						this.ScrollToSelectedItem();
					}
					else if (HasItems) {
						SelectedIndex += 10;
						this.ScrollToSelectedItem();
					}
					e.Handled = true;
					break;
			}
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
			base.OnRenderSizeChanged(sizeInfo);
			if (sizeInfo.WidthChanged) {
				//Canvas.SetLeft(this, left < 0 ? 0 : left);
				if (Container != null) {
					var left = Canvas.GetLeft(this);
					var top = Canvas.GetTop(this);
					if (top + sizeInfo.NewSize.Height > Container.RenderSize.Height) {
						Canvas.SetTop(this, Container.RenderSize.Height - sizeInfo.NewSize.Height);
					}
					if (left + sizeInfo.NewSize.Width > Container.RenderSize.Width) {
						Canvas.SetLeft(this, Container.ActualWidth - sizeInfo.NewSize.Width);
					}
				}
			}
		}

		protected override AutomationPeer OnCreateAutomationPeer() {
			return null;
		}

		public virtual void Dispose() {
			Container = null;
			FilteredItems = null;
			Owner = null;
			DataContext = null;
		}
	}

	abstract class ListItem /*: INotifyPropertyChanged*/
	{
		UIElement _Icon;

		public abstract int ImageId { get; }
		public UIElement Icon {
			get => _Icon ?? ThemeHelper.GetImage(ImageId);
			set => _Icon = value;
		}
		public string Hint { get; set; }
		public TextBlock Content { get; set; }
		public VirtualList Container { get; }
	}
}
