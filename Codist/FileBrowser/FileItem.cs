using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using CLR;
using Microsoft.VisualStudio.Shell;

namespace Codist.FileBrowser;

sealed class FileItem : INotifyPropertyChanged
{
	static SolutionItemInfo __DefaultSolutionState = GetDefaultSolutionState();

	static SolutionItemInfo GetDefaultSolutionState() {
		Config.RegisterUpdateHandler(UpdateSolutionStateOnConfigChange);
		return Config.Instance.FileBrowserOptions.MatchFlags(FileBrowserOptions.DimNonSolutionItems)
			? SolutionItemInfo.Unknown
			: SolutionItemInfo.Yes;
	}

	static void UpdateSolutionStateOnConfigChange(ConfigUpdatedEventArgs args) {
		if (args.UpdatedFeature.MatchFlags(Features.FileBrowser)) {
			__DefaultSolutionState = GetDefaultSolutionState();
		}
	}

	readonly FileSystemInfo _Info;
	readonly FileItemType _Type;
	readonly string _Name;
	string _Hint;
	bool _IsCurrent;
	SolutionItemInfo _IsSolutionItem = __DefaultSolutionState;
	FileState _State;
	FrameworkElement _Icon;
	FrameworkElement _Note;

	long _FileSize = -1;
	DateTime _CreationTime;
	DateTime _LastWriteTime;

	public event PropertyChangedEventHandler PropertyChanged;

	public string Name => _Name;
	public string Hint => _Hint;
	public string FullPath => _Info.FullName;
	public FileItemType Type => _Type;
	public bool IsEmptyFolder => _Type == FileItemType.EmptyFolder;
	public bool IsFolder => _Type.CeqAny(FileItemType.Folder, FileItemType.EmptyFolder);
	public bool IsFile => _Type.CeqAny(FileItemType.File, FileItemType.OpenedDocument);
	public bool IsCurrent {
		get => _IsCurrent;
		set {
			if (_IsCurrent != value) {
				_IsCurrent = value;
				PropertyChanged?.Invoke(this, new (nameof(IsCurrent)));
			}
		}
	}

	public FrameworkElement Icon => _Icon ??= VsImageHelper.GetImage(_Type switch {
		FileItemType.Folder => IconIds.Folder,
		FileItemType.EmptyFolder => IconIds.EmptyFolder,
		FileItemType.InaccessibleFolder => IconIds.InaccessibleFolder,
		FileItemType.Solution => IconIds.GoToSolutionFolder,
		FileItemType.UnloadedProject => IconIds.UnloadedProject,
		FileItemType.OpenedDocument => VsImageHelper.GetImageIdForFile(_Info.Name),
		_ => VsImageHelper.GetImageIdForFile(_Name)
	});

	public long FileSize {
		get {
			if (_FileSize < 0) {
				if (IsFile) {
					try {
						_FileSize = _Info is FileInfo info
							? info.Length
							: new FileInfo(_Info.FullName).Length;
					}
					catch {
						_FileSize = 0;
					}
				}
				else {
					_FileSize = 0;
				}
			}
			return _FileSize;
		}
	}

	public DateTime CreationTime {
		get {
			if (_CreationTime == default) {
				try { _CreationTime = _Info.CreationTime; }
				catch { _CreationTime = DateTime.MaxValue; }
			}
			return _CreationTime;
		}
	}

	public DateTime LastWriteTime {
		get {
			if (_LastWriteTime == default) {
				try { _LastWriteTime = _Info.LastWriteTime; }
				catch { _LastWriteTime = DateTime.MaxValue; }
			}
			return _LastWriteTime;
		}
	}

	public string FormattedFileSize => IsFile ? FormatFileSize(FileSize) : null;

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	public bool IsSolutionItem {
		get {
			if (_Type != FileItemType.File) {
				return _Type != FileItemType.UnloadedProject;
			}
			if (_IsSolutionItem == SolutionItemInfo.Unknown) {
				_IsSolutionItem = GetIsSolutionItem();
			}
			return _IsSolutionItem == SolutionItemInfo.Yes;
		}
	}

