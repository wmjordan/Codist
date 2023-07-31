using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using TH = Microsoft.VisualStudio.Shell.ThreadHelper;
using VsBrushes = Microsoft.VisualStudio.Shell.VsBrushes;

namespace Codist.SmartBars
{
	//todo Make this class async
	/// <summary>The contextual toolbar.</summary>
	internal partial class SmartBar
	{
		const int Selecting = 1, Working = 2;
		internal const string QuickInfoSuppressionId = nameof(SmartBar);

		/// <summary>The layer for the smart bar.</summary>
		TextViewOverlay _ToolBarLayer;
		readonly ToolBarTray _ToolBarTray;
		readonly bool _IsDiffWindow;
		CancellationTokenSource _Cancellation = new CancellationTokenSource();
		IWpfTextView _View;
		ITextSearchService2 _TextSearchService;
		DateTime _LastExecute;
		DateTime _LastShiftHit;
		int _SelectionStatus;

		/// <summary>
		/// Initializes a new instance of the <see cref="SmartBar"/> class.
		/// </summary>
		/// <param name="view">The <see cref="IWpfTextView"/> upon which the smart bar will be drawn.</param>
		public SmartBar(IWpfTextView view, ITextSearchService2 textSearchService) {
			_View = view ?? throw new ArgumentNullException(nameof(view));
			_IsDiffWindow = view.Roles.Contains("DIFF");
			_TextSearchService = textSearchService;
			_ToolBarLayer = TextViewOverlay.GetOrCreate(view);
			Config.RegisterUpdateHandler(UpdateSmartBarConfig);
			if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.ShiftToggleDisplay)) {
				_View.VisualElement.PreviewKeyUp += ViewKeyUp;
			}
			if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.ManualDisplaySmartBar) == false) {
				_View.Selection.SelectionChanged += ViewSelectionChanged;
			}
			_View.Closed += ViewClosed;
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
				Background = null, // enables the mouse to click throw transparent part of the ToolBarTray 
				UseLayoutRounding = true
			};
			_ToolBarTray.MouseEnter += ToolBarMouseEnter;
			_ToolBarTray.MouseLeave += ToolBarMouseLeave;
			_ToolBarTray.DragEnter += HideToolBar;
			_ToolBarLayer.Add(_ToolBarTray);
			_ToolBarTray.Visibility = Visibility.Hidden;
		}

		protected ToolBar ToolBar { get; }
		protected ToolBar ToolBar2 { get; }
		protected IWpfTextView View => _View;
		protected ITextSearchService2 TextSearchService => _TextSearchService;
		protected CancellationToken CancellationToken => _Cancellation?.Token ?? new CancellationToken(true);

		protected void AddCommand(ToolBar toolBar, int imageId, string tooltip, Action<CommandContext> handler) {
			toolBar.Items.Add(new CommandButton(this, imageId, tooltip, handler));
		}
		protected void AddCommand(ToolBar toolBar, int imageId, string tooltip, Func<CommandContext, Task> handler) {
			toolBar.Items.Add(new CommandButton(this, imageId, tooltip, handler));
		}

		protected virtual void AddCommands() {
			var readOnly = _View.IsCaretInReadOnlyRegion();
			if (readOnly == false) {
				AddCutCommand();
			}
			AddCopyCommand();
			if (readOnly == false) {
				AddPasteCommand();
				AddDuplicateCommand();
				AddDeleteCommand();
				AddSpecialFormatCommand();
				AddWrapTextCommand();
				AddEditAllMatchingCommand();
			}
			if (_IsDiffWindow) {
				AddDiffCommands();
			}
			if (_View.IsMultilineSelected() == false) {
				AddFindAndReplaceCommands();
				AddViewInBrowserCommand();
				if (Config.Instance.DeveloperOptions.MatchFlags(DeveloperOptions.ShowSyntaxClassificationInfo)) {
					AddClassificationInfoCommand();
				}
			}
			AddDebuggerCommands();
		}
		protected virtual Task AddCommandsAsync(CancellationToken cancellationToken) {
			return Task.CompletedTask;
		}

		protected void AddCommands(ToolBar toolBar, int imageId, string tooltip, Action<CommandContext> leftClickHandler, Func<CommandContext, IEnumerable<CommandItem>> getItemsHandler) {
			toolBar.Items.Add(new CommandButton(this, imageId, tooltip, leftClickHandler, getItemsHandler));
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
			TH.ThrowIfNotOnUIThread();
			if (CodistPackage.DTE.Commands.Item(command).IsAvailable) {
				AddCommand(toolBar, imageId, tooltip, (ctx) => TextEditorHelper.ExecuteEditorCommand(command));
			}
		}

		protected void AddEditorCommand(ToolBar toolBar, int imageId, string command, string tooltip, string rightClickCommand) {
			TH.ThrowIfNotOnUIThread();
			if (CodistPackage.DTE.Commands.Item(command).IsAvailable) {
				AddCommand(toolBar, imageId, tooltip, (ctx) => TextEditorHelper.ExecuteEditorCommand(ctx.RightClick ? rightClickCommand : command));
			}
		}

		async Task CreateToolBarAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			while ((Mouse.LeftButton == MouseButtonState.Pressed || Keyboard.Modifiers == ModifierKeys.Shift)
				&& cancellationToken.IsCancellationRequested == false) {
				// postpone the even handler until the mouse button is released
				await Task.Delay(100, cancellationToken);
			}
			if (_View?.Selection.IsEmpty == true
				|| Interlocked.Exchange(ref _SelectionStatus, Working) != Selecting) {
				goto EXIT;
			}
			await InternalCreateToolBarAsync(cancellationToken);
			EXIT:
			_SelectionStatus = 0;
		}

		async Task InternalCreateToolBarAsync(CancellationToken cancellationToken = default) {
			_ToolBarTray.Visibility = Visibility.Hidden;
			ToolBar.DisposeCollection();
			ToolBar2.DisposeCollection();
			await AddCommandsAsync(cancellationToken);
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
			_ToolBarTray.Opacity = WpfHelper.DimmedOpacity;
			_ToolBarTray.SizeChanged += ToolBarSizeChanged;
			_View.VisualElement.MouseMove += ViewMouseMove;
		}

		protected void HideToolBar() {
			_ToolBarTray.Visibility = Visibility.Hidden;
			_View.VisualElement.MouseMove -= ViewMouseMove;
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
			if (DateTime.Now < _LastExecute.AddSeconds(1)) {
				return;
			}
			var v = _View;
			var pos = Mouse.GetPosition(v.VisualElement);
			var rs = _ToolBarTray.RenderSize;
			var z = v.ZoomLevel / 100;
			var x = Math.Max(0, (pos.X - 35) * z);
			var y = (pos.Y - 10) * z - rs.Height;
			Canvas.SetLeft(_ToolBarTray, Math.Min(x, v.ViewportWidth * z - rs.Width));
			Canvas.SetTop(_ToolBarTray, y > 0 ? y : (pos.Y + 10) * z);
		}

		#region Event handlers
		void UpdateSmartBarConfig(ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.MatchFlags(Features.SmartBar)) {
				var v = _View;
				v.VisualElement.PreviewKeyUp -= ViewKeyUp;
				if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.ShiftToggleDisplay)) {
					v.VisualElement.PreviewKeyUp += ViewKeyUp;
				}
				v.Selection.SelectionChanged -= ViewSelectionChanged;
				if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.ManualDisplaySmartBar) == false) {
					v.Selection.SelectionChanged += ViewSelectionChanged;
				}
			}
		}

		void ToolBarMouseEnter(object sender, EventArgs e) {
			_View.VisualElement.MouseMove -= ViewMouseMove;
			_ToolBarTray.Opacity = 1;
			_View.Properties[QuickInfoSuppressionId] = true;
		}

		void ToolBarMouseLeave(object sender, EventArgs e) {
			_View.VisualElement.MouseMove += ViewMouseMove;
			_View.Properties.RemoveProperty(QuickInfoSuppressionId);
		}

		void ToolBarSizeChanged(object sender, SizeChangedEventArgs e) {
			SetToolBarPosition();
			_ToolBarTray.SizeChanged -= ToolBarSizeChanged;
		}

		void ViewKeyUp(object sender, KeyEventArgs e) {
			if (TextEditorHelper.ActiveViewFocused()) {
				return;
			}
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
				CreateToolBar(this, _Cancellation.GetToken());
			}
			else {
				_LastShiftHit = DateTime.Now;
			}

			async void CreateToolBar(SmartBar me, CancellationToken token) {
				try {
					await me.InternalCreateToolBarAsync(token);
				}
				catch (OperationCanceledException) {
					// ignore
				}
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
			if (TextEditorHelper.ActiveViewFocused() == false
				|| DateTime.Now < _LastExecute.AddSeconds(1) && _ToolBarTray.Visibility == Visibility.Visible) {
				return;
			}
			if (_View.Selection.IsEmpty) {
				_ToolBarTray.Visibility = Visibility.Hidden;
				_View.VisualElement.MouseMove -= ViewMouseMove;
				SyncHelper.CancelAndDispose(ref _Cancellation, true);
				_SelectionStatus = 0;
				return;
			}
			if (Interlocked.CompareExchange(ref _SelectionStatus, Selecting, 0) != 0) {
				return;
			}
			SyncHelper.CancelAndDispose(ref _Cancellation, true);
			CreateToolBar(this, _Cancellation.Token);

			async void CreateToolBar(SmartBar me, CancellationToken token) {
				try {
					if (me._ToolBarTray.Visibility != Visibility.Visible) {
						await Task.Delay(400, token);
					}
					if (token.IsCancellationRequested == false) {
						await me.CreateToolBarAsync(token);
					}
				}
				catch (OperationCanceledException) {
					// ignore
				}
			}
		}

		void ViewClosed(object sender, EventArgs e) {
			SyncHelper.CancelAndDispose(ref _Cancellation, false);
			ToolBar.DisposeCollection();
			ToolBar2.DisposeCollection();
			_ToolBarTray.ToolBars.Clear();
			_ToolBarTray.MouseEnter -= ToolBarMouseEnter;
			_ToolBarTray.MouseLeave -= ToolBarMouseLeave;
			_ToolBarTray.SizeChanged -= ToolBarSizeChanged;
			_ToolBarTray.DragEnter -= HideToolBar;
			if (_ToolBarTray != null) {
				_ToolBarLayer = null;
			}
			if (_View != null) {
				_View.Selection.SelectionChanged -= ViewSelectionChanged;
				_View.VisualElement.MouseMove -= ViewMouseMove;
				_View.VisualElement.PreviewKeyUp -= ViewKeyUp;
				_View.Closed -= ViewClosed;
				_View = null;
			}
			Config.UnregisterUpdateHandler(UpdateSmartBarConfig);
			_TextSearchService = null;
		}
		#endregion

		protected sealed class CommandContext
		{
			public CommandContext(SmartBar bar, Control control) {
				Bar = bar;
				Sender = control;
			}
			public CommandContext(SmartBar bar, Control control, bool rightClick) : this(bar, control) {
				RightClick = rightClick;
			}
			public SmartBar Bar { get; }
			public bool RightClick { get; }
			public Control Sender { get; }
			public bool KeepToolBarOnClick { get; set; }
			public IWpfTextView View => Bar.View;
			public ITextSearchService2 TextSearchService => Bar.TextSearchService;
			public CancellationToken CancellationToken => Bar._Cancellation.GetToken();

			public void HideToolBar() {
				Bar.HideToolBar();
			}
			public void KeepToolBar(bool refresh) {
				Bar.KeepToolbar();
				KeepToolBarOnClick = true;
				if (refresh) {
					SyncHelper.RunSync(() => Bar.InternalCreateToolBarAsync(CancellationToken));
				}
			}
		}

		sealed class CommandButton : Button, IDisposable
		{
			SmartBar _Bar;
			Action<CommandContext> _ClickHandler;
			Func<CommandContext, Task> _AsyncClickHandler;
			Func<CommandContext, IEnumerable<CommandItem>> _MenuFactory;

			public CommandButton(SmartBar bar, int imageId, string tooltip, Action<CommandContext> clickHandler)
				: this(bar, imageId, tooltip, clickHandler, null) { }
			public CommandButton(SmartBar bar, int imageId, string tooltip, Func<CommandContext, Task> clickHandler)
				: this(bar, imageId, tooltip, null, null) {
				_AsyncClickHandler = clickHandler;
			}
			public CommandButton(SmartBar bar, int imageId, string tooltip, Action<CommandContext> clickHandler, Func<CommandContext, IEnumerable<CommandItem>> menuFactory) {
				_Bar = bar;
				_ClickHandler = clickHandler;
				_MenuFactory = menuFactory;
				this.SetLazyToolTip(() => new CommandToolTip(imageId, tooltip));
				Cursor = Cursors.Hand;
				BorderThickness = WpfHelper.TinyMargin;
				Content = ThemeHelper.GetImage(imageId, (int)(ThemeHelper.DefaultIconSize * bar.View.ZoomFactor())).WrapMargin(WpfHelper.SmallMargin);
				this.InheritStyle<Button>(SharedDictionaryManager.ThemedControls);
				this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
				Click += CommandButton_Click;
				MouseRightButtonUp += CommandButton_MouseRightButtonUp;
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void CommandButton_Click(object sender, RoutedEventArgs e) {
				var ctx = new CommandContext(_Bar, this);
				Action<CommandContext> clickHandler = _ClickHandler;
				Func<CommandContext, Task> asyncClickHandler;
				if (clickHandler != null) {
					clickHandler(ctx);
				}
				else if ((asyncClickHandler = _AsyncClickHandler) != null) {
					try {
						await asyncClickHandler(ctx);
					}
					catch (Exception ex) {
						MessageWindow.Error(ex);
					}
				}
				else {
					if (ContextMenu != null && ContextMenu.GetIsRightClicked() == false) {
						ContextMenu.IsOpen = true;
						return;
					}
					ClearContextMenu();
					var m = CreateContextMenuFromMenuFactory(ctx);
				}
				if (_Bar != null && ctx.KeepToolBarOnClick == false && ContextMenu?.IsOpen != true) {
					_Bar.HideToolBar();
				}
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void CommandButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
				if (ContextMenu != null && ContextMenu.GetIsRightClicked()) {
					ContextMenu.IsOpen = true;
					e.Handled = true;
					return;
				}
				var ctx = new CommandContext(_Bar, this, true);
				Action<CommandContext> clickHandler;
				Func<CommandContext, Task> asyncClickHandler;
				SmartBar bar;
				if (_MenuFactory != null) {
					ClearContextMenu();
					var m = CreateContextMenuFromMenuFactory(ctx);
					m.SetIsRightClicked();
				}
				else if ((clickHandler = _ClickHandler) != null) {
					clickHandler(ctx);
				}
				else if ((asyncClickHandler = _AsyncClickHandler) != null) {
					try {
						await asyncClickHandler(ctx);
					}
					catch (Exception ex) {
						MessageWindow.Error(ex);
					}
				}
				if ((bar = _Bar) != null && ctx.KeepToolBarOnClick == false && ContextMenu?.IsOpen != true) {
					bar.HideToolBar();
				}
				e.Handled = true;
			}

			protected override AutomationPeer OnCreateAutomationPeer() {
				return null;
			}

			ContextMenu CreateContextMenuFromMenuFactory(CommandContext ctx) {
				var m = SetupContextMenu(this);
				foreach (var item in _MenuFactory(ctx)) {
					m.Items.Add(new CommandMenuItem(_Bar, item));
				}
				ContextMenu = m;
				m.IsOpen = true;
				return m;
			}

			void ClearContextMenu() {
				if (ContextMenu is IDisposable d) {
					d.Dispose();
				}
				else {
					ContextMenu?.DisposeCollection();
				}
			}

			public void Dispose() {
				if (_Bar != null) {
					_ClickHandler = null;
					_MenuFactory = null;
					if (ContextMenu != null) {
						ClearContextMenu();
						ContextMenu.PlacementTarget = null;
						ContextMenu = null;
					}
					DataContext = null;
					_Bar = null;
				}
			}
		}

		protected sealed class CommandItem
		{
			public CommandItem(int imageId, string name, Action<CommandContext> action)
				: this(imageId, name, null, action) { }
			public CommandItem(int imageId, string name, Action<MenuItem> controlInitializer, Action<CommandContext> action) {
				Name = name;
				ImageId = imageId;
				ItemInitializer = controlInitializer;
				Action = action;
			}
			public CommandItem(int imageId, string name, Action<MenuItem> controlInitializer, Func<CommandContext, Task> asyncAction) {
				Name = name;
				ImageId = imageId;
				ItemInitializer = controlInitializer;
				AsyncAction = asyncAction;
			}

			public Action<CommandContext> Action { get; }
			public int ImageId { get; }
			public Action<MenuItem> ItemInitializer { get; }
			public string Name { get; }
			public Func<CommandContext, Task> AsyncAction { get; }
		}

		protected sealed class CommandMenuItem : ThemedMenuItem, IDisposable
		{
			SmartBar _SmartBar;

			public CommandMenuItem(SmartBar bar, CommandItem item) {
				_SmartBar = bar;
				CommandItem = item;
				Icon = ThemeHelper.GetImage(item.ImageId);
				Header = new TextBlock { Text = item.Name };
				item.ItemInitializer?.Invoke(this);
				// the action is installed only when called by this method
				if (item.Action != null) {
					Click += ClickHandler;
				}
				else if (item.AsyncAction != null) {
					Click += AsyncClickHandler;
				}
				MaxHeight = _SmartBar.View.ViewportHeight / 2;
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void AsyncClickHandler(object sender, RoutedEventArgs e) {
				var bar = _SmartBar;
				if (bar == null) {
					return;
				}
				var ctx = new CommandContext(bar, sender as Control);
				await CommandItem.AsyncAction.Invoke(ctx);
				if (ctx.KeepToolBarOnClick == false) {
					bar.HideToolBar();
				}
			}

			public CommandItem CommandItem { get; }

			public override void Dispose() {
				if (_SmartBar != null) {
					Click -= ClickHandler;
					_SmartBar = null;
					base.Dispose();
				}
			}

			void ClickHandler(object s, RoutedEventArgs e) {
				var bar = _SmartBar;
				if (bar == null) {
					return;
				}
				var ctx = new CommandContext(bar, s as Control);
				CommandItem.Action(ctx);
				if (ctx.KeepToolBarOnClick == false) {
					bar.HideToolBar();
				}
			}
		}
	}
}
