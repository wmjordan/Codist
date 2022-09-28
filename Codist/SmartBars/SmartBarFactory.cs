using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using AppHelpers;
using Microsoft.VisualStudio.Text.Operations;

namespace Codist.SmartBars
{
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.Markdown)]
	[ContentType(Constants.CodeTypes.Output)]
	[ContentType(Constants.CodeTypes.HtmlxProjection)]
	[ContentType(Constants.CodeTypes.InteractiveContent)]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	internal sealed class SmartBarFactory : IWpfTextViewCreationListener
	{
		// Disable "Field is never assigned to..." and "Field is never used" compiler's warnings. Justification: the field is used by MEF.
#pragma warning disable 649, 169, IDE0044

		[Import(typeof(ITextSearchService2))]
		ITextSearchService2 _TextSearchService;

#pragma warning restore 649, 169, IDE0044

		/// <summary>
		/// Instantiates a SelectionBar when a textView is created.
		/// </summary>
		/// <param name="textView">The <see cref="IWpfTextView"/> upon which the adornment should be placed</param>
		public void TextViewCreated(IWpfTextView textView) {
			if (Config.Instance.Features.MatchFlags(Features.SmartBar) == false || textView.TextBuffer.MayBeEditor() == false) {
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
			else if (contentType.IsOfType(Constants.CodeTypes.Output)
				|| contentType.IsOfType(Constants.CodeTypes.FindResults)
				|| contentType.IsOfType(Constants.CodeTypes.InteractiveContent)
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
	}
}
