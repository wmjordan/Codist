using System;
using System.Collections.Generic;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;

namespace Codist.SyntaxHighlight
{
	interface IFormatCache
	{
		string Category { get; }
		IClassificationFormatMap ClassificationFormatMap { get; }
		IEditorFormatMap EditorFormatMap { get; }
		TextFormattingRunProperties DefaultTextProperties { get; }
		Color ViewBackground { get; }

		TextFormattingRunProperties GetCachedProperty(IClassificationType classificationType);
		void Refresh();
	}
}
