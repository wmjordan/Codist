using System;
using System.ComponentModel.Composition;
using AppHelpers;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Codist.SmartBars
{
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.Markdown)]
	[ContentType(Constants.CodeTypes.Text)]
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
		/// Instantiates a view-specific <see cref="SmartBar"/> when a textView is created.
		/// </summary>
		/// <param name="textView">The <see cref="IWpfTextView"/> where the <see cref="SmartBar"/> should be placed.</param>
		public void TextViewCreated(IWpfTextView textView) {
			if (Config.Instance.Features.MatchFlags(Features.SmartBar) == false
				|| textView.TextBuffer.MayBeEditor() == false
				|| textView.Roles.Contains("WATCHWINDOWEDIT")) {
				return;
			}
			// The toolbar will get wired to the text view events
			var contentType = textView.TextBuffer.ContentType;
			if (contentType.IsOfType("snippet picker")) {
				return;
			}
			if (Constants.CodeTypes.CSharp.Equals(contentType.TypeName, StringComparison.OrdinalIgnoreCase)) {
				SemanticContext.GetOrCreateSingletonInstance(textView);
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
