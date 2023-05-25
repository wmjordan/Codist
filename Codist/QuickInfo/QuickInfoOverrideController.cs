using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Codist.QuickInfo
{
	sealed class QuickInfoOverrideController : SingletonQuickInfoSource
	{
		protected override async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			return QuickInfoOverride.CheckCtrlSuppression()
				? null
				: new QuickInfoItem(null, QuickInfoOverride.CreateOverride(session).CreateControl(session));
		}
	}
}
