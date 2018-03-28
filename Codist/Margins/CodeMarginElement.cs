using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Codist.Classifiers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Codist.Helpers;

namespace Codist.Margins
{
	sealed class CodeMarginElement : FrameworkElement, IDisposable
	{
		readonly IWpfTextView _TextView;
		readonly IEditorFormatMap _EditorFormatMap;
		readonly IVerticalScrollBar _ScrollBar;
		readonly TaggerResult _Tags;

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
		static readonly Brush TaskBrush = new SolidColorBrush(Constants.TaskColor);
		static readonly Brush PreProcessorBrush = Brushes.Gray;
		//note: this dictionary determines which style has a scrollbar marker
		static readonly Dictionary<string, Brush> ClassificationBrushMapper = new Dictionary<string, Brush> {
			{ Constants.EmphasisComment, EmphasisBrush },
			{ Constants.TodoComment, ToDoBrush },
			{ Constants.NoteComment, NoteBrush },
			{ Constants.HackComment, HackBrush },
			{ Constants.Task1Comment, TaskBrush },
			{ Constants.Task2Comment, TaskBrush },
			{ Constants.Task3Comment, TaskBrush },
			{ Constants.Task4Comment, TaskBrush },
			{ Constants.Task5Comment, TaskBrush },
			{ Constants.Task6Comment, TaskBrush },
			{ Constants.Task7Comment, TaskBrush },
			{ Constants.Task8Comment, TaskBrush },
			{ Constants.Task9Comment, TaskBrush },
			{ Constants.CodePreprocessorKeyword, PreProcessorBrush },
		};
		bool _HasEvents;
		const double MarkPadding = 1.0;
		const double MarkSize = 4.0;
		const double HalfMarkSize = MarkSize / 2 + MarkPadding;

		public CodeMarginElement(IWpfTextView textView, CodeMarginFactory factory, ITagAggregator<ClassificationTag> tagger, IVerticalScrollBar verticalScrollbar) {
			_TextView = textView;

			IsHitTestVisible = false;

			_ScrollBar = verticalScrollbar;
			_Tags = textView.Properties.GetOrCreateSingletonProperty(() => new TaggerResult());
			_EditorFormatMap = factory.EditorFormatMapService.GetEditorFormatMap(textView);

			Width = MarkSize + MarkPadding + MarkPadding + /*extra padding*/ 2 * MarkPadding;

			Visibility = Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.CodeMarginMask) ? Visibility.Visible : Visibility.Collapsed;
			Config.Updated += Config_Updated;
			//subscribe to change events and use them to update the markers
			_TextView.TextBuffer.Changed += TextView_TextBufferChanged;
			IsVisibleChanged += OnViewOrMarginVisiblityChanged;
			//_TextView.VisualElement.IsVisibleChanged += OnViewOrMarginVisiblityChanged;
			_ScrollBar.TrackSpanChanged += OnMappingChanged;
		}

		void Config_Updated(object sender, EventArgs e) {
			var setVisible = Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.CodeMarginMask);
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
			Debug.WriteLine($"snapshot version: {args.AfterVersion.VersionNumber}");
			var tags = _Tags.Tags;
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
				_Tags.Version = args.AfterVersion.VersionNumber;
				_Tags.LastParsed = args.Before.GetLineFromPosition(args.Changes[0].OldPosition).Start.Position;
			}
			catch (ArgumentOutOfRangeException) {
				MessageBox.Show(String.Join("\n",
					"Code margin exception:", args.Changes[0].OldPosition,
					"Before length:", args.Before.Length,
					"After length:", args.After.Length
				));
			}
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
				else {
					_ScrollBar.TrackSpanChanged -= OnMappingChanged;
				}
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
			if (_TextView.IsClosed) {
				return;
			}
			if (_Tags.Tags.Count > 0) {
				DrawMarkers(drawingContext);
			}
		}

		void DrawMarkers(DrawingContext drawingContext) {
			var lastY = 0.0;
			Brush lastBrush = null;
			var tags = new List<SpanTag>(_Tags.Tags);
			tags.Sort((x, y) => x.Start - y.Start);
			var snapshot = _TextView.TextSnapshot;
			var snapshotLength = snapshot.Length;
			foreach (var tag in tags) {
				if (tag.End >= snapshotLength) {
					continue;
				}
				//todo: customizable marker style
				var c = tag.Tag.ClassificationType.Classification;
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
					//note the text relies on the last character of Constants.Task1Comment, etc.
					DrawTaskMark(drawingContext, b, y, c[c.Length - 1].ToString());
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

		static void DrawTaskMark(DrawingContext dc, Brush brush, double y, string taskName) {
			var ft = InterfaceHelper.ToFormattedText(taskName, 9, Brushes.White).SetBold();
			dc.DrawRectangle(brush, EmptyPen, new Rect(0, y - ft.Height / 2, ft.Width, ft.Height));
			dc.DrawText(ft, new Point(0, y - ft.Height / 2));
		}

		static void DrawCommentMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawRectangle(brush, CommentPen, new Rect(MarkPadding, y - HalfMarkSize, MarkSize, MarkSize));
		}

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
		bool disposedValue = false;

		void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					Config.Updated -= Config_Updated;
					_TextView.TextBuffer.Changed -= TextView_TextBufferChanged;
					IsVisibleChanged -= OnViewOrMarginVisiblityChanged;
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
