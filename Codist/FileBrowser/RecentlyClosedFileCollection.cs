using System.Collections.Generic;

namespace Codist.FileBrowser;

static class RecentlyClosedFileCollection
{
	readonly static LinkedList<string> __ClosedFiles = new();
	readonly static Dictionary<string, int> __FilePositions = [];

	public static void Add(string path, int caretPosition) {
		__ClosedFiles.Remove(path);
		__ClosedFiles.AddFirst(path);
		__FilePositions[path] = caretPosition;
		if (__ClosedFiles.Count > FileBrowserConfig.MaxRecentClosedFilesCount) {
			var last = __ClosedFiles.Last;
			__ClosedFiles.RemoveLast();
			__FilePositions.Remove(last.Value);
		}
	}

	public static void Clear() {
		__ClosedFiles.Clear();
		__FilePositions.Clear();
	}

	public static void Reopen(string path) {
		if (__FilePositions.TryGetValue(path, out var p)) {
			TextEditorHelper.OpenFile(path, p);
			__FilePositions.Remove(path);
			__ClosedFiles.Remove(path);
		}
	}
	public static bool HasItem => __ClosedFiles.Count != 0;
	public static IEnumerable<string> Items => __ClosedFiles;
}
