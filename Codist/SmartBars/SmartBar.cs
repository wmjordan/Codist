using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.SmartBars
{
	/// <summary>
	/// Adornment class that draws a square box in the top right hand corner of the viewport
	/// </summary>
	internal class SmartBar
	{
		const int Selecting = 1, Working = 2;
		static IVsImageService2 _ImageService;
		readonly Timer _CreateToolBarTimer;
		int _TimerStatus;
		readonly ToolBarTray _ToolBarTray;
		/// <summary>The layer for the smart bar adornment.</summary>
		readonly IAdornmentLayer _ToolBarLayer;
		private readonly int _IconSize;
		DateTime _LastExecute;

		/// <summary>
		/// Initializes a new instance of the <see cref="SmartBar"/> class.
		/// </summary>
		/// <param name="view">The <see cref="IWpfTextView"/> upon which the adornment will be drawn</param>
		public SmartBar(IWpfTextView view, int iconSize) {
			View = view ?? throw new ArgumentNullException(nameof(view));
			_IconSize = iconSize;
			_ToolBarLayer = view.GetAdornmentLayer(nameof(SmartBar));
			View.Selection.SelectionChanged += ViewSelectionChanged;
			View.Closed += ViewClosed;
			ToolBar = new ToolBar { BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, Band = 1, IsOverflowOpen = false }.HideOverflow();
			ToolBar2 = new ToolBar { BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, Band = 2, IsOverflowOpen = false }.HideOverflow();
			_ToolBarTray = new ToolBarTray() {
				ToolBars = { ToolBar, ToolBar2 },
				IsLocked = true,
				Cursor = Cursors.Arrow,
				Background = Brushes.Transparent,
			};
			_ToolBarTray.MouseEnter += ToolBarMouseEnter;
			_ToolBarTray.MouseLeave += ToolBarMouseLeave;
			_CreateToolBarTimer = new Timer(CreateToolBar);
			if (_ImageService == null) {
				_ImageService = ServiceProvider.GlobalProvider.GetService(typeof(SVsImageService)) as IVsImageService2;
			}
			_ToolBarLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, _ToolBarTray, null);
			_ToolBarTray.Visibility = Visibility.Hidden;
		}

		protected IWpfTextView View { get; }

		protected ToolBar ToolBar { get; }
		protected ToolBar ToolBar2 { get; }

		#region Event handlers
		void ToolBarSizeChanged(object sender, SizeChangedEventArgs e) {
			SetToolBarPosition();
			_ToolBarTray.SizeChanged -= ToolBarSizeChanged;
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
			_ToolBarTray.Opacity = 1;
			_ToolBarTray.SizeChanged += ToolBarSizeChanged;
			View.VisualElement.MouseMove += ViewMouseMove;

			EXIT:
			_TimerStatus = 0;
		}

		void SetToolBarPosition() {
			// keep tool bar position when the selection is restored and the tool bar reappears after executing command
			if (DateTime.Now > _LastExecute.AddSeconds(1)) {
				var pos = Mouse.GetPosition(View.VisualElement);
				var rs = _ToolBarTray.RenderSize;
				var x = pos.X - 20;
				var y = pos.Y - rs.Height - 10;
				Canvas.SetLeft(_ToolBarTray, x < View.ViewportLeft ? View.ViewportLeft
					: x + rs.Width < View.ViewportRight ? x
					: View.ViewportRight - rs.Width);
				Canvas.SetTop(_ToolBarTray, (y < 0 ? y + rs.Height + 30 : y) + View.ViewportTop);
			}
			_ToolBarTray.Visibility = Visibility.Visible;
		}

		protected virtual void AddCommands() {
			if (CodistPackage.DebuggerStatus == DebuggerStatus.Break) {
				AddEditorCommand(ToolBar, KnownMonikers.ToolTip, "Edit.QuickInfo", "Show quick info");
			}
			if (CodistPackage.DebuggerStatus != DebuggerStatus.Running) {
				AddCommand(ToolBar, KnownMonikers.Cut, "Cut selected text\nRight click: Cut line", ctx => {
					if (ctx.RightClick) {
						View.ExpandSelectionToLine();
					}
					TextEditorHelper.ExecuteEditorCommand("Edit.Cut");
				});
			}
			AddCommand(ToolBar, KnownMonikers.Copy, "Copy selected text\nRight click: Copy line", ctx => {
				if (ctx.RightClick) {
					View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.Copy");
			});
			if (CodistPackage.DebuggerStatus != DebuggerStatus.Running) {
				if (Clipboard.ContainsText()) {
					AddEditorCommand(ToolBar, KnownMonikers.Paste, "Edit.Paste", "Paste text from clipboard");
				}
				AddCommand(ToolBar, KnownMonikers.Cancel, "Delete selected text\nRight click: Delete line", ctx => {
					if (ctx.RightClick) {
						View.ExpandSelectionToLine();
					}
					TextEditorHelper.ExecuteEditorCommand("Edit.Delete");
				});
				AddEditorCommand(ToolBar, KnownMonikers.FormatSelection, "Edit.FormatSelection", "Format selected text\nRight click: Format document", "Edit.FormatDocument");
			}
			AddEditorCommand(ToolBar, KnownMonikers.FindNext, "Edit.FindNextSelected", "Find next selected text\nRight click: Find previous selected", "Edit.FindPreviousSelected");
			//AddEditorCommand(ToolBar, "Edit.Capitalize", KnownMonikers.ASerif, "Capitalize");
		}

		protected void AddEditorCommand(ToolBar toolBar, ImageMoniker moniker, string command, string tooltip) {
			if (CodistPackage.DTE.Commands.Item(command).IsAvailable) {
				AddCommand(toolBar, moniker, tooltip, (ctx) => {
					TextEditorHelper.ExecuteEditorCommand(command);
					View.Selection.Clear();
				});
			}
		}
		protected void AddEditorCommand(ToolBar toolBar, ImageMoniker moniker, string command, string tooltip, string command2) {
			if (CodistPackage.DTE.Commands.Item(command).IsAvailable) {
				AddCommand(toolBar, moniker, tooltip, (ctx) => {
					TextEditorHelper.ExecuteEditorCommand(ctx.RightClick ? command2 : command);
					View.Selection.Clear();
				});
			}
		}

		protected void AddCommand(ToolBar toolBar, ImageMoniker moniker, string tooltip, Action<CommandContext> handler) {
			var b = new Button {
				Content = new Image { Source = GetImage(moniker, _IconSize) },
				ToolTip = tooltip,
				Cursor = Cursors.Hand
			};
			b.Click += (s, args) => {
				HideToolBar(s, args);
				SetLastExecuteTime(s, args);
				handler(new CommandContext());
			};
			b.MouseRightButtonUp += (s, args) => {
				HideToolBar(s, args);
				handler(new CommandContext(true));
				args.Handled = true;
			};
			toolBar.Items.Add(b);
		}

		protected void AddCommands(ToolBar toolBar, ImageMoniker moniker, string tooltip, params CommandItem[] items) {
			AddCommands(toolBar, moniker, tooltip, () => items);
		}

		protected void AddCommands(ToolBar toolBar, ImageMoniker moniker, string tooltip, Func<IEnumerable<CommandItem>> items) {
			var b = new Button {
				Content = new Image { Source = GetImage(moniker, _IconSize) },
				ToolTip = tooltip,
				ContextMenu = new ContextMenu()
			};
			b.Click += (s, args) => {
				var btn = s as Button;
				var m = btn.ContextMenu;
				m.IsEnabled = true;
				m.PlacementTarget = btn;
				m.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
				m.IsOpen = true;
				if (m.Items.Count == 0) {
					foreach (var item in items()) {
						var mi = new MenuItem {
							Icon = new Image { Source = GetImage(item.Moniker, _IconSize) },
							Header = item.Name,
							ToolTip = item.Tooltip
						};
						mi.Click += (sender, e) => {
							HideToolBar(sender, e);
							//SetLastExecuteTime(sender, e);
							item.Action(new CommandContext());
						};
						m.Items.Add(mi);
					}
				}
			};
			toolBar.Items.Add(b);
		}

		void SetLastExecuteTime(object sender, RoutedEventArgs e) {
			_LastExecute = DateTime.Now;
		}
		void HideToolBar(object sender, RoutedEventArgs e) {
			_ToolBarTray.Visibility = Visibility.Hidden;
			View.VisualElement.MouseMove -= ViewMouseMove;
		}

		static System.Windows.Media.Imaging.BitmapSource GetImage(ImageMoniker moniker, int iconSize) {
			var imageAttributes = new ImageAttributes {
				Flags = (uint)_ImageAttributesFlags.IAF_RequiredFlags,
				ImageType = (uint)_UIImageType.IT_Bitmap,
				Format = (uint)_UIDataFormat.DF_WPF,
				StructSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ImageAttributes)),
				LogicalHeight = iconSize,
				LogicalWidth = iconSize,
			};
			Object data;
			_ImageService.GetImage(moniker, imageAttributes).get_Data(out data);
			return data as System.Windows.Media.Imaging.BitmapSource;
		}

		protected sealed class CommandItem
		{
			public CommandItem(string name, ImageMoniker moniker, string tooltip, Action<CommandContext> action) {
				Name = name;
				Tooltip = tooltip;
				Moniker = moniker;
				Action = action;
			}

			public string Name { get; }
			public string Tooltip { get; }
			public ImageMoniker Moniker { get; }
			public Action<CommandContext> Action { get; }
		}

		protected sealed class CommandContext
		{
			public bool RightClick { get; }
			public CommandContext() {
			}
			public CommandContext(bool rightClick) {
				RightClick = rightClick;
			}
		}
	}
}
