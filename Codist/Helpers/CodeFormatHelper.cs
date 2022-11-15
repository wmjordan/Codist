using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;

namespace Codist
{
	static class CodeFormatHelper
	{
		const string Lang = "C#";

		internal static readonly SyntaxAnnotation Reformat = new SyntaxAnnotation();
		internal static readonly SyntaxAnnotation Select = new SyntaxAnnotation();
		internal static readonly SyntaxAnnotation Caret = new SyntaxAnnotation();

		internal static TNode AnnotateReformatAndSelect<TNode>(this TNode node)
			where TNode : SyntaxNode {
			return node.WithAdditionalAnnotations(Reformat, Select);
		}
		internal static TNode AnnotateSelect<TNode>(this TNode node)
			where TNode : SyntaxNode {
			return node.WithAdditionalAnnotations(Select);
		}
		internal static SyntaxList<TNode> AttachAnnotation<TNode>(this SyntaxList<TNode> nodes, params SyntaxAnnotation[] annotations)
			where TNode : SyntaxNode {
			return new SyntaxList<TNode>(nodes.Select(i => i.WithAdditionalAnnotations(annotations)));
		}
		internal static string GetIndentString(this OptionSet options) {
			int indentSize = options.GetOption(FormattingOptions.IndentationSize, Lang);
			return options.GetOption(FormattingOptions.UseTabs, "C#")
				? new string('\t', indentSize / options.GetOption(FormattingOptions.TabSize, Lang))
				: new string(' ', indentSize);
		}
		internal static string GetNewLineString(this OptionSet options) {
			return options.GetOption(FormattingOptions.NewLine, Lang);
		}
	}
}
