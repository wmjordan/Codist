using System;
using System.Windows;
using System.Windows.Controls;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands
{
	/// <summary>
	/// A command handler which shows the context menus for current semantic context
	/// </summary>
	internal static class SemanticContextCommand
	{
		public static void Initialize() {
			Command.CodeRefactoring.Register(ExecuteCodeRefactoring, HandleRefactoringMenuState);
		}

		static void HandleRefactoringMenuState(object s, EventArgs args) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var ctx = SemanticContext.GetActive();
			((OleMenuCommand)s).Visible = ctx != null
				&& SyncHelper.RunSync(() => ctx.UpdateAsync(default))
				&& new Refactorings.RefactoringContext(ctx).AcceptAny(Refactorings.All.Refactorings);
		}

		static void ExecuteCodeRefactoring(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var ctx = SemanticContext.GetActive();
			if (ctx == null || SyncHelper.RunSync(() => ctx.UpdateAsync(default)) == false) {
				return;
			}
			var m = new ContextMenu() {
				Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
				PlacementTarget = ctx.View.VisualElement,
				Resources = SharedDictionaryManager.ContextMenu,
			};
			ctx.View.Caret.EnsureVisible();
			var viewLines = ctx.View.TextViewLines;
			var lineOffset = viewLines.FirstVisibleLine.VisibleArea;
			var b = viewLines.GetCharacterBounds(ctx.View.Caret.Position.BufferPosition);
			m.HorizontalOffset = b.Left - lineOffset.Left;
			m.VerticalOffset = b.Bottom - lineOffset.Top;
			m.SetValue(TextBlock.ForegroundProperty, ThemeCache.MenuTextBrush);
			var rc = new Refactorings.RefactoringContext(ctx);
			AddRefactoringCommands(m, Refactorings.All.Refactorings, rc);
			if (m.Items.Count > 0 && m.Items[0] is ThemedMenuItem item) {
				item.Highlight(true);
				item.Focus();
			}
			m.Closed += Menu_Closed;
			m.IsOpen = true;
		}

		static void AddRefactoringCommands(ContextMenu menu, Refactorings.IRefactoring[] refactorings, Refactorings.RefactoringContext ctx) {
			foreach (var item in refactorings) {
				if (item.Accept(ctx)) {
					menu.Items.Add(new ThemedMenuItem(item.IconId, item.Title, (s, args) => item.Refactor(ctx.SemanticContext)));
				}
			}
		}

		static void Menu_Closed(object sender, RoutedEventArgs e) {
			var m = sender as ContextMenu;
			m.PlacementTarget = null;
			m.DisposeCollection();
			m.Closed -= Menu_Closed;
		}

	}
}
