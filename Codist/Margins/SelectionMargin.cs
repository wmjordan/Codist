using System;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Margins
{
	sealed class SelectionMargin : MarginElementBase, IDisposable, IWpfTextViewMargin
	{
		const string FormatName = "Selected Text";
		const double MarginOpacity = 0.3;

		IWpfTextView _TextView;
		IEditorFormatMap _EditorFormatMap;
		IVerticalScrollBar _ScrollBar;
		Brush _SelectionBrush;

		public SelectionMargin(IWpfTextView textView, IVerticalScrollBar scrollBar) {
			_TextView = textView;
			_ScrollBar = scrollBar;
			_EditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(textView);

			Config.RegisterUpdateHandler(UpdateSelectionMarginConfig);
			if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.Selection)) {
				Visibility = Visibility.Visible;
				Setup();
			}
			else {
				Visibility = Visibility.Collapsed;
			}
			_TextView.Closed += _TextView_Closed;
		}

		public override string MarginName => nameof(SelectionMargin);
		public override double MarginSize => 0;

		void Setup() {
			_EditorFormatMap.FormatMappingChanged += _EditorFormatMap_FormatMappingChanged;
			_TextView.Selection.SelectionChanged += TextView_SelectionChanged;
			_ScrollBar.TrackSpanChanged += OnMappingChanged;
			_SelectionBrush = GetMarginBrush();
		}

		void UpdateSelectionMarginConfig(ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.MatchFlags(Features.ScrollbarMarkers) == false) {
				return;
			}
			var setVisible = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.Selection);
			var visible = Visibility == Visibility.Visible;
			_TextView.Selection.SelectionChanged -= TextView_SelectionChanged;
			_EditorFormatMap.FormatMappingChanged -= _EditorFormatMap_FormatMappingChanged;
			_ScrollBar.TrackSpanChanged -= OnMappingChanged;
			if (setVisible == false && visible) {
				Visibility = Visibility.Collapsed;
			}
			else if (setVisible && visible == false) {
				Visibility = Visibility.Visible;
				Setup();
			}
			InvalidateVisual();
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
			if (_TextView?.IsClosed != false) {
				return;
			}
			if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.Selection)) {
				DrawSelections(drawingContext);
			}
		}

		void DrawSelections(DrawingContext drawingContext) {
			var ss = _TextView.Selection.SelectedSpans;
			var threshold = ss.Count < 2 ? 3 : 0.5;
			foreach (var item in ss) {
				var top = _ScrollBar.GetYCoordinateOfBufferPosition(item.Start);
				var height = _ScrollBar.GetYCoordinateOfBufferPosition(item.End) - top;
				if (height > threshold) {
					drawingContext.DrawRectangle(_SelectionBrush, null, new Rect(-100, top, 200, height));
				}
			}
		}

		void _TextView_Closed(object sender, EventArgs e) {
			Dispose();
		}

		#region IDisposable Support
		void UnbindEvents() {
			_TextView.Closed -= _TextView_Closed;
			Config.UnregisterUpdateHandler(UpdateSelectionMarginConfig);
			_TextView.Selection.SelectionChanged -= TextView_SelectionChanged;
			_EditorFormatMap.FormatMappingChanged -= _EditorFormatMap_FormatMappingChanged;
			_ScrollBar.TrackSpanChanged -= OnMappingChanged;
		}

		public override void Dispose() {
			if (_TextView != null) {
				UnbindEvents();
				_TextView = null;
				_EditorFormatMap = null;
				_ScrollBar = null;
				_SelectionBrush = null;
			}
		}
		#endregion
	}
}
