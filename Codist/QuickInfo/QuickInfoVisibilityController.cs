using System;
using System.Threading;
using System.Threading.Tasks;
using CLR;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Codist.QuickInfo;

sealed class QuickInfoVisibilityController : SingletonQuickInfoSource
{
	protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
		// hide Quick Info when:
		//   CtrlQuickInfo option is on and shift is not pressed,
		//   or CtrlQuickInfo is off and shift is pressed
		if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.CtrlQuickInfo)
			^ UIHelper.IsShiftDown
			) {
			return DismissQuickInfoOnSpecialUIElementsAsync(session);
		}
		// If quick info is not triggered by mouse (usually by keyboard instead),
		//   do not control the visibility and display delay
		if (!session.Options.MatchFlags(QuickInfoSessionOptions.TrackMouse)) {
			return Task.FromResult<QuickInfoItem>(null);
		}
		// do not show Quick Info when user is hovering on the SmartBar or the SymbolList
		if (session.TextView.Properties.ContainsProperty(SmartBars.SmartBar.QuickInfoSuppressionId)
			|| session.TextView.Properties.ContainsProperty(Controls.TextViewOverlay.QuickInfoSuppressionId)) {
			return DismissQuickInfoOnSpecialUIElementsAsync(session);
		}
		return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
			? GetDelayedQuickInfoItemAsync(cancellationToken)
			: Task.FromResult<QuickInfoItem>(null);
	}

	static async Task<QuickInfoItem> DismissQuickInfoOnSpecialUIElementsAsync(IAsyncQuickInfoSession session) {
		await session.DismissAsync();
		return null;
	}

	static Task<QuickInfoItem> GetDelayedQuickInfoItemAsync(CancellationToken cancellationToken) {
		var delay = Config.Instance.QuickInfo.DelayDisplay;
		return delay > 50
			? DelayQuickInfoAsync(delay, cancellationToken)
			: Task.FromResult<QuickInfoItem>(null);
	}

	static async Task<QuickInfoItem> DelayQuickInfoAsync(int delay, CancellationToken cancellationToken) {
		await Task.Delay(delay, cancellationToken);
		return null;
	}
}
