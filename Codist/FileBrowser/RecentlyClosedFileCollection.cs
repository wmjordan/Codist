using System;
using System.Collections.Generic;
using CLR;

namespace Codist.FileBrowser;

static class RecentlyClosedFileCollection
{
	readonly static LinkedList<string> __ClosedFiles = new();
	static int __MaxFileCount = Init();

	static int Init() {
		Config.RegisterUpdateHandler(UpdateConfig);
		return Config.Instance.FileBrowser.ListRecentClosedFiles;
	}

	static void UpdateConfig(ConfigUpdatedEventArgs args) {
		if (!args.UpdatedFeature.MatchFlags(Features.FileBrowser)) {
			return;
		}

		__MaxFileCount = Config.Instance.FileBrowser.ListRecentClosedFiles;
		if (__MaxFileCount == 0) {
			Clear();
		}
		else {
			while (__ClosedFiles.Count > __MaxFileCount) {
				__ClosedFiles.RemoveLast();
			}
		}
	}

	public static void Add(string path) {
		__ClosedFiles.Remove(path);
		__ClosedFiles.AddFirst(path);
		if (__ClosedFiles.Count > __MaxFileCount) {
			__ClosedFiles.RemoveLast();
		}
	}

	public static void Clear() {
		__ClosedFiles.Clear();
	}

	public static bool ShouldTrackFileClose => __MaxFileCount != 0;
	public static bool HasItem => __ClosedFiles.Count != 0;
	public static IEnumerable<string> Items => __ClosedFiles;
}
