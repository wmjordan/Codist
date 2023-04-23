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
		}

		public override string MarginName => nameof(SelectionMargin);
		public override double MarginSize => 0;

		void Setup() {
			_EditorFormatMap.FormatMappingChanged += EditorFormatMap_FormatMappingChanged;
			_TextView.Selection.SelectionChanged += TextView_SelectionChanged;
			_ScrollBar.TrackSpanChanged += ScrollBar_TrackSpanChanged;
			_SelectionBrush = GetMarginBrush();
		}

		void UpdateSelectionMarginConfig(ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.MatchFlags(Features.ScrollbarMarkers) == false) {
				return;
			}
			var setVisible = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.Selection);
			var visible = Visibility == Visibility.Visible;
			_TextView.Selection.SelectionChanged -= TextView_SelectionChanged;
			_EditorFormatMap.FormatMappingChanged -= EditorFormatMap_FormatMappingChanged;
			_ScrollBar.TrackSpanChanged -= ScrollBar_TrackSpanChanged;
			if (setVisible == false && visible) {
				Visibility = Visibility.Collapsed;
			}
			else if (setVisible && visible == false) {
				Visibility = Visibility.Visible;
				Setup();
			}
			InvalidateVisual();
		}

		void EditorFormatMap_FormatMappingChanged(object sender, FormatItemsEventArgs e) {
			foreach (var item in e.ChangedItems) {
				if (item == FormatName) {
					_SelectionBrush = GetMarginBrush();
					InvalidateVisual();
					return;
				}
			}
		}

		Brush GetMarginBrush() {
			return (_EditorFormatMap.GetProperties(FormatName).Get<Brush>(EditorFormatDefinition.BackgroundBrushId) ?? ThemeHelper.FileTabProvisionalSelectionBrush).Alpha(WpfHelper.DimmedOpacity);
		}

		void TextView_SelectionChanged(object sender, EventArgs args) {
			InvalidateVisual();
		}

		/// <summary>
		/// Handler for the scrollbar changing its coordinate mapping.
		/// </summary>
		void ScrollBar_TrackSpanChanged(object sender, EventArgs e) {
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

		#region IDisposable Support
		void UnbindEvents() {
			Config.UnregisterUpdateHandler(UpdateSelectionMarginConfig);
			_TextView.Selection.SelectionChanged -= TextView_SelectionChanged;
			_EditorFormatMap.FormatMappingChanged -= EditorFormatMap_FormatMappingChanged;
			_ScrollBar.TrackSpanChanged -= ScrollBar_TrackSpanChanged;
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
