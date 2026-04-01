using Microsoft.VisualStudio.Text;

namespace Codist.SnippetTexts;

readonly struct PlaceholderInfo(string name, int position, int length)
{
	public string Name { get; } = name;
	public int Position { get; } = position;
	public int Length { get; } = length;

	public SnapshotSpan ToSnapshotSpan(ITextSnapshot snapshot) {
		return new SnapshotSpan(snapshot, Position, Length);
	}
}
