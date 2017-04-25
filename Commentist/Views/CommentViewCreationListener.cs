using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Views
{
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType("code")]
	[TextViewRole("DOCUMENT")]
	internal sealed class CommentViewCreationListener : IWpfTextViewCreationListener
	{
		public void TextViewCreated(IWpfTextView textView) {
			textView.Properties.GetOrCreateSingletonProperty(() => CreateDecorator(textView));
		}

		public CommentViewDecorator CreateDecorator(IWpfTextView textView) {
			return new CommentViewDecorator(textView, formatMapService.GetClassificationFormatMap(textView), typeRegistryService);
		}

#pragma warning disable 649
		[Import]
		private IClassificationFormatMapService formatMapService;

		[Import]
		private IClassificationTypeRegistryService typeRegistryService;
#pragma warning restore 649
	}
}
