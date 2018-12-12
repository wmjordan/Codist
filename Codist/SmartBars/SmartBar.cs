using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Text.Operations;

namespace Codist.SmartBars
{
	//todo Make this class async
	/// <summary>The contextual toolbar.</summary>
	internal partial class SmartBar {
		const int Selecting = 1, Working = 2;
		/// <summary>The layer for the smart bar adornment.</summary>
		readonly IAdornmentLayer _ToolBarLayer;
		readonly ToolBarTray _ToolBarTray;
		CancellationTokenSource _Cancellation = new CancellationTokenSource();
		DateTime _LastExecute;
		DateTime _LastShiftHit;
		private int _SelectionStatus;

		/// <summary>
		/// Initializes a new instance of the <see cref="SmartBar"/> class.
		/// </summary>
		/// <param name="view">The <see cref="IWpfTextView"/> upon which the adornment will be drawn</param>
		public SmartBar(IWpfTextView view, ITextSearchService2 textSearchService) {
			View = view ?? throw new ArgumentNullException(nameof(view));
			TextSearchService = textSearchService;
			_ToolBarLayer = view.GetAdornmentLayer(nameof(SmartBar));
			Config.Updated += ConfigUpdated;
			if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.ShiftToggleDisplay)) {
				View.VisualElement.PreviewKeyUp += ViewKeyUp;
			}
			if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.ManualDisplaySmartBar) == false) {
				View.Selection.SelectionChanged += ViewSelectionChanged;
			}
			View.Closed += ViewClosed;
			ToolBar = new ToolBar {
				BorderThickness = new Thickness(1),
				BorderBrush = Brushes.Gray,
				Band = 1,
				IsOverflowOpen = false
			}.HideOverflow();
			ToolBar.SetResourceReference(Control.BackgroundProperty, VsBrushes.CommandBarGradientBeginKey);
			ToolBar2 = new ToolBar {
				BorderThickness = new Thickness(1),
				BorderBrush = Brushes.Gray,
				Band = 2,
				IsOverflowOpen = false
			}.HideOverflow();
			ToolBar2.SetResourceReference(Control.BackgroundProperty, VsBrushes.CommandBarGradientBeginKey);
			_ToolBarTray = new ToolBarTray {
				ToolBars = { ToolBar, ToolBar2 },
				IsLocked = true,
				Cursor = Cursors.Arrow,
				Background = Brushes.Transparent,
			};
			_ToolBarTray.MouseEnter += ToolBarMouseEnter;
			_ToolBarTray.MouseLeave += ToolBarMouseLeave;
			_ToolBarTray.DragEnter += HideToolBar;
			_ToolBarLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, _ToolBarTray, null);
			_ToolBarTray.Visibility = Visibility.Hidden;
		}

		protected ToolBar ToolBar { get; }
		protected ToolBar ToolBar2 { get; }
		protected IWpfTextView View { get; }
		protected ITextSearchService2 TextSearchService { get; }
		protected CancellationToken CancellationToken {
			get {
				var c = _Cancellation;
				return c != null ? c.Token : new CancellationToken(true);
			}
		}

		protected void AddCommand(ToolBar toolBar, int imageId, string tooltip, Action<CommandContext> handler) {
			var b = CreateButton(imageId, tooltip);
			b.Click += (s, args) => {
				var ctx = new CommandContext(this, s as Control, args);
				handler(ctx);
				if (ctx.KeepToolBarOnClick == false) {
					HideToolBar(s, args);
				}
			};
			b.MouseRightButtonUp += (s, args) => {
				var ctx = new CommandContext(this, s as Control, args, true);
				handler(ctx);
				if (ctx.KeepToolBarOnClick == false) {
					HideToolBar(s, args);
				}
				args.Handled = true;
			};
			toolBar.Items.Add(b);
		}

		protected virtual void AddCommands(CancellationToken cancellationToken) {
			var readOnly = View.IsCaretInReadOnlyRegion();
			if (readOnly == false) {
				AddCutCommand();
			}
			AddCopyCommand();
			if (readOnly == false) {
				AddPasteCommand();
				AddDuplicateCommand();
				AddDeleteCommand();
				AddSpecialFormatCommand();
			}
			//if (CodistPackage.DebuggerStatus != DebuggerStatus.Design) {
			//	AddEditorCommand(ToolBar, KnownImageIds.ToolTip, "Edit.QuickInfo", "Show quick info");
			//}
			AddFindAndReplaceCommands();
			//AddEditorCommand(ToolBar, KnownImageIds.FindNext, "Edit.FindNextSelected", "Find next selected text\nRight click: Find previous selected", "Edit.FindPreviousSelected");
			//AddEditorCommand(ToolBar, "Edit.Capitalize", KnownImageIds.ASerif, "Capitalize");
		}

		protected void AddCommands(ToolBar toolBar, int imageId, string tooltip, Func<CommandContext, Task<IEnumerable<CommandItem>>> getItemsHandler) {
			AddCommands(toolBar, imageId, tooltip, null, getItemsHandler);
		}

		protected void AddCommands(ToolBar toolBar, int imageId, string tooltip, Action<CommandContext> leftClickHandler, Func<CommandContext, Task<IEnumerable<CommandItem>>> getItemsHandler) {
			var b = CreateButton(imageId, tooltip);
			if (leftClickHandler != null) {
				b.Click += (s, args) => {
					leftClickHandler(new CommandContext(this, s as Control, args));
				};
			}
			else {
				b.Click += (s, args) => {
					ButtonEventHandler(s as Button, new CommandContext(this, s as Control, args));
				};
			}
			b.MouseRightButtonUp += (s, args) => {
				ButtonEventHandler(s as Button, new CommandContext(this, s as Control, args, true));
				args.Handled = true;
			};
			toolBar.Items.Add(b);

			async void ButtonEventHandler(Button btn, CommandContext ctx) {
				var m = SetupContextMenu(btn);
				if (m.Tag == null || (bool)m.Tag != ctx.RightClick) {
					m.Items.Clear();
					foreach (var item in await getItemsHandler(ctx)) {
						if (ctx.CancellationToken.IsCancellationRequested) {
							return;
						}
						m.Items.Add(new CommandMenuItem(this, item));
					}
					m.Tag = ctx.RightClick;
				}
			}

		}

		protected void AddCommands(ToolBar toolBar, int imageId, string tooltip, Action<CommandContext> leftClickHandler, Func<CommandContext, IEnumerable<CommandItem>> getItemsHandler) {
			var b = CreateButton(imageId, tooltip);
			void ButtonEventHandler(Button btn, CommandContext ctx) {
				var m = SetupContextMenu(btn);
				if (m.Tag == null || (bool)m.Tag != ctx.RightClick) {
					m.Items.Clear();
					foreach (var item in getItemsHandler(ctx)) {
						if (ctx.CancellationToken.IsCancellationRequested) {
							return;
						}
						m.Items.Add(new CommandMenuItem(this, item));
					}
					m.Tag = ctx.RightClick;
				}
			}
			if (leftClickHandler != null) {
				b.Click += (s, args) => {
					leftClickHandler(new CommandContext(this, s as Control, args));
				};
			}
			else {
				b.Click += (s, args) => {
					ButtonEventHandler(s as Button, new CommandContext(this, s as Control, args));
				};
			}
			b.MouseRightButtonUp += (s, args) => {
				ButtonEventHandler(s as Button, new CommandContext(this, s as Control, args, true));
				args.Handled = true;
			};
			toolBar.Items.Add(b);
		}

		static ContextMenu SetupContextMenu(Button btn) {
			var m = new ContextMenu {
				Resources = SharedDictionaryManager.ContextMenu,
				Foreground = ThemeHelper.ToolWindowTextBrush,
				IsEnabled = true,
				PlacementTarget = btn,
				Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
				IsOpen = true
			};
			m.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			return m;
		}

		protected void AddEditorCommand(ToolBar toolBar, int imageId, string command, string tooltip) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (CodistPackage.DTE.Commands.Item(command).IsAvailable) {
				AddCommand(toolBar, imageId, tooltip, (ctx) => {
					TextEditorHelper.ExecuteEditorCommand(command);
					//View.Selection.Clear();
				});
			}
		}

		protected void AddEditorCommand(ToolBar toolBar, int imageId, string command, string tooltip, string command2) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (CodistPackage.DTE.Commands.Item(command).IsAvailable) {
				AddCommand(toolBar, imageId, tooltip, (ctx) => {
					TextEditorHelper.ExecuteEditorCommand(ctx.RightClick ? command2 : command);
					//View.Selection.Clear();
				});
			}
		}

		static Button CreateButton(int imageId, string tooltip) {
			var b = new Button {
				Content = ThemeHelper.GetImage(imageId, Config.Instance.SmartBarButtonSize),
				ToolTip = tooltip,
				Cursor = Cursors.Hand
			};
			b.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			return b;
		}

		async Task CreateToolBarAsync(CancellationToken cancellationToken) {
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			while ((Mouse.LeftButton == MouseButtonState.Pressed || Keyboard.Modifiers == ModifierKeys.Shift)
				&& cancellationToken.IsCancellationRequested == false) {
				// postpone the even handler until the mouse button is released
				await Task.Delay(100);
			}
			if (View.Selection.IsEmpty || Interlocked.Exchange(ref _SelectionStatus, Working) != Selecting) {
				goto EXIT;
			}
			InternalCreateToolBar(cancellationToken);
			EXIT:
			_SelectionStatus = 0;
		}

		void InternalCreateToolBar(CancellationToken cancellationToken = default) {
			_ToolBarTray.Visibility = Visibility.Hidden;
			ToolBar.Items.Clear();
			ToolBar2.Items.Clear();
			AddCommands(cancellationToken);
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

		void HideToolBar() {
			_ToolBarTray.Visibility = Visibility.Hidden;
			View.VisualElement.MouseMove -= ViewMouseMove;
			_LastShiftHit = DateTime.MinValue;
		}

		void HideToolBar(object sender, RoutedEventArgs e) {
			HideToolBar();
		}

		void KeepToolbar() {
			_LastExecute = DateTime.Now;
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
				Canvas.SetTop(_ToolBarTray, (y < 0 || x < View.ViewportLeft && View.Selection.IsReversed == false ? y + rs.Height + 30 : y) + View.ViewportTop);
			}
		}

		#region Event handlers
		void ConfigUpdated(object sender, ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.MatchFlags(Features.SmartBar)) {
				View.VisualElement.PreviewKeyUp -= ViewKeyUp;
				if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.ShiftToggleDisplay)) {
					View.VisualElement.PreviewKeyUp += ViewKeyUp;
				}
				View.Selection.SelectionChanged -= ViewSelectionChanged;
				if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.ManualDisplaySmartBar) == false) {
					View.Selection.SelectionChanged += ViewSelectionChanged;
				}
			}
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

		void ToolBarSizeChanged(object sender, SizeChangedEventArgs e) {
			SetToolBarPosition();
			_ToolBarTray.SizeChanged -= ToolBarSizeChanged;
		}

		void ViewClosed(object sender, EventArgs e) {
			CancellationHelper.CancelAndDispose(ref _Cancellation, false);
			_ToolBarTray.ToolBars.Clear();
			_ToolBarTray.MouseEnter -= ToolBarMouseEnter;
			_ToolBarTray.MouseLeave -= ToolBarMouseLeave;
			View.Selection.SelectionChanged -= ViewSelectionChanged;
			View.VisualElement.MouseMove -= ViewMouseMove;
			View.VisualElement.PreviewKeyUp -= ViewKeyUp;
			//View.LayoutChanged -= ViewLayoutChanged;
			View.Closed -= ViewClosed;
			Config.Updated -= ConfigUpdated;
		}

		void ViewKeyUp(object sender, KeyEventArgs e) {
			if (e.Key != Key.LeftShift && e.Key != Key.RightShift) {
				_LastShiftHit = DateTime.MinValue;
				return;
			}
			var now = DateTime.Now;
			// ignore the shift hit after shift clicking a SmartBar button
			if ((now - _LastExecute).Ticks < TimeSpan.TicksPerSecond) {
				return;
			}
			e.Handled = true;
			if (_ToolBarTray.Visibility == Visibility.Visible) {
				HideToolBar(this, null);
				return;
			}
			if ((now - _LastShiftHit).Ticks < TimeSpan.TicksPerSecond) {
				try {
					InternalCreateToolBar(_Cancellation.GetToken());
				}
				catch (OperationCanceledException) {
					// ignore
				}
			}
			else {
				_LastShiftHit = DateTime.Now;
			}
		}

		void ViewLayoutChanged(object sender, EventArgs e) {
			HideToolBar(sender, null);
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
			// suppress event handler if KeepToolBar
			if (DateTime.Now < _LastExecute.AddSeconds(1)) {
				return;
			}
			if (View.Selection.IsEmpty) {
				_ToolBarTray.Visibility = Visibility.Hidden;
				View.VisualElement.MouseMove -= ViewMouseMove;
				CancellationHelper.CancelAndDispose(ref _Cancellation, true);
				_SelectionStatus = 0;
				return;
			}
			if (Interlocked.CompareExchange(ref _SelectionStatus, Selecting, 0) != 0) {
				return;
			}
			CancellationHelper.CancelAndDispose(ref _Cancellation, true);
			CreateToolBar(_Cancellation.Token);
			async void CreateToolBar(CancellationToken token) {
				try {
					await Task.Delay(400, token);
					if (token.IsCancellationRequested == false) {
						await CreateToolBarAsync(token);
					}
				}
				catch (OperationCanceledException) {
					// ignore
				}
			}
		}
		#endregion

		protected sealed class CommandContext
		{
			readonly SmartBar _Bar;

			public CommandContext(SmartBar bar, Control control, RoutedEventArgs eventArgs) {
				_Bar = bar;
				Sender = control;
				EventArgs = eventArgs;
			}
			public CommandContext(SmartBar bar, Control control, RoutedEventArgs eventArgs, bool rightClick) : this(bar, control, eventArgs) {
				RightClick = rightClick;
			}
			public RoutedEventArgs EventArgs { get; }
			public bool KeepToolBarOnClick { get; set; }
			public bool RightClick { get; }
			public Control Sender { get; }
			public IWpfTextView View => _Bar.View;
			public ITextSearchService2 TextSearchService => _Bar.TextSearchService;
			public CancellationToken CancellationToken => _Bar._Cancellation.GetToken();
			public void HideToolBar() {
				_Bar.HideToolBar();
			}
			public void KeepToolBar(bool refresh) {
				_Bar.KeepToolbar();
				KeepToolBarOnClick = true;
				if (refresh) {
					_Bar.InternalCreateToolBar(CancellationToken);
				}
			}
		}

		protected sealed class CommandItem
		{
			public CommandItem(ISymbol symbol, string alias) {
				Name = alias;
				ImageId = symbol.GetImageId();
			}
			public CommandItem(int imageId, string name, Action<CommandContext> action)
				: this(imageId, name, null, action) { }
			public CommandItem(int imageId, string name, Action<MenuItem> controlInitializer, Action<CommandContext> action) {
				Name = name;
				ImageId = imageId;
				ItemInitializer = controlInitializer;
				Action = action;
			}

			public Action<CommandContext> Action { get; }
			public int ImageId { get; }
			public Action<MenuItem> ItemInitializer { get; }
			public string Name { get; }
		}

		protected class CommandMenuItem : ThemedMenuItem
		{
			public CommandMenuItem(SmartBar bar, CommandItem item) {
				SmartBar = bar;
				CommandItem = item;
				Icon = ThemeHelper.GetImage(item.ImageId);
				Header = new TextBlock { Text = item.Name };
				item.ItemInitializer?.Invoke(this);
				// the action is installed only when called by this method
				if (item.Action != null) {
					Click += ClickHandler;
				}
				MaxHeight = SmartBar.View.ViewportHeight / 2;
			}

			public CommandItem CommandItem { get; }
			protected SmartBar SmartBar { get; }

			void ClickHandler(object s, RoutedEventArgs e) {
				var ctx2 = new CommandContext(SmartBar, s as Control, e);
				CommandItem.Action(ctx2);
				if (ctx2.KeepToolBarOnClick == false) {
					SmartBar.HideToolBar();
				}
			}
		}
	}
}
