using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Controls
{
	// HACK: put the symbol list, smart bar, etc. on top of the WpfTextView
	// don't use AdornmentLayer to do so, otherwise contained objects will go up and down when scrolling code window
	sealed class ExternalAdornment : ContentPresenter
	{
		internal const string QuickInfoSuppressionId = nameof(ExternalAdornment);

		IWpfTextView _View;
		readonly Canvas _Canvas;
		int _LayerZIndex;
		bool _isDragging;
		Point _beginDragPosition;

		public ExternalAdornment(IWpfTextView view) {
			UseLayoutRounding = true;
			SnapsToDevicePixels = true;
			// put the control on top of the editor window and share the same size
			Grid.SetColumn(this, 1);
			Grid.SetRow(this, 1);
			Grid.SetIsSharedSizeScope(this, true);
			var grid = view.VisualElement.GetParent<Grid>();
			if (grid != null) {
				grid.Children.Add(this);
				view.Selection.SelectionChanged += ViewSeletionChanged;
			}
			else {
				view.VisualElement.Loaded += VisualElement_Loaded;
			}
			view.Closed += View_Closed;
			Content = _Canvas = new Canvas();
			_Canvas.PreviewMouseRightButtonUp += Canvas_PreviewMouseRightButtonUp;
			_View = view;
		}

		// if nothing in the adornment, it is sized (0,0)
		public double DisplayHeight => ActualHeight > 0 ? ActualHeight : this.GetParent<FrameworkElement>().ActualHeight;

		public static ExternalAdornment GetOrCreate(IWpfTextView view) {
			return view.Properties.GetOrCreateSingletonProperty(() => new ExternalAdornment(view));
		}
		public static ExternalAdornment Get(IWpfTextView view) {
			return view.Properties.TryGetProperty(typeof(ExternalAdornment), out ExternalAdornment a) ? a : null;
		}

		public event EventHandler<AdornmentChildRemovedEventArgs> ChildRemoved;

		public void FocusOnTextView() {
			_View.VisualElement.Focus();
		}

		public bool Contains(UIElement element) {
			return _Canvas.Children.Contains(element);
		}
		public void Add(UIElement element) {
			_Canvas.Children.Add(element);
			element.MouseLeave -= ReleaseQuickInfo;
			element.MouseLeave += ReleaseQuickInfo;
			element.MouseEnter -= SuppressQuickInfo;
			element.MouseEnter += SuppressQuickInfo;
			element.MouseLeftButtonDown -= BringToFront;
			element.MouseLeftButtonDown += BringToFront;
			Panel.SetZIndex(element, ++_LayerZIndex);
		}
		public void Remove(UIElement element) {
			if (element is null) {
				return;
			}
			_Canvas.Children.Remove(element);
			UnhookChild(element);
			AfterChildRemoved();
		}

		void UnhookChild(UIElement element) {
			element.MouseLeave -= ReleaseQuickInfo;
			element.MouseEnter -= SuppressQuickInfo;
			element.MouseLeftButtonDown -= BringToFront;
			if (_View.IsClosed == false) {
				ChildRemoved?.Invoke(this, new AdornmentChildRemovedEventArgs(element));
			}
		}

		void AfterChildRemoved() {
			_View.Properties.RemoveProperty(QuickInfoSuppressionId);
			if (_View.IsClosed == false) {
				var children = _Canvas.Children;
				for (int i = children.Count - 1; i >= 0; i--) {
					var f = children[i].GetFirstVisualChild<TextBox>();
					if (f != null && f.Focus()) {
						return;
					}
				}
				FocusOnTextView();
			}
		}

		public void RemoveAndDispose(UIElement element) {
			_Canvas.Children.RemoveAndDispose(element);
			UnhookChild(element);
			AfterChildRemoved();
		}

		public void ClearUnpinnedChildren() {
			var children = _Canvas.Children;
			for (int i = children.Count - 1; i >= 0; i--) {
				var child = children[i];
				if (child is SymbolList l && l.IsPinned == false) {
					if (l.Owner == null) {
						children.RemoveAndDisposeAt(i);
					}
					else {
						children.RemoveAt(i);
					}
					UnhookChild(child);
					AfterChildRemoved();
				}
			}
		}

		public void Position(FrameworkElement child, Point point, int minVisibleSize) {
			ConstrainChildWindow(child, point, minVisibleSize);
		}

		void Canvas_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs args) {
			// hack: suppress the undesired built-in context menu of tabs in VS 16.5
			if (args.Source is SymbolList symbolList) {
				symbolList.ShowContextMenu(args);
			}
			args.Handled = true;
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
			base.OnRenderSizeChanged(sizeInfo);
			foreach (var item in _Canvas.Children) {
				var child = item as FrameworkElement;
				if (child != null) {
					ConstrainChildWindow(child, new Point(Canvas.GetLeft(child), Canvas.GetTop(child)));
				}
			}
		}

		void ConstrainChildWindow(FrameworkElement child, Point newPos, int minVisibleSize = 30) {
			// constrain window within editor view
			if (newPos.X + child.ActualWidth < minVisibleSize) {
				newPos.X = minVisibleSize - child.ActualWidth;
			}
			else if (newPos.X > ActualWidth - minVisibleSize) {
				newPos.X = ActualWidth - minVisibleSize;
			}
			if (newPos.X + child.ActualWidth > ActualWidth) {
				newPos.X = ActualWidth - child.ActualWidth;
			}
			if (newPos.Y + child.ActualHeight < minVisibleSize) {
				newPos.Y = minVisibleSize - child.ActualHeight;
			}
			else if (newPos.Y > ActualHeight - minVisibleSize) {
				newPos.Y = ActualHeight - minVisibleSize;
			}
			Canvas.SetLeft(child, newPos.X);
			Canvas.SetTop(child, newPos.Y);
		}

		#region Draggable
		public void MakeDraggable(FrameworkElement draggablePart) {
			draggablePart.MouseLeftButtonDown += MenuHeader_MouseDown;
		}
		public void DisableDraggable(FrameworkElement element) {
			element.MouseLeftButtonDown -= MenuHeader_MouseDown;
			element.MouseMove -= MenuHeader_DragMove;
		}

		void MenuHeader_MouseDown(object sender, MouseButtonEventArgs e) {
			var s = e.Source as UIElement;
			s.MouseLeftButtonDown -= MenuHeader_MouseDown;
			s.MouseMove += MenuHeader_DragMove;
		}

		void MenuHeader_DragMove(object sender, MouseEventArgs e) {
			var s = e.Source as FrameworkElement;
			var p = (e.OriginalSource as DependencyObject).GetParentOrSelf<FrameworkElement>();
			if (e.LeftButton == MouseButtonState.Pressed) {
				if (_isDragging == false && p.CaptureMouse()) {
					_isDragging = true;
					s.Cursor = Cursors.SizeAll;
					_beginDragPosition = e.GetPosition(s);
				}
				else if (_isDragging) {
					var cp = e.GetPosition(this);
					cp.X -= _beginDragPosition.X;
					cp.Y -= _beginDragPosition.Y;
					ConstrainChildWindow(s, cp);
				}
			}
			else {
				if (_isDragging) {
					_isDragging = false;
					s.Cursor = null;
					s.MouseMove -= MenuHeader_DragMove;
					p.ReleaseMouseCapture();
					s.MouseLeftButtonDown += MenuHeader_MouseDown;
				}
			}
		}
		#endregion

		void VisualElement_Loaded(object sender, RoutedEventArgs e) {
			_View.VisualElement.Loaded -= VisualElement_Loaded;
			_View.Selection.SelectionChanged += ViewSeletionChanged;
			_View.VisualElement.GetParent<Grid>().Children.Add(this);
		}

		void View_Closed(object sender, EventArgs e) {
			if (_View != null) {
				_View.Closed -= View_Closed;
				_View.Selection.SelectionChanged -= ViewSeletionChanged;
				_View.Properties.RemoveProperty(typeof(ExternalAdornment));
				_View.VisualElement.GetParent<Grid>().Children.Remove(this);
				foreach (var item in _Canvas.Children) {
					if (item is FrameworkElement fe) {
						fe.MouseLeave -= ReleaseQuickInfo;
						fe.MouseEnter -= SuppressQuickInfo;
						fe.MouseLeftButtonDown -= BringToFront;
					}
					if (item is IDisposable d) {
						d.Dispose();
					}
				}
				_Canvas.PreviewMouseRightButtonUp += Canvas_PreviewMouseRightButtonUp;
				_Canvas.Children.Clear();
				_View = null;
			}
		}

		void ViewSeletionChanged(object sender, EventArgs e) {
			ClearUnpinnedChildren();
		}

		void BringToFront(object sender, MouseButtonEventArgs e) {
			Canvas.SetZIndex(e.Source as UIElement, ++_LayerZIndex);
		}

		void ReleaseQuickInfo(object sender, MouseEventArgs e) {
			_View?.Properties.RemoveProperty(QuickInfoSuppressionId);
		}

		void SuppressQuickInfo(object sender, MouseEventArgs e) {
			_View.Properties[QuickInfoSuppressionId] = true;
		}
	}

	public class AdornmentChildRemovedEventArgs
	{
		public readonly UIElement RemovedElement;

		public AdornmentChildRemovedEventArgs(UIElement removed) {
			RemovedElement = removed;
		}
	}
}
