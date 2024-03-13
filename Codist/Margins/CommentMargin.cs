using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using CLR;
using Codist.Taggers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Margins
{
	sealed class CommentMargin : MarginElementBase, IWpfTextViewMargin
	{
		//ToDo: Configurable marker styles
		//ToDo: Change brush colors according to user settings
		static readonly Pen __CommentPen = new Pen(Brushes.LightGreen, 1);
		static readonly SolidColorBrush __LineNumberBrush = Brushes.DarkGray;
		static readonly Pen __LineNumberPen = new Pen(__LineNumberBrush, 1) { DashStyle = DashStyles.Dash };
		static readonly Pen __EmptyPen = new Pen();
		static readonly SolidColorBrush __EmphasisBrush = new SolidColorBrush(Constants.CommentColor);
		static readonly SolidColorBrush __ToDoBrush = new SolidColorBrush(Constants.ToDoColor);
		static readonly SolidColorBrush __NoteBrush = new SolidColorBrush(Constants.NoteColor);
		static readonly SolidColorBrush __HackBrush = new SolidColorBrush(Constants.HackColor);
		static readonly SolidColorBrush __UndoneBrush = new SolidColorBrush(Constants.UndoneColor);
		static readonly SolidColorBrush __TaskBrush = new SolidColorBrush(Constants.TaskColor);
		static readonly SolidColorBrush __PreProcessorBrush = Brushes.Gray;
		static readonly SolidColorBrush __TaskBackgroundBrush = Brushes.White.Alpha(0.5);
		//note: this dictionary determines which style has a scrollbar marker
		static readonly Dictionary<IClassificationType, Brush> __ClassificationBrushMapper = InitClassificationBrushMapper();

		const double MarkPadding = 1.0;
		const double MarkSize = 4.0;
		const double HalfMarkSize = MarkSize / 2 + MarkPadding;

		IWpfTextView _TextView;
		IEditorFormatMap _EditorFormatMap;
		IVerticalScrollBar _ScrollBar;
		TaggerResult _Tags;
		CommentTagger _CommentTagger;
		bool _HasEvents;

		public CommentMargin(IWpfTextView textView, IVerticalScrollBar verticalScrollbar)
			: base(textView) {
			_TextView = textView;
			_ScrollBar = verticalScrollbar;
			_Tags = textView.GetOrCreateSingletonProperty<TaggerResult>();
			if (textView.Properties.TryGetProperty(nameof(CommentTaggerProvider), out _CommentTagger)) {
				_CommentTagger.TagAdded += CommentTagger_TagAdded;
			}
			_EditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(textView);

			Visibility = Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.CodeMarginMask) ? Visibility.Visible : Visibility.Collapsed;

			Config.RegisterUpdateHandler(UpdateCommentMarginConfig);
			_TextView.TextBuffer.ChangedLowPriority += TextView_TextBufferChanged;
			IsVisibleChanged += Margin_VisibilityChanged;
			_ScrollBar.TrackSpanChanged += ScrollBar_TrackSpanChanged;

			Width = MarginSize;
		}

		FrameworkElement IWpfTextViewMargin.VisualElement => this;
		bool ITextViewMargin.Enabled => true;
		public override string MarginName => nameof(CommentMargin);
		public override double MarginSize => MarkSize + MarkPadding + MarkPadding + /*extra padding*/ 2 * MarkPadding;

		static Dictionary<IClassificationType, Brush> InitClassificationBrushMapper() {
			var r = ServicesHelper.Instance.ClassificationTypeRegistry;
			return new Dictionary<IClassificationType, Brush> {
				{ r.GetClassificationType(Constants.EmphasisComment), __EmphasisBrush },
				{ r.GetClassificationType(Constants.TodoComment), __ToDoBrush },
				{ r.GetClassificationType(Constants.NoteComment), __NoteBrush },
				{ r.GetClassificationType(Constants.HackComment), __HackBrush },
				{ r.GetClassificationType(Constants.UndoneComment), __UndoneBrush },
				{ r.GetClassificationType(Constants.Task1Comment), __TaskBrush },
				{ r.GetClassificationType(Constants.Task2Comment), __TaskBrush },
				{ r.GetClassificationType(Constants.Task3Comment), __TaskBrush },
				{ r.GetClassificationType(Constants.Task4Comment), __TaskBrush },
				{ r.GetClassificationType(Constants.Task5Comment), __TaskBrush },
				{ r.GetClassificationType(Constants.Task6Comment), __TaskBrush },
				{ r.GetClassificationType(Constants.Task7Comment), __TaskBrush },
				{ r.GetClassificationType(Constants.Task8Comment), __TaskBrush },
				{ r.GetClassificationType(Constants.Task9Comment), __TaskBrush },
				{ r.GetClassificationType(Constants.CodePreprocessorKeyword), __PreProcessorBrush },
				{ MarkdownTagger.HeaderClassificationTypes[1].ClassificationType, __TaskBrush },
				{ MarkdownTagger.DummyHeaderTags[1].ClassificationType, __TaskBrush },
			};
		}

		void UpdateCommentMarginConfig(ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.HasAnyFlag(Features.SyntaxHighlight | Features.ScrollbarMarkers) == false) {
				return;
			}
			var setVisible = Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.CodeMarginMask);
			var visible = Visibility == Visibility.Visible;
			if (setVisible == false && visible) {
				Visibility = Visibility.Collapsed;
				_TextView.TextBuffer.ChangedLowPriority -= TextView_TextBufferChanged;
				_ScrollBar.TrackSpanChanged -= ScrollBar_TrackSpanChanged;
			}
			else if (setVisible && visible == false) {
				Visibility = Visibility.Visible;
				_TextView.TextBuffer.ChangedLowPriority += TextView_TextBufferChanged;
				_ScrollBar.TrackSpanChanged += ScrollBar_TrackSpanChanged;
			}
			if (Visibility == Visibility.Visible) {
				InvalidateVisual();
			}
		}

		void TextView_TextBufferChanged(object sender, TextContentChangedEventArgs args) {
			if (args.Changes.Count == 0) {
				return;
			}
			_Tags.PurgeOutdatedTags(args);
			InvalidateVisual();
		}

		void CommentTagger_TagAdded(object sender, EventArgs e) {
			InvalidateVisual();
		}

		void Margin_VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) {
			//There is no need to update event handlers if the visibility change is the result of an options change (since we will
			//update the event handlers after changing all the options).
			//
			//It is possible this will get called twice in quick succession (when the tab containing the host is made visible, the view and the margin
			//will get visibility changed events).
			Debug.WriteLine(e.Property + " changed: " + e.OldValue + " -> " + e.NewValue);
			UpdateEventHandlers(true);
		}

		bool UpdateEventHandlers(bool checkEvents) {
			bool needEvents = checkEvents && _TextView.VisualElement.IsVisible;

			if (needEvents != _HasEvents) {
				_HasEvents = needEvents;
				if (needEvents) {
					_ScrollBar.TrackSpanChanged += ScrollBar_TrackSpanChanged;
					return true;
				}
				_ScrollBar.TrackSpanChanged -= ScrollBar_TrackSpanChanged;
			}

			return false;
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
			if (_TextView?.IsClosed == false && _Tags.HasTag) {
				DrawMarkers(drawingContext);
			}
		}

		void DrawMarkers(DrawingContext drawingContext) {
			var lastY = 0.0;
			Brush lastBrush = null;
			var snapshot = _TextView.TextSnapshot;
			var snapshotLength = snapshot.Length;
			foreach (var tag in _Tags.GetTags()) {
				if (tag.End >= snapshotLength) {
					continue;
				}
				//todo: customizable marker style
				var c = tag.Tag.ClassificationType;
				Brush b;
				if (__ClassificationBrushMapper.TryGetValue(c, out b) == false) {
					continue;
				}
				var y = _ScrollBar.GetYCoordinateOfBufferPosition(tag.TrackingSpan.GetStartPoint(snapshot));
				if (lastY + HalfMarkSize > y && lastBrush == b) {
					// avoid drawing too many closed markers
					continue;
				}
				else if (b == __PreProcessorBrush) {
					if (!Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.CompilerDirective)) {
						continue;
					}
					DrawMark(drawingContext, b, y, 0);
				}
				else if (b == __TaskBrush) {
					if (!Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.SpecialComment)) {
						continue;
					}
					DrawTaskMark(drawingContext, b, y, String.Empty, tag.ContentText);
				}
				else {
					if (!Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.SpecialComment)) {
						continue;
					}
					DrawCommentMark(drawingContext, b, y);
				}
				lastY = y;
				lastBrush = b;
			}
		}

		/// <summary>draws task name (inverted) and the task content</summary>
		static void DrawTaskMark(DrawingContext dc, Brush brush, double y, string taskName, string taskContent) {
			var tt = WpfHelper.ToFormattedText(taskContent, 9, brush);
			dc.DrawRectangle(__TaskBackgroundBrush, __EmptyPen, new Rect(0, y - tt.Height / 2, tt.Width, tt.Height));
			dc.DrawText(tt, new Point(0, y - tt.Height / 2));
		}

		/// <summary>draws a rectangle, with a border</summary>
		static void DrawCommentMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawRectangle(brush, __CommentPen, new Rect(MarkPadding, y - HalfMarkSize, MarkSize, MarkSize));
		}

		/// <summary>draws circle or a rectangle</summary>
		static void DrawMark(DrawingContext dc, Brush brush, double y, int style) {
			switch (style) {
				case 0:
					dc.DrawEllipse(brush, null, new Point(HalfMarkSize, y - HalfMarkSize), MarkSize, MarkSize);
					break;
				default:
					dc.DrawRectangle(brush, null, new Rect(MarkPadding, y - HalfMarkSize, MarkSize, MarkSize));
					break;
			}
		}

		#region IDisposable Support
		public override void Dispose() {
			if (_TextView != null) {
				Config.UnregisterUpdateHandler(UpdateCommentMarginConfig);
				_TextView.TextBuffer.ChangedLowPriority -= TextView_TextBufferChanged;
				_TextView.Properties.RemoveProperty(nameof(CommentTaggerProvider));
				_TextView.Properties.RemoveProperty(typeof(TaggerResult));
				_TextView = null;
				IsVisibleChanged -= Margin_VisibilityChanged;
				_ScrollBar.TrackSpanChanged -= ScrollBar_TrackSpanChanged;
				if (_CommentTagger != null) {
					_CommentTagger.TagAdded -= CommentTagger_TagAdded;
					_CommentTagger = null;
				}
				_EditorFormatMap = null;
				_ScrollBar = null;
				_Tags = null;
			}
		}
		#endregion
	}
}
