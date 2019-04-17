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
				// do not show Quick Info for C# since CSharpQuickInfo will deal with this
				&& session.TextView.TextBuffer.ContentType.IsOfType(Constants.CodeTypes.CSharp) == false
				) {
				QuickInfoOverrider.CreateOverrider(qiContent)
					.LimitQuickInfoItemSize(qiContent);
			}
			applicableToSpan = null;
		}

		void IDisposable.Dispose() { }
	}
}
