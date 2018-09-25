using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Input;
using AppHelpers;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Codist.QuickInfo
{
	sealed class QuickInfoVisibilityController : IQuickInfoSource
	{
		public void AugmentQuickInfoSession(IQuickInfoSession session, IList<Object> qiContent, out ITrackingSpan applicableToSpan) {
			// don't show Quick Info when CtrlQuickInfo option is on and shift is not pressed
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.CtrlQuickInfo)
				&& Keyboard.Modifiers.MatchFlags(ModifierKeys.Shift) == false
				// do not show Quick Info when user is hovering on the smart bar
				|| session.TextView.Properties.ContainsProperty(nameof(SmartBars.SmartBar))
				) {
				session.Dismiss();
			}
			applicableToSpan = null;
		}

		void IDisposable.Dispose() { }

		/// <summary>
		/// Controls whether quick info should be displayed. When activated, quick info would not be displayed unless Shift key is pressed.
		/// </summary>
		[Export(typeof(IQuickInfoSourceProvider))]
		[Name("Quick Info Visibility Controller")]
		[Order(Before = "Default Quick Info Presenter")]
		[ContentType(Constants.CodeTypes.Code)]
		sealed class QuickInfoVisibilityControllerProvider : IQuickInfoSourceProvider
		{
			public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
				return new QuickInfoVisibilityController();
			}
		}
	}
}
