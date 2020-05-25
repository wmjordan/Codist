using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	/// <summary>
	/// Selectable <see cref="TextBlock"/> used in Quick Info.
	/// </summary>
	sealed class ThemedTipText : TextBlock
	{
		static ThemedTipText() {
			TextEditorWrapper.RegisterCommandHandlers(typeof(TextBlock), true, true, true);
		}
		public ThemedTipText() {
			TextWrapping = TextWrapping.Wrap;
			Foreground = ThemeHelper.ToolTipTextBrush;
			TextEditorWrapper.CreateFor(this);
		}
		public ThemedTipText(string text) : this() {
			Inlines.Add(text);
		}
		public ThemedTipText(string text, bool bold) : this() {
			this.Append(text, bold);
		}
	}
	sealed class ThemedTipDocument : StackPanel
	{
		public ThemedTipDocument Append(ThemedTipParagraph block) {
			Children.Add(block);
			return this;
		}
		public ThemedTipDocument AppendTitle(int imageId, string text) {
			Children.Add(new ThemedTipParagraph(imageId, new ThemedTipText(text, true)));
			return this;
		}
		public void ApplySizeLimit() {
			var w = Config.Instance.QuickInfoMaxWidth;
			if (w == 0) {
				w = Application.Current.MainWindow.RenderSize.Width;
			}
			w -= WpfHelper.IconRightMargin + ThemeHelper.DefaultIconSize + WpfHelper.SmallMarginSize + WpfHelper.SmallMarginSize + 22/*scrollbar width*/;
			foreach (var item in Children) {
				var r = item as ThemedTipParagraph;
				if (r != null) {
					r.Content.MaxWidth = w;
				}
			}
		}
	}
	sealed class ThemedTipParagraph : StackPanel
	{
		const int PlaceHolderSize = WpfHelper.IconRightMargin + ThemeHelper.DefaultIconSize;

		ThemedTipParagraph() {
			Margin = WpfHelper.TinyMargin;
			Orientation = Orientation.Horizontal;
		}
		public ThemedTipParagraph(int iconId, TextBlock content) : this() {
			if (iconId == 0) {
				Children.Add(new Border { Height = WpfHelper.IconRightMargin, Width = PlaceHolderSize });
			}
			else {
				var icon = ThemeHelper.GetImage(iconId).WrapMargin(WpfHelper.GlyphMargin);
				icon.VerticalAlignment = VerticalAlignment.Top;
				Children.Add(icon);
			}
			Children.Add(Content = content ?? new ThemedTipText());
		}
		public ThemedTipParagraph(int iconId) : this(iconId, null) {
		}
		public ThemedTipParagraph(TextBlock content) : this(0, content) {
		}
		public Image Icon => Children[0] as Image;
		public TextBlock Content { get; }
	}
	sealed class ThemedToolBarText : TextBlock
	{
		public ThemedToolBarText() {
			SetResourceReference(ForegroundProperty, EnvironmentColors.SystemMenuTextBrushKey);
		}
		public ThemedToolBarText(string text) : this() {
			Inlines.Add(text);
		}
	}
	sealed class ThemedMenuText : TextBlock
	{
		public ThemedMenuText() {
			SetResourceReference(ForegroundProperty, EnvironmentColors.SystemMenuTextBrushKey);
		}
		public ThemedMenuText(string text) : this() {
			Inlines.Add(text);
		}
		public ThemedMenuText(string text, bool bold) : this() {
			this.Append(text, bold);
		}
	}

	// https://stackoverflow.com/questions/136435/any-way-to-make-a-wpf-textblock-selectable
	sealed class TextEditorWrapper
	{
		static readonly Type TextEditorType = Type.GetType("System.Windows.Documents.TextEditor, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly PropertyInfo IsReadOnlyProp = TextEditorType?.GetProperty("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
		static readonly PropertyInfo TextViewProp = TextEditorType?.GetProperty("TextView", BindingFlags.Instance | BindingFlags.NonPublic);
		static readonly MethodInfo RegisterMethod = TextEditorType?.GetMethod("RegisterCommandHandlers", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Type), typeof(bool), typeof(bool), typeof(bool) }, null);
		static readonly MethodInfo OnDetachMethod = TextEditorType?.GetMethod("OnDetach", BindingFlags.Instance | BindingFlags.NonPublic);

		static readonly Type TextContainerType = Type.GetType("System.Windows.Documents.ITextContainer, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly PropertyInfo TextContainerTextViewProp = TextContainerType?.GetProperty("TextView");

		static readonly PropertyInfo TextContainerProp = typeof(TextBlock).GetProperty("TextContainer", BindingFlags.Instance | BindingFlags.NonPublic);

		static readonly Type TextEditorCopyPaste = Type.GetType("System.Windows.Documents.TextEditorCopyPaste, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly MethodInfo CopyMethod = TextEditorCopyPaste?.GetMethod("Copy", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { TextEditorType, typeof(bool) }, null);

		static readonly bool __IsInitialized = IsReadOnlyProp != null && TextViewProp != null && RegisterMethod != null
			&& TextContainerTextViewProp != null && TextContainerProp != null;
		static readonly bool __CanCopy = CopyMethod != null;

		readonly FrameworkElement _uiScope;
		readonly object _editor;

		public TextEditorWrapper(object textContainer, FrameworkElement uiScope, bool isUndoEnabled) {
			_editor = Activator.CreateInstance(TextEditorType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance, null, new[] { textContainer, uiScope, isUndoEnabled }, null);
			_uiScope = uiScope;
			uiScope.PreviewMouseLeftButtonDown += HandleSelectStart;
			// hooking this event could make QuickFix on QuickInfo not working, weird!
			// uiScope.Unloaded += Detach;
		}

		public bool Copy() {
			if (__CanCopy) {
				CopyMethod.Invoke(null, new object[] { _editor, true });
				return true;
			}
			return false;
		}

		public static void RegisterCommandHandlers(Type controlType, bool acceptsRichContent, bool readOnly, bool registerEventListeners) {
			RegisterMethod?.Invoke(null, new object[] { controlType, acceptsRichContent, readOnly, registerEventListeners });
		}

		public static TextEditorWrapper CreateFor(TextBlock text) {
			if (__IsInitialized == false) {
				return null;
			}
			text.Focusable = true;
			var textContainer = TextContainerProp.GetValue(text);
			var editor = new TextEditorWrapper(textContainer, text, false);
			IsReadOnlyProp.SetValue(editor._editor, true);
			TextViewProp.SetValue(editor._editor, TextContainerTextViewProp.GetValue(textContainer));
			return editor;
		}

		void HandleSelectStart(object sender, MouseButtonEventArgs e) {
			_uiScope.PreviewMouseLeftButtonDown -= HandleSelectStart;
			// don't mess so much if not selected
			_uiScope.Focus();
			_uiScope.PreviewKeyUp += HandleCopyShortcut;
			_uiScope.MouseRightButtonUp += ShowContextMenu;
			_uiScope.Style = new Style(_uiScope.GetType()) {
				Setters = {
					new Setter(System.Windows.Controls.Primitives.TextBoxBase.SelectionBrushProperty, ThemeHelper.TextSelectionHighlightBrush)
				}
			};
		}
		void ShowContextMenu(object sender, RoutedEventArgs e) {
			var s = sender as FrameworkElement;
			if (e.Source != _uiScope) {
				QuickInfo.QuickInfoOverrider.HoldQuickInfo(s, true);
				return;
			}
			if (s.ContextMenu == null) {
				var m = new ContextMenu {
					Resources = SharedDictionaryManager.ContextMenu,
					Foreground = ThemeHelper.ToolWindowTextBrush,
					IsEnabled = true
				};
				m.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
				var newItem = new ThemedMenuItem { Icon = ThemeHelper.GetImage(IconIds.Copy), Header = "Copy selection" };
				newItem.Click += HandleMouseCopy;
				m.Items.Add(newItem);
				m.Closed += ReleaseQuickInfo;
				s.ContextMenu = m;
			}
			QuickInfo.QuickInfoOverrider.HoldQuickInfo(s, true);
			s.ContextMenu.IsOpen = true;
		}
		void ReleaseQuickInfo(object sender, RoutedEventArgs e) {
			QuickInfo.QuickInfoOverrider.HoldQuickInfo(_uiScope, false);
			_uiScope.ContextMenu = null;
		}
		void HandleMouseCopy(object sender, RoutedEventArgs e) {
			if (e.Handled = Copy()) {
				QuickInfo.QuickInfoOverrider.DismissQuickInfo(_uiScope);
			}
		}
		void HandleCopyShortcut(object sender, KeyEventArgs e) {
			if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control
				&& _editor != null
				&& Copy()) {
				e.Handled = true;
				System.Diagnostics.Debug.WriteLine("Copied: " + Clipboard.GetText());
			}
		}

		void Detach(object sender, RoutedEventArgs e) {
			_uiScope.Unloaded -= Detach;
			_uiScope.PreviewMouseLeftButtonDown -= HandleSelectStart;
			_uiScope.PreviewKeyUp -= HandleCopyShortcut;
			//_uiScope.MouseRightButtonUp -= HandleMouseCopy;
			//TextViewProp.SetValue(_editor, null);
			//OnDetachMethod.Invoke(_editor, null);
		}
	}
}
