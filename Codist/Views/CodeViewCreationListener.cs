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
		public void TextViewCreated(IWpfTextView textView) {
			textView.Properties.GetOrCreateSingletonProperty(() => {
				return new CodeViewDecorator(
					textView,
					_FormatMapService.GetClassificationFormatMap(textView),
					_TypeRegistryService,
					_EditorFormatMapService.GetEditorFormatMap(textView));
			});
			//IEditorFormatMap formatMap = _EditorFormatMapService.GetEditorFormatMap(textView);
			//ChangeEditorFormat(formatMap, EditorTextViewBackground, m => m[EditorFormatDefinition.BackgroundBrushId] = Brushes.LightYellow);
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
