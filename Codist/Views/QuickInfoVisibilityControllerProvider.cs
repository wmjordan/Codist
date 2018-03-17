using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using AppHelpers;

namespace Codist.Views
{
	/// <summary>
	/// Controls whether quick info should be displayed. When activated, quick info would not be displayed unless Shift key is pressed.
	/// </summary>
	[Export(typeof(IQuickInfoSourceProvider))]
	[Name("Quick Info Visibility Controller")]
	[Order(Before = "Default Quick Info Presenter")]
	[ContentType(Constants.CodeTypes.Code)]
	internal sealed class QuickInfoVisibilityControllerProvider : IQuickInfoSourceProvider
	{
		public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return new QuickInfoVisibilityController();
		}

		internal sealed class QuickInfoVisibilityController : IQuickInfoSource
		{
			public void AugmentQuickInfoSession(IQuickInfoSession session, IList<Object> quickInfoContent, out ITrackingSpan applicableToSpan) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.CtrlQuickInfo)
					&& Keyboard.Modifiers.MatchFlags(ModifierKeys.Shift) == false) {
					session.Dismiss();
				}
				applicableToSpan = null;
			}

			void IDisposable.Dispose() {}

		}
	}

}
