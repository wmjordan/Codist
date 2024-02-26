using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CLR;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Codist.QuickInfo
{
	sealed class QuickInfoVisibilityController : SingletonQuickInfoSource
	{
		protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			// do not show Quick Info when user is hovering on the SmartBar or the SymbolList
			if (session.TextView.Properties.ContainsProperty(SmartBars.SmartBar.QuickInfoSuppressionId)
				|| session.TextView.Properties.ContainsProperty(Controls.TextViewOverlay.QuickInfoSuppressionId)) {
				return DismissQuickInfoOnSpecialUIElementsAsync(session);
			}
			if (Config.Instance.Features.MatchFlags(Features.SuperQuickInfo) == false) {
				return Task.FromResult<QuickInfoItem>(null);
			}
			return GetAsync(session, cancellationToken);
		}

		static async Task<QuickInfoItem> DismissQuickInfoOnSpecialUIElementsAsync(IAsyncQuickInfoSession session) {
			await session.DismissAsync();
			return null;
		}

		static async Task<QuickInfoItem> GetAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			var delay = Config.Instance.QuickInfo.DelayDisplay;
			if (delay > 50) {
				await Task.Delay(delay, cancellationToken);
			}
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			// hide Quick Info when:
			//   CtrlQuickInfo option is on and shift is not pressed,
			//   or CtrlQuickInfo is off and shift is pressed
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.CtrlQuickInfo)
				^ Keyboard.Modifiers.MatchFlags(ModifierKeys.Shift)
				) {
				await session.DismissAsync();
			}
			return null;
		}
	}
}
