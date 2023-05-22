using System;
using System.ComponentModel.Composition;
using CLR;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.SyntaxHighlight
{
	/// <summary>
	/// Applies customized syntax highlight styles to editor.
	/// </summary>
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.FindResults)]
	[ContentType(Constants.CodeTypes.Output)]
	[ContentType(Constants.CodeTypes.InteractiveContent)]
	[ContentType(Constants.CodeTypes.Markdown)]
	[ContentType(Constants.CodeTypes.VsMarkdown)]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class HighlighterFactory : IWpfTextViewCreationListener
	{
		public void TextViewCreated(IWpfTextView textView) {
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false
				|| textView.TextBuffer.MayBeEditor() == false) {
				return;
			}
			textView.Properties.GetOrCreateSingletonProperty(typeof(FormatStore), () => nameof(FormatStore));
			FormatStore.Highlight(textView);
			//var formatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(textView);
			//ChangeEditorFormat(formatMap, "TextView Background", m => m[EditorFormatDefinition.BackgroundBrushId] = Brushes.LightGreen);
		}

		static void ChangeEditorFormat(IEditorFormatMap formatMap, string propertyId, Action<System.Windows.ResourceDictionary> changer) {
			var m = formatMap.GetProperties(propertyId);
			if (m != null) {
				changer(m);
			}
			formatMap.SetProperties(propertyId, m);
		}
	}
}
