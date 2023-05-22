using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	// https://stackoverflow.com/questions/136435/any-way-to-make-a-wpf-textblock-selectable
	sealed class TextEditorWrapper
	{
		static readonly ExtensionProperty<FrameworkElement, bool> __SelectableProperty = ExtensionProperty<FrameworkElement, bool>.Register("IsSelectable");
		static readonly Type __TextEditorType = Type.GetType("System.Windows.Documents.TextEditor, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly PropertyInfo __IsReadOnlyProp = __TextEditorType?.GetProperty("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
		static readonly PropertyInfo __TextViewProp = __TextEditorType?.GetProperty("TextView", BindingFlags.Instance | BindingFlags.NonPublic);
		static readonly MethodInfo __RegisterMethod = __TextEditorType?.GetMethod("RegisterCommandHandlers", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Type), typeof(bool), typeof(bool), typeof(bool) }, null);
		static readonly MethodInfo __OnDetachMethod = __TextEditorType?.GetMethod("OnDetach", BindingFlags.Instance | BindingFlags.NonPublic);
		static readonly PropertyInfo __TextSelectionProp = __TextEditorType?.GetProperty("Selection", BindingFlags.Instance | BindingFlags.NonPublic);

		static readonly Type __TextContainerType = Type.GetType("System.Windows.Documents.ITextContainer, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly PropertyInfo __TextContainerTextViewProp = __TextContainerType?.GetProperty("TextView");
		static readonly Type __TextSelectionType = Type.GetType("System.Windows.Documents.ITextSelection, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly MethodInfo __TextSelectionContains = __TextSelectionType?.GetMethod("Contains", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Point) }, null);
		static readonly PropertyInfo __TextSelectionAnchorPositionProp = __TextSelectionType?.GetProperty("AnchorPosition");
		static readonly Type __TextRangeType = Type.GetType("System.Windows.Documents.ITextRange, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly PropertyInfo __TextRangeIsEmptyProp = __TextRangeType?.GetProperty("IsEmpty");
		static readonly PropertyInfo __TextRangeTextProp = __TextRangeType?.GetProperty("Text");
		static readonly MethodInfo __TextRangeSelect = __TextRangeType?.GetMethod("Select", BindingFlags.Public | BindingFlags.Instance, null, new[] { __TextSelectionAnchorPositionProp?.PropertyType ?? typeof(int), __TextSelectionAnchorPositionProp?.PropertyType ?? typeof(int) }, null);

		static readonly PropertyInfo __TextContainerProp = typeof(TextBlock).GetProperty("TextContainer", BindingFlags.Instance | BindingFlags.NonPublic);

		static readonly Type __TextEditorCopyPaste = Type.GetType("System.Windows.Documents.TextEditorCopyPaste, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly MethodInfo __CopyMethod = __TextEditorCopyPaste?.GetMethod("Copy", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { __TextEditorType, typeof(bool) }, null);

		static readonly bool __IsInitialized = __IsReadOnlyProp != null
			&& __TextViewProp != null
			&& __RegisterMethod != null
			&& __TextContainerTextViewProp != null
			&& __TextSelectionProp != null
			&& __TextSelectionContains != null
			&& __TextRangeIsEmptyProp != null
			&& __TextContainerProp != null
			&& RegisterCommandHandlers(typeof(TextBlock), true, true, true);
		static readonly bool __CanCopy = __CopyMethod != null;

		FrameworkElement _UiScope;
		object _Editor;

		public static TextEditorWrapper CreateFor(TextBlock text) {
			if (__IsInitialized == false) {
				return null;
			}
			if (__SelectableProperty.Get(text)) {
				return null;
			}
			text.Focusable = true;
			var textContainer = __TextContainerProp.GetValue(text);
			var editor = new TextEditorWrapper(textContainer, text, false);
			__IsReadOnlyProp.SetValue(editor._Editor, true);
			__TextViewProp.SetValue(editor._Editor, __TextContainerTextViewProp.GetValue(textContainer));
			__SelectableProperty.Set(text, true);
			return editor;
		}

		TextEditorWrapper(object textContainer, FrameworkElement uiScope, bool isUndoEnabled) {
			_Editor = Activator.CreateInstance(__TextEditorType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance, null, new[] { textContainer, uiScope, isUndoEnabled }, null);
			_UiScope = uiScope;
			uiScope.PreviewMouseLeftButtonDown += HandleSelectStart;
			// hooking this event could make QuickFix on QuickInfo not working, weird!
			uiScope.Unloaded += Detach;
		}

		public bool Copy() {
			if (__CanCopy) {
				__CopyMethod.Invoke(null, new object[] { _Editor, true });
				return true;
			}
			return false;
		}

		static bool RegisterCommandHandlers(Type controlType, bool acceptsRichContent, bool readOnly, bool registerEventListeners) {
			__RegisterMethod?.Invoke(null, new object[] { controlType, acceptsRichContent, readOnly, registerEventListeners });
			return __RegisterMethod != null;
		}

		void HandleSelectStart(object sender, MouseButtonEventArgs e) {
			_UiScope.PreviewMouseLeftButtonDown -= HandleSelectStart;
			_UiScope.Focus();
			// don't mess so much if not selected
			// lazy initialization (only when selection is started)
			if (_UiScope.ContextMenu == null) {
				_UiScope.PreviewKeyUp += HandleCopyShortcut;
				_UiScope.Style = new Style(_UiScope.GetType()) {
					Setters = {
						new Setter(System.Windows.Controls.Primitives.TextBoxBase.SelectionBrushProperty, ThemeHelper.TextSelectionHighlightBrush)
					}
				};
				var m = new ContextMenu {
					Resources = SharedDictionaryManager.ContextMenu,
					Foreground = ThemeHelper.ToolWindowTextBrush,
					IsEnabled = true
				};
				m.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
				var newItem = new ThemedMenuItem {
					Icon = ThemeHelper.GetImage(IconIds.Copy),
					Header = R.CMD_CopySelection
				};
				newItem.Click += HandleMouseCopy;
				m.Items.Add(newItem);
				m.Items.AddRange(Config.Instance.SearchEngines.ConvertAll(CreateMenuItemForWebSearch));
				_UiScope.ContextMenu = m;
				_UiScope.ContextMenuOpening += HandleContextMenuOpening;
				_UiScope.ContextMenuClosing += HandleContextMenuClosing;
			}
		}

		ThemedMenuItem CreateMenuItemForWebSearch(SearchEngine s) {
			var m = new ThemedMenuItem {
				Icon = ThemeHelper.GetImage(IconIds.SearchWebSite),
				Header = R.CMD_SearchWith.Replace("<NAME>", s.Name),
			};
			m.SetSearchUrlPattern(s.Pattern, null);
			m.Click += HandleWebSearch;
			return m;
		}

		void HandleWebSearch(object sender, RoutedEventArgs e) {
			var s = sender as MenuItem;
			ExternalCommand.OpenWithWebBrowser(s.GetSearchUrl(), __TextRangeTextProp.GetValue(__TextSelectionProp.GetValue(_Editor)) as string);
		}

		void HandleContextMenuOpening(object sender, ContextMenuEventArgs e) {
			var s = sender as FrameworkElement;
			var selection = __TextSelectionProp.GetValue(_Editor);
			var selectionEmpty = (bool)__TextRangeIsEmptyProp.GetValue(selection);
			if (selectionEmpty
				|| (bool)__TextSelectionContains.Invoke(selection, new object[] { new Point(e.CursorLeft, e.CursorTop) }) == false) {
				_UiScope.ContextMenu.IsOpen = false;
				e.Handled = true;
				return;
			}
			QuickInfo.QuickInfoOverride.HoldQuickInfo(s, true);
		}
		void HandleContextMenuClosing(object sender, ContextMenuEventArgs e) {
			// clears selection
			var selection = __TextSelectionProp.GetValue(_Editor);
			var anchor = __TextSelectionAnchorPositionProp.GetValue(selection);
			__TextRangeSelect.Invoke(selection, new object[] { anchor, anchor });
		}
		void ReleaseQuickInfo(object sender, RoutedEventArgs e) {
			QuickInfo.QuickInfoOverride.HoldQuickInfo(_UiScope, false);
			_UiScope.ContextMenu = null;
		}
		void HandleMouseCopy(object sender, RoutedEventArgs e) {
			if (e.Handled = Copy()) {
				QuickInfo.QuickInfoOverride.DismissQuickInfo(_UiScope);
			}
		}
		void HandleCopyShortcut(object sender, KeyEventArgs e) {
			if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control
				&& _Editor != null
				&& Copy()) {
				e.Handled = true;
				System.Diagnostics.Debug.WriteLine("Copied: " + Clipboard.GetText());
			}
		}

		void Detach(object sender, RoutedEventArgs e) {
			if (_UiScope != null) {
				_UiScope.Unloaded -= Detach;
				_UiScope.PreviewMouseLeftButtonDown -= HandleSelectStart;
				_UiScope.PreviewKeyUp -= HandleCopyShortcut;
				_UiScope.MouseRightButtonUp -= HandleMouseCopy;
				_UiScope.ContextMenuOpening -= HandleContextMenuOpening;
				_UiScope.ContextMenuClosing -= HandleContextMenuClosing;
				if (_UiScope.ContextMenu != null) {
					foreach (MenuItem item in _UiScope.ContextMenu.Items) {
						item.Click -= HandleMouseCopy;
						item.Click -= HandleWebSearch;
					}
					_UiScope.ContextMenu = null;
				}
				__TextViewProp.SetValue(_Editor, null);
				__SelectableProperty.Clear(_UiScope);
				_UiScope = null;
				_Editor = null;
			}
		}
	}
}
