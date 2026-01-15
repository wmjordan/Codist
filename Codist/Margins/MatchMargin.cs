using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using CLR;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using R = Codist.Properties.Resources;

namespace Codist.Margins
{
	sealed class MatchMargin : MarginElementBase, IDisposable, IWpfTextViewMargin
	{
		const string FormatName = "Selected Text", PartialMatchFormatName = "Inactive Selected Text";
		const double MarkerSize = 2, FullMarkerSize = MarkerSize * 2;

		IEditorFormatMap _EditorFormatMap;
		IWpfTextView _TextView;
		IVerticalScrollBar _ScrollBar;
		ITextSearchService2 _SearchService;
		ITextStructureNavigator _TextNavigator;
		DispatcherTimer _DelayTimer;
		CancellationTokenSource _currentSearchCts;
		Brush _MatchBrush, _CaseMismatchBrush;
		Pen _MatchPen, _CaseMismatchPen;
		List<MatchedSpan> _Matches;
		SearchContext _SearchContext;
		bool _KeyboardControl;

		public MatchMargin(IWpfTextView textView, IVerticalScrollBar scrollBar) : base(textView) {
			_TextView = textView;
			_ScrollBar = scrollBar;
			_EditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(textView);
			_SearchService = ServicesHelper.Instance.TextSearch;
			_TextNavigator = ServicesHelper.Instance.TextStructureNavigator.GetTextStructureNavigator(textView.TextBuffer);

			_DelayTimer = new DispatcherTimer(
				DispatcherPriority.Normal,
				Dispatcher.CurrentDispatcher) {
				Interval = TimeSpan.FromMilliseconds(200),
				IsEnabled = false
			};
			Config.RegisterUpdateHandler(UpdateSelectionMarginConfig);
			if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MatchSelection)) {
				Visibility = Visibility.Visible;
				Setup();
			}
			else {
				Visibility = Visibility.Collapsed;
			}
			_KeyboardControl = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.KeyboardControlMatch);
			Width = FullMarkerSize;
		}

		public override string MarginName => nameof(MatchMargin);
		public override double MarginSize => FullMarkerSize;

		void Setup() {
			_EditorFormatMap.FormatMappingChanged += EditorFormatMap_FormatMappingChanged;
			_TextView.Selection.SelectionChanged += TextView_SelectionChanged;
			_ScrollBar.TrackSpanChanged += ScrollBar_TrackSpanChanged;
			_DelayTimer.Tick += OnDelayTimerElapsed;
			UpdateDrawingElements();
		}

		void UpdateSelectionMarginConfig(ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.MatchFlags(Features.ScrollbarMarkers) == false) {
				return;
			}
			var setVisible = IsFeatureEnabled && Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MatchSelection);
			_TextView.Selection.SelectionChanged -= TextView_SelectionChanged;
			_ScrollBar.TrackSpanChanged -= ScrollBar_TrackSpanChanged;
			_DelayTimer.Stop();
			_currentSearchCts.CancelAndDispose();
			_KeyboardControl = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.KeyboardControlMatch);
			var visible = Visibility == Visibility.Visible;
			if (setVisible == false && visible) {
				Visibility = Visibility.Collapsed;
			}
			else if (setVisible) {
				if (visible == false) {
					Visibility = Visibility.Visible;
				}
				Setup();
			}
			InvalidateVisual();
		}

		void EditorFormatMap_FormatMappingChanged(object sender, FormatItemsEventArgs e) {
			foreach (var item in e.ChangedItems) {
				if (item == FormatName) {
					UpdateDrawingElements();
					InvalidateVisual();
					return;
				}
			}
		}

		void UpdateDrawingElements() {
			_MatchBrush = _EditorFormatMap.GetProperties(FormatName).GetBackgroundBrush() ?? ThemeCache.FileTabProvisionalSelectionBrush;
			_MatchPen = new Pen(_MatchBrush, 1);
			_CaseMismatchBrush = _EditorFormatMap.GetProperties(PartialMatchFormatName).GetBackgroundBrush() ?? ThemeCache.FileTabProvisionalSelectionBrush.Alpha(0.5).MakeFrozen();
			_CaseMismatchPen = new Pen(_CaseMismatchBrush, 1);
		}

		async Task ExecuteSearchAsync() {
			var token = SyncHelper.CancelAndRetainToken(ref _currentSearchCts);

			const int MAX_MATCH = 10000;
			int c = 0;
			var ctx = _SearchContext;
			string t;
			if ((t = ctx?.Text) is null) {
				goto QUIT;
			}
			var searchSpan = ctx.Span;
			var r = new List<MatchedSpan>(16);
			var sp = searchSpan.Start.Position;
			var l = _TextView.TextSnapshot.Length - 1;
			var w = !ctx.Options.MatchFlags(FindOptions.WholeWord) && IsWord(searchSpan, l);
			foreach (var span in _SearchService.FindAll(_TextView.TextSnapshot.ToSnapshotSpan(), searchSpan.End, t, ctx.Options)) {
				if (token.IsCancellationRequested) {
					_Matches = null;
					goto RETURN;
				}
				if (span.Start.Position == sp) {
					continue;
				}
				r.Add(new MatchedSpan(span, t == span.GetText(), !w || IsWord(span, l)));
				if (++c > MAX_MATCH) {
					break;
				}
			}
			if (r.Count == 0) {
				goto QUIT;
			}
			_Matches = r;

		RETURN:
			await SyncHelper.SwitchToMainThreadAsync(token);
			ctx.Success = true;
			InvalidateVisual();
			return;
		QUIT:
			if (_Matches != null) {
				_Matches = null;
				goto RETURN;
			}
			// matches is already null

			static bool IsWord(SnapshotSpan ss, int l) {
				return (ss.Start.Position == 0 || !(ss.Start - 1).GetChar().IsProgrammaticChar())
					&& (ss.End.Position == l || !ss.End.GetChar().IsProgrammaticChar());
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
		async void OnDelayTimerElapsed(object sender, EventArgs e) {
			_DelayTimer.Stop();
			try {
				await ExecuteSearchAsync();
			}
			catch (Exception) {
#if DEBUG
				throw;
#endif
			}
		}

		void RequestSearch() {
			var ctx = new SearchContext(this);
			if (ctx.Text != null) {
				if (_SearchContext?.Success == true && ctx.Span.Equals(_SearchContext.Span)) {
					return;
				}
				_DelayTimer.Stop();
				_DelayTimer.Start();
				_SearchContext = ctx;
			}
			else {
				_DelayTimer.Stop();
				SyncHelper.CancelAndDispose(ref _currentSearchCts, false);
				_SearchContext = null;
				if (_Matches != null) {
					_Matches = null;
					InvalidateVisual();
				}
			}
		}

		void TextView_SelectionChanged(object sender, EventArgs args) {
			RequestSearch();
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
			if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MatchSelection)) {
				DrawMatches(drawingContext);
			}
		}

		void DrawMatches(DrawingContext drawingContext) {
			var ss = _Matches;
			if (ss is null) {
				return;
			}
			double lastPos = -100;
			foreach (var item in ss) {
				var top = _ScrollBar.GetYCoordinateOfBufferPosition(item.Span.Start);
				if (top - lastPos >= MarkerSize || top < lastPos) {
					if (item.WholeWord) {
						drawingContext.DrawRectangle(item.MatchCase ? _MatchBrush : _CaseMismatchBrush, null, new Rect(0, top - MarkerSize, FullMarkerSize, FullMarkerSize));
					}
					else {
						drawingContext.DrawRectangle(null, item.MatchCase ? _MatchPen : _CaseMismatchPen, new Rect(0, top - MarkerSize, FullMarkerSize, FullMarkerSize));
					}
					lastPos = top;
				}
			}
			drawingContext.DrawText(WpfHelper.ToFormattedText(ss.Count > 9999 ? R.T_10KPlus : ss.Count.ToText(), 9, _MatchBrush), new Point(FullMarkerSize, _ScrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(_TextView.TextSnapshot, 0))));
		}

		#region IDisposable Support
		void UnbindEvents() {
			Config.UnregisterUpdateHandler(UpdateSelectionMarginConfig);
			_TextView.Selection.SelectionChanged -= TextView_SelectionChanged;
			_EditorFormatMap.FormatMappingChanged -= EditorFormatMap_FormatMappingChanged;
			_ScrollBar.TrackSpanChanged -= ScrollBar_TrackSpanChanged;
			_DelayTimer.Tick -= OnDelayTimerElapsed;
		}

		public override void Dispose() {
			if (_TextView != null) {
				UnbindEvents();
				_TextView = null;
				_EditorFormatMap = null;
				_ScrollBar = null;
				_MatchBrush = null;
				_DelayTimer.Stop();
				SyncHelper.CancelAndDispose(ref _currentSearchCts, false);
			}
		}
		#endregion

		sealed class SearchContext
		{
			public SnapshotSpan Span;
			public string Text;
			public FindOptions Options;
			public bool Success;

			public SearchContext(MatchMargin me) {
				bool emptySelection;
				var view = me._TextView;
				if (view.Selection.IsEmpty) {
					Span = me._TextNavigator.GetExtentOfWord(view.Caret.Position.BufferPosition).Span;
					emptySelection = true;
				}
				else if (!view.IsMultilineSelected()) {
					Span = view.FirstSelectionSpan();
					emptySelection = false;
				}
				else {
					return;
				}

				if (Span.Length.IsBetween(1, 256)) {
					if (String.IsNullOrWhiteSpace(Text = Span.GetText())
						|| emptySelection && !Text.IsProgrammaticSymbol()) {
						Text = null;
						return;
					}
					Options = FindOptions.OrdinalComparison | FindOptions.Wrap;
					if (me._KeyboardControl) {
						if (UIHelper.IsCtrlDown) {
							Options |= FindOptions.WholeWord;
						}
						if (UIHelper.IsShiftDown) {
							Options |= FindOptions.MatchCase;
						}
					}
				}
			}
		}

		readonly struct MatchedSpan(SnapshotSpan span, bool matchCase, bool wholeWord)
		{
			public readonly SnapshotSpan Span = span;
			public readonly bool MatchCase = matchCase;
			public readonly bool WholeWord = wholeWord;
		}
	}
}
