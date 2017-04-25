using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Codist.Classifiers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Margins
{
	class CodeMarginElement : FrameworkElement
	{
		readonly IWpfTextView _textView;
		readonly IEditorFormatMap _editorFormatMap;
		readonly IVerticalScrollBar _scrollBar;
		readonly TaggerResult _tags;
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
		const double MarkPadding = 1.0;
		const double MarkThickness = 4.0;

		public CodeMarginElement(IWpfTextView textView, CodeMarginFactory factory, ITagAggregator<ClassificationTag> tagger, IVerticalScrollBar verticalScrollbar) {
			_textView = textView;

			IsHitTestVisible = false;

			_scrollBar = verticalScrollbar;
			_tags = textView.Properties.GetOrCreateSingletonProperty(() => new TaggerResult());
			//_tagger = textView.Properties.GetProperty<ITagAggregator<ClassificationTag>>("_Tagger");
			_editorFormatMap = factory.EditorFormatMapService.GetEditorFormatMap(textView);

			Width = 6.0;

			_textView.Options.OptionChanged += OnOptionChanged;
			//subscribe to change events and use them to update the markers
			_textView.TextBuffer.Changed += (s, args) => {
				foreach (var change in args.Changes) {
					//TODO: shift positions of remained items
					for (int i = _tags.Tags.Count - 1; i >= 0; i--) {
						var t = _tags.Tags[i];
						if (!(t.Start > change.OldEnd || t.End < change.OldPosition))
							_tags.Tags.RemoveAt(i);
						else if (t.Start > change.OldEnd) {
							t.Start += change.Delta;
						}
					}
				}
				_tags.LastParsed = args.Changes[0].OldPosition;
				InvalidateVisual();
			};
			IsVisibleChanged += OnViewOrMarginVisiblityChanged;
			_textView.VisualElement.IsVisibleChanged += OnViewOrMarginVisiblityChanged;

			OnOptionChanged(null, null);
		}

		private void OnViewOrMarginVisiblityChanged(object sender, DependencyPropertyChangedEventArgs e) {
			//There is no need to update event handlers if the visibility change is the result of an options change (since we will
			//update the event handlers after changing all the options).
			//
			//It is possible this will get called twice in quick succession (when the tab containing the host is made visible, the view and the margin
			//will get visibility changed events).
			if (!_optionsChanging) {
				UpdateEventHandlers(true);
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

			bool refreshed = UpdateEventHandlers(true);

			//If the UpdateEventHandlers call above didn't initiate a search then we need to force the margin to update
			//to update if they were turned on/off.
			if (!refreshed) {
				//if (wasMarginEnabled != _isMarginEnabled) {
				InvalidateVisual();
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
					OnFormatMappingChanged(null, null);

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
			InvalidateVisual();
		}
		/// <summary>
		/// Override for the FrameworkElement's OnRender. When called, redraw all markers.
		/// </summary>
		/// <param name="drawingContext">The <see cref="DrawingContext"/> used to render the margin.</param>
		protected override void OnRender(DrawingContext drawingContext) {
			base.OnRender(drawingContext);
			if (_textView.IsClosed) {
				return;
			}
			var lastY = double.MinValue;
			var tags = new List<SpanTag>(_tags.Tags);
			_tags.Tags.Sort((x, y) => { return x.Start - y.Start; });
			foreach (var tag in tags) {
				var c = tag.Tag.ClassificationType.Classification;
				Brush b;
				if (ClassificationBrushMapper.TryGetValue(c, out b) == false) {
					continue;
				}
				var y = _scrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(_textView.TextSnapshot, tag.Start));
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
				//break;
			}
		}

		void DrawRectangleMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawRectangle(brush, MarkerPen, new Rect(MarkPadding, y - MarkThickness * 0.5, Width - MarkPadding * 2.0, MarkThickness));
			//dc.DrawEllipse(brush, null, new Point(Width / 2.0, y + MarkThickness / 2.0), MarkThickness / 2, MarkThickness / 2);
		}
		void DrawCircleMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawEllipse(brush, null, new Point(Width / 2.0, y + MarkThickness / 2.0), MarkThickness, MarkThickness);
		}
	}
}
