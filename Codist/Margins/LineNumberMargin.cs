using System;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Margins
{
	sealed class LineNumberMargin : MarginElementBase, IDisposable, IWpfTextViewMargin
	{
		static readonly SolidColorBrush __LineNumberBrush = Brushes.DarkGray;
		static readonly Pen __LineNumberPen = new Pen(__LineNumberBrush, 1) { DashStyle = DashStyles.Dash };
		const double LineNumberRenderPadding = -3;

		IWpfTextView _TextView;
		IEditorFormatMap _EditorFormatMap;
		IVerticalScrollBar _ScrollBar;
		double _ScrollbarWidth;
		bool _Disposed;

		public LineNumberMargin(IWpfTextView textView, IVerticalScrollBar scrollBar) {
			_TextView = textView;
			_ScrollBar = scrollBar;
			_EditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(textView);

			Visibility = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.LineNumber) ? Visibility.Visible : Visibility.Collapsed;

			Config.RegisterUpdateHandler(UpdateLineNumberMarginConfig);
			Setup();
		}

		public override string MarginName => nameof(LineNumberMargin);
		public override double MarginSize => 0;

		void UpdateLineNumberMarginConfig(ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.MatchFlags(Features.ScrollbarMarkers) == false) {
				return;
			}
			var setVisible = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.LineNumber);
			var visible = Visibility == Visibility.Visible;
			if (setVisible == false && visible) {
				Visibility = Visibility.Collapsed;
				_TextView.TextBuffer.Changed -= TextView_TextBufferChanged;
				_ScrollBar.TrackSpanChanged -= OnMappingChanged;
				InvalidateVisual();
			}
			else if (setVisible && visible == false) {
				Visibility = Visibility.Visible;
				Setup();
				InvalidateVisual();
			}
		}

		void Setup() {
			_TextView.TextBuffer.Changed += TextView_TextBufferChanged;
			_ScrollBar.TrackSpanChanged += OnMappingChanged;
		}

		void TextView_TextBufferChanged(object sender, TextContentChangedEventArgs args) {
			if (args.Changes.Count == 0) {
				return;
			}
			InvalidateVisual();
		}

		/// <summary>
		/// Handler for the scrollbar changing its coordinate mapping.
		/// </summary>
		void OnMappingChanged(object sender, EventArgs e) {
			InvalidateVisual();
		}
		/// <summary>
		/// Override for the FrameworkElement's OnRender. When called, redraw all markers.
		/// </summary>
		/// <param name="drawingContext">The <see cref="DrawingContext"/> used to render the margin.</param>
		protected override void OnRender(DrawingContext drawingContext) {
			base.OnRender(drawingContext);
			if (_TextView?.IsClosed != false) {
				return;
			}
			if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.LineNumber)) {
				DrawLineNumbers(drawingContext);
			}
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
			base.OnRenderSizeChanged(sizeInfo);
			var b = _ScrollBar as FrameworkElement;
			_ScrollbarWidth = b.ActualWidth + LineNumberRenderPadding;
			InvalidateVisual();
		}

		void DrawLineNumbers(DrawingContext drawingContext) {
			var snapshot = _TextView.TextSnapshot;
			var lc = snapshot.LineCount;
			var step = lc < 500 ? 50 : lc < 2000 ? 100 : lc < 3000 ? 200 : lc < 5000 ? 500 : lc < 20000 ? 1000 : lc < 100000 ? 5000 : 10000;
			int i;
			double prevY = 0, y, maxY;

			#region Draw line count at bottom
			var tt = WpfHelper.ToFormattedText(lc.ToText(), 9, __LineNumberBrush);
			y = _ScrollBar.TrackSpanBottom - tt.Height;
			DrawTextAdjusted(drawingContext, tt, y);
			maxY = y - tt.Height;
			#endregion

			#region Draw intermediate line numbers
			for (i = step; i < lc; i += step) {
				y = _ScrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(snapshot, snapshot.GetLineFromLineNumber(i - 1).Start));
				if (y > maxY) {
					break;
				}
				if (y - prevY < 50) {
					continue;
				}
				prevY = y;
				drawingContext.DrawLine(__LineNumberPen, new Point(-100, y), new Point(100, y));
				var t = WpfHelper.ToFormattedText(i.ToText(), 9, __LineNumberBrush);
				DrawTextAdjusted(drawingContext, t, y);
			}
			#endregion
		}

		void DrawTextAdjusted(DrawingContext drawingContext, FormattedText t, double y) {
			var x = _ScrollbarWidth - t.Width;
			if (x < 0) {
				drawingContext.PushTransform(new ScaleTransform(_ScrollbarWidth / t.Width, 1));
				drawingContext.DrawText(t, new Point(0, y));
				drawingContext.Pop();
			}
			else {
				drawingContext.DrawText(t, new Point(x, y));
			}
		}

		#region IDisposable Support
		void Dispose(bool disposing) {
			if (!_Disposed) {
				if (disposing) {
					Config.UnregisterUpdateHandler(UpdateLineNumberMarginConfig);
					_TextView.TextBuffer.Changed -= TextView_TextBufferChanged;
					_ScrollBar.TrackSpanChanged -= OnMappingChanged;
					_TextView = null;
					_ScrollBar = null;
					_EditorFormatMap = null;
				}

				_Disposed = true;
			}
		}

		public override void Dispose() {
			Dispose(true);
		}
		#endregion
	}
}
