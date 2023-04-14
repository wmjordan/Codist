using System;
using System.Collections.Generic;
using System.Linq;
using AppHelpers;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist.Taggers
{
	sealed class FindResultTagger : IClassifier
	{
		static readonly IClassificationTypeRegistryService __ClassificationTypes = ServicesHelper.Instance.ClassificationTypeRegistry;
		static readonly IClassificationType __Number = __ClassificationTypes.GetClassificationType("line number");
		static readonly IClassificationType __Url = __ClassificationTypes.GetClassificationType(Constants.CodeUrl);
		static readonly Microsoft.VisualStudio.LanguageServices.VisualStudioWorkspace __Workspace = ServicesHelper.Instance.VisualStudioWorkspace;

		public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

		public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
			if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SearchResult) == false) {
				goto NOT_FOUND;
			}
			var t = span.GetText();
			// note: match pattern: [SPACE][SPACE]file name([NUMBER]):[CONTENT]\r\n
			var c1 = t.IndexOf(':'); // colon after drive letter
			if (--c1 < 0
				|| t[c1] < 'A' || t[c1] > 'Z'
				|| (c1 > 0 && IsWhitespaceOnly(t, 0, c1) == false)) {
				goto NOT_FOUND;
			}
			var c2 = t.IndexOf(':', c1 + 2); // colon after file name and line number
			if (--c2 <= 0 || t[c2] != ')') {
				goto NOT_FOUND;
			}
			var b1 = t.LastIndexOf('(', c2 - 1, c2 - c1);
			if (b1 < 0) {
				goto NOT_FOUND;
			}
			var snapshot = span.Snapshot;
			var r = new List<ClassificationSpan> {
				new ClassificationSpan(new SnapshotSpan(snapshot, span.Start.Position + c1, b1 - c1), __Url),
				new ClassificationSpan(new SnapshotSpan(snapshot, span.Start.Position + b1 + 1, c2 - b1 - 1), __Number)
			};

			var filePath = t.Substring(c1, b1 - c1);
			if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) {
				int lineNumber;
				if (Int32.TryParse(t.Substring(b1 + 1, c2 - b1 - 1), out lineNumber)) {
					var docId = __Workspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
					if (docId != null) {
						var semanticModel = SyncHelper.RunSync(() => __Workspace.CurrentSolution.GetDocument(docId).GetSemanticModelAsync());
						var sourceText = semanticModel.SyntaxTree.GetText();
						var lines = sourceText.Lines;
						if (lineNumber < lines.Count) {
							var sourceSpan = lines[lineNumber - 1].Span;
							var offset = span.Start.Position + c2 + 2 - sourceSpan.Start;
							// verify that the source text is the same as the find result
							if (t.Length >= c2 + 2 + sourceSpan.Length
								&& t.IndexOf(sourceText.GetSubText(sourceSpan).ToString(), c2 + 2, sourceSpan.Length) >= 0) {
								var cs = Classifier.GetClassifiedSpans(semanticModel, sourceSpan, __Workspace);
								foreach (var item in cs) {
									r.Add(new ClassificationSpan(new SnapshotSpan(snapshot, offset + item.TextSpan.Start, item.TextSpan.Length), __ClassificationTypes.GetClassificationType(item.ClassificationType)));
								}
							}
						}
					}
				}
			}
			return r;
			NOT_FOUND:
			return Array.Empty<ClassificationSpan>();

			bool IsWhitespaceOnly(string text, int start, int end) {
				for (int i = start; i < end; i++) {
					if (text[i] != ' ') {
						return false;
					}
				}
				return true;
			}
		}
	}
}
