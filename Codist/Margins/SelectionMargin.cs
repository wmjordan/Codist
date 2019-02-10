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
	sealed class SelectionMargin : FrameworkElement, IDisposable, IWpfTextViewMargin
	{
		public const string MarginName = nameof(SelectionMargin);
		const string FormatName = "Selected Text";
		const double SelectionRenderPadding = -3;
		const double MarginOpacity = 0.3;
		readonly IWpfTextView _TextView;
		readonly IEditorFormatMap _EditorFormatMap;
		readonly IVerticalScrollBar _ScrollBar;

		Brush _SelectionBrush;
		double _ScrollbarWidth;

		public SelectionMargin(IWpfTextView textView, IVerticalScrollBar scrollBar) {
			_TextView = textView;

			IsHitTestVisible = false;

			_ScrollBar = scrollBar;
			_EditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(textView);

			Width = 0;

			var showSelection = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.Selection);
			Visibility = showSelection ? Visibility.Visible : Visibility.Collapsed;
			Config.Updated += Config_Updated;
			if (showSelection) {
				Setup();
			}
			_TextView.Closed += (s, args) => Dispose();
		}

		public FrameworkElement VisualElement => this;
		public double MarginSize => ActualWidth;
		public bool Enabled => true;

		public ITextViewMargin GetTextViewMargin(string marginName) {
			return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		void Setup() {
			_EditorFormatMap.FormatMappingChanged += _EditorFormatMap_FormatMappingChanged;
			_TextView.Selection.SelectionChanged += TextView_SelectionChanged;
			_ScrollBar.TrackSpanChanged += OnMappingChanged;
			_SelectionBrush = GetMarginBrush();
		}

		void Config_Updated(object sender, ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.MatchFlags(Features.ScrollbarMarkers) == false) {
				return;
			}
			var setVisible = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.Selection);
			var visible = Visibility == Visibility.Visible;
			if (setVisible == false && visible) {
				Visibility = Visibility.Collapsed;
				_TextView.Selection.SelectionChanged -= TextView_SelectionChanged;
				_EditorFormatMap.FormatMappingChanged -= _EditorFormatMap_FormatMappingChanged;
				_ScrollBar.TrackSpanChanged -= OnMappingChanged;
				InvalidateVisual();
			}
			else if (setVisible && visible == false) {
				Visibility = Visibility.Visible;
				Setup();
				InvalidateVisual();
			}
		}

		void _EditorFormatMap_FormatMappingChanged(object sender, FormatItemsEventArgs e) {
			foreach (var item in e.ChangedItems) {
				if (item == FormatName) {
					_SelectionBrush = GetMarginBrush();
					InvalidateVisual();
					return;
				}
			}
		}

		Brush GetMarginBrush() {
			return (_EditorFormatMap.GetProperties(FormatName).Get<Brush>(EditorFormatDefinition.BackgroundBrushId) ?? ThemeHelper.FileTabProvisionalSelectionBrush).Alpha(MarginOpacity);
		}

		void TextView_SelectionChanged(object sender, EventArgs args) {
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
			if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.Selection)) {
				DrawSelections(drawingContext);
			}
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
			base.OnRenderSizeChanged(sizeInfo);
			var b = _ScrollBar as FrameworkElement;
			_ScrollbarWidth = b.ActualWidth + SelectionRenderPadding;
			InvalidateVisual();
		}

		void DrawSelections(DrawingContext drawingContext) {
			foreach (var item in _TextView.Selection.SelectedSpans) {
				var top = _ScrollBar.GetYCoordinateOfBufferPosition(item.Start);
				var height = _ScrollBar.GetYCoordinateOfBufferPosition(item.End) - top;
				if (height > 3) {
					drawingContext.DrawRectangle(_SelectionBrush, null, new Rect(-100, top, 200, height));
				}
			}
		}

		#region IDisposable Support
		bool disposedValue = false;

		void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					//_TextView.VisualElement.IsVisibleChanged -= OnViewOrMarginVisiblityChanged;
					Config.Updated -= Config_Updated;
					_TextView.Selection.SelectionChanged -= TextView_SelectionChanged;
					_EditorFormatMap.FormatMappingChanged -= _EditorFormatMap_FormatMappingChanged;
					_ScrollBar.TrackSpanChanged -= OnMappingChanged;
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
