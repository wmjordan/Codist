using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;

namespace Codist
{
	static partial class CodeAnalysisHelper
	{
		public static Document GetDocument(this Workspace workspace, ITextBuffer textBuffer) {
			if (workspace == null) {
				throw new ArgumentNullException(nameof(workspace));
			}
			if (textBuffer == null) {
				throw new InvalidOperationException("textBuffer is null");
			}
			var solution = workspace.CurrentSolution ?? throw new InvalidOperationException("solution is null");
			var textContainer = textBuffer.AsTextContainer() ?? throw new InvalidOperationException("textContainer is null");
			var docId = workspace.GetDocumentIdInCurrentContext(textContainer) ?? throw new InvalidOperationException("docId is null");
			return solution.WithDocumentText(docId, textContainer.CurrentText, PreservationMode.PreserveIdentity).GetDocument(docId);
		}
		public static Document GetDocument(this ITextBuffer textBuffer) {
			return textBuffer.GetWorkspace().GetDocument(textBuffer);
		}
		public static Document GetDocument(this Project project, string filePath) {
			return project.Documents.FirstOrDefault(d => String.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>Gets all <see cref="Document"/>s from a given <see cref="Project"/> and referencing/referenced projects.</summary>
		public static IEnumerable<Document> GetRelatedProjectDocuments(this Project project) {
			foreach (var p in project.GetRelatedProjects()) {
				foreach (var doc in p.Documents) {
					yield return doc;
				}
			}
		}

		/// <summary>
		/// Gets a collection containing <paramref name="project"/> itself, and projects referenced by <paramref name="project"/> or referencing <paramref name="project"/>.
		/// </summary>
		/// <param name="project">The project to be examined.</param>
		public static HashSet<Project> GetRelatedProjects(this Project project) {
			var projects = new HashSet<Project>();
			GetRelatedProjects(project, projects);
			var id = project.Id;
			foreach (var p in project.Solution.Projects) {
				if (projects.Contains(p) == false
					&& p.AllProjectReferences.Any(i => i.ProjectId == id)) {
					projects.Add(p);
				}
			}
			return projects;
		}

		static void GetRelatedProjects(Project project, HashSet<Project> projects) {
			if (project == null) {
				return;
			}
			if (projects.Add(project)) {
				foreach (var pr in project.AllProjectReferences) {
					GetRelatedProjects(project.Solution.GetProject(pr.ProjectId), projects);
				}
			}
		}

		public static bool IsCSharp(this SemanticModel model) {
			return model.Language == "C#";
		}

		public static string GetDocId(this Document document) {
			return document == null
				? String.Empty
				: $"{document.Name}({document.Id.Id.ToString("N").Substring(0, 8)})";
		}

		public static string GetText(this Location location, CancellationToken cancellationToken = default) {
			return location.SourceTree.GetText(cancellationToken).ToString(location.SourceSpan);
		}
	}
}
