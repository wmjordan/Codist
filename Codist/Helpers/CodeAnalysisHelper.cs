using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

namespace Codist.Helpers
{
	static class CodeAnalysisHelper
	{
		public static bool AnyTextChanges(ITextVersion oldVersion, ITextVersion currentVersion) {
			while (oldVersion != currentVersion) {
				if (oldVersion.Changes.Count > 0) {
					return true;
				}

				oldVersion = oldVersion.Next;
			}

			return false;
		}

		public static Document GetDocument(this Workspace workspace, SnapshotSpan span) {
			var solution = workspace.CurrentSolution;
			var sourceText = span.Snapshot.AsText();
			var docId = workspace.GetDocumentIdInCurrentContext(sourceText.Container);
			return solution.ContainsDocument(docId)
				? solution.GetDocument(docId)
				: solution.WithDocumentText(docId, sourceText, PreservationMode.PreserveIdentity).GetDocument(docId);
		}

		public static void GoToSymbol(this ISymbol symbol) {
			if (symbol != null && symbol.DeclaringSyntaxReferences.Length > 0) {
				var loc = symbol.DeclaringSyntaxReferences[0];
				var path = loc.SyntaxTree?.FilePath;
				if (path == null) {
					return;
				}
				var pos = loc.SyntaxTree.GetLineSpan(loc.Span);
				var openDoc = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
				openDoc.OpenFile(path, pos.StartLinePosition.Line + 1, pos.StartLinePosition.Character + 1);
			}
		}

		public static void OpenFile(this EnvDTE.DTE dte, string file, int line, int column) {
			if (file == null) {
				return;
			}
			file = System.IO.Path.GetFullPath(file);
			if (System.IO.File.Exists(file) == false) {
				return;
			}
			using (new NewDocumentStateScope(__VSNEWDOCUMENTSTATE.NDS_Provisional, VSConstants.NewDocumentStateReason.Navigation)) {
				dte.ItemOperations.OpenFile(file);
				((EnvDTE.TextSelection)dte.ActiveDocument.Selection).MoveToLineAndOffset(line, column);
			}
		}

	}
}
