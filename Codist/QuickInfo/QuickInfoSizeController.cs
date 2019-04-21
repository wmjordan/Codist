using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Codist.QuickInfo
{
	sealed class QuickInfoSizeController : IQuickInfoSource
	{
		public void AugmentQuickInfoSession(IQuickInfoSession session, IList<Object> qiContent, out ITrackingSpan applicableToSpan) {
			if ((Config.Instance.QuickInfoMaxWidth > 0 || Config.Instance.QuickInfoMaxHeight > 0)
				&& System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Control
				) {
				QuickInfoOverrider.CreateOverrider(qiContent)
					.LimitQuickInfoItemSize(qiContent);
			}
			applicableToSpan = null;
		}

		void IDisposable.Dispose() { }
	}
}
