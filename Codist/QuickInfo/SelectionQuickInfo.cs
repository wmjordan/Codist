using System;
using System.Threading;
using System.Threading.Tasks;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	sealed class SelectionQuickInfo : SingletonQuickInfoSource
	{
		const string Name = "SelectionInfo";

		protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			return Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Selection) == false
				|| session.Mark(nameof(SelectionQuickInfo)) == false
				? Task.FromResult<QuickInfoItem>(null)
				: InternalGetQuickInfoItemAsync(session, cancellationToken);
		}

		static async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			if (QuickInfoOverride.CheckCtrlSuppression()) {
				return null;
			}
			var textSnapshot = session.TextView.TextSnapshot;
			var triggerPoint = session.GetTriggerPoint(textSnapshot).GetValueOrDefault();
			try {
				return ShowSelectionInfo(session, triggerPoint);
			}
			catch (ArgumentException /*triggerPoint has a differ TextBuffer from textSnapshot*/) {
				return null;
			}
		}

		/// <summary>Displays numbers about selected characters and lines in quick info.</summary>
		static QuickInfoItem ShowSelectionInfo(IAsyncQuickInfoSession session, SnapshotPoint point) {
			var selection = session.TextView.Selection;
			if (selection.IsEmpty) {
				return null;
			}
			var p1 = selection.Start.Position;
			if (p1 > point || point > selection.End.Position) {
				var tes = session.TextView.GetTextElementSpan(point);
				if (tes.Contains(p1) == false) {
					return null;
				}
			}
			var c = 0;
			var lines = selection.SelectedSpans.Count;
			SnapshotSpan activeSpan = default;
			foreach (var item in selection.SelectedSpans) {
				c += item.Length;
				if (item.Contains(point)) {
					activeSpan = item;
				}
			}
			if (activeSpan.IsEmpty) {
				activeSpan = selection.SelectedSpans[0];
			}
			ThemedTipText info;
			if (c == 1) {
				var ch = point.Snapshot.GetText(p1, 1);
				info = new ThemedTipText { Name = Name }
					.Append(R.T_SelectedCharacter + "\"", true)
					.Append(ch)
					.Append("\" (Unicode: 0x" + ((int)ch[0]).ToString("X2") + ")");
				goto RETURN;
			}
			info = new ThemedTipText() { Name = Name }
				.Append(R.T_Selection, true)
				.Append($": {c} {R.T_Characters}");
			if (lines > 1) {
				info.Append($", {lines} {R.T_Spans}");
			}
			else {
				lines = selection.StreamSelectionSpan.SnapshotSpan.GetLineSpan().Length;
				if (lines > 0) {
					info.Append(", " + (lines + 1).ToString() + R.T_Lines);
				}
			}
		RETURN:
			return new QuickInfoItem(activeSpan.ToTrackingSpan(), info.SetGlyph(VsImageHelper.GetImage(IconIds.SelectCode)).Tag());
		}
	}
}
