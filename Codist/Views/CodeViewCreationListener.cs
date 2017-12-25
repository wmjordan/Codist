using System;
using System.ComponentModel.Composition;
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
			textView.Properties.GetOrCreateSingletonProperty(() => CreateDecorator(textView));
		}

		public CodeViewDecorator CreateDecorator(IWpfTextView textView) {
			return new CodeViewDecorator(textView, formatMapService.GetClassificationFormatMap(textView), typeRegistryService);
		}

#pragma warning disable 649
		[Import]
		private IClassificationFormatMapService formatMapService;

		[Import]
		private IClassificationTypeRegistryService typeRegistryService;
#pragma warning restore 649
	}
}
