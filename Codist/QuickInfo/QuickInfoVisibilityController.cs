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
		protected override async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
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
				// do not show Quick Info when user is hovering on the SmartBar or the SymbolList
				|| session.TextView.Properties.ContainsProperty(SmartBars.SmartBar.QuickInfoSuppressionId)
				|| session.TextView.Properties.ContainsProperty(Controls.TextViewOverlay.QuickInfoSuppressionId)
				) {
				await session.DismissAsync();
			}
			return null;
		}
	}
}
