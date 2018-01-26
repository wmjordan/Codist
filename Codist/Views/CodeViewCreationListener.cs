using System;
using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Views
{
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType("code")]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	internal sealed class CodeViewCreationListener : IWpfTextViewCreationListener
	{
		const string EditorTextViewBackground = "TextView Background";
		const string EditorCaret = "Caret";
		const string EditorOverwriteCaret = "Overwrite Caret";
		const string EditorSelectedText = "Selected Text";
		const string EditorInactiveSelectedText = "Inactive Selected Text";
		const string EditorVisibleWhitespace = "Visible Whitespace";

		public void TextViewCreated(IWpfTextView textView) {
			textView.Properties.GetOrCreateSingletonProperty(() => CreateDecorator(textView));
			//IEditorFormatMap formatMap = _EditorFormatMapService.GetEditorFormatMap(textView);
			//ChangeEditorFormat(formatMap, EditorTextViewBackground, m => m[EditorFormatDefinition.BackgroundBrushId] = Brushes.LightYellow);
		}

		public CodeViewDecorator CreateDecorator(IWpfTextView textView) {
			return new CodeViewDecorator(textView, _FormatMapService.GetClassificationFormatMap(textView), _TypeRegistryService);
		}

		static void ChangeEditorFormat(IEditorFormatMap formatMap, string propertyId, Action<System.Windows.ResourceDictionary> changer) {
			var m = formatMap.GetProperties(propertyId);
			if (m != null) {
				changer(m);
			}
			formatMap.SetProperties(propertyId, m);
		}

#pragma warning disable 649
		[Import]
		IClassificationFormatMapService _FormatMapService;

		[Import]
		IClassificationTypeRegistryService _TypeRegistryService;

		[Import]
		IEditorFormatMapService _EditorFormatMapService;

#pragma warning restore 649
	}
}
