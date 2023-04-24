using System;
using System.Threading;
using System.Threading.Tasks;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.QuickInfo
{
	sealed class CSharpNodeRangeQuickInfo : IAsyncQuickInfoSource
	{
		public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			SemanticContext context;
			return Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color) == false
				|| session.TextView is IWpfTextView view == false
				|| (context = SemanticContext.GetOrCreateSingletonInstance(view)) == null
				? Task.FromResult<QuickInfoItem>(null)
				: InternalGetQuickInfoItemAsync(session, view, context, cancellationToken);
		}

		async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, IWpfTextView view, SemanticContext sc, CancellationToken cancellationToken) {
			await sc.UpdateAsync(cancellationToken).ConfigureAwait(false);
			var node = sc.GetNode(session.GetTriggerPoint(view.TextBuffer).GetPosition(view.TextSnapshot), true, false);
			if (node != null) {
				node = node.GetNodePurpose();
				session.Properties.AddProperty(typeof(CSharpNodeRangeQuickInfo), node.Span);
				session.StateChanged += Session_StateChanged;
			}
			return null;
		}

		void Session_StateChanged(object sender, QuickInfoSessionStateChangedEventArgs e) {
			var s = (IAsyncQuickInfoSession)sender;
			if (s.TextView is IWpfTextView view) {
				switch (e.NewState) {
					case QuickInfoSessionState.Dismissed:
						s.StateChanged -= Session_StateChanged;
						TextViewOverlay.Get(view)?.ClearRangeAdornments();
						break;
					case QuickInfoSessionState.Visible:
						TextViewOverlay.Get(view)?.SetRangeAdornment(s.Properties.GetProperty<TextSpan>(typeof(CSharpNodeRangeQuickInfo)).CreateSnapshotSpan(view.TextSnapshot));
						break;
				}
			}
		}

		void IDisposable.Dispose() {}
	}
}
