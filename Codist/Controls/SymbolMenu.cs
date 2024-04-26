using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Utilities;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	sealed class SymbolMenu : SymbolList
	{
		readonly StackPanel _HeaderPanel;
		readonly SymbolFilterBox _FilterBox;
		TextViewOverlay _ExternalAdornment;

		public SymbolMenu(SemanticContext semanticContext) : this(semanticContext, SymbolListType.None) { }
		public SymbolMenu(SemanticContext semanticContext, SymbolListType listType) : base(semanticContext) {
			Container = _ExternalAdornment = TextViewOverlay.GetOrCreate(semanticContext.View);
			ContainerType = listType;
			Header = _HeaderPanel = new StackPanel {
				Margin = WpfHelper.MenuItemMargin,
				Children = {
						(Title = new ThemedMenuText {
							TextAlignment = TextAlignment.Left,
							Padding = WpfHelper.SmallVerticalMargin
						}),
						(_FilterBox = new SymbolFilterBox(this) {
							Margin = WpfHelper.NoMargin
						}),
						new Separator()
					}
			};
			HeaderButtons = new ThemedControlGroup(
				new ThemedButton(IconIds.Copy, R.CMD_CopyListContent, CopyListContent),
				new ThemedToggleButton(IconIds.TogglePinning, R.CMD_Pin, TogglePinButton),
				new ThemedButton(IconIds.Close, R.CMD_Close, CloseMenu))
				{ Opacity = 0.8 }
				.HandleEvent(MouseEnterEvent, MouseEnterHeader)
				.HandleEvent(MouseLeaveEvent, MouseLeaveHeader);
			MouseLeftButtonUp += MenuItemSelect;
			_ExternalAdornment.MakeDraggable(this);
		}

		public ThemedMenuText Title { get; }

		public override void Dispose() {
			if (_ExternalAdornment != null) {
				base.Dispose();
				FilteredItems = null;
				ItemsSource = null;
				PreviewKeyUp -= OnMenuKeyUp;
				MouseLeftButtonUp -= MenuItemSelect;
				_ExternalAdornment.DisableDraggable(this);
				_ExternalAdornment = null;
			}
		}

		void TogglePinButton(object sender, RoutedEventArgs e) {
			((ThemedToggleButton)e.Source).Content = VsImageHelper.GetImage((IsPinned = !IsPinned) ? IconIds.Pin : IconIds.Unpin);
		}

		void CloseMenu() {
			var a = _ExternalAdornment;
			a.RemoveAndDispose(this);
			a.FocusOnTextView();
		}

		public void Show(UIElement relativeElement = null) {
			ShowMenu(relativeElement);
			UpdateNumbers();
			_FilterBox.FocusFilterBox();
		}

		void ShowMenu(UIElement positionElement) {
			Visibility = Visibility.Hidden; // avoid flickering

			if (Symbols.Count > 50) {
				EnableVirtualMode = true;
			}
			_ExternalAdornment.Add(this);
			ItemsControlMaxHeight = _ExternalAdornment.DisplayHeight / 2;
			RefreshItemsSource();
			this.ScrollToSelectedItem();
			PreviewKeyUp -= OnMenuKeyUp;
			PreviewKeyUp += OnMenuKeyUp;

			var p = positionElement != null
				? positionElement.TranslatePoint(new Point(positionElement.RenderSize.Width, 0), _ExternalAdornment)
				: Mouse.GetPosition(_ExternalAdornment);
			_ExternalAdornment.Position(this, p, 100);
			Visibility = Visibility.Visible;
		}
		void UpdateNumbers() {
			_FilterBox.UpdateNumbers(Symbols);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
		async void MenuItemSelect(object sender, MouseButtonEventArgs e) {
			if ((e.OriginalSource as DependencyObject).GetParent<ListBoxItem>(i => i.IsSelected && i.IsFocused) != null) {
				_ExternalAdornment.FocusOnTextView();
				try {
					await ((SymbolItem)((VirtualList)sender).SelectedItem)?.GoToSourceAsync();
				}
				catch (OperationCanceledException) {
				}
			}
		}

		void OnMenuKeyUp(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				_ExternalAdornment.RemoveAndDispose(this);
				e.Handled = true;
			}
		}

		void CopyListContent() {
			using (var sbr = ReusableStringBuilder.AcquireDefault(100)) {
				var sb = sbr.Resource;
				foreach (var symbolItem in Symbols) {
					if (symbolItem.IndentLevel != 0) {
						sb.Append(' ', symbolItem.IndentLevel);
					}
					sb.AppendLine(symbolItem.Content.GetText());
				}
				try {
					Clipboard.SetDataObject(sb.ToString());
				}
				catch (System.Runtime.InteropServices.ExternalException ex) {
					ex.Log();
				}
			}
			_FilterBox.FocusFilterBox();
		}

		void MouseEnterHeader(object sender, RoutedEventArgs e) {
			((UIElement)e.Source).Opacity = 1;
		}

		void MouseLeaveHeader(object sender, RoutedEventArgs e) {
			((UIElement)e.Source).Opacity = 0.8;
		}
	}
}
