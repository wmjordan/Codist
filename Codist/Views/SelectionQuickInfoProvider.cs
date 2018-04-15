using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Controls;
using AppHelpers;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Views
{
	/// <summary>Shows information about selections.</summary>
	[Export(typeof(IQuickInfoSourceProvider))]
	[Name(Name)]
	[Order(After = CSharpQuickInfoSourceProvider.Name)]
	[ContentType(Constants.CodeTypes.Text)]
	sealed class SelectionQuickInfoProvider : IQuickInfoSourceProvider
	{
		const string Name = nameof(SelectionQuickInfoProvider);
		const string QuickInfoName = "SelectionInfo";

		public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperTooltip)
				? new SelectionQuickInfoController()
				: null;
		}

		internal sealed class SelectionQuickInfoController : IQuickInfoSource
		{
			public void AugmentQuickInfoSession(IQuickInfoSession session, IList<Object> qiContent, out ITrackingSpan applicableToSpan) {
				var textSnapshot = session.TextView.TextSnapshot;
				var triggerPoint = session.GetTriggerPoint(textSnapshot).GetValueOrDefault();
				if (qiContent.FirstOrDefault(i => (i as TextBlock)?.Name == QuickInfoName) != null) {
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

			void IDisposable.Dispose() {}
		}

		/// <summary>Displays numbers about selected characters and lines in quick info.</summary>
		static SnapshotSpan ShowSelectionInfo(IQuickInfoSession session, IList<object> qiContent, SnapshotPoint point) {
			var selection = session.TextView.Selection;
			var activeSpan = default(SnapshotSpan);
			if (selection.IsEmpty) {
				return activeSpan;
			}
			var p1 = selection.Start.Position;
			var p2 = selection.End.Position;
			if (p1 > point || point > p2) {
				var tes = session.TextView.GetTextElementSpan(point);
				if (tes.Contains(p1) == false) {
					return activeSpan;
				}
			}
			var c = 0;
			foreach (var item in selection.SelectedSpans) {
				c += item.Length;
				if (item.Contains(point)) {
					activeSpan = item;
				}
			}
			if (c == 1) {
				var ch = point.Snapshot.GetText(p1, 1);
				qiContent.Add(new TextBlock { Name = QuickInfoName }
					.AddText("Selected character: \"", true)
					.AddText(ch)
					.AddText("\" (Unicode: 0x" + ((int)ch[0]).ToString("X2") + ")")
				);
				return activeSpan;
			}
			var y1 = point.Snapshot.GetLineNumberFromPosition(p1);
			var y2 = point.Snapshot.GetLineNumberFromPosition(p2) + 1;
			var info = new TextBlock() { Name = QuickInfoName }.AddText("Selection: ", true).AddText(c.ToString()).AddText(" characters");
			if (y2 - y1 > 1) {
				info.AddText(", " + (y2 - y1).ToString() + " lines");
			}
			qiContent.Add(info);
			return activeSpan;
		}
	}

}
