using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	sealed class MessageWindow : Window
	{
		readonly ScrollViewer _Content;
		readonly StackPanel _ButtonPanel;
		readonly Button _DefaultButton;
		readonly ContentPresenter _Icon;

		public MessageWindow() {
			MinHeight = 100;
			MinWidth = 200;

			var ss = WpfHelper.GetActiveScreenSize();
			ShowInTaskbar = false;
			ResizeMode = ResizeMode.NoResize;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Owner = Application.Current.MainWindow;
			Resources = SharedDictionaryManager.ThemedControls;
			Content = new StackPanel {
				Margin = WpfHelper.MiddleMargin,
				Children = {
					new Grid {
						MaxHeight = Math.Min(Math.Max(ss.Height / 2, 400), ss.Height),
						ColumnDefinitions = {
							new ColumnDefinition(),
							new ColumnDefinition { MaxWidth = Math.Min(Math.Max(ss.Width / 2, 800), ss.Width) },
						},
						Children = {
							new ContentPresenter { VerticalAlignment = VerticalAlignment.Top, Margin = WpfHelper.MiddleMargin }.Set(ref _Icon),
							new Border {
								BorderThickness = WpfHelper.TinyMargin,
								Child = new ScrollViewer {
									CanContentScroll = true,
									VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
									Padding = WpfHelper.MiddleMargin,
								}.ReferenceProperty(BackgroundProperty, CommonControlsColors.TextBoxBackgroundBrushKey)
								.ReferenceStyle(VsResourceKeys.ScrollViewerStyleKey)
								.Set(ref _Content)
							}.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey).SetValue(Grid.SetColumn, 1),
						}
					},
					new StackPanel {
						Margin = WpfHelper.MiddleMargin,
						Orientation = Orientation.Horizontal,
						HorizontalAlignment = HorizontalAlignment.Right,
						Children = {
							CreateButton(R.CMD_OK, DefaultButton_Click).Set(ref _DefaultButton)
						}
					}.Set(ref _ButtonPanel)
				}
			};
			this.ReferenceProperty(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
			SizeToContent = SizeToContent.WidthAndHeight;

			_DefaultButton.IsDefault = true;
		}

		public MessageWindow(object message)
			: this(message, nameof(Codist), MessageBoxButton.OK, MessageBoxImage.None) { }

		public MessageWindow(object message,
			string title = null,
			MessageBoxButton button = MessageBoxButton.OK,
			MessageBoxImage icon = MessageBoxImage.None)
			: this() {
			Title = title ?? nameof(Codist);
			Message = message;
			Button b;
			switch (button) {
				case MessageBoxButton.OKCancel:
					_ButtonPanel.Children.Add(b = CreateButton(R.CMD_Cancel, CancelButton_Click));
					b.IsCancel = true;
					break;
				case MessageBoxButton.YesNoCancel:
					_DefaultButton.Content = R.CMD_Yes;
					_ButtonPanel.Children.Add(CreateButton(R.CMD_No, NegativeButton_Click));
					goto case MessageBoxButton.OKCancel;
				case MessageBoxButton.YesNo:
					_DefaultButton.Content = R.CMD_Yes;
					_ButtonPanel.Children.Add(b = CreateButton(R.CMD_No, NegativeButton_Click));
					b.IsCancel = true;
					break;
				case MessageBoxButton.OK:
					_DefaultButton.IsCancel = true;
					break;
			}
			int img;
			switch (icon) {
				case MessageBoxImage.Question: img = IconIds.Question; break;
				case MessageBoxImage.Error: img = IconIds.Error; break;
				case MessageBoxImage.Warning: img = IconIds.Stop; break;
				case MessageBoxImage.Information: img = IconIds.Info; break;
				default: img = 0; break;
			}
			if (img != 0) {
				_Icon.Content = ThemeHelper.GetImage(img, ThemeHelper.LargeIconSize);
			}
			else {
				_Icon.Visibility = Visibility.Collapsed;
			}
		}

		public static bool? Show(object content) {
			return new MessageWindow(content).ShowDialog();
		}
		public static bool? Show(object content, string title) {
			return new MessageWindow(content, title).ShowDialog();
		}
		public static bool? Error(string content) {
			return new MessageWindow(content, null, MessageBoxButton.OK, MessageBoxImage.Error).ShowDialog();
		}
		public static bool? Error(string content, string title) {
			return new MessageWindow(content, title, MessageBoxButton.OK, MessageBoxImage.Error).ShowDialog();
		}
		public static bool? Error(Exception content) {
			return new MessageWindow(content, null, MessageBoxButton.OK, MessageBoxImage.Error).ShowDialog();
		}
		public static bool? Error(Exception content, string description) {
			return new MessageWindow($"{description}{Environment.NewLine}{content}", null, MessageBoxButton.OK, MessageBoxImage.Error).ShowDialog();
		}
		public static bool? Error(Exception content, string description, string title) {
			return new MessageWindow($"{description}{Environment.NewLine}{content}", title, MessageBoxButton.OK, MessageBoxImage.Error).ShowDialog();
		}
		public static bool? OkCancel(object content) {
			return new MessageWindow(content, null, MessageBoxButton.OKCancel, MessageBoxImage.Question).ShowDialog();
		}
		public static bool? AskYesNo(object content) {
			return new MessageWindow(content, null, MessageBoxButton.YesNo, MessageBoxImage.Question).ShowDialog();
		}
		public static bool? AskYesNoCancel(object content) {
			return new MessageWindow(content, null, MessageBoxButton.YesNoCancel, MessageBoxImage.Question).ShowDialog();
		}

		public object Message {
			get => _Content.Content;
			set {
				if (value is string s) {
					_Content.Content = new ThemedTipText(s) {
						Padding = WpfHelper.MiddleMargin,
					}.ReferenceProperty(ForegroundProperty, CommonControlsColors.TextBoxTextBrushKey);
				}
				else {
					_Content.Content = value;
				}
			}
		}

		void DefaultButton_Click(object sender, RoutedEventArgs e) {
			DialogResult = true;
			Close();
		}

		void NegativeButton_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
			Close();
		}

		void CancelButton_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		static Button CreateButton(string content, RoutedEventHandler clickHandler) {
			return new Button {
				Margin = WpfHelper.MiddleMargin,
				Content = content,
			}.ReferenceStyle(VsResourceKeys.ButtonStyleKey)
			.HandleEvent(Button.ClickEvent, clickHandler);
		}
	}
}
