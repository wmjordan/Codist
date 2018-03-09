using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Views
{
	[Export(typeof(IIntellisenseControllerProvider))]
	[Name("C# QuickInfo Controller")]
	[ContentType("CSharp")]
	sealed class CSharpQuickInfoControllerProvider : IIntellisenseControllerProvider
	{
		[Import]
		internal IQuickInfoBroker QuickInfoBroker { get; set; }

		public IIntellisenseController TryCreateIntellisenseController(ITextView textView, IList<ITextBuffer> subjectBuffers) {
			return new QuickInfoController(textView, subjectBuffers, this);
		}

		sealed class QuickInfoController : IIntellisenseController
		{
			CSharpQuickInfoControllerProvider _Provider;
			IQuickInfoSession _Session;
			IList<ITextBuffer> _SubjectBuffers;
			ITextView _TextView;

			internal QuickInfoController(ITextView textView, IList<ITextBuffer> subjectBuffers, CSharpQuickInfoControllerProvider provider) {
				_TextView = textView;
				_SubjectBuffers = subjectBuffers;
				_Provider = provider;
				_TextView.MouseHover += OnTextViewMouseHover;
			}

			public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) {
			}

			public void Detach(ITextView textView) {
				if (_TextView == textView) {
					_TextView.MouseHover -= OnTextViewMouseHover;
					_TextView = null;
				}
			}

			public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) {
			}

			void OnTextViewMouseHover(object sender, MouseHoverEventArgs e) {
				if (Config.Instance.QuickInfoOptions != QuickInfoOptions.None) {
					return;
				}
				//find the mouse position by mapping down to the subject buffer
				var point = _TextView.BufferGraph.MapDownToFirstMatch(
					new SnapshotPoint(_TextView.TextSnapshot, e.Position),
					PointTrackingMode.Positive,
					snapshot => _SubjectBuffers.Contains(snapshot.TextBuffer),
					PositionAffinity.Predecessor
					);

				if (point != null && !_Provider.QuickInfoBroker.IsQuickInfoActive(_TextView)) {
					var triggerPoint = point.Value.Snapshot.CreateTrackingPoint(point.Value.Position, PointTrackingMode.Positive);
					_Session = _Provider.QuickInfoBroker.TriggerQuickInfo(_TextView, triggerPoint, true);
				}
			}
		}
	}
}
