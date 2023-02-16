using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;

namespace Codist
{
	static class CodeFormatHelper
	{
		const string Lang = "C#";

		public static readonly SyntaxAnnotation Reformat = new SyntaxAnnotation();
		public static readonly SyntaxAnnotation Select = new SyntaxAnnotation();
		public static readonly SyntaxAnnotation Caret = new SyntaxAnnotation();

		public static TNode AnnotateReformatAndSelect<TNode>(this TNode node)
			where TNode : SyntaxNode {
			return node.WithAdditionalAnnotations(Reformat, Select);
		}
		public static TNode AnnotateSelect<TNode>(this TNode node)
			where TNode : SyntaxNode {
			return node.WithAdditionalAnnotations(Select);
		}
		public static TNode AnnotateReformat<TNode>(this TNode node)
			where TNode : SyntaxNode {
			return node.WithAdditionalAnnotations(Reformat);
		}

		public static TSyntaxNode Format<TSyntaxNode>(this TSyntaxNode node, Workspace workspace, CancellationToken cancellation = default)
			where TSyntaxNode : SyntaxNode {
			return (TSyntaxNode)Formatter.Format(node, workspace, null, cancellation);
		}
		public static TSyntaxNode Format<TSyntaxNode>(this TSyntaxNode node, IEnumerable<SyntaxNode> nodes, Workspace workspace, CancellationToken cancellation = default)
			where TSyntaxNode : SyntaxNode {
			return (TSyntaxNode)Formatter.Format(node, nodes.Select(n => n.FullSpan), workspace, null, cancellation);
		}
		public static TSyntaxNode Format<TSyntaxNode>(this TSyntaxNode node, SyntaxAnnotation annotation, Workspace workspace, CancellationToken cancellation = default)
			where TSyntaxNode : SyntaxNode {
			return (TSyntaxNode)Formatter.Format(node, annotation, workspace, null, cancellation);
		}

		public static SyntaxList<TNode> AttachAnnotation<TNode>(this SyntaxList<TNode> nodes, params SyntaxAnnotation[] annotations)
			where TNode : SyntaxNode {
			return new SyntaxList<TNode>(nodes.Select(i => i.WithAdditionalAnnotations(annotations)));
		}
		public static string GetIndentString(this OptionSet options, int unit = 1) {
			if (unit == 0) {
				return String.Empty;
			}
			int indentSize = options.GetOption(FormattingOptions.IndentationSize, Lang);
			if (unit > 1) {
				indentSize *= unit;
			}
			return options.GetOption(FormattingOptions.UseTabs, "C#")
				? new string('\t', indentSize / options.GetOption(FormattingOptions.TabSize, Lang))
				: new string(' ', indentSize);
		}
		public static string GetNewLineString(this OptionSet options) {
			return options.GetOption(FormattingOptions.NewLine, Lang);
		}
	}
}
