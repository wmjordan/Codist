using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist.Classifiers
{
	class ClassifierContext
	{
		private readonly ITextSnapshot _snapshot;
		private readonly Workspace _workspace;
		private readonly Document _document;
		private readonly SemanticModel _semanticModel;
		private readonly SyntaxNode _syntaxRoot;

		private static readonly IDictionary<ITextSnapshot, ClassifierContext> _cachedContexts = new Dictionary<ITextSnapshot, ClassifierContext>();
		private readonly IDictionary<SnapshotSpan, IEnumerable<ClassifiedSpan>> _cachedClassifiedSpans = new Dictionary<SnapshotSpan, IEnumerable<ClassifiedSpan>>();

		public static ClassifierContext GetContext(ITextSnapshot snapshot) {
			if (!_cachedContexts.ContainsKey(snapshot)) {
				_cachedContexts[snapshot] = new ClassifierContext(snapshot);
			}

			return _cachedContexts[snapshot];
		}

		private ClassifierContext(ITextSnapshot snapshot) {
			_snapshot = snapshot;
			_workspace = snapshot.TextBuffer.GetWorkspace();
			_document = snapshot.GetOpenDocumentInCurrentContextWithChanges();
			if (_document != null) {
				_semanticModel = _document.GetSemanticModelAsync().Result;
				_syntaxRoot = _document.GetSyntaxRootAsync().Result;
			}
		}

		public IEnumerable<ClassifiedSpan> GetDefaultClassifiedSpans(SnapshotSpan span) {
			if (_document == null) {
				return Enumerable.Empty<ClassifiedSpan>();
			}

			if (!_cachedClassifiedSpans.ContainsKey(span)) {
				var textSpan = TextSpan.FromBounds(span.Start, span.End);
				_cachedClassifiedSpans[span] = Classifier.GetClassifiedSpans(_semanticModel, textSpan, _workspace).ToList();
			}

			return _cachedClassifiedSpans[span];
		}

		public IEnumerable<ClassificationSpan> ClassifySpan(IEnumerable<ClassifiedSpan> spans, IClassificationType classificationType) {
			foreach (var span in spans) {
				var k = _syntaxRoot.FindNode(span.TextSpan).Kind();

				yield return new ClassificationSpan(new SnapshotSpan(_snapshot, new Span(span.TextSpan.Start, span.TextSpan.Length)), classificationType);
			}
		}
		public IEnumerable<ClassificationSpan> ClassifyTokens(IEnumerable<ClassifiedSpan> classifiedSpans, IClassificationType classificationType, params SyntaxKind[] syntaxKinds) {
			var matchedTokens = classifiedSpans.Select(x => _syntaxRoot.FindToken(x.TextSpan.Start)).Where(x => syntaxKinds.Any(sk => x.IsKind(sk)));
			return matchedTokens.Select(x => new ClassificationSpan(new SnapshotSpan(_snapshot, new Span(x.Span.Start, x.Span.Length)), classificationType));
		}

	}
}
