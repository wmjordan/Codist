using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Controls;
using AppHelpers;
using Codist.Controls;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Codist.QuickInfo
{
	internal sealed class SelectionQuickInfo : IQuickInfoSource
	{
		const string Name = "SelectionInfo";
		public void AugmentQuickInfoSession(IQuickInfoSession session, IList<Object> qiContent, out ITrackingSpan applicableToSpan) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Selection) == false) {
				applicableToSpan = null;
				return;
			}
			var textSnapshot = session.TextView.TextSnapshot;
			var triggerPoint = session.GetTriggerPoint(textSnapshot).GetValueOrDefault();
			if (qiContent.Any(i => (i as TextBlock)?.Name == Name)) {
				applicableToSpan = null;
				return;
			}
			var span = ShowSelectionInfo(session, qiContent, triggerPoint);
			if (span.IsEmpty) {
				applicableToSpan = null;
				return;
			}
			applicableToSpan = textSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);
		}

		void IDisposable.Dispose() { }

		/// <summary>Displays numbers about selected characters and lines in quick info.</summary>
		static SnapshotSpan ShowSelectionInfo(IQuickInfoSession session, IList<object> qiContent, SnapshotPoint point) {
			var selection = session.TextView.Selection;
			var activeSpan = default(SnapshotSpan);
			if (selection.IsEmpty) {
				return activeSpan;
			}
			var p1 = selection.Start.Position;
			if (p1 > point || point > selection.End.Position) {
				var tes = session.TextView.GetTextElementSpan(point);
				if (tes.Contains(p1) == false) {
					return activeSpan;
				}
			}
			var c = 0;
			var lines = selection.SelectedSpans.Count;
			foreach (var item in selection.SelectedSpans) {
				c += item.Length;
				if (item.Contains(point)) {
					activeSpan = item;
				}
			}
			if (c == 1) {
				var ch = point.Snapshot.GetText(p1, 1);
				qiContent.Add(new ThemedTipText { Name = Name }
					.Append("Selected character: \"", true)
					.Append(ch)
					.Append("\" (Unicode: 0x" + ((int)ch[0]).ToString("X2") + ")")
				);
				return activeSpan;
			}
			var info = new ThemedTipText() { Name = Name }
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
			qiContent.Add(info);
			return activeSpan;
		}
	}

}
