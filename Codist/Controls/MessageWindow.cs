using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CLR;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	sealed class MessageWindow : Window
	{
		readonly ScrollViewer _Content;
		readonly StackPanel _ExtraControlPanel, _ButtonPanel;
		readonly Button _DefaultButton;
		readonly ContentPresenter _Icon;
		CheckBox _SuppressExceptionBox;

		public MessageWindow() {
			MinHeight = 100;
			MinWidth = 200;

			var screen = WpfHelper.GetActiveScreenSize();
			ShowInTaskbar = false;
			ResizeMode = ResizeMode.NoResize;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Owner = Application.Current.MainWindow;
			Resources = SharedDictionaryManager.ThemedControls;
			Content = new StackPanel {
				Margin = WpfHelper.MiddleMargin,
				Children = {
					new Grid {
						MaxHeight = Math.Min(Math.Max(screen.Height / 2, 400), screen.Height),
						ColumnDefinitions = {
							new ColumnDefinition(),
							new ColumnDefinition { MaxWidth = Math.Min(Math.Max(screen.Width / 2, 800), screen.Width) },
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
					new Grid {
						ColumnDefinitions = {
							new ColumnDefinition(),
							new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
						},
						Children = {
							new StackPanel {
								Margin = WpfHelper.MiddleMargin,
								VerticalAlignment = VerticalAlignment.Center
							}.Set(ref _ExtraControlPanel),
							new StackPanel {
								Margin = WpfHelper.MiddleMargin,
								Orientation = Orientation.Horizontal,
								HorizontalAlignment = HorizontalAlignment.Right,
								Children = {
									CreateButton(R.CMD_OK, DefaultButton_Click).Set(ref _DefaultButton)
								}
							}.Set(ref _ButtonPanel).SetValue(Grid.SetColumn, 1)
						}
					}
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
			int img = icon.Case(MessageBoxImage.Question, IconIds.Question,
				MessageBoxImage.Error, IconIds.Error,
				MessageBoxImage.Warning, IconIds.Stop,
				MessageBoxImage.Information, IconIds.Info,
				0);
			if (img != 0) {
				_Icon.Content = VsImageHelper.GetImage(img, VsImageHelper.LargeIconSize);
			}
			else {
				_Icon.Visibility = Visibility.Collapsed;
			}
		}

		public void AddExtraControl(UIElement control) {
			_ExtraControlPanel.Children.Add(control);
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
		public static bool? Error(Exception error, string description = null, string title = null, object source = null) {
			if (ExceptionFilter.IsIgnored(source)) {
				return false;
			}

			var content = description != null
				? GetErrorDescription(description, error)
				: (UIElement)MakeText(error.ToString());
			var w = ShowErrorWindow(content, title, source);
			bool? result = w.ShowDialog();
			if (result == true && source != null && w._SuppressExceptionBox.IsChecked == true) {
				ExceptionFilter.Ignore(source);
			}
			return result;
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
		static MessageWindow ShowErrorWindow(UIElement errorDescription, string title = null, object source = null) {
			var w = new MessageWindow(errorDescription, title, MessageBoxButton.OK, MessageBoxImage.Error);
			if (source != null) {
				w._ExtraControlPanel.Children.Add(new CheckBox {
					Content = R.T_DontReportUntilRestart
				}.Set(ref w._SuppressExceptionBox).ReferenceStyle(VsResourceKeys.CheckBoxStyleKey));
			}
			return w;
		}
		static StackPanel GetErrorDescription(string description, Exception exception) {
			return new StackPanel {
				Children = {
					MakeText(description).SetProperty(TextBlock.FontSizeProperty, ThemeCache.ToolTipFontSize * 1.5d),
					MakeText(exception.Message),
					MakeText(R.T_StackTrace + Environment.NewLine + exception.StackTrace)
				}
			};
		}

		public object Message {
			get => _Content.Content;
			set {
				if (value is string s) {
				}
				else if (value is System.Windows.Media.Visual v) {
					_Content.Content = v;
					return;
				}
				else {
					s = value.ToString();
				}
				_Content.Content = new ThemedTipText(s) {
					Padding = WpfHelper.MiddleMargin,
				}.ReferenceProperty(ForegroundProperty, CommonControlsColors.TextBoxTextBrushKey);
			}
		}

		static ThemedTipText MakeText(string text) {
			return new ThemedTipText(text) {
				Padding = WpfHelper.MiddleMargin,
			}.ReferenceProperty(ForegroundProperty, CommonControlsColors.TextBoxTextBrushKey);
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

		static class ExceptionFilter
		{
			static readonly HashSet<Type> __IgnoreExceptions = new HashSet<Type>();

			public static bool IsIgnored(object source) {
				return source != null && __IgnoreExceptions.Contains(source.GetType());
			}
			public static void Ignore(object source) {
				__IgnoreExceptions.Add(source.GetType());
			}
		}
	}
}
