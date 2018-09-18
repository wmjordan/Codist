using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using AppHelpers;

namespace Codist.SmartBars
{
	/// <summary>
	/// Establishes an <see cref="IAdornmentLayer"/> to place the adornment on and exports the <see cref="IWpfTextViewCreationListener"/>
	/// that instantiates the adornment on the event of a <see cref="IWpfTextView"/>'s creation
	/// </summary>
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType(Constants.CodeTypes.Text)]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	internal sealed class SmartBarTextViewCreationListener : IWpfTextViewCreationListener
	{
		// Disable "Field is never assigned to..." and "Field is never used" compiler's warnings. Justification: the field is used by MEF.
#pragma warning disable 649, 169

		/// <summary>
		/// Defines the adornment layer for the scarlet adornment. This layer is ordered
		/// after the selection layer in the Z-order
		/// </summary>
		[Export(typeof(AdornmentLayerDefinition))]
		[Name(nameof(SmartBar))]
		[Order(After = PredefinedAdornmentLayers.Caret)]
		AdornmentLayerDefinition _EditorAdornmentLayer;

#pragma warning restore 649, 169

		/// <summary>
		/// Instantiates a SelectionBar when a textView is created.
		/// </summary>
		/// <param name="textView">The <see cref="IWpfTextView"/> upon which the adornment should be placed</param>
		public void TextViewCreated(IWpfTextView textView) {
			if (Config.Instance.Features.MatchFlags(Features.SmartBar) == false) {
				return;
			}
			// The toolbar will get wired to the text view events
			if (String.Equals(Constants.CodeTypes.CSharp, textView.TextBuffer.ContentType.TypeName, StringComparison.OrdinalIgnoreCase)) {
				new CSharpSmartBar(textView);
			}
			else {
				new SmartBar(textView);
			}
		}
	}
}
