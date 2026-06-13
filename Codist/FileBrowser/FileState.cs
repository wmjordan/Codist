using System;

namespace Codist.FileBrowser;

[Flags]
enum FileState
{
	None,
	Modified = 1,
	ReadOnly = 1 << 1,
	Pinned = 1 << 2,
	DontSave = 1 << 3,
	Virtual = 1 << 4,
	New = 1 << 5,
	//Uninitialized = 1 << 6,
	RecentlyClosed = 1 << 6,
}
