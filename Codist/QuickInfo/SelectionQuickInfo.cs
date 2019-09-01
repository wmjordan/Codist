using System;
using System.Threading;
using System.Threading.Tasks;
using AppHelpers;
using Codist.Controls;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;

namespace Codist.QuickInfo
{
	internal sealed class SelectionQuickInfo : IAsyncQuickInfoSource
	{
		const string Name = "SelectionInfo";

		public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Selection) == false
				|| session.Mark(nameof(SelectionQuickInfo)) == false) {
				return null;
			}
			var textSnapshot = session.TextView.TextSnapshot;
			var triggerPoint = session.GetTriggerPoint(textSnapshot).GetValueOrDefault();
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			return ShowSelectionInfo(session, triggerPoint);
		}

		void IDisposable.Dispose() { }

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
					.Append("Selected character: \"", true)
					.Append(ch)
					.Append("\" (Unicode: 0x" + ((int)ch[0]).ToString("X2") + ")");
				goto RETURN;
			}
			info = new ThemedTipText() { Name = Name }
				.Append("Selection: ", true)
				.Append(c.ToString() + " characters");
			if (lines > 1) {
				info.Append(", " + lines + " spans");
			}
			else {
				lines = selection.StreamSelectionSpan.SnapshotSpan.GetLineSpan().Length;
				if (lines > 0) {
					info.Append(", " + (lines + 1).ToString() + " lines");
				}
			}
		RETURN:
			return new QuickInfoItem(activeSpan.ToTrackingSpan(), info);
		}

		sealed class Mark { }
	}

}
