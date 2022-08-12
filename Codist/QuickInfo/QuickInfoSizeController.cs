using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;

namespace Codist.QuickInfo
{
	sealed class QuickInfoSizeController : IAsyncQuickInfoSource
	{
		ITextBuffer _TextBuffer;

		public QuickInfoSizeController(ITextBuffer textBuffer) {
			_TextBuffer = textBuffer;
		}

		public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			return QuickInfoOverrider.CheckCtrlSuppression()
				? null
				: new QuickInfoItem(null, QuickInfoOverrider.CreateOverrider(session).Control);
		}

		void IDisposable.Dispose() {
			if (_TextBuffer != null) {
				_TextBuffer.Properties.RemoveProperty(typeof(QuickInfoSizeController));
				_TextBuffer = null;
			}
		}
	}
}