	public FileState FileState {
		get => _State;
		set => _State = value;
	}

	public FrameworkElement Note {
		get => _Note;
		set => _Note = value;
	}

	public FileItem(FileInfo fileInfo, bool isCurrent) {
		(_Info, _Type, _IsCurrent, _Name) = (fileInfo, FileItemType.File, isCurrent, fileInfo.Name);
	}

	public FileItem(DirectoryInfo dirInfo, bool isEmpty, bool isCurrent) {
		(_Info, _Type, _IsCurrent, _Name) = (dirInfo, isEmpty ? FileItemType.Folder : FileItemType.EmptyFolder, isCurrent, dirInfo.Name);
	}
	public FileItem(DirectoryInfo dirInfo, FileItemType type) {
		(_Info, _Type, _Name) = (dirInfo, type, dirInfo.Name);
	}
	public FileItem(FileSystemInfo fsInfo, FileItemType type, bool isCurrent) {
		(_Info, _Type, _IsCurrent, _Name) = (fsInfo, type, isCurrent, fsInfo.Name);
	}
	public FileItem(FileSystemInfo fsInfo, FileItemType type, bool isCurrent, string alias) {
		(_Info, _Type, _IsCurrent, _Name) = (fsInfo, type, isCurrent, alias);
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	internal void RefreshIsSolutionItem() {
		if (_Type != FileItemType.File
			|| _IsSolutionItem == 0) { // do not update IsSolutionItem if it is not initialized
			return;
		}
		var i = GetIsSolutionItem();
		if (i != _IsSolutionItem) {
			_IsSolutionItem = i;
			PropertyChanged?.Invoke(this, new(nameof(IsSolutionItem)));
		}
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	SolutionItemInfo GetIsSolutionItem() {
		var projItem = ServicesHelper.Instance.DTE.Solution.FindProjectItem(_Info.FullName);
		return projItem != null && projItem.Kind != VsShellHelper.MiscFilesKind
			? SolutionItemInfo.Yes
			: SolutionItemInfo.No;
	}

	internal void ClearIsSolutionItem() {
		if (_Type != FileItemType.File) {
			return;
		}
		if (_IsSolutionItem != 0) {
			_IsSolutionItem = 0;
			PropertyChanged?.Invoke(this, new(nameof(IsSolutionItem)));
		}
	}
	internal void ClearIsCurrent() {
		if (_IsCurrent) {
			_IsCurrent = false;
			PropertyChanged?.Invoke(this, new(nameof(IsCurrent)));
		}
	}
	internal void SetStateFromRunningDocumentInfo(RunningDocumentInfo docInfo, Chain<int> icons) {
		if (!docInfo.IsDocumentInitialized) {
			_State |= FileState.Uninitialized;
			icons.Add(IconIds.Hibernated);
			return;
		}

		if (docInfo.IsReadOnly) {
			_State |= FileState.ReadOnly;
			icons.Add(IconIds.Readonly);
		}
		uint flags = docInfo.Flags;
		if (flags.MatchFlags((uint)(VsRdtFlags.DontAddToMRU | VsRdtFlags.DontAutoOpen))) {
			_State |= FileState.New;
			icons.Add(IconIds.NewFile);
		}
		if (flags.HasAnyFlag((uint)(VsRdtFlags.DontSave | VsRdtFlags.DontSaveAs))) {
			_State |= FileState.DontSave;
			icons.Add(IconIds.DontSave);
		}
		if (flags.MatchFlags((uint)VsRdtFlags.VirtualDocument)) {
			_State |= FileState.Virtual;
			icons.Add(IconIds.FileVirtual);
		}
		if (docInfo.IsDirty) {
			_State |= FileState.Modified;
			icons.Add(IconIds.Modified);
		}
	}

	static string FormatFileSize(long bytes) {
		string[] sizes = { "B", "KB", "MB", "GB", "TB" };
		int order = 0;
		double size = bytes;
		while (size >= 1024 && order < sizes.Length - 1) {
			order++;
			size /= 1024;
		}
		return $"{size:0.##} {sizes[order]}";
	}

	internal static bool DisambiguateNames(List<FileItem> fileItems) {
		return AliasDisambiguator.DisambiguateAliases(fileItems);
	}

	enum SolutionItemInfo : byte
	{
		Unknown,
		Yes,
		No
	}

	/// <summary>
	/// Disambiguates FileItems sharing the same display name by writing a directory
	/// hint (e.g. "2024", "Backup") into FileItem._Hint.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Preconditions:
	/// - Paths come from VS/file-system APIs and are normalized (no duplicated
	///   separators mid-path); case-sensitive ordinal comparison is used throughout.
	/// - FileItem._Name may differ from the on-disk file name (e.g. Diff views use
	///   "name [Diff]" aliases for the same physical path). All path parsing must
	///   therefore rely on the actual path, never on _Name; grouping by _Name is
	///   correct because same-path/different-alias items already differ visually.	/// </para>
	/// <para>
	/// Algorithm (allocation-minimal):
	/// 1. Group items by _Name; skip groups referring to the same physical file.
	/// 2. Per distinct path, locate separator positions once (LastIndexOf from the
	///    tail) and cache a segment table (start/length) — no Split, no per-segment
	///    strings. Segments are indexed from the tail (0 = parent folder).
	/// 3. Strip segments common to the whole group from BOTH ends: head = shared
	///    ancestors, tail = shared leaf folders. E.g. "...\a\b\c\1.txt" vs
	///    "...\a\d\c\1.txt" reduce to hints "b" / "d", omitting the shared "c".
	/// 4. Phase 1: span-based comparison picks, per item, the smallest count of
	///    trailing middle segments guaranteeing unique hints within the group
	///    (equal-count hints collide iff count &lt;= their common suffix length;
	///    different counts never collide — multi-segment hints contain a separator,
	///    single-segment hints never do).
	/// 5. Phase 2: each hint is ONE contiguous substring of the original path —
	///    a single allocation even for multi-segment hints.
	/// </para>
	/// <para>Items with no usable segments are left untouched; no "name (N)" suffixes.</para>
	/// </remarks>
	static class AliasDisambiguator
	{
		public static bool DisambiguateAliases(List<FileItem> items) {
			bool hasDuplicate = false;

			// Segment table cache keyed by full path: each path is parsed at most once
			// per call. Parsing stores (start, length) per segment — no Split, no
			// per-segment strings; segments are indexed from the tail (0 = parent folder).
			var pathCache = new Dictionary<string, PathInfo>(StringComparer.Ordinal);

			foreach (var group in GroupBySinglePass(items)) {
				var indices = group.Value;
				int n = indices.Count;
				if (n < 2) {
					continue;
				}

				// Fetch FullName once per item (the property chain has a cost).
				var fullPaths = new string[n];
				for (int i = 0; i < n; i++) {
					fullPaths[i] = items[indices[i]].FullPath;
				}

				// Same physical file: no disambiguation needed. For n == 2 compare
				// directly to avoid a hash set allocation.
				bool sameFile = n == 2
					? fullPaths[0] == fullPaths[1]
					: SameFileAll(fullPaths);
				if (sameFile) {
					continue;
				}
				hasDuplicate = true;

				var paths = new PathInfo[n];
				for (int i = 0; i < n; i++) {
					paths[i] = GetPathInfo(pathCache, fullPaths[i]);
				}

				// Strip segments common to the whole group from BOTH ends:
				// head = shared ancestors, tail = shared leaf folders. E.g.
				// "...\a\b\c\1.txt" vs "...\a\d\c\1.txt" reduce to "b" / "d",
				// omitting the shared "c". Span-level comparison, no allocations.
				int minCount = paths[0].Count;
				for (int i = 1; i < n; i++) {
					minCount = Math.Min(minCount, paths[i].Count);
				}
				int cp = 0; // common prefix count, from head
				while (cp < minCount && AllEqualHead(paths, n, cp)) {
					cp++;
				}
				int cs = 0; // common suffix count, from tail; never overlaps the prefix
				while (cs < minCount - cp && AllEqualTail(paths, n, cs)) {
					cs++;
				}

				// Middle region of item i = tail indices [cs, Count - cp):
				// the only segments that can carry distinguishing information.
				var depth = new int[n];
				var qualified = new List<int>();
				for (int i = 0; i < n; i++) {
					depth[i] = paths[i].Count - cp - cs;
					if (depth[i] > 0) {
						qualified.Add(i);
					}
				}
				int qn = qualified.Count;
				if (qn == 0) {
					continue; // no usable segments; leave untouched (no fallback)
				}

				// ---------- Phase 1: segment analysis, zero string allocations ----------

				// m[a,b] = common suffix length of the two middle regions.
				// Two equal-count hints collide iff count <= m[a,b]; a shorter hint
				// is a tail-segment suffix of a longer one iff longerCount <= m and
				// shorterCount <= longerCount. Different counts never yield equal
				// strings (multi-segment hints contain a separator, single ones don't).
				var m = new int[qn, qn];
				for (int a = 0; a < qn; a++) {
					var pa = paths[qualified[a]];
					for (int b = a + 1; b < qn; b++) {
						var pb = paths[qualified[b]];
						int lim = Math.Min(depth[qualified[a]], depth[qualified[b]]), s = 0;
						while (s < lim && SegEqual(pa, cs + s, pb, cs + s)) {
							s++;
						}
						m[a, b] = m[b, a] = s;
					}
				}

				// Items whose middle regions are IDENTICAL can never produce distinct
				// hints (e.g. two aliases of files in the same directory): exclude them
				// entirely — no hint, no fallback.
				var excluded = new bool[qn];
				for (int a = 0; a < qn; a++) {
					for (int b = a + 1; b < qn; b++) {
						if (depth[qualified[a]] == depth[qualified[b]] && m[a, b] == depth[qualified[a]]) {
							excluded[a] = excluded[b] = true;
						}
					}
				}

				// Start every item at 1 segment, then iteratively repair "suffix"
				// conflicts: a conflict exists when the shorter hint is exactly the
				// tail-segment sequence of the longer one. Effect: a bare "c" that is
				// a suffix of "e\c" gets extended to "b\c", making every hint a
				// self-contained relative address.
				// Termination: t only grows and is bounded by depth.
				var t = new int[qn];
				for (int i = 0; i < qn; i++) {
					t[i] = 1;
				}
				bool changed = true;
				while (changed) {
					changed = false;
					for (int a = 0; a < qn; a++) {
						if (excluded[a]) {
							continue;
						}
						for (int b = a + 1; b < qn; b++) {
							if (excluded[b]) {
								continue;
							}
							int ta = t[a], tb = t[b], mm = m[a, b];
							// Suffix conflict in either direction?
							if (!(ta <= mm && tb >= ta) && !(tb <= mm && ta >= tb)) {
								continue;
							}
							// Extend the shorter side; if both are equal or the
							// preferred side is at its depth limit, extend the other.
							// If BOTH are at their limits, accept the residual
							// conflict — the rule is unsatisfiable within the
							// available segments, and the strings remain distinct.
							int v;
							if (ta <= tb && t[a] < depth[qualified[a]]) {
								v = a;
							}
							else if (t[b] < depth[qualified[b]]) {
								v = b;
							}
							else if (t[a] < depth[qualified[a]]) {
								v = a;
							}
							else {
								continue;
							}
							t[v]++;
							changed = true;
						}
					}
				}

				// ---------- Phase 2: build each hint as ONE contiguous substring ----------
				// The middle region's trailing segments are consecutive in the original
				// path (separated by the path's own separator characters), so even a
				// multi-segment hint is a single Substring allocation.
				for (int i = 0; i < qn; i++) {
					if (excluded[i]) {
						continue; // unfixable member: keep original name and hint
					}
					int qi = qualified[i];
					var p = paths[qi];
					int tt = t[i];
					int start = p.Start[cs + tt - 1];
					int len = tt == 1
						? p.Len[cs]
						: p.Start[cs] + p.Len[cs] - start; // span through the middle region's end
					items[indices[qi]]._Hint = p.Path.Substring(start, len);
				}
			}
			return hasDuplicate;
		}
		static bool SegEqual(PathInfo a, int ia, PathInfo b, int ib) {
			int len;
			return (len = a.Len[ia]) == b.Len[ib] // length pre-check: int compare before any content scan
				&& String.CompareOrdinal(a.Path, a.Start[ia], b.Path, b.Start[ib], len) == 0;
		}

		static bool AllEqualHead(PathInfo[] paths, int n, int headIndex) {
			var first = paths[0];
			int ti = first.Count - 1 - headIndex;
			for (int i = 1; i < n; i++) {
				var p = paths[i];
				if (!SegEqual(p, p.Count - 1 - headIndex, first, ti)) {
					return false;
				}
			}
			return true;
		}

		static bool AllEqualTail(PathInfo[] paths, int n, int tailIndex) {
			var first = paths[0];
			for (int i = 1; i < n; i++) {
				if (!SegEqual(paths[i], tailIndex, first, tailIndex)) {
					return false;
				}
			}
			return true;
		}

		static PathInfo GetPathInfo(Dictionary<string, PathInfo> cache, string fullPath) {
			if (cache.TryGetValue(fullPath, out var cached)) {
				return cached;
			}
			int cut = fullPath.LastIndexOf(Path.DirectorySeparatorChar); // separator before the file name
			int rootEnd = 0; // skip leading separator run (UNC "\\server\...")
			while (rootEnd < cut && fullPath[rootEnd] == Path.DirectorySeparatorChar) {
				rootEnd++;
			}

			int total = 0;
			for (int e = cut; e > rootEnd;) {
				total++;
				e = SegStart(fullPath, rootEnd, e) - 1;
			}

			var starts = new int[total];
			var lens = new int[total];
			for (int i = 0, e = cut; i < total; i++) {
				int s = SegStart(fullPath, rootEnd, e);
				starts[i] = s;
				lens[i] = e - s;
				e = s - 1;
			}
			return cache[fullPath] = new PathInfo(fullPath, starts, lens);
		}

		/// <summary>Start of the directory segment whose exclusive end is 'end'; never before rootEnd.</summary>
		static int SegStart(string path, int rootEnd, int end) {
			int prev = end - 1 > rootEnd
				? path.LastIndexOf(Path.DirectorySeparatorChar, end - 2, end - 1 - rootEnd)
				: -1;
			return prev < 0 ? rootEnd : prev + 1;
		}

		/// <summary>Single-pass grouping: one hash lookup per item, no tuple allocations.</summary>
		static Dictionary<string, List<int>> GroupBySinglePass(List<FileItem> items) {
			var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
			for (int i = 0; i < items.Count; i++) {
				var name = items[i].Name;
				if (!groups.TryGetValue(name, out var list)) {
					groups[name] = list = new List<int>();
				}
				list.Add(i);
			}
			return groups;
		}

		static bool SameFileAll(string[] fullPaths) {
			var first = fullPaths[0];
			for (int i = 1; i < fullPaths.Length; i++) {
				if (first != fullPaths[i]) {
					return false;
				}
			}
			return true;
		}
	}

	/// <summary>Cached segment table of one path, indexed from the tail (0 = parent folder).</summary>
	readonly record struct PathInfo(string Path, int[] Start, int[] Len)
	{
		public int Count => Start.Length;
	}
}
