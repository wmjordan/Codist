using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Codist.Taggers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Margins
{
	sealed class CommentMargin : FrameworkElement, IWpfTextViewMargin
	{
		public const string MarginName = nameof(CommentMargin);

		readonly IWpfTextView _TextView;
		readonly IEditorFormatMap _EditorFormatMap;
		readonly IVerticalScrollBar _ScrollBar;
		readonly TaggerResult _Tags;
		readonly CommentTagger _CommentTagger;

		//ToDo: Configurable marker styles
		//ToDo: Change brush colors according to user settings
		static readonly Pen CommentPen = new Pen(Brushes.LightGreen, 1);
		static readonly Brush LineNumberBrush = Brushes.DarkGray;
		static readonly Pen LineNumberPen = new Pen(LineNumberBrush, 1) { DashStyle = DashStyles.Dash };
		static readonly Pen EmptyPen = new Pen();
		static readonly Brush EmphasisBrush = new SolidColorBrush(Constants.CommentColor);
		static readonly Brush ToDoBrush = new SolidColorBrush(Constants.ToDoColor);
		static readonly Brush NoteBrush = new SolidColorBrush(Constants.NoteColor);
		static readonly Brush HackBrush = new SolidColorBrush(Constants.HackColor);
		static readonly Brush UndoneBrush = new SolidColorBrush(Constants.UndoneColor);
		static readonly Brush TaskBrush = new SolidColorBrush(Constants.TaskColor);
		static readonly Brush PreProcessorBrush = Brushes.Gray;
		static readonly Brush TaskBackgroundBrsh = Brushes.White.Alpha(0.5);
		//note: this dictionary determines which style has a scrollbar marker
		static readonly Dictionary<IClassificationType, Brush> ClassificationBrushMapper = InitClassificationBrushMapper();
		bool _HasEvents;
		const double MarkPadding = 1.0;
		const double MarkSize = 4.0;
		const double HalfMarkSize = MarkSize / 2 + MarkPadding;

		public CommentMargin(IWpfTextView textView, IVerticalScrollBar verticalScrollbar) {
			_TextView = textView;

			IsHitTestVisible = false;

			_ScrollBar = verticalScrollbar;
			_Tags = textView.Properties.GetOrCreateSingletonProperty(() => new TaggerResult());
			if (textView.Properties.TryGetProperty(nameof(CommentTaggerProvider), out _CommentTagger)) {
				_CommentTagger.TagAdded += CommentTagger_TagAdded;
			}
			_EditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(textView);

			Width = MarkSize + MarkPadding + MarkPadding + /*extra padding*/ 2 * MarkPadding;

			Visibility = Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.CodeMarginMask) ? Visibility.Visible : Visibility.Collapsed;
			Config.Updated += Config_Updated;
			//subscribe to change events and use them to clean up and update the markers
			_TextView.TextBuffer.Changed += TextView_TextBufferChanged;
			IsVisibleChanged += OnViewOrMarginVisiblityChanged;
			//_TextView.VisualElement.IsVisibleChanged += OnViewOrMarginVisiblityChanged;
			_ScrollBar.TrackSpanChanged += OnMappingChanged;
		}

		FrameworkElement IWpfTextViewMargin.VisualElement => this;
		double ITextViewMargin.MarginSize => ActualWidth;
		bool ITextViewMargin.Enabled => true;

		static Dictionary<IClassificationType, Brush> InitClassificationBrushMapper() {
			var r = ServicesHelper.Instance.ClassificationTypeRegistry;
			return new Dictionary<IClassificationType, Brush> {
				{ r.GetClassificationType(Constants.EmphasisComment), EmphasisBrush },
				{ r.GetClassificationType(Constants.TodoComment), ToDoBrush },
				{ r.GetClassificationType(Constants.NoteComment), NoteBrush },
				{ r.GetClassificationType(Constants.HackComment), HackBrush },
				{ r.GetClassificationType(Constants.UndoneComment), UndoneBrush },
				{ r.GetClassificationType(Constants.Task1Comment), TaskBrush },
				{ r.GetClassificationType(Constants.Task2Comment), TaskBrush },
				{ r.GetClassificationType(Constants.Task3Comment), TaskBrush },
				{ r.GetClassificationType(Constants.Task4Comment), TaskBrush },
				{ r.GetClassificationType(Constants.Task5Comment), TaskBrush },
				{ r.GetClassificationType(Constants.Task6Comment), TaskBrush },
				{ r.GetClassificationType(Constants.Task7Comment), TaskBrush },
				{ r.GetClassificationType(Constants.Task8Comment), TaskBrush },
				{ r.GetClassificationType(Constants.Task9Comment), TaskBrush },
				{ r.GetClassificationType(Constants.CodePreprocessorKeyword), PreProcessorBrush },
				{ MarkdownTaggerProvider.HeaderClassificationTypes[1].ClassificationType, TaskBrush },
				{ MarkdownTaggerProvider.DummyHeaderTags[1].ClassificationType, TaskBrush },
			};
		}

		ITextViewMargin ITextViewMargin.GetTextViewMargin(string marginName) {
			return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		void Config_Updated(object sender, ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.HasAnyFlag(Features.SyntaxHighlight | Features.ScrollbarMarkers) == false) {
				return;
			}
			var setVisible = Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.CodeMarginMask);
			var visible = Visibility == Visibility.Visible;
			if (setVisible == false && visible) {
				Visibility = Visibility.Collapsed;
				_TextView.TextBuffer.Changed -= TextView_TextBufferChanged;
				_ScrollBar.TrackSpanChanged -= OnMappingChanged;
			}
			else if (setVisible && visible == false) {
				Visibility = Visibility.Visible;
				_TextView.TextBuffer.Changed += TextView_TextBufferChanged;
				_ScrollBar.TrackSpanChanged += OnMappingChanged;
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

		void OnViewOrMarginVisiblityChanged(object sender, DependencyPropertyChangedEventArgs e) {
			//There is no need to update event handlers if the visibility change is the result of an options change (since we will
			//update the event handlers after changing all the options).
			//
			//It is possible this will get called twice in quick succession (when the tab containing the host is made visible, the view and the margin
			//will get visibility changed events).
			Debug.WriteLine(e.Property + " changed: " + e.OldValue + " -> " + e.NewValue);
			UpdateEventHandlers(true);
		}

		Brush GetBrush(string name, string resource) {
			var rd = _EditorFormatMap.GetProperties(name);
			return rd.Contains(resource) ? rd[resource] as Brush : null;
		}

		bool UpdateEventHandlers(bool checkEvents) {
			bool needEvents = checkEvents && _TextView.VisualElement.IsVisible;

			if (needEvents != _HasEvents) {
				_HasEvents = needEvents;
				if (needEvents) {
					_ScrollBar.TrackSpanChanged += OnMappingChanged;
					return true;
				}
				_ScrollBar.TrackSpanChanged -= OnMappingChanged;
			}

			return false;
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
			if (_TextView.IsClosed == false && _Tags.HasTag) {
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
				if (ClassificationBrushMapper.TryGetValue(c, out b) == false) {
					continue;
				}
				var y = _ScrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(snapshot, tag.Start));
				if (lastY + HalfMarkSize > y && lastBrush == b) {
					// avoid drawing too many closed markers
					continue;
				}
				else if (b == PreProcessorBrush) {
					if (!Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.CompilerDirective)) {
						continue;
					}
					DrawMark(drawingContext, b, y, 0);
				}
				else if (b == TaskBrush) {
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
			//var ft = WpfHelper.ToFormattedText(taskName, 9, TaskBackgroundBrsh).SetBold();
			//dc.DrawRectangle(brush, EmptyPen, new Rect(0, y - ft.Height / 2, ft.Width, ft.Height));
			//dc.DrawText(ft, new Point(0, y - ft.Height / 2));
			var tt = WpfHelper.ToFormattedText(taskContent, 9, brush);
			dc.DrawRectangle(TaskBackgroundBrsh, EmptyPen, new Rect(0, y - tt.Height / 2, tt.Width, tt.Height));
			dc.DrawText(tt, new Point(0, y - tt.Height / 2));
		}

		/// <summary>draws a rectangle, with a border</summary>
		static void DrawCommentMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawRectangle(brush, CommentPen, new Rect(MarkPadding, y - HalfMarkSize, MarkSize, MarkSize));
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
		bool disposedValue;

		void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					Config.Updated -= Config_Updated;
					_TextView.TextBuffer.Changed -= TextView_TextBufferChanged;
					IsVisibleChanged -= OnViewOrMarginVisiblityChanged;
					_ScrollBar.TrackSpanChanged -= OnMappingChanged;
					if (_CommentTagger != null) {
						_CommentTagger.TagAdded -= CommentTagger_TagAdded;
					}
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
