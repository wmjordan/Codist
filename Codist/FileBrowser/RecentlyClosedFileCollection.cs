using System.Collections.Generic;

namespace Codist.FileBrowser;

static class RecentlyClosedFileCollection
{
	readonly static LinkedList<string> __ClosedFiles = new();

	public static void Add(string path) {
		__ClosedFiles.Remove(path);
		__ClosedFiles.AddFirst(path);
		if (__ClosedFiles.Count > FileBrowserConfig.MaxRecentClosedFilesCount) {
			__ClosedFiles.RemoveLast();
		}
	}

	public static void Clear() {
		__ClosedFiles.Clear();
	}
	public static bool HasItem => __ClosedFiles.Count != 0;
	public static IEnumerable<string> Items => __ClosedFiles;
}
