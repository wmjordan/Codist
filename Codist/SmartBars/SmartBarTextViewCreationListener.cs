using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using AppHelpers;
using Microsoft.VisualStudio.Text.Operations;

namespace Codist.SmartBars
{
	/// <summary>
	/// Establishes an <see cref="IAdornmentLayer"/> to place the adornment on and exports the <see cref="IWpfTextViewCreationListener"/>
	/// that instantiates the adornment on the event of a <see cref="IWpfTextView"/>'s creation
	/// </summary>
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType(Constants.CodeTypes.Text)]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
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

		[Import(typeof(ITextSearchService2))]
		ITextSearchService2 _TextSearchService;

#pragma warning restore 649, 169

		/// <summary>
		/// Instantiates a SelectionBar when a textView is created.
		/// </summary>
		/// <param name="textView">The <see cref="IWpfTextView"/> upon which the adornment should be placed</param>
		public void TextViewCreated(IWpfTextView textView) {
			textView.VisualElement.Loaded += TextViewLoaded;
			if (Config.Instance.Features.MatchFlags(Features.SmartBar) == false) {
				return;
			}
			// The toolbar will get wired to the text view events
			var contentType = textView.TextBuffer.ContentType;
			if (Constants.CodeTypes.CSharp.Equals(contentType.TypeName, StringComparison.OrdinalIgnoreCase)) {
				SemanticContext.GetOrCreateSingetonInstance(textView);
				new CSharpSmartBar(textView, _TextSearchService);
			}
			else if (textView.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown)) {
				new MarkdownSmartBar(textView, _TextSearchService);
			}
			else if (contentType.IsOfType("output")
				|| contentType.IsOfType(Constants.CodeTypes.FindResults)
				|| contentType.IsOfType("Interactive Content")
				|| contentType.IsOfType("DebugOutput")
				|| contentType.IsOfType("Command")
				|| contentType.IsOfType("PackageConsole")
				) {
				new OutputSmartBar(textView, _TextSearchService);
			}
			else if (contentType.IsOfType(Constants.CodeTypes.CPlusPlus)) {
				new CppSmartBar(textView, _TextSearchService);
			}
			else {
				new SmartBar(textView, _TextSearchService);
			}
		}

		void TextViewLoaded(object sender, EventArgs args) {
			var e = sender as System.Windows.FrameworkElement;
			e.Loaded -= TextViewLoaded;
			if ((Config.Instance.DisplayOptimizations & DisplayOptimizations.CodeWindow) != 0) {
				WpfHelper.SetUITextRenderOptions(e, true);
			}
		}
	}
}
