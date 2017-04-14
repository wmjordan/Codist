using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Commentist.Margins
{
	class CodeMarginElement : FrameworkElement
	{
		readonly IWpfTextView _textView;
		readonly IEditorFormatMap _editorFormatMap;
		readonly IVerticalScrollBar _scrollBar;
		readonly ITagAggregator<ClassificationTag> _commentTagAggregator;
		//ToDo: Change brush colors according to user settings
		readonly static Pen MarkerPen = new Pen(Brushes.LightGreen, 1);
		readonly static Brush EmphasisBrush = new SolidColorBrush(Constants.CommentColor);
		readonly static Brush ToDoBrush = new SolidColorBrush(Constants.ToDoColor);
		readonly static Brush NoteBrush = new SolidColorBrush(Constants.NoteColor);
		readonly static Brush HackBrush = new SolidColorBrush(Constants.HackColor);
		readonly static Brush ClassNameBrush = Brushes.Blue;
		readonly static Brush PreProcessorBrush = Brushes.Gray;
		readonly static Dictionary<string, Brush> ClassificationBrushMapper = new Dictionary<string, Brush> {
			{ Constants.EmphasisComment, EmphasisBrush },
			{ Constants.TodoComment, ToDoBrush },
			{ Constants.NoteComment, NoteBrush },
			{ Constants.HackComment, HackBrush },
			{ Constants.ClassName, ClassNameBrush },
			{ Constants.StructName, ClassNameBrush },
			{ Constants.InterfaceName, ClassNameBrush },
			{ Constants.EnumName, ClassNameBrush },
			{ Constants.PreProcessorKeyword, PreProcessorBrush },
		};
		bool _hasEvents;
		bool _optionsChanging;
		bool _isMarginEnabled;
		Brush[] _markers;
		const double MarkPadding = 1.0;
		const double MarkThickness = 4.0;

		public CodeMarginElement(IWpfTextView textView, CodeMarginFactory factory, ITagAggregator<ClassificationTag> tagger, IVerticalScrollBar verticalScrollbar) {
			_textView = textView;

			this.IsHitTestVisible = false;

			_scrollBar = verticalScrollbar;
			_commentTagAggregator = tagger;
			//_commentTagAggregator.TagsChanged += (s, args) => {
			//	System.Diagnostics.Debug.WriteLine(args.Span.Start.GetPoint(_textView.TextBuffer, PositionAffinity.Predecessor).Value.ToString());
			//};
			_editorFormatMap = factory.EditorFormatMapService.GetEditorFormatMap(textView);

			this.Width = 6.0;

			_textView.Options.OptionChanged += this.OnOptionChanged;
			//TODO: subscribe to change events and use them to update the markers
			//_textView.TextBuffer.Changed += (s, args) => {
			//	int d = 0;
			//	int e = 0;
			//	if (_markers == null) {
			//		_markers = new Brush[args.After.LineCount];
			//	}
			//	var m = _markers;
			//	foreach (var change in args.Changes) {
			//		var start = args.Before.GetLineFromPosition(change.OldSpan.Start).LineNumber;
			//		var end = args.Before.GetLineFromPosition(change.OldSpan.End).LineNumber;
			//		for (int i = start; i <= end && i < m.Length; i++) {
			//			m[i] = null;
			//		}
			//		d = end;
			//		var ss = new SnapshotSpan(args.AfterVersion.TextBuffer.CurrentSnapshot, change.NewPosition, change.NewLength);
			//		foreach (var tag in _commentTagAggregator.GetTags(ss)) {
			//			var c = tag.Tag.ClassificationType.Classification;
			//			Brush b;
			//			if (ClassificationBrushMapper.TryGetValue(c, out b) == false) {
			//				continue;
			//			}
			//			var l = args.After.GetLineFromPosition(tag.Span.Start.GetPoint(args.After, PositionAffinity.Predecessor).Value.Position).LineNumber;
			//			if (l >= m.Length) {
			//				Array.Resize(ref m, l + 10);
			//			}
			//			m[l] = b;
			//			e = l;
			//		}
			//	}
			//	if (e - d != 0) {

			//	}
			//	_markers = m;
			//	InvalidateVisual();
			//};
			this.IsVisibleChanged += this.OnViewOrMarginVisiblityChanged;
			_textView.VisualElement.IsVisibleChanged += this.OnViewOrMarginVisiblityChanged;

			this.OnOptionChanged(null, null);
		}

		private void OnViewOrMarginVisiblityChanged(object sender, DependencyPropertyChangedEventArgs e) {
			//There is no need to update event handlers if the visibility change is the result of an options change (since we will
			//update the event handlers after changing all the options).
			//
			//It is possible this will get called twice in quick succession (when the tab containing the host is made visible, the view and the margin
			//will get visibility changed events).
			if (!_optionsChanging) {
				this.UpdateEventHandlers(true);
			}
		}
		private void OnOptionChanged(object sender, EditorOptionChangedEventArgs e) {
			//TODO: track option changing events
			//bool wasMarginEnabled = _isMarginEnabled;
			//_isMarginEnabled = _textView.Options.GetOptionValue(nameof(CommentMargin));

			//try {
			//	_optionsChanging = true;

			//	this.Visibility = this.Enabled ? Visibility.Visible : Visibility.Collapsed;
			//}
			//finally {
			//	_optionsChanging = false;
			//}

			bool refreshed = this.UpdateEventHandlers(true);

			//If the UpdateEventHandlers call above didn't initiate a search then we need to force the margin to update
			//to update if they were turned on/off.
			if (!refreshed) {
				//if (wasMarginEnabled != _isMarginEnabled) {
					this.InvalidateVisual();
				//}
			}
		}

		private void OnFormatMappingChanged(object sender, FormatItemsEventArgs e) {
			//_marginBrush = this.GetBrush(nameof(CommentMargin), EditorFormatDefinition.ForegroundBrushId);
		}

		private Brush GetBrush(string name, string resource) {
			var rd = _editorFormatMap.GetProperties(name);

			if (rd.Contains(resource)) {
				return rd[resource] as Brush;
			}

			return null;
		}

		private bool UpdateEventHandlers(bool checkEvents) {
			bool needEvents = checkEvents && _textView.VisualElement.IsVisible;

			if (needEvents != _hasEvents) {
				_hasEvents = needEvents;
				if (needEvents) {
					_editorFormatMap.FormatMappingChanged += OnFormatMappingChanged;
					//_textView.LayoutChanged += OnLayoutChanged;
					//_textView.Selection.SelectionChanged += OnPositionChanged;
					//_scrollBar.Map.MappingChanged += OnMappingChanged;
					_scrollBar.TrackSpanChanged += OnMappingChanged;
					this.OnFormatMappingChanged(null, null);

					return true;
				}
				else {
					_editorFormatMap.FormatMappingChanged -= OnFormatMappingChanged;
					//_textView.LayoutChanged -= OnLayoutChanged;
					//_textView.Selection.SelectionChanged -= OnPositionChanged;
					//_scrollBar.Map.MappingChanged -= OnMappingChanged;
					_scrollBar.TrackSpanChanged -= OnMappingChanged;
					//if (_search != null) {
					//	_search.Abort();
					//	_search = null;
					//}
					//_highlight = null;
					//_highlightSpan = null;
				}
			}

			return false;
		}

		/// <summary>
		/// Handler for the scrollbar changing its coordinate mapping.
		/// </summary>
		private void OnMappingChanged(object sender, EventArgs e) {
			//Simply invalidate the visual: the positions of the various highlights haven't changed.
			this.InvalidateVisual();
		}
		/// <summary>
		/// Override for the FrameworkElement's OnRender. When called, redraw all markers.
		/// </summary>
		/// <param name="drawingContext">The <see cref="DrawingContext"/> used to render the margin.</param>
		protected override void OnRender(DrawingContext drawingContext) {
			base.OnRender(drawingContext);
			//TODO: update using cached subsets
			if (_textView.IsClosed || _textView.TextSnapshot.LineCount > 1000) {
				return;
			}
			var lastY = double.MinValue;
			//if (_markers != null) {
			//	var m = _markers;
			//	for (int i = 0; i < m.Length; i++) {
			//		if (m[i] == null) {
			//			continue;
			//		}
			//		var y = _scrollBar.GetYCoordinateOfBufferPosition(_textView.TextSnapshot.GetLineFromLineNumber(i).Start);
			//		if (y + MarkThickness < lastY) {
			//			// avoid drawing too many closed markers
			//			continue;
			//		}
			//		lastY = y;
			//		var b = m[i];
			//		if (b == ClassNameBrush || b == PreProcessorBrush) {
			//			DrawCircleMark(drawingContext, b, y);
			//		}
			//		else {
			//			DrawRectangleMark(drawingContext, b, y);
			//		}
			//	}
			//}

			foreach (var line in _textView.TextSnapshot.Lines) {
				foreach (var tag in _commentTagAggregator.GetTags(line.Extent)) {
					var c = tag.Tag.ClassificationType.Classification;
					Brush b;
					if (ClassificationBrushMapper.TryGetValue(c, out b) == false) {
						continue;
					}
					var y = _scrollBar.GetYCoordinateOfBufferPosition(line.Start.TranslateTo(_textView.TextSnapshot, PointTrackingMode.Negative));
					if (y + MarkThickness < lastY) {
						// avoid drawing too many closed markers
						continue;
					}
					lastY = y;
					if (b == ClassNameBrush || b == PreProcessorBrush) {
						DrawCircleMark(drawingContext, b, y);
					}
					else {
						DrawRectangleMark(drawingContext, b, y);
					}
					break;
				}
			}
		}

		void DrawRectangleMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawRectangle(brush, MarkerPen, new Rect(MarkPadding, y - MarkThickness * 0.5, this.Width - MarkPadding * 2.0, MarkThickness));
			//dc.DrawEllipse(brush, null, new Point(Width / 2.0, y + MarkThickness / 2.0), MarkThickness / 2, MarkThickness / 2);
		}
		void DrawCircleMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawEllipse(brush, null, new Point(Width / 2.0, y + MarkThickness / 2.0), MarkThickness, MarkThickness);
		}
	}
}
