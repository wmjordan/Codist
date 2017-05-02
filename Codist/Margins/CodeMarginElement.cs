using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		readonly static Brush KeywordBrush = Brushes.Blue;
		readonly static Brush PreProcessorBrush = Brushes.Gray;
		//readonly static Brush ThrowKeywordBrush = Brushes.Red;
		//readonly static Brush ReturnKeywordBrush = Brushes.Blue;
		readonly static Dictionary<string, Brush> ClassificationBrushMapper = new Dictionary<string, Brush> {
			{ Constants.EmphasisComment, EmphasisBrush },
			{ Constants.TodoComment, ToDoBrush },
			{ Constants.NoteComment, NoteBrush },
			{ Constants.HackComment, HackBrush },
			{ Constants.ClassName, KeywordBrush },
			{ Constants.StructName, KeywordBrush },
			{ Constants.InterfaceName, KeywordBrush },
			{ Constants.EnumName, KeywordBrush },
			{ Constants.Keyword, KeywordBrush },
			{ Constants.PreProcessorKeyword, PreProcessorBrush },
			//{ Constants.ThrowKeyword, ThrowKeywordBrush },
			//{ Constants.ReturnKeyword, ReturnKeywordBrush },
		};
		bool _hasEvents;
		bool _optionsChanging;
		bool _isMarginEnabled;
		const double MarkPadding = 1.0;
		const double MarkSize = 4.0;
		const double HalfMarkSize = MarkSize / 2;

		public CodeMarginElement(IWpfTextView textView, CodeMarginFactory factory, ITagAggregator<ClassificationTag> tagger, IVerticalScrollBar verticalScrollbar) {
			_textView = textView;

			IsHitTestVisible = false;

			_scrollBar = verticalScrollbar;
			_tags = textView.Properties.GetOrCreateSingletonProperty(() => new TaggerResult());
			_editorFormatMap = factory.EditorFormatMapService.GetEditorFormatMap(textView);

			Width = 6.0;

			_textView.Options.OptionChanged += OnOptionChanged;
			//subscribe to change events and use them to update the markers
			_textView.TextBuffer.Changed += (s, args) => {
				if (args.Changes.Count == 0) {
					return;
				}
				Debug.WriteLine($"snapshot version: {args.AfterVersion.VersionNumber}");
				var tags = _tags.Tags;
				foreach (var change in args.Changes) {
					Debug.WriteLine($"change:{change.OldPosition}->{change.NewPosition}");
					for (int i = tags.Count - 1; i >= 0; i--) {
						var t = tags[i];
						if (!(t.Start > change.OldEnd || t.End < change.OldPosition)) {
							// remove tags within the updated range
							Debug.WriteLine($"Removed [{t.Start}..{t.End}) {t.Tag.ClassificationType}");
							tags.RemoveAt(i);
						}
						else if (t.Start > change.OldEnd) {
							// shift positions of remained items
							t.Start += change.Delta;
						}
					}
				}
				try {
					_tags.Version = args.AfterVersion.VersionNumber;
					_tags.LastParsed = args.Before.GetLineFromPosition(args.Changes[0].OldPosition).Start.Position;
				}
				catch (ArgumentOutOfRangeException) {
					MessageBox.Show(String.Join("\n",
						"Code margin exception:", args.Changes[0].OldPosition,
						"Before length:", args.Before.Length,
						"After length:", args.After.Length
					));
				}
				InvalidateVisual();
			};
			_tags.Tagger.Margin = this;
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
			tags.Sort((x, y) => { return x.Start - y.Start; });
			foreach (var tag in tags) {
				var c = tag.Tag.ClassificationType.Classification;
				Brush b;
				if (ClassificationBrushMapper.TryGetValue(c, out b) == false) {
					continue;
				}
				var y = _scrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(_textView.TextSnapshot, tag.Start));
				if (y + MarkSize < lastY) {
					// avoid drawing too many closed markers
					continue;
				}
				lastY = y;
				if (b == KeywordBrush || b == PreProcessorBrush) {
					DrawDeclarationMark(drawingContext, b, y);
				}
				//else if (b == ThrowKeywordBrush) {
				//	DrawKeywordMark(drawingContext, b, y);
				//}
				else {
					DrawCommentMark(drawingContext, b, y);
				}
			}
		}

		void DrawCommentMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawRectangle(brush, MarkerPen, new Rect(MarkPadding, y - HalfMarkSize, Width - MarkPadding - MarkPadding, MarkSize));
		}
		void DrawDeclarationMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawEllipse(brush, null, new Point(Width * 0.5, y - HalfMarkSize), MarkSize, MarkSize);
		}
		void DrawKeywordMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawEllipse(brush, null, new Point(Width * 0.5, y - HalfMarkSize * 0.5), HalfMarkSize, HalfMarkSize);
		}
	}
}
