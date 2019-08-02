using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AppHelpers;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;

namespace Codist.QuickInfo
{
	sealed class QuickInfoVisibilityController : IAsyncQuickInfoSource
	{
		public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			// don't show Quick Info when CtrlQuickInfo option is on and shift is not pressed
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.CtrlQuickInfo)
				&& Keyboard.Modifiers.MatchFlags(ModifierKeys.Shift) == false
				// do not show Quick Info when user is hovering on the SmartBar or the SymbolList
				|| session.TextView.Properties.ContainsProperty(nameof(SmartBars.SmartBar))
				|| session.TextView.Properties.ContainsProperty(nameof(Controls.ExternalAdornment))
				) {
				await session.DismissAsync();
			}
			return null;
		}

		void IDisposable.Dispose() { }
	}
}
