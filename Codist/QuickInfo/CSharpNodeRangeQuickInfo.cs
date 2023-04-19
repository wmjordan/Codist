using System;
using System.Threading;
using System.Threading.Tasks;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.QuickInfo
{
	sealed class CSharpNodeRangeQuickInfo : IAsyncQuickInfoSource
	{
		public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			return Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color) == false
				? System.Threading.Tasks.Task.FromResult<QuickInfoItem>(null)
				: InternalGetQuickInfoItemAsync(session, cancellationToken);
		}

		async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			if (QuickInfoOverrider.CheckCtrlSuppression()) {
				return null;
			}
			if (session.TextView is IWpfTextView view) {
				var sc = SemanticContext.GetHovered();
				if (sc != null) {
					await sc.UpdateAsync(cancellationToken);
					var node = sc.GetNode(session.GetTriggerPoint(view.TextBuffer).GetPosition(view.TextSnapshot), true, false);
					if (node != null) {
						node = node.GetNodePurpose();
						session.Properties.AddProperty(typeof(CSharpNodeRangeQuickInfo), node.Span);
						session.StateChanged += Session_StateChanged;
					}
				}
			}
			return null;
		}

		void Session_StateChanged(object sender, QuickInfoSessionStateChangedEventArgs e) {
			var s = sender as IAsyncQuickInfoSession;
			if (s.TextView is IWpfTextView wpfView) {
				switch (e.NewState) {
					case QuickInfoSessionState.Dismissed:
						s.StateChanged -= Session_StateChanged;
						TextViewOverlay.Get(wpfView)?.ClearRangeAdornments();
						break;
					case QuickInfoSessionState.Visible:
						TextViewOverlay.Get(wpfView)?.SetRangeAdornment(s.Properties.GetProperty<TextSpan>(typeof(CSharpNodeRangeQuickInfo)).CreateSnapshotSpan(wpfView.TextSnapshot));
						break;
				}
			}
		}

		void IDisposable.Dispose() {}
	}
}
