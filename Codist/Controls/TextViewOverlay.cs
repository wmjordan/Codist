using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Controls
{
	// HACK: put the symbol list, smart bar, etc. on top of the WpfTextView.
	// Don't use AdornmentLayer to do so, otherwise contained objects will go up and down when scrolling code window.
	// Another side effect of AdornmentLayer is that it scales images automatically and makes CrispImages blurry (github: #213).
	sealed class TextViewOverlay : ContentPresenter
	{
		internal const string QuickInfoSuppressionId = nameof(TextViewOverlay);
		const string SyntaxNodeRange = nameof(SyntaxNodeRange);

		IWpfTextView _View;
		Canvas _Canvas;
		int _LayerZIndex;
		bool _IsDragging;
		Point _BeginDragPosition;
		IAdornmentLayer _TextRangeAdornment;

		public TextViewOverlay(IWpfTextView view) {
			UseLayoutRounding = true;
			SnapsToDevicePixels = true;
			// put the control on top of the editor window and share the same size
			Grid.SetColumn(this, 1);
			Grid.SetRow(this, 1);
			Grid.SetIsSharedSizeScope(this, true);
			var grid = view.VisualElement.GetParent<Grid>();
			_TextRangeAdornment = view.GetAdornmentLayer(SyntaxNodeRange);
			if (grid != null) {
				grid.Children.Add(this);
				view.Selection.SelectionChanged += ViewSelectionChanged;
			}
			else {
				view.VisualElement.Loaded += VisualElement_Loaded;
			}
			view.Closed += View_Closed;
			Content = _Canvas = new Canvas();
			_Canvas.PreviewMouseRightButtonUp += Canvas_PreviewMouseRightButtonUp;
			_View = view;
		}

		// if nothing on the overlay, it is sized (0,0)
		public double DisplayHeight => ActualHeight > 0 ? ActualHeight : this.GetParent<FrameworkElement>().ActualHeight;

		public static TextViewOverlay GetOrCreate(IWpfTextView view) {
			return view.Properties.GetOrCreateSingletonProperty(() => new TextViewOverlay(view));
		}
		public static TextViewOverlay Get(IWpfTextView view) {
			return view.Properties.TryGetProperty(typeof(TextViewOverlay), out TextViewOverlay a) ? a : null;
		}

		public event EventHandler<OverlayElementRemovedEventArgs> ChildRemoved;

		public void FocusOnTextView() {
			_View.VisualElement.Focus();
		}

		public bool AddRangeAdornment(SnapshotSpan span) {
			return AddRangeAdornment(span, ThemeHelper.MenuHoverBackgroundColor, 1);
		}
		public bool AddRangeAdornment(SnapshotSpan span, Color color, double thickness) {
			return _TextRangeAdornment.AddAdornment(span, null, new GeometryAdornment(color, _View.TextViewLines.GetMarkerGeometry(span), thickness));
		}
		public bool SetRangeAdornment(SnapshotSpan span) {
			ClearRangeAdornments();
			return AddRangeAdornment(span);
		}

		public void ClearRangeAdornments() {
			_TextRangeAdornment.RemoveAllAdornments();
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
			int c;
			if (element is null || (c = _Canvas.Children.Count) == 0) {
				return;
			}
			_Canvas.Children.Remove(element);
			if (_Canvas.Children.Count != c) {
				UnhookChild(element);
				AfterChildRemoved();
			}
		}

		void UnhookChild(UIElement element) {
			element.MouseLeave -= ReleaseQuickInfo;
			element.MouseEnter -= SuppressQuickInfo;
			element.MouseLeftButtonDown -= BringToFront;
			if (_View.IsClosed == false) {
				ChildRemoved?.Invoke(this, new OverlayElementRemovedEventArgs(element));
			}
		}

		void AfterChildRemoved() {
			_View.Properties.RemoveProperty(QuickInfoSuppressionId);
			if (_View.IsClosed == false) {
				var children = _Canvas.Children;
				for (int i = children.Count - 1; i >= 0; i--) {
					if (children[i].GetFirstVisualChild<TextBox>()?.Focus() == true) {
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
				return;
			}
			if ((args.Source as FrameworkElement).GetParent<Button>() == null) {
				args.Handled = true;
			}
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
			base.OnRenderSizeChanged(sizeInfo);
			foreach (var item in _Canvas.Children) {
				if (item is FrameworkElement child) {
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
			//if (newPos.Y + child.ActualHeight < minVisibleSize) {
			//	newPos.Y = minVisibleSize - child.ActualHeight;
			//}
			if (newPos.Y > ActualHeight - minVisibleSize) {
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
				if (_IsDragging == false && p.CaptureMouse()) {
					_IsDragging = true;
					s.Cursor = Cursors.SizeAll;
					_BeginDragPosition = e.GetPosition(s);
				}
				else if (_IsDragging) {
					var cp = e.GetPosition(this);
					cp.X -= _BeginDragPosition.X;
					cp.Y -= _BeginDragPosition.Y;
					ConstrainChildWindow(s, cp);
				}
			}
			else {
				if (_IsDragging) {
					_IsDragging = false;
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
			var container = _View.VisualElement.GetParent<Grid>();
			if (container != null) {
				_View.Selection.SelectionChanged += ViewSelectionChanged;
				container.Children.Add(this);
			}
		}

		void View_Closed(object sender, EventArgs e) {
			if (_View != null) {
				_View.Closed -= View_Closed;
				_View.Selection.SelectionChanged -= ViewSelectionChanged;
				_View.Properties.RemoveProperty(typeof(TextViewOverlay));
				_View.VisualElement.GetParent<Grid>()?.Children.Remove(this);
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
				_Canvas.PreviewMouseRightButtonUp -= Canvas_PreviewMouseRightButtonUp;
				_Canvas.Children.Clear();
				_Canvas = null;
				_TextRangeAdornment.RemoveAllAdornments();
				_TextRangeAdornment = null;
				_View = null;
			}
		}

		void ViewSelectionChanged(object sender, EventArgs e) {
			ClearUnpinnedChildren();
		}

		void BringToFront(object sender, MouseButtonEventArgs e) {
			Panel.SetZIndex(e.Source as UIElement, ++_LayerZIndex);
		}

		void ReleaseQuickInfo(object sender, MouseEventArgs e) {
			_View?.Properties.RemoveProperty(QuickInfoSuppressionId);
		}

		void SuppressQuickInfo(object sender, MouseEventArgs e) {
			_View.Properties[QuickInfoSuppressionId] = true;
		}

		sealed class GeometryAdornment : UIElement
		{
			readonly DrawingVisual _Child;

			public GeometryAdornment(Color color, Geometry geometry, double thickness) {
				_Child = new DrawingVisual();
				using (var context = _Child.RenderOpen()) {
					context.DrawGeometry(new SolidColorBrush(color.Alpha(25)),
						thickness < 0.1 ? null : new Pen(ThemeHelper.MenuHoverBorderBrush, thickness),
						geometry);
				}
				AddVisualChild(_Child);
			}

			protected override int VisualChildrenCount => 1;

			protected override Visual GetVisualChild(int index) {
				return _Child;
			}
		}

		sealed class OverlayDefinition
		{
			/// <summary>
			/// Defines the adornment layer for syntax node range highlight.
			/// </summary>
			[Export(typeof(AdornmentLayerDefinition))]
			[Name(nameof(SyntaxNodeRange))]
			[Order(After = PredefinedAdornmentLayers.CurrentLineHighlighter)]
			AdornmentLayerDefinition _SyntaxNodeRangeAdornmentLayer;
		}
	}
}
