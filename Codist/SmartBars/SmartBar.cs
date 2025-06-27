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
using VsBrushes = Microsoft.VisualStudio.Shell.VsBrushes;
using VsColors = Microsoft.VisualStudio.Shell.VsColors;

namespace Codist.SmartBars
{
	//todo Make this class async
	/// <summary>The contextual toolbar.</summary>
	internal partial class SmartBar
	{
		const int Selecting = 1, Working = 2;
		internal const string QuickInfoSuppressionId = nameof(SmartBar);

		// The layer for the smart bar.
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
		protected virtual BarType Type => BarType.General;
		protected bool IsReadOnly { get; private set; }
		protected bool IsMultilineSelected { get; private set; }
		protected CommandItem QuickAccessCommand { get; private set; }

		protected void AddCommand(ToolBar toolBar, int imageId, string tooltip, Action<CommandContext> handler) {
			toolBar.Items.Add(new CommandButton(this, imageId, tooltip, handler));
		}
		protected void AddCommand(ToolBar toolBar, int imageId, string tooltip, Func<CommandContext, Task> handler) {
			toolBar.Items.Add(new CommandButton(this, imageId, tooltip, handler));
		}

		protected virtual void AddCommands() {
			if (IsReadOnly == false) {
				AddCutCommand();
			}
			AddCopyCommand();
			if (IsReadOnly == false) {
				AddPasteCommand();
				AddDuplicateCommand();
				AddDeleteCommand();
				if (Type.CeqAny(BarType.CSharp, BarType.Cpp) == false) {
					AddCommentCommand(ToolBar);
				}
				var q = QuickAccessCommand;
				if (q?.QuickAccessCondition(this) == true) {
					ToolBar.Items.Add(new CommandButton(this, q));
				}
				AddSpecialFormatCommand();
				AddWrapTextCommand();
				AddEditAllMatchingCommand();
			}
			if (_IsDiffWindow) {
				AddDiffCommands();
			}
			if (IsMultilineSelected == false) {
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
				Foreground = ThemeCache.ToolWindowTextBrush,
				IsEnabled = true,
				PlacementTarget = btn,
				Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
				IsOpen = true
			};
			m.SetBackgroundForCrispImage(ThemeCache.TitleBackgroundColor);
			return m;
		}

		protected void AddEditorCommand(ToolBar toolBar, int imageId, string command, string tooltip) {
			if (TextEditorHelper.IsCommandAvailable(command)) {
				AddCommand(toolBar, imageId, tooltip, (ctx) => TextEditorHelper.ExecuteEditorCommand(command));
			}
		}

		protected void AddEditorCommand(ToolBar toolBar, int imageId, string command, string tooltip, string rightClickCommand) {
			if (TextEditorHelper.IsCommandAvailable(command)) {
				AddCommand(toolBar, imageId, tooltip, (ctx) => TextEditorHelper.ExecuteEditorCommand(ctx.RightClick ? rightClickCommand : command));
			}
		}

		async Task CreateToolBarAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			while ((Mouse.LeftButton == MouseButtonState.Pressed
				|| UIHelper.IsShiftDown)
				&& cancellationToken.IsCancellationRequested == false) {
				// postpone the even handler until the left mouse button and keyboard modifiers are released
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
			IsReadOnly = _View.IsCaretInReadOnlyRegion();
			IsMultilineSelected = _View.IsMultilineSelected();
			try {
				await AddCommandsAsync(cancellationToken);
				AddCommands();
			}
			catch (OperationCanceledException) {
				return;
			}
			catch (Exception ex) {
				MessageWindow.Error(ex, null, null, this);
				return;
			}
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
			_ToolBarTray.SizeChanged -= ToolBarSizeChanged;
			_ToolBarTray.SizeChanged += ToolBarSizeChanged;
			_View.VisualElement.MouseMove -= ViewMouseMove;
			_View.VisualElement.MouseMove += ViewMouseMove;
		}

