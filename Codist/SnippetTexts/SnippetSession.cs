using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using R = Codist.Properties.Resources;

namespace Codist.SnippetTexts
{
	sealed class SnippetSession : IOleCommandTarget
	{
		static readonly TextMarkerTag __PlaceholderTag = new("Code Snippet Field"),
			__PlaceholderDependentTag = new("Code Snippet Dependent Field");
		readonly IWpfTextView _view;
		readonly IVsTextView _vsView;

		readonly List<PlaceholderGroup> _placeholderGroups;
		readonly SimpleTagger<TextMarkerTag> _markerTagger;
		readonly List<TrackingTagSpan<TextMarkerTag>> _markerSpans = new List<TrackingTagSpan<TextMarkerTag>>();

		readonly ITextUndoHistory _undoHistory;
		readonly int _snapshotVersion;
		readonly IOleCommandTarget _nextCommandTarget;
		int _currentGroupIndex;
		bool _isDisposed;

		public SnippetSession(IWpfTextView view, IEnumerable<PlaceholderInfo> placeholderInfos, ITextUndoHistory undoHistory) {
			_view = view;
			_placeholderGroups = new List<PlaceholderGroup>();
			_currentGroupIndex = -1;
			_markerTagger = ServicesHelper.Instance.TextMarkerProvider.GetTextMarkerTagger(_view.TextBuffer);

			CreatePlaceholderGroups(placeholderInfos);

			if (_placeholderGroups.Count == 0) {
				goto ERROR;
			}

			var vsView = ServicesHelper.Instance.EditorAdaptersFactoryService.GetViewAdapter(view);
			if (vsView.AddCommandFilter(this, out var nextTarget) != VSConstants.S_OK) {
				goto ERROR;
			}
			_nextCommandTarget = nextTarget;
			_vsView = vsView;

			_snapshotVersion = view.TextSnapshot.Version.ReiteratedVersionNumber;
			_undoHistory = undoHistory;
			_undoHistory.UndoRedoHappened += HandleUndo;
			MoveToPlaceholderGroup(0);

			_view.Caret.PositionChanged += View_CaretPositionChanged;
			_view.Closed += View_Closed;
			return;
		ERROR:
			_isDisposed = true;
		}

		void CreatePlaceholderGroups(IEnumerable<PlaceholderInfo> placeholderInfos) {
			var snapshot = _view.TextSnapshot;
			var groupsDict = new Dictionary<string, PlaceholderGroup>(StringComparer.Ordinal);

			foreach (var info in placeholderInfos) {

				bool isNewGroup;
				if (isNewGroup = !groupsDict.TryGetValue(info.Name, out var group)) {
					group = new PlaceholderGroup(info.Name);
					groupsDict[info.Name] = group;
					_placeholderGroups.Add(group);
				}

				var span = info.ToSnapshotSpan(snapshot).ToTrackingSpan();
				group.Spans.Add(span);
				_markerSpans.Add(_markerTagger.CreateTagSpan(span, isNewGroup ? __PlaceholderTag : __PlaceholderDependentTag));
			}
		}

		bool IsCompletionActive() {
			return ServicesHelper.Instance.CompletionBroker.IsCompletionActive(_view);
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
			var nextCmd = _nextCommandTarget;
			if (pguidCmdGroup == VSConstants.VSStd2K
				&& _placeholderGroups.Count != 0) {
				switch ((VSConstants.VSStd2KCmdID)nCmdID) {
					case VSConstants.VSStd2KCmdID.TAB:
						if (IsCompletionActive()) {
							break;
						}
						MoveToNextGroup();
						return VSConstants.S_OK;
					case VSConstants.VSStd2KCmdID.BACKTAB: // Shift+Tab
						MoveToPreviousGroup();
						return VSConstants.S_OK;
					case VSConstants.VSStd2KCmdID.CANCEL: // Esc
					case VSConstants.VSStd2KCmdID.RETURN: // Enter
						if (IsCompletionActive()) {
							break;
						}

						if (!IsCaretInPlaceholder()) {
							nextCmd.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
						}
						SynchronizeCurrentGroup();
						Terminate();
						return VSConstants.S_OK;
				}
			}
			return nextCmd.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
			return _nextCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
		}

		void MoveToNextGroup() {
			if (_placeholderGroups.Count < 2) {
				return;
			}
			SynchronizeCurrentGroup();
			MoveToPlaceholderGroup((_currentGroupIndex + 1) % _placeholderGroups.Count);
		}

		void MoveToPreviousGroup() {
			int c = _placeholderGroups.Count;
			if (c < 2) {
				return;
			}
			SynchronizeCurrentGroup();
			MoveToPlaceholderGroup((_currentGroupIndex - 1 + c) % c);
		}

