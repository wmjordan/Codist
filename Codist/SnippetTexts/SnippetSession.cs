using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CLR;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Codist.SnippetTexts
{
	sealed class SnippetSession : IOleCommandTarget
	{
		static readonly TextMarkerTag __PlaceholderTag = new ("Code Snippet Field");
		readonly IWpfTextView _view;
		readonly IVsTextView _vsView;
		readonly List<ITrackingSpan> _placeholders;
		readonly SimpleTagger<TextMarkerTag> _markerTagger;
		readonly List<TrackingTagSpan<TextMarkerTag>> _markerSpans = new List<TrackingTagSpan<TextMarkerTag>>();
		int _currentIndex;
		IOleCommandTarget _nextCommandTarget;
		bool _isDisposed;

		public SnippetSession(IWpfTextView view, string insertedText, int position) {
			_view = view;
			_placeholders = new List<ITrackingSpan>();
			_currentIndex = -1;
			_markerTagger = ServicesHelper.Instance.TextMarkerProvider.GetTextMarkerTagger(_view.TextBuffer);

			MarkPlaceholders(insertedText, position);

			if (_placeholders.Count == 0) {
				_isDisposed = true;
				return;
			}

			MoveToPlaceholder(0);

			_view.Caret.PositionChanged += Caret_PositionChanged;

			var vsView = ServicesHelper.Instance.EditorAdaptersFactoryService.GetViewAdapter(view);
			vsView.AddCommandFilter(this, out var nextTarget);
			SetCommandTarget(nextTarget);
			_vsView = vsView;

			_view.Closed += View_Closed;
		}

		bool IsCompletionActive() {
			return ServicesHelper.Instance.CompletionBroker.IsCompletionActive(_view);
		}

		void MarkPlaceholders(string text, int position) {
			var snapshot = _view.TextSnapshot;
			var textSpan = new Span(position, text.Length);

			foreach (var match in GetPlaceholders(new SnapshotSpan(snapshot, textSpan).GetText(), "[[", "]]")) {
				var span = new SnapshotSpan(snapshot, position + match.Start, match.Length)
					.ToTrackingSpan();
				_placeholders.Add(span);
				_markerSpans.Add(_markerTagger.CreateTagSpan(span, __PlaceholderTag));
			}
		}

		static IEnumerable<Span> GetPlaceholders(string text, string patternStart, string patternEnd) {
			int start = 0, end, length = text.Length;
			while ((start = text.IndexOf(patternStart, start, StringComparison.Ordinal)) >= 0
				&& (end = text.IndexOf(patternEnd, start + patternStart.Length, StringComparison.Ordinal)) > 0) {
				yield return new Span(start, (end += patternEnd.Length) - start);
				start = end;
				if (start >= length) {
					yield break;
				}
			}
		}

		public void SetCommandTarget(IOleCommandTarget nextCommandTarget) {
			_nextCommandTarget = nextCommandTarget;
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
			if (pguidCmdGroup == VSConstants.VSStd2K && _placeholders.Count > 0) {
				if (nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB) {
					if (IsCompletionActive()) {
						goto DEFAULT;
					}
					MoveToNext();
					return VSConstants.S_OK;
				}
				// Shift+Tab
				if (nCmdID == (uint)VSConstants.VSStd2KCmdID.BACKTAB) {
					MoveToPrevious();
					return VSConstants.S_OK;
				}
				// Esc or Enter
				if (nCmdID.CeqAny((uint)VSConstants.VSStd2KCmdID.CANCEL, (uint)VSConstants.VSStd2KCmdID.RETURN)) {
					if (IsCompletionActive()) {
						goto DEFAULT;
					}
					Terminate();
					return VSConstants.S_OK;
				}
			}

		DEFAULT:
			return _nextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
			return _nextCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
		}

		void MoveToNext() {
			int nextIndex = (_currentIndex + 1) % _placeholders.Count;
			MoveToPlaceholder(nextIndex);
		}

		void MoveToPrevious() {
			int prevIndex = (_currentIndex - 1 + _placeholders.Count) % _placeholders.Count;
			MoveToPlaceholder(prevIndex);
		}

		void MoveToPlaceholder(int index) {
			if (index < 0 || index >= _placeholders.Count) return;

			_currentIndex = index;
			var currentSpan = _placeholders[index].GetSpan(_view.TextSnapshot);
			_view.Selection.Select(currentSpan, false);
			_view.Caret.MoveTo(currentSpan.Start);
		}

		void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e) {
			if (_isDisposed || _placeholders.Count == 0) return;

			var currentSpan = _placeholders[_currentIndex].GetSpan(_view.TextSnapshot);

			if (!currentSpan.Contains(e.NewPosition.BufferPosition)) {
				for (int i = 0; i < _placeholders.Count; i++) {
					if (_placeholders[i].GetSpan(_view.TextSnapshot).Contains(e.NewPosition.BufferPosition)) {
						_currentIndex = i;
						_view.Selection.Select(_placeholders[i].GetSpan(_view.TextSnapshot), false);
						return;
					}
				}
				Terminate();
			}
		}

		public void Terminate() {
			if (_isDisposed) return;

			_view.Caret.PositionChanged -= Caret_PositionChanged;
			_view.Closed -= View_Closed;
			foreach (var span in _markerSpans) {
				_markerTagger.RemoveTagSpan(span);
			}
			_markerSpans.Clear();
			_vsView.RemoveCommandFilter(this);
			_isDisposed = true;

			OnSessionEnd();
		}

		void OnSessionEnd() {
			OnSessionEnded?.Invoke(this, EventArgs.Empty);
		}

		void View_Closed(object sender, EventArgs args) {
			Terminate();
		}

		public event EventHandler OnSessionEnded;
	}
}
