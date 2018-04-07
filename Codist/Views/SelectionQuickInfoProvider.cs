using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Controls;
using Codist.Helpers;
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
	internal sealed class SelectionQuickInfoProvider : IQuickInfoSourceProvider
	{
		const string Name = nameof(SelectionQuickInfoProvider);
		const string QuickInfoName = "SelectionInfo";
		public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return new SelectionQuickInfoController();
		}

		internal sealed class SelectionQuickInfoController : IQuickInfoSource
		{
			public void AugmentQuickInfoSession(IQuickInfoSession session, IList<Object> qiContent, out ITrackingSpan applicableToSpan) {
				var textSnapshot = session.TextView.TextSnapshot;
				var triggerPoint = session.GetTriggerPoint(textSnapshot).GetValueOrDefault();
				applicableToSpan = qiContent.FirstOrDefault(i => (i as TextBlock)?.Name == QuickInfoName) == null
					&& ShowSelectionInfo(session, qiContent, triggerPoint)
					? textSnapshot.CreateTrackingSpan(session.TextView.GetTextElementSpan(triggerPoint), SpanTrackingMode.EdgeInclusive)
					: null;
			}

			void IDisposable.Dispose() {}
		}

		/// <summary>Displays numbers about selected characters and lines in quick info.</summary>
		static bool ShowSelectionInfo(IQuickInfoSession session, IList<object> qiContent, SnapshotPoint point) {
			var selection = session.TextView.Selection;
			if (selection.IsEmpty) {
				return false;
			}
			var p1 = selection.Start.Position;
			var p2 = selection.End.Position;
			if (p1 > point || point > p2) {
				return false;
			}
			var c = 0;
			foreach (var item in selection.SelectedSpans) {
				c += item.Length;
			}
			var y1 = point.Snapshot.GetLineNumberFromPosition(p1);
			var y2 = point.Snapshot.GetLineNumberFromPosition(p2) + 1;
			var info = new TextBlock() { Name = QuickInfoName }.AddText("Selection: ", true).AddText(c.ToString()).AddText(" characters");
			if (y2 - y1 > 1) {
				info.AddText(", " + (y2 - y1).ToString() + " lines");
			}
			qiContent.Add(info);
			return true;
		}
	}

}
