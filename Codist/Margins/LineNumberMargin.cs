using System;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Codist.Classifiers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Margins
{
	sealed class LineNumberMargin : FrameworkElement, IDisposable, IWpfTextViewMargin
	{
		public const string MarginName = nameof(LineNumberMargin);
		const double LineNumberRenderPadding = -3;
		readonly IWpfTextView _TextView;
		readonly IEditorFormatMap _EditorFormatMap;
		readonly IVerticalScrollBar _ScrollBar;
		readonly TaggerResult _Tags;

		static readonly SolidColorBrush LineNumberBrush = Brushes.DarkGray;
		static readonly Pen LineNumberPen = new Pen(LineNumberBrush, 1) { DashStyle = DashStyles.Dash };

		double _ScrollbarWidth;

		public LineNumberMargin(IWpfTextView textView, IVerticalScrollBar scrollBar) {
			_TextView = textView;

			IsHitTestVisible = false;

			_ScrollBar = scrollBar;
			_Tags = textView.Properties.GetOrCreateSingletonProperty(() => new TaggerResult());
			_EditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(textView);

			Width = 0;

			Visibility = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.LineNumber) ? Visibility.Visible : Visibility.Collapsed;
			Config.Updated += Config_Updated;
			_TextView.TextBuffer.Changed += TextView_TextBufferChanged;
			_ScrollBar.TrackSpanChanged += OnMappingChanged;
			_TextView.Closed += (s, args) => Dispose();
		}

		public FrameworkElement VisualElement => this;
		public double MarginSize => ActualWidth;
		public bool Enabled => true;

		public ITextViewMargin GetTextViewMargin(string marginName) {
			return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		void Config_Updated(object sender, ConfigUpdatedEventArgs e) {
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
				_TextView.TextBuffer.Changed += TextView_TextBufferChanged;
				_ScrollBar.TrackSpanChanged += OnMappingChanged;
				InvalidateVisual();
			}
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
			if (_TextView.IsClosed) {
				return;
			}
			if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.LineNumber)) {
				DrawLineNumbers(drawingContext);
			}
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
			base.OnRenderSizeChanged(sizeInfo);
			_ScrollbarWidth = (_ScrollBar as FrameworkElement).ActualWidth + LineNumberRenderPadding;
		}

		void DrawLineNumbers(DrawingContext drawingContext) {
			var snapshot = _TextView.TextSnapshot;
			var lc = snapshot.LineCount;
			var step = lc < 500 ? 50 : lc < 2000 ? 100 : lc < 3000 ? 200 : lc < 5000 ? 500 : lc < 20000 ? 1000 : lc < 100000 ? 5000 : 10000;
			for (int i = step; i < lc; i += step) {
				var y = _ScrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(snapshot, snapshot.GetLineFromLineNumber(i - 1).Start));
				drawingContext.DrawLine(LineNumberPen, new Point(-100, y), new Point(100, y));
				var t = WpfHelper.ToFormattedText(i.ToString(), 9, LineNumberBrush);
				drawingContext.DrawText(t, new Point(_ScrollbarWidth - t.Width, y));
			}
		}

		#region IDisposable Support
		bool disposedValue = false;

		void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					//_TextView.VisualElement.IsVisibleChanged -= OnViewOrMarginVisiblityChanged;
					Config.Updated -= Config_Updated;
					_TextView.TextBuffer.Changed -= TextView_TextBufferChanged;
					_ScrollBar.TrackSpanChanged += OnMappingChanged;
				}

				disposedValue = true;
			}
		}

		public void Dispose() {
			Dispose(true);
		}
		#endregion
	}
}