		void MoveToPlaceholderGroup(int index) {
			if (index < 0 || index >= _placeholderGroups.Count) return;

			var currentGroup = _placeholderGroups[index];
			var snapshot = _view.TextSnapshot;

			// 修改点：只获取第一个 Span 进行选中，不再遍历添加所有 Spans
			if (currentGroup.Spans.Count > 0) {
				_view.Selection.Clear();
				_view.SelectSpan(currentGroup.Spans[0].GetSpan(snapshot));
			}

			_currentGroupIndex = index;
		}

		void SynchronizeCurrentGroup() {
			if (_currentGroupIndex < 0 || _currentGroupIndex >= _placeholderGroups.Count) return;

			var group = _placeholderGroups[_currentGroupIndex];
			var snapshot = _view.TextSnapshot;

			var sourceTrackingSpan = GetSourceTrackingSpanWithCaret(group);
			if (sourceTrackingSpan == null) return;

			string newText = sourceTrackingSpan.GetText(snapshot);

			using (var tran = _undoHistory.CreateTransaction(R.T_UpdatePlaceholderText))
			using (var edit = _view.TextBuffer.CreateEdit()) {
				foreach (var span in group.Spans) {
					var currentSpan = span.GetSpan(snapshot);
					if (currentSpan.GetText() != newText) {
						edit.Replace(currentSpan, newText);
					}
				}
				if (edit.HasEffectiveChanges) {
					edit.Apply();
					tran.Complete();
				}
			}
		}

		ITrackingSpan GetSourceTrackingSpanWithCaret(PlaceholderGroup group) {
			ITrackingSpan sourceTrackingSpan = null;
			var caretPos = _view.Caret.Position.BufferPosition;
			var snapshot = _view.TextSnapshot;
			foreach (var span in group.Spans) {
				if (span.GetSpan(snapshot).Contains(caretPos)) {
					sourceTrackingSpan = span;
					break;
				}
			}
			if (sourceTrackingSpan == null && group.Spans.Count > 0) {
				sourceTrackingSpan = group.Spans[0];
			}

			return sourceTrackingSpan;
		}

		bool IsCaretInPlaceholder() {
			if (_isDisposed || _placeholderGroups.Count == 0) {
				return false;
			}

			var snapshot = _view.TextSnapshot;
			var pos = _view.Caret.Position.BufferPosition;

			if (_currentGroupIndex >= 0 && _currentGroupIndex < _placeholderGroups.Count) {
				var currentGroup = _placeholderGroups[_currentGroupIndex];
				foreach (var span in currentGroup.Spans) {
					if (span.GetSpan(snapshot).Contains(pos, true)) {
						return true;
					}
				}
			}
			for (int i = 0; i < _placeholderGroups.Count; i++) {
				if (i == _currentGroupIndex) continue;

				var group = _placeholderGroups[i];
				foreach (var span in group.Spans) {
					if (span.GetSpan(snapshot).Contains(pos, true)) {
						_currentGroupIndex = i;
						return true;
					}
				}
			}
			return false;
		}

		void View_CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) {
			if (_isDisposed || _currentGroupIndex < 0 || _currentGroupIndex >= _placeholderGroups.Count) return;

			var newPosition = e.NewPosition.BufferPosition;
			var currentGroup = _placeholderGroups[_currentGroupIndex];

			if (!currentGroup.Spans[0].GetSpan(newPosition.Snapshot).Contains(newPosition)) {
				SynchronizeCurrentGroup();
			}
		}

		public void Terminate() {
			if (_isDisposed) return;
			_undoHistory.UndoRedoHappened -= HandleUndo;
			_view.Caret.PositionChanged -= View_CaretPositionChanged;
			_view.Closed -= View_Closed;
			foreach (var span in _markerSpans) {
				_markerTagger.RemoveTagSpan(span);
			}
			_markerSpans.Clear();
			_vsView.RemoveCommandFilter(this);
			_isDisposed = true;

			OnSessionEnd();
		}

		void HandleUndo(object sender, TextUndoRedoEventArgs e) {
			if (e.State == TextUndoHistoryState.Undoing
				&& _view.TextSnapshot.Version.ReiteratedVersionNumber < _snapshotVersion) {
				Terminate();
			}
		}

		void OnSessionEnd() {
			OnSessionEnded?.Invoke(this, EventArgs.Empty);
		}

		void View_Closed(object sender, EventArgs args) {
			Terminate();
		}

		public event EventHandler OnSessionEnded;

		sealed class PlaceholderGroup(string name)
		{
			public readonly string Name = name;
			public List<ITrackingSpan> Spans = [];
		}
	}
}
