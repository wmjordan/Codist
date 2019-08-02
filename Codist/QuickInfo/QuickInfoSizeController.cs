using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;

namespace Codist.QuickInfo
{
	sealed class QuickInfoSizeController : IAsyncQuickInfoSource
	{
		public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			if (Keyboard.Modifiers != ModifierKeys.Control) {
				return new QuickInfoItem(null, QuickInfoOverrider.CreateOverrider(session).Control);
			}
			return null;
		}

		void IDisposable.Dispose() { }
	}
}
