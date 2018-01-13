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
	sealed class CodeMarginElement : FrameworkElement
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
		static readonly Brush EmphasisBrush = new SolidColorBrush(Constants.CommentColor);
		static readonly Brush ToDoBrush = new SolidColorBrush(Constants.ToDoColor);
		static readonly Brush NoteBrush = new SolidColorBrush(Constants.NoteColor);
		static readonly Brush HackBrush = new SolidColorBrush(Constants.HackColor);
		static readonly Brush ClassNameBrush = Brushes.Blue;
		static readonly Brush StructNameBrush = Brushes.Teal;
		static readonly Brush InterfaceNameBrush = Brushes.DeepSkyBlue;
		static readonly Brush EnumNameBrush = Brushes.Purple;
		static readonly Brush PreProcessorBrush = Brushes.Gray;
		static readonly Brush AbstractionBrush = Brushes.DarkOrange;
		//readonly static Brush ThrowKeywordBrush = Brushes.Red;
		//readonly static Brush ReturnKeywordBrush = Brushes.Blue;
		static readonly Dictionary<string, Brush> ClassificationBrushMapper = new Dictionary<string, Brush> {
			{ Constants.EmphasisComment, EmphasisBrush },
			{ Constants.TodoComment, ToDoBrush },
			{ Constants.NoteComment, NoteBrush },
			{ Constants.HackComment, HackBrush },
			{ Constants.CodeClassName, ClassNameBrush },
			{ Constants.CodeStructName, StructNameBrush },
			{ Constants.CodeInterfaceName, InterfaceNameBrush },
			{ Constants.CodeEnumName, EnumNameBrush },
			{ Constants.CodeKeyword, ClassNameBrush },
			{ Constants.CodePreprocessorKeyword, PreProcessorBrush },
			{ Constants.CodeAbstractionKeyword, AbstractionBrush },
			//{ Constants.ThrowKeyword, ThrowKeywordBrush },
			//{ Constants.ReturnKeyword, ReturnKeywordBrush },
		};
		bool _HasEvents;
		bool _OptionsChanging;
		bool _IsMarginEnabled;
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

			_TextView.Options.OptionChanged += OnOptionChanged;
			//subscribe to change events and use them to update the markers
			_TextView.TextBuffer.Changed += (s, args) => {
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
			};
			_Tags.Tagger.Margin = this;
			IsVisibleChanged += OnViewOrMarginVisiblityChanged;
			_TextView.VisualElement.IsVisibleChanged += OnViewOrMarginVisiblityChanged;

			OnOptionChanged(null, null);
		}

		private void OnViewOrMarginVisiblityChanged(object sender, DependencyPropertyChangedEventArgs e) {
			//There is no need to update event handlers if the visibility change is the result of an options change (since we will
			//update the event handlers after changing all the options).
			//
			//It is possible this will get called twice in quick succession (when the tab containing the host is made visible, the view and the margin
			//will get visibility changed events).
			if (!_OptionsChanging) {
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
			var rd = _EditorFormatMap.GetProperties(name);

			if (rd.Contains(resource)) {
				return rd[resource] as Brush;
			}

			return null;
		}

		private bool UpdateEventHandlers(bool checkEvents) {
			bool needEvents = checkEvents && _TextView.VisualElement.IsVisible;

			if (needEvents != _HasEvents) {
				_HasEvents = needEvents;
				if (needEvents) {
					_EditorFormatMap.FormatMappingChanged += OnFormatMappingChanged;
					//_textView.LayoutChanged += OnLayoutChanged;
					//_textView.Selection.SelectionChanged += OnPositionChanged;
					//_scrollBar.Map.MappingChanged += OnMappingChanged;
					_ScrollBar.TrackSpanChanged += OnMappingChanged;
					OnFormatMappingChanged(null, null);

					return true;
				}
				else {
					_EditorFormatMap.FormatMappingChanged -= OnFormatMappingChanged;
					//_textView.LayoutChanged -= OnLayoutChanged;
					//_textView.Selection.SelectionChanged -= OnPositionChanged;
					//_scrollBar.Map.MappingChanged -= OnMappingChanged;
					_ScrollBar.TrackSpanChanged -= OnMappingChanged;
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
			if (_TextView.IsClosed) {
				return;
			}
			if (Config.Instance.MarkLineNumbers) {
				DrawLineNumbers(drawingContext);
			}
			if (_Tags.Tags.Count > 0) {
				DrawMarkers(drawingContext);
			}
		}

		private void DrawLineNumbers(DrawingContext drawingContext) {
			var snapshot = _TextView.TextSnapshot;
			var lc = snapshot.LineCount;
			var step = lc < 500 ? 50 : lc < 1000 ? 100 : lc < 5000 ? 500 : 1000;
			for (int i = step; i < lc; i += step) {
				var y = _ScrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(snapshot, snapshot.GetLineFromLineNumber(i - 1).Start));
				drawingContext.DrawLine(LineNumberPen, new Point(-100, y), new Point(100, y));
				var t = new FormattedText((i).ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, SystemFonts.StatusFontFamily.GetTypefaces().First(), 9, LineNumberBrush);
				drawingContext.DrawText(t, new Point(0, y));
			}
		}

		private void DrawMarkers(DrawingContext drawingContext) {
			var lastY = 0.0;
			Brush lastBrush = null;
			var tags = new List<SpanTag>(_Tags.Tags);
			tags.Sort((x, y) => x.Start - y.Start);
			var snapshot = _TextView.TextSnapshot;
			var snapshotLength = snapshot.Length;
			foreach (var tag in tags) {
				if (tag.End >= snapshotLength || tag.Start >= snapshotLength) {
					continue;
				}
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
				if (b == ClassNameBrush || b == InterfaceNameBrush || b == StructNameBrush || b == EnumNameBrush) {
					if (tag.Length > 0) {
						DrawDeclarationMark(drawingContext, b, y, c, snapshot.GetText(tag.Start, tag.Length));
					}
					continue;
				}
				else if (b == AbstractionBrush) {
					DrawMark(drawingContext, b, y, 1);
				}
				else if (b == PreProcessorBrush) {
					DrawMark(drawingContext, b, y, 0);
				}
				//else if (b == ThrowKeywordBrush) {
				//	DrawKeywordMark(drawingContext, b, y);
				//}
				else {
					DrawCommentMark(drawingContext, b, y);
				}
				lastY = y;
				lastBrush = b;
			}
		}

		static void DrawCommentMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawRectangle(brush, CommentPen, new Rect(MarkPadding, y - HalfMarkSize, MarkSize, MarkSize));
		}

		static void DrawDeclarationMark(DrawingContext dc, Brush brush, double y, string type, string typeName) {
			//dc.DrawEllipse(brush, null, new Point(HalfMarkSize, y - HalfMarkSize), MarkSize, MarkSize);
			string t = null;
			for (int i = 1; i < typeName.Length; i++) {
				var ch = typeName[i];
				if (!char.IsUpper(ch)) {
					continue;
				}
				char[] c = new char[2];
				c[0] = typeName[0];
				c[1] = ch;
				t = new string(c);
				break;
			}
			if (t == null) {
				t = typeName[0].ToString();
			}
			var ft = new FormattedText(t, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, SystemFonts.StatusFontFamily.GetTypefaces().First(), 9, brush);
			ft.SetFontWeight(FontWeight.FromOpenTypeWeight(800));
			dc.DrawText(ft, new Point(0, y - ft.Height / 2));
			//dc.DrawText(new FormattedText(type.Substring(0, 1), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SystemFonts.StatusFontFamily.GetTypefaces().First(), 12, Brushes.White), new Point(HalfMarkSize, y - HalfMarkSize));
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

		static void DrawKeywordMark(DrawingContext dc, Brush brush, double y) {
			dc.DrawEllipse(brush, null, new Point(HalfMarkSize, y - HalfMarkSize * 0.5), HalfMarkSize, HalfMarkSize);
		}
	}
}