		protected void HideToolBar() {
			_ToolBarTray.Visibility = Visibility.Hidden;
			_ToolBarTray.SizeChanged -= ToolBarSizeChanged;
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
			if (TextEditorHelper.ActiveViewFocused() == false) {
				return;
			}
			if (e.Key == Key.Escape) {
				HideToolBar();
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
				HideToolBar();
				return;
			}
			if ((now - _LastShiftHit).Ticks < TimeSpan.TicksPerSecond) {
				CreateToolBar(this, _Cancellation.GetToken());
			}
			else {
				_LastShiftHit = DateTime.Now;
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.ExceptionHandled)]
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
			HideToolBar();
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
				HideToolBar();
				return;
			}
			_ToolBarTray.Opacity = (SensibleRange - op) / SensibleRange;
		}

		void ViewSelectionChanged(object sender, EventArgs e) {
			// suppress event handler if KeepToolBar
			if (TextEditorHelper.ActiveViewFocused() == false && _View.VisualElement.IsKeyboardFocused == false
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
			if (_View.HasRepeatingAction() // do not show smart bar if repeating action
				|| Interlocked.CompareExchange(ref _SelectionStatus, Selecting, 0) != 0) {
				return;
			}
			SyncHelper.CancelAndDispose(ref _Cancellation, true);
			CreateToolBar(this, _Cancellation.Token);

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.ExceptionHandled)]
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
			const ModifierKeys UnknownModifier = (ModifierKeys)(-1);
			ModifierKeys _ModifierKeys = UnknownModifier;
			int _MultiLine;

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

			public ModifierKeys ModifierKeys => _ModifierKeys == UnknownModifier ? (_ModifierKeys = Keyboard.Modifiers) : _ModifierKeys;
			public bool HasMultiLineSelection => (_MultiLine != 0 ? _MultiLine : _MultiLine = View.IsMultilineSelected() ? 2 : 1) > 1;

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
			public CommandButton(SmartBar bar, CommandItem item)
				: this(bar, item.ImageId, item.ToolTip != null ? $"{item.Name}\n{item.ToolTip}" : item.Name, item.Action) {
				_AsyncClickHandler = item.AsyncAction;
			}
			public CommandButton(SmartBar bar, int imageId, string tooltip, Action<CommandContext> clickHandler, Func<CommandContext, IEnumerable<CommandItem>> menuFactory) {
				_Bar = bar;
				_ClickHandler = clickHandler;
				_MenuFactory = menuFactory;
				this.SetLazyToolTip(() => new CommandToolTip(imageId, tooltip));
				Cursor = Cursors.Hand;
				BorderThickness = WpfHelper.TinyMargin;
				Content = VsImageHelper.GetImage(imageId, (int)(VsImageHelper.DefaultIconSize * bar.View.ZoomFactor())).WrapMargin(WpfHelper.SmallMargin);
				this.InheritStyle<Button>(SharedDictionaryManager.ThemedControls);
				this.ReferenceCrispImageBackground(VsColors.CommandBarGradientBeginKey);
				Click += CommandButton_Click;
				MouseRightButtonUp += CommandButton_MouseRightButtonUp;
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void CommandButton_Click(object sender, RoutedEventArgs e) {
				var ctx = new CommandContext(_Bar, this);
				Action<CommandContext> clickHandler = _ClickHandler;
				Func<CommandContext, Task> asyncClickHandler;
				if (clickHandler != null) {
					try {
						clickHandler(ctx);
					}
					catch (Exception ex) {
						MessageWindow.Error(ex, null, null, this);
					}
				}
				else if ((asyncClickHandler = _AsyncClickHandler) != null) {
					try {
						await asyncClickHandler(ctx);
					}
					catch (Exception ex) {
						MessageWindow.Error(ex, null, null, this);
					}
				}
				else {
					if (ContextMenu?.GetIsRightClicked() == false) {
						ContextMenu.IsOpen = true;
						return;
					}
					ClearContextMenu();
					var m = CreateContextMenuFromMenuFactory(ctx);
				}
				TryHideToolBar(ctx);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void CommandButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
				if (ContextMenu?.GetIsRightClicked() == true) {
					ContextMenu.IsOpen = true;
					e.Handled = true;
					return;
				}
				var ctx = new CommandContext(_Bar, this, true);
				Action<CommandContext> clickHandler;
				Func<CommandContext, Task> asyncClickHandler;
				if (_MenuFactory != null) {
					ClearContextMenu();
					var m = CreateContextMenuFromMenuFactory(ctx);
					m.SetIsRightClicked();
				}
				else if ((clickHandler = _ClickHandler) != null) {
					try {
						clickHandler(ctx);
					}
					catch (Exception ex) {
						MessageWindow.Error(ex, null, null, this);
					}
				}
				else if ((asyncClickHandler = _AsyncClickHandler) != null) {
					try {
						await asyncClickHandler(ctx);
					}
					catch (Exception ex) {
						MessageWindow.Error(ex, null, null, this);
					}
				}
				TryHideToolBar(ctx);
				e.Handled = true;
			}

			void TryHideToolBar(CommandContext ctx) {
				SmartBar bar;
				if ((bar = _Bar) != null && ctx.KeepToolBarOnClick == false && ContextMenu?.IsOpen != true) {
					bar.HideToolBar();
				}
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
					_AsyncClickHandler = null;
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
			public string ToolTip { get; set; }
			public Func<CommandContext, Task> AsyncAction { get; }
			public Predicate<SmartBar> QuickAccessCondition { get; set; }

			public static readonly Predicate<SmartBar> HasSelection = b => b._View.TryGetFirstSelectionSpan(out _);
			public static readonly Predicate<SmartBar> HasEditableSelection = b => b.IsReadOnly == false && b._View.TryGetFirstSelectionSpan(out _);
			public static readonly Predicate<SmartBar> Editable = b => b.IsReadOnly == false;
			public static readonly Predicate<SmartBar> EditableAndMultiline = b => b.IsReadOnly == false && b.IsMultilineSelected;
		}

		protected sealed class CommandMenuItem : ThemedMenuItem, IDisposable
		{
			SmartBar _SmartBar;

			public CommandMenuItem(SmartBar bar, CommandItem item) {
				_SmartBar = bar;
				CommandItem = item;
				Icon = VsImageHelper.GetImage(item.ImageId);
				Header = new TextBlock { Text = item.Name };
				if (item.ToolTip != null) {
					this.SetLazyToolTip(() => new CommandToolTip(item.ImageId, item.Name, new ThemedTipText(item.ToolTip)));
				}
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

			public CommandItem CommandItem { get; }

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void AsyncClickHandler(object sender, RoutedEventArgs e) {
				var bar = _SmartBar;
				if (bar == null) {
					return;
				}
				var ctx = new CommandContext(bar, sender as Control);
				try {
					await CommandItem.AsyncAction.Invoke(ctx);
				}
				catch (Exception ex) {
					MessageWindow.Error(ex, null, null, this);
				}
				AllowQuickAccessCommandWithCondition(bar);
				if (ctx.KeepToolBarOnClick == false) {
					bar.HideToolBar();
				}
			}

			void ClickHandler(object s, RoutedEventArgs e) {
				var bar = _SmartBar;
				if (bar == null) {
					return;
				}
				var ctx = new CommandContext(bar, s as Control);
				try {
					CommandItem.Action(ctx);
				}
				catch (Exception ex) {
					MessageWindow.Error(ex, null, null, this);
				}
				AllowQuickAccessCommandWithCondition(bar);
				if (ctx.KeepToolBarOnClick == false) {
					bar.HideToolBar();
				}
			}

			void AllowQuickAccessCommandWithCondition(SmartBar bar) {
				if (CommandItem.QuickAccessCondition != null) {
					bar.QuickAccessCommand = CommandItem;
				}
			}

			public override void Dispose() {
				if (_SmartBar != null) {
					Click -= ClickHandler;
					_SmartBar = null;
					base.Dispose();
				}
			}
		}

		internal enum BarType
		{
			General,
			CSharp,
			Cpp,
			Markdown,
			Markup,
			PlainText,
			Output
		}
	}
}
