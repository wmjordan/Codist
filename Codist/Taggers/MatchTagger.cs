using System;
using System.Collections.Generic;
using CLR;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers;

sealed class MatchTagger : ITagger<TextMarkerTag>
{
	internal static readonly TextMarkerTag MatchMarkerTag = new("MatchMarker"),
		PartialMatchMarkerTag = new("PartialMatchMarker"),
		CaseMismatchMarkerTag = new("CaseMismatchMarker"),
		PartialCaseMismatchMarkerTag = new("PartialCaseMismatchMarker"),
		NoMatchTag = new("NoMatch");

	readonly ITextView _View;
	readonly ITextSearchService2 _SearchService;
	readonly ITextStructureNavigator _Navigator;

	bool _Enabled;

	string _SearchText;
	int _SearchTextLength;
	SnapshotSpan _SearchSpan;

	MatchCache _Matches;

	public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

	public MatchTagger(ITextView view, ITextStructureNavigator navigator) {
		_View = view;
		_SearchService = ServicesHelper.Instance.TextSearch;
		_Navigator = navigator;

		_Matches = new();

		Config.RegisterUpdateHandler(UpdateConfig);
		LoadConfig();
		_View.Closed += ViewClosed;
	}

	void UpdateConfig(ConfigUpdatedEventArgs args) {
		if (args.UpdatedFeature.MatchFlags(Features.ScrollbarMarkers)) {
			LoadConfig();
		}
	}

	void LoadConfig() {
		_View.Selection.SelectionChanged -= OnSelectionChanged;
		if (_Enabled = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MatchSelection)
			&& Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.MatchSelection)) {
			_View.Selection.SelectionChanged += OnSelectionChanged;
		}
	}

	void ViewClosed(object sender, EventArgs e) {
		_View.Closed -= ViewClosed;
		_View.Selection.SelectionChanged -= OnSelectionChanged;
		Config.UnregisterUpdateHandler(UpdateConfig);
	}

	void OnSelectionChanged(object sender, EventArgs e) {
		UpdateSearchText();
		RaiseTagsChanged();
	}

	void UpdateSearchText() {
		SnapshotSpan searchSpan;
		string searchText;
		bool isSelectionEmpty;
		if (isSelectionEmpty = _View.Selection.IsEmpty) {
			var extent = _Navigator.GetExtentOfWord(_View.GetCaretPosition());
			if (!extent.IsSignificant) {
				searchText = null;
				searchSpan = default;
				goto EXIT;
			}
			searchSpan = extent.Span;
		}
		else {
			searchSpan = _View.Selection.StreamSelectionSpan.SnapshotSpan;
		}

		if (searchSpan.Length.IsOutside(0, Config.Instance.ScrollbarMarker.MaxSearchCharLength)) {
			searchText = null;
			searchSpan = default;
		}
		else if (string.IsNullOrWhiteSpace(searchText = searchSpan.GetText())) {
			searchText = null;
		}
		else if (isSelectionEmpty) {
			if (searchSpan.Length < 2 || !searchText.IsProgrammaticSymbol()) {
				searchText = null;
				searchSpan = default;
			}
		}
		else if (searchText.Contains('\n')) {
			searchText = null;
			searchSpan = default;
		}
	EXIT:
		if (searchText != _SearchText) {
			_SearchText = searchText;
			_SearchSpan = searchSpan;
			_SearchTextLength = searchSpan.Length;
			_Matches.Reset(_View.TextSnapshot);
		}
		else {
			_SearchSpan = searchSpan;
		}
	}

	void RaiseTagsChanged() {
		if (_View.TextBuffer?.CurrentSnapshot != null) {
			TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(_View.TextViewLines.FormattedSpan));
		}
	}

	public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
		return !_Enabled || _SearchTextLength == 0 || spans.Count == 0
			? []
			: FindMatches(spans);
	}

	IEnumerable<ITagSpan<TextMarkerTag>> FindMatches(NormalizedSnapshotSpanCollection spans) {
		var snapshot = spans[0].Snapshot;
		var sl = snapshot.Length;
		_Matches.ResetIfOutdated(snapshot);
		var hasCache = _Matches.Count != 0;
		foreach (var span in spans) {
			var s = span;
			if (s.Length > 8192) {
				// prevents unreasonable excessive range search
				s = s.Intersection(_View.TextViewLines.FormattedSpan) ?? default;
			}
			if (s.Length < _SearchTextLength) {
				continue;
			}
			bool hasMatch = false;
			if (hasCache) {
				foreach (var match in _Matches.GetTaggedSpans(s)) {
					hasMatch = true;
					if (match.Tag != NoMatchTag) {
						yield return match;
					}
				}
				if (hasMatch) {
					continue;
				}
			}
			foreach (var match in _SearchService.FindAll(s, _SearchText, FindOptions.None)) {
				var intersection = match.Intersection(span);
				if (intersection.HasValue && intersection != _SearchSpan) {
					hasMatch = true;
					yield return _Matches.Add(MakeResultSpan(intersection.Value, sl));
				}
			}
			if (!hasMatch) {
				_Matches.Add(new TagSpan<TextMarkerTag>(s, NoMatchTag));
			}
		}
	}

	TagSpan<TextMarkerTag> MakeResultSpan(SnapshotSpan span, int snapshotLength) {
		return new TagSpan<TextMarkerTag>(span,
			span.GetText() == _SearchText ? IsWord(span, snapshotLength) ? MatchMarkerTag : PartialMatchMarkerTag
				: IsWord(span, snapshotLength) ? CaseMismatchMarkerTag : PartialCaseMismatchMarkerTag
			);
	}

	static bool IsWord(SnapshotSpan s, int snapshotLength) {
		return (s.Start.Position == 0 || !(s.Start - 1).GetChar().IsProgrammaticChar())
			&& (s.End.Position == snapshotLength || !s.End.GetChar().IsProgrammaticChar());
	}

	sealed class MatchCache
	{
		readonly SortedSet<TagSpan<TextMarkerTag>> _Tags = new(Comparer<TagSpan<TextMarkerTag>>.Create((x, y) => {
			if (ReferenceEquals(x, y)) {
				return 0;
			}
			var s1 = x.Span.Start;
			var s2 = y.Span.Start;
			return s1 < s2 ?
				x.Span.End <= s2 ? -1 : 0
				: s1 >= y.Span.End ? 1 : 0;
		}));

		public int SnapshotVersionNumber { get; private set; }
		public int Count => _Tags.Count;

		public IEnumerable<TagSpan<TextMarkerTag>> GetTaggedSpans(SnapshotSpan span) {
			foreach (var tag in _Tags.GetViewBetween(new TagSpan<TextMarkerTag>(new SnapshotSpan(span.Start, 0), NoMatchTag), new TagSpan<TextMarkerTag>(new SnapshotSpan(span.End, 0), NoMatchTag))) {
				if (span.OverlapsWith(tag.Span)) {
					yield return tag;
				}
			}
		}

		public TagSpan<TextMarkerTag> Add(TagSpan<TextMarkerTag> tag) {
			_Tags.Add(tag);
			return tag;
		}

		public void Clear() {
			_Tags.Clear();
		}

		public void ResetIfOutdated(ITextSnapshot snapshot) {
			if (snapshot.Version.VersionNumber != SnapshotVersionNumber) {
				Reset(snapshot);
			}
		}
		public void Reset(ITextSnapshot snapshot) {
			SnapshotVersionNumber = snapshot.Version.VersionNumber;
			_Tags.Clear();
		}
	}
}
