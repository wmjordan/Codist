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
		static readonly Type TextEditorType = Type.GetType("System.Windows.Documents.TextEditor, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly PropertyInfo IsReadOnlyProp = TextEditorType?.GetProperty("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
		static readonly PropertyInfo TextViewProp = TextEditorType?.GetProperty("TextView", BindingFlags.Instance | BindingFlags.NonPublic);
		static readonly MethodInfo RegisterMethod = TextEditorType?.GetMethod("RegisterCommandHandlers", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Type), typeof(bool), typeof(bool), typeof(bool) }, null);
		static readonly MethodInfo OnDetachMethod = TextEditorType?.GetMethod("OnDetach", BindingFlags.Instance | BindingFlags.NonPublic);
		static readonly PropertyInfo TextSelectionProp = TextEditorType?.GetProperty("Selection", BindingFlags.Instance | BindingFlags.NonPublic);

		static readonly Type TextContainerType = Type.GetType("System.Windows.Documents.ITextContainer, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly PropertyInfo TextContainerTextViewProp = TextContainerType?.GetProperty("TextView");
		static readonly Type TextSelectionType = Type.GetType("System.Windows.Documents.ITextSelection, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly MethodInfo TextSelectionContains = TextSelectionType?.GetMethod("Contains", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Point) }, null);
		static readonly PropertyInfo TextSelectionAnchorPositionProp = TextSelectionType?.GetProperty("AnchorPosition");
		static readonly Type TextRangeType = Type.GetType("System.Windows.Documents.ITextRange, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly PropertyInfo TextRangeIsEmptyProp = TextRangeType?.GetProperty("IsEmpty");
		static readonly PropertyInfo TextRangeTextProp = TextRangeType?.GetProperty("Text");
		static readonly MethodInfo TextRangeSelect = TextRangeType?.GetMethod("Select", BindingFlags.Public | BindingFlags.Instance, null, new[] { TextSelectionAnchorPositionProp?.PropertyType ?? typeof(int), TextSelectionAnchorPositionProp?.PropertyType ?? typeof(int) }, null);

		static readonly PropertyInfo TextContainerProp = typeof(TextBlock).GetProperty("TextContainer", BindingFlags.Instance | BindingFlags.NonPublic);

		static readonly Type TextEditorCopyPaste = Type.GetType("System.Windows.Documents.TextEditorCopyPaste, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
		static readonly MethodInfo CopyMethod = TextEditorCopyPaste?.GetMethod("Copy", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { TextEditorType, typeof(bool) }, null);

		static readonly bool __IsInitialized = IsReadOnlyProp != null && TextViewProp != null && RegisterMethod != null
			&& TextContainerTextViewProp != null && TextSelectionProp != null && TextSelectionContains != null && TextRangeIsEmptyProp != null && TextContainerProp != null;
		static readonly bool __CanCopy = CopyMethod != null;

		FrameworkElement _uiScope;
		object _editor;

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

		public TextEditorWrapper(object textContainer, FrameworkElement uiScope, bool isUndoEnabled) {
			_editor = Activator.CreateInstance(TextEditorType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance, null, new[] { textContainer, uiScope, isUndoEnabled }, null);
			_uiScope = uiScope;
			uiScope.PreviewMouseLeftButtonDown += HandleSelectStart;
			// hooking this event could make QuickFix on QuickInfo not working, weird!
			uiScope.Unloaded += Detach;
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

		void HandleSelectStart(object sender, MouseButtonEventArgs e) {
			_uiScope.PreviewMouseLeftButtonDown -= HandleSelectStart;
			_uiScope.Focus();
			// don't mess so much if not selected
			// lazy initialization (only when selection is started)
			if (_uiScope.ContextMenu == null) {
				_uiScope.PreviewKeyUp += HandleCopyShortcut;
				//_uiScope.ContextMenuOpening += ShowContextMenu;
				_uiScope.Style = new Style(_uiScope.GetType()) {
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
					Header = R.CMD_CopySelection,
					Tag = "Copy"
				};
				newItem.Click += HandleMouseCopy;
				m.Items.Add(newItem);
				m.Items.AddRange(Config.Instance.SearchEngines.ConvertAll(CreateMenuItemForWebSearch));
				_uiScope.ContextMenu = m;
				_uiScope.ContextMenuOpening += HandleContextMenuOpening;
				_uiScope.ContextMenuClosing += HandleContextMenuClosing;
			}
		}

		ThemedMenuItem CreateMenuItemForWebSearch(SearchEngine s) {
			var m = new ThemedMenuItem {
				Icon = ThemeHelper.GetImage(IconIds.SearchWebSite),
				Header = R.CMD_SearchWith.Replace("<NAME>", s.Name),
				Tag = s.Pattern
			};
			m.Click += HandleWebSearch;
			return m;
		}

		void HandleWebSearch(object sender, RoutedEventArgs e) {
			var s = sender as MenuItem;
			ExternalCommand.OpenWithWebBrowser(s.Tag as string, TextRangeTextProp.GetValue(TextSelectionProp.GetValue(_editor)) as string);
		}

		void HandleContextMenuOpening(object sender, ContextMenuEventArgs e) {
			var s = sender as FrameworkElement;
			var selection = TextSelectionProp.GetValue(_editor);
			var selectionEmpty = (bool)TextRangeIsEmptyProp.GetValue(selection);
			if (selectionEmpty || (bool)TextSelectionContains.Invoke(selection, new object[] { new Point(e.CursorLeft, e.CursorTop) }) == false) {
				_uiScope.ContextMenu.IsOpen = false;
				e.Handled = true;
				return;
			}
			QuickInfo.QuickInfoOverrider.HoldQuickInfo(s, true);
		}
		void HandleContextMenuClosing(object sender, ContextMenuEventArgs e) {
			// clears selection
			var selection = TextSelectionProp.GetValue(_editor);
			var anchor = TextSelectionAnchorPositionProp.GetValue(selection);
			TextRangeSelect.Invoke(selection, new object[] { anchor, anchor });
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
			if (_uiScope != null) {
				_uiScope.Unloaded -= Detach;
				_uiScope.PreviewMouseLeftButtonDown -= HandleSelectStart;
				_uiScope.PreviewKeyUp -= HandleCopyShortcut;
				_uiScope.MouseRightButtonUp -= HandleMouseCopy;
				_uiScope.ContextMenuOpening -= HandleContextMenuOpening;
				_uiScope.ContextMenuClosing -= HandleContextMenuClosing;
				if (_uiScope.ContextMenu != null) {
					foreach (MenuItem item in _uiScope.ContextMenu.Items) {
						item.Click -= HandleMouseCopy;
						item.Click -= HandleWebSearch;
					}
				}
				TextViewProp.SetValue(_editor, null);
				_uiScope = null;
				_editor = null;
			}
		}
	}
}
