using System;
using System.ComponentModel.Composition;
using CLR;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.SmartBars;

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
		if (textView.TextBuffer.IsContentTypeIncludingProjection(Constants.CodeTypes.CSharp)) {
			SemanticContext.GetOrCreateSingletonInstance(textView);
			new CSharpSmartBar(textView);
		}
		else if (textView.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown)) {
			new MarkdownSmartBar(textView);
		}
		else if (contentType.TypeName == Constants.CodeTypes.PlainText) {
			new TextSmartBar(textView);
		}
		else if (contentType.IsOfType(Constants.CodeTypes.Output)
			|| contentType.IsOfType(Constants.CodeTypes.FindResults)
			|| contentType.IsOfType(Constants.CodeTypes.InteractiveContent)
			|| contentType.IsOfType("DebugOutput")
			|| contentType.IsOfType("Command")
			|| contentType.IsOfType("PackageConsole")
			) {
			new OutputSmartBar(textView);
		}
		else if (contentType.IsOfType(Constants.CodeTypes.CPlusPlus)) {
			new CppSmartBar(textView);
		}
		else if (contentType.IsOfType(Constants.CodeTypes.Xml)
			|| contentType.IsOfType(Constants.CodeTypes.Xaml)) {
			new MarkupSmartBar(textView);
		}
		else {
			new SmartBar(textView);
		}
	}
}
