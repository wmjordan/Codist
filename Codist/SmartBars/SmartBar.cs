using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.SmartBars
{
	/// <summary>The contextual toolbar.</summary>
	internal class SmartBar
	{
		const int Selecting = 1, Working = 2;
		readonly Timer _CreateToolBarTimer;
		int _TimerStatus;
		readonly ToolBarTray _ToolBarTray;
		/// <summary>The layer for the smart bar adornment.</summary>
		readonly IAdornmentLayer _ToolBarLayer;
		DateTime _LastExecute;

		/// <summary>
		/// Initializes a new instance of the <see cref="SmartBar"/> class.
		/// </summary>
		/// <param name="view">The <see cref="IWpfTextView"/> upon which the adornment will be drawn</param>
		public SmartBar(IWpfTextView view) {
			View = view ?? throw new ArgumentNullException(nameof(view));
			_ToolBarLayer = view.GetAdornmentLayer(nameof(SmartBar));
			View.Selection.SelectionChanged += ViewSelectionChanged;
			View.Closed += ViewClosed;
			ToolBar = new ToolBar { BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, Band = 1, IsOverflowOpen = false }.HideOverflow();
			ToolBar2 = new ToolBar { BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, Band = 2, IsOverflowOpen = false }.HideOverflow();
			_ToolBarTray = new ToolBarTray {
				ToolBars = { ToolBar, ToolBar2 },
				IsLocked = true,
				Cursor = Cursors.Arrow,
				Background = Brushes.Transparent,
			};
			_ToolBarTray.MouseEnter += ToolBarMouseEnter;
			_ToolBarTray.MouseLeave += ToolBarMouseLeave;
			_ToolBarTray.DragEnter += HideToolBar;
			_CreateToolBarTimer = new Timer(CreateToolBar);
			_ToolBarLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, _ToolBarTray, null);
			_ToolBarTray.Visibility = Visibility.Hidden;
			VSColorTheme.ThemeChanged += ThemeChanged;
			LoadThemeColor();
		}

		protected IWpfTextView View { get; }

		protected ToolBar ToolBar { get; }
		protected ToolBar ToolBar2 { get; }

		#region Event handlers
		void ThemeChanged(ThemeChangedEventArgs args) {
			LoadThemeColor();
		}
		void ToolBarSizeChanged(object sender, SizeChangedEventArgs e) {
			SetToolBarPosition();
			_ToolBarTray.SizeChanged -= ToolBarSizeChanged;
		}
		void LoadThemeColor() {
			var c = ThemeHelper.TitleBackgroundColor;
			var b = new SolidColorBrush(c.ToWpfColor());
			b.Freeze();
			ToolBar.Background = b;
			ToolBar2.Background = b;
		}
		void ViewMouseMove(object sender, MouseEventArgs e) {
			if (_ToolBarTray.IsVisible == false) {
				return;
			}
			const double SensibleRange = 100;
			var p = e.GetPosition(_ToolBarTray);
			double x = p.X, y = p.Y;
			var s = _ToolBarTray.RenderSize;
			if (x > 0 && x <= s.Width) {
				x = 0;
			}
			else if (x > s.Width) {
				x -= s.Width;
			}
			if (y > 0 && y <= s.Height) {
				y = 0;
			}
			else if (y > s.Height) {
				y -= s.Height;
			}
			var op = Math.Abs(x) + Math.Abs(y);
			if (op > SensibleRange) {
				HideToolBar(this, null);
				return;
			}
			_ToolBarTray.Opacity = (SensibleRange - op) / SensibleRange;
		}
		void ViewSelectionChanged(object sender, EventArgs e) {
			if (AppHelpers.EnumHelper.HasAnyFlag(Keyboard.Modifiers, ModifierKeys.Shift)) {
				return;
			}
			if (View.Selection.IsEmpty) {
				_ToolBarTray.Visibility = Visibility.Hidden;
				View.VisualElement.MouseMove -= ViewMouseMove;
				_CreateToolBarTimer.Change(Timeout.Infinite, Timeout.Infinite);
				_TimerStatus = 0;
				return;
			}
			if (Interlocked.CompareExchange(ref _TimerStatus, Selecting, 0) != 0) {
				return;
			}
			_CreateToolBarTimer.Change(400, Timeout.Infinite);
		}

		void ViewLayoutChanged(object sender, EventArgs e) {
			HideToolBar(sender, null);
		}

		void ViewClosed(object sender, EventArgs e) {
			_CreateToolBarTimer.Dispose();
			_ToolBarTray.ToolBars.Clear();
			_ToolBarTray.MouseEnter -= ToolBarMouseEnter;
			_ToolBarTray.MouseLeave -= ToolBarMouseLeave;
			View.Selection.SelectionChanged -= ViewSelectionChanged;
			View.VisualElement.MouseMove -= ViewMouseMove;
			//View.LayoutChanged -= ViewLayoutChanged;
			View.Closed -= ViewClosed;
			VSColorTheme.ThemeChanged -= ThemeChanged;
		}

		void ToolBarMouseEnter(object sender, EventArgs e) {
			View.VisualElement.MouseMove -= ViewMouseMove;
			((ToolBarTray)sender).Opacity = 1;
			View.Properties[nameof(SmartBar)] = true;
		}
		void ToolBarMouseLeave(object sender, EventArgs e) {
			View.VisualElement.MouseMove += ViewMouseMove;
			View.Properties.RemoveProperty(nameof(SmartBar));
		}

		#endregion

		void CreateToolBar(object dummy) {
			if (ToolBar.Dispatcher.Thread != Thread.CurrentThread) {
				ToolBar.Dispatcher.Invoke(() => CreateToolBar(null));
				return;
			}
			if (Mouse.LeftButton == MouseButtonState.Pressed) {
				// postpone the even handler until the mouse button is released
				_CreateToolBarTimer.Change(100, Timeout.Infinite);
				return;
			}
			if (View.Selection.IsEmpty || Interlocked.Exchange(ref _TimerStatus, Working) != Selecting) {
				goto EXIT;
			}
			CreateToolBar();

			EXIT:
			_TimerStatus = 0;
		}

		void CreateToolBar() {
			_ToolBarTray.Visibility = Visibility.Hidden;
			ToolBar.Items.Clear();
			ToolBar2.Items.Clear();
			AddCommands();
			SetToolBarPosition();
			if (ToolBar2.Items.Count == 0) {
				ToolBar2.Visibility = Visibility.Collapsed;
			}
			else if (ToolBar2.Visibility == Visibility.Collapsed) {
				ToolBar2.Visibility = Visibility.Visible;
				ToolBar2.HideOverflow();
			}
			_ToolBarTray.Visibility = Visibility.Visible;
			_ToolBarTray.Opacity = 0.3;
			_ToolBarTray.SizeChanged += ToolBarSizeChanged;
			View.VisualElement.MouseMove += ViewMouseMove;
		}

		void SetToolBarPosition() {
			// keep tool bar position when the selection is restored and the tool bar reappears after executing command
			if (DateTime.Now > _LastExecute.AddSeconds(1)) {
				var pos = Mouse.GetPosition(View.VisualElement);
				var rs = _ToolBarTray.RenderSize;
				var x = pos.X - 35;
				var y = pos.Y - rs.Height - 10;
				Canvas.SetLeft(_ToolBarTray, x < View.ViewportLeft ? View.ViewportLeft
					: x + rs.Width < View.ViewportRight ? x
					: View.ViewportRight - rs.Width);
				Canvas.SetTop(_ToolBarTray, (y < 0 ? y + rs.Height + 30 : y) + View.ViewportTop);
			}
		}

		protected virtual void AddCommands() {
			if (CodistPackage.DebuggerStatus != DebuggerStatus.Running) {
				AddCommand(ToolBar, KnownImageIds.Cut, "Cut selected text\nRight click: Cut line", ctx => {
					if (ctx.RightClick) {
						ctx.View.ExpandSelectionToLine();
					}
					TextEditorHelper.ExecuteEditorCommand("Edit.Cut");
				});
			}
			AddCommand(ToolBar, KnownImageIds.Copy, "Copy selected text\nRight click: Copy line", ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.Copy");
			});
			if (CodistPackage.DebuggerStatus != DebuggerStatus.Running) {
				if (Clipboard.ContainsText()) {
					AddCommand(ToolBar, KnownImageIds.Paste, "Paste text from clipboard\nRight click: Paste over line\nCtrl click: Paste and select next", ctx => ExecuteAndFind(ctx, "Edit.Paste"));
				}
				AddCommand(ToolBar, KnownImageIds.CopyItem, "Duplicate selection\nRight click: Duplicate line", ctx => {
					if (ctx.RightClick) {
						ctx.View.ExpandSelectionToLine();
					}
					TextEditorHelper.ExecuteEditorCommand("Edit.Duplicate");
					ctx.KeepToolbar();
				});
				AddCommand(ToolBar, KnownImageIds.Cancel, "Delete selected text\nRight click: Delete line\nCtrl click: Delete and select next", ctx => ExecuteAndFind(ctx, "Edit.Delete"));
				switch (View.GetSelectedTokenType()) {
					case TokenType.None:
						AddEditorCommand(ToolBar, KnownImageIds.FormatSelection, "Edit.FormatSelection", "Format selected text\nRight click: Format document", "Edit.FormatDocument");
						break;
					case TokenType.Digit:
						AddCommand(ToolBar, KnownImageIds.Counter, "Increment number", ctx => {
							var span = ctx.View.Selection.SelectedSpans[0];
							var t = span.GetText();
							long l;
							if (long.TryParse(t, out l)) {
								using (var ed = ctx.View.TextBuffer.CreateEdit()) {
									t = (++l).ToString(System.Globalization.CultureInfo.InvariantCulture);
									if (ed.Replace(span.Span, t)) {
										ed.Apply();
										ctx.View.Selection.Select(new Microsoft.VisualStudio.Text.SnapshotSpan(ctx.View.TextSnapshot, span.Start, t.Length), false);
										ctx.KeepToolbar();
									}
								}
							}
						});
						break;
				}
				//var selection = View.Selection;
				//if (View.Selection.Mode == TextSelectionMode.Stream && View.TextViewLines.GetTextViewLineContainingBufferPosition(selection.Start.Position) != View.TextViewLines.GetTextViewLineContainingBufferPosition(selection.End.Position)) {
				//	AddCommand(ToolBar, KnownImageIds.Join, "Join lines", ctx => {
				//		var span = View.Selection.SelectedSpans[0];
				//		var t = span.GetText();
				//		View.TextBuffer.Replace(span, System.Text.RegularExpressions.Regex.Replace(t, @"[ \t]*\r?\n[ \t]*", " "));
				//	});
				//}
			}
			if (CodistPackage.DebuggerStatus != DebuggerStatus.Design) {
				AddEditorCommand(ToolBar, KnownImageIds.ToolTip, "Edit.QuickInfo", "Show quick info");
			}
			AddEditorCommand(ToolBar, KnownImageIds.FindNext, "Edit.FindNextSelected", "Find next selected text\nRight click: Find previous selected", "Edit.FindPreviousSelected");
			//AddEditorCommand(ToolBar, "Edit.Capitalize", KnownImageIds.ASerif, "Capitalize");
		}

		static void ExecuteAndFind(CommandContext ctx, string command) {
			if (ctx.RightClick) {
				ctx.View.ExpandSelectionToLine(false);
			}
			string t = null;
			if (Keyboard.Modifiers == ModifierKeys.Control && ctx.View.Selection.IsEmpty == false) {
				t = ctx.View.TextSnapshot.GetText(ctx.View.Selection.SelectedSpans[0]);
			}
			TextEditorHelper.ExecuteEditorCommand(command);
			if (t != null) {
				var p = (CodistPackage.DTE.ActiveDocument.Object() as EnvDTE.TextDocument).Selection;
				if (p != null && p.FindText(t, 0)) {
					ctx.KeepToolbar();
				}
			}
		}

		protected void AddEditorCommand(ToolBar toolBar, int imageId, string command, string tooltip) {
			if (CodistPackage.DTE.Commands.Item(command).IsAvailable) {
				AddCommand(toolBar, imageId, tooltip, (ctx) => {
					TextEditorHelper.ExecuteEditorCommand(command);
					//View.Selection.Clear();
				});
			}
		}
		protected void AddEditorCommand(ToolBar toolBar, int imageId, string command, string tooltip, string command2) {
			if (CodistPackage.DTE.Commands.Item(command).IsAvailable) {
				AddCommand(toolBar, imageId, tooltip, (ctx) => {
					TextEditorHelper.ExecuteEditorCommand(ctx.RightClick ? command2 : command);
					//View.Selection.Clear();
				});
			}
		}

		protected void AddCommand(ToolBar toolBar, int imageId, string tooltip, Action<CommandContext> handler) {
			var b = new Button {
				Content = ThemeHelper.GetImage(imageId),
				ToolTip = tooltip,
				Cursor = Cursors.Hand
			};
			b.Click += (s, args) => {
				var ctx = new CommandContext(this, s as Control, args);
				handler(ctx);
				if (ctx.KeepToolbarOnClick == false) {
					HideToolBar(s, args);
				}
			};
			b.MouseRightButtonUp += (s, args) => {
				var ctx = new CommandContext(this, s as Control, args, true);
				handler(ctx);
				if (ctx.KeepToolbarOnClick == false) {
					HideToolBar(s, args);
				}
				args.Handled = true;
			};
			toolBar.Items.Add(b);
		}

		protected void AddCommands(ToolBar toolBar, int imageId, string tooltip, Func<CommandContext, IEnumerable<CommandItem>> getItemsHandler) {
			var b = new Button {
				Content = ThemeHelper.GetImage(imageId),
				ToolTip = tooltip,
				ContextMenu = new ContextMenu()
			};
			void ButtonEventHandler(Button btn, CommandContext ctx) {
				var m = btn.ContextMenu;
				m.IsEnabled = true;
				m.PlacementTarget = btn;
				m.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
				m.IsOpen = true;
				if (m.Tag == null || (bool)m.Tag != ctx.RightClick) {
					m.Items.Clear();
					foreach (var item in getItemsHandler(ctx)) {
						m.Items.Add(new CommandMenuItem(this, item));
					}
					m.Tag = ctx.RightClick;
				}
			}
			b.Click += (s, args) => {
				ButtonEventHandler(s as Button, new CommandContext(this, s as Control, args));
			};
			b.MouseRightButtonUp += (s, args) => {
				ButtonEventHandler(s as Button, new CommandContext(this, s as Control, args, true));
				args.Handled = true;
			};
			toolBar.Items.Add(b);
		}

		void KeepToolbar() {
			_LastExecute = DateTime.Now;
		}
		void HideToolBar(object sender, RoutedEventArgs e) {
			_ToolBarTray.Visibility = Visibility.Hidden;
			View.VisualElement.MouseMove -= ViewMouseMove;
		}

		protected class CommandMenuItem : MenuItem
		{
			public CommandMenuItem(SmartBar bar, CommandItem item) {
				SmartBar = bar;
				CommandItem = item;
				Icon = ThemeHelper.GetImage(item.ImageId);
				Header = new TextBlock { Text = item.Name };
				item.ControlInitializer?.Invoke(this);
				// the action is installed only when called by this method
				if (item.Action != null) {
					Click += ClickHandler;
				}
			}

			public CommandItem CommandItem { get; }
			protected SmartBar SmartBar { get; }

			void ClickHandler(object s, RoutedEventArgs e) {
				var ctx2 = new CommandContext(SmartBar, s as Control, e);
				CommandItem.Action(ctx2);
				if (ctx2.KeepToolbarOnClick == false) {
					SmartBar.HideToolBar(s, e);
				}
			}
		}

		protected sealed class CommandItem
		{
			public CommandItem(ISymbol symbol, string alias) {
				Name = alias;
				ImageId = symbol.GetImageId();
			}

			public CommandItem(string name, int imageId, Action<Control> controlInitializer, Action<CommandContext> action) {
				Name = name;
				ImageId = imageId;
				ControlInitializer = controlInitializer;
				Action = action;
			}

			public string Name { get; }
			public int ImageId { get; }
			public Action<Control> ControlInitializer { get; }
			public Action<CommandContext> Action { get; }
		}

		protected sealed class CommandContext
		{
			readonly SmartBar _Bar;

			public CommandContext(SmartBar bar, Control control, RoutedEventArgs eventArgs) {
				View = bar.View;
				_Bar = bar;
				Sender = control;
				EventArgs = eventArgs;
			}
			public CommandContext(SmartBar bar, Control control, RoutedEventArgs eventArgs, bool rightClick) : this(bar, control, eventArgs) {
				RightClick = rightClick;
			}
			public RoutedEventArgs EventArgs { get; }
			public bool RightClick { get; }
			public Control Sender { get; }
			public IWpfTextView View { get; }
			public bool KeepToolbarOnClick { get; set; }
			public void KeepToolbar() {
				_Bar.KeepToolbar();
			}
		}

		sealed class DocumentInfo
		{
			[Category("Document")]
			[Description("Number of lines in active document")]
			[DisplayName("Line count")]
			public int LineCount { get; set; }

			[Category("Document")]
			[Description("Number of characters in active document")]
			[DisplayName("Char count")]
			public int CharCount { get; set; }
		}
	}
}
