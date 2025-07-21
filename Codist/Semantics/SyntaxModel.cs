using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist
{
	/// <summary>
	/// The snapshot of the syntax state of a document.
	/// </summary>
	readonly struct SyntaxModel
	{
		internal static readonly SyntaxModel Empty = default;

		public readonly Workspace Workspace;
		public readonly ITextBuffer SourceBuffer;
		public readonly Document Document;
		public readonly SemanticModel SemanticModel;
		public readonly CompilationUnitSyntax Compilation;
		public readonly VersionStamp Version;

		public SyntaxModel(Workspace workspace, ITextBuffer textBuffer, Document document, SemanticModel semanticModel, CompilationUnitSyntax compilation, VersionStamp version) {
			Workspace = workspace;
			Document = document;
			SourceBuffer = textBuffer;
			SemanticModel = semanticModel;
			Compilation = compilation;
			Version = version;
		}

		public bool IsEmpty => Document == null;

		public bool IsSourceBufferInView(ITextView view) {
			return view.TextBuffer == SourceBuffer;
		}

		public SnapshotSpan MapSourceSpan(TextSpan span, ITextView view) {
			var c = view.BufferGraph.MapUpToBuffer(new SnapshotSpan(SourceBuffer.CurrentSnapshot, span.ToSpan()), SpanTrackingMode.EdgeInclusive, view.TextBuffer);
			return c.Count != 0 ? c[0] : default;
		}

		public NormalizedSnapshotSpanCollection MapDownToSourceSpan(SnapshotSpan span, ITextView view) {
			return view.BufferGraph.MapDownToBuffer(span, SpanTrackingMode.EdgeInclusive, SourceBuffer);
		}
	}
}
