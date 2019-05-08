using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;

namespace Codist
{
	static partial class CodeAnalysisHelper
	{
		public static Document GetDocument(this Workspace workspace, SnapshotSpan span) {
			if (workspace == null) {
				throw new ArgumentNullException(nameof(workspace));
			}
			var solution = workspace.CurrentSolution;
			if (solution == null) {
				throw new InvalidOperationException("solution is null");
			}
			if (span.Snapshot == null) {
				throw new InvalidOperationException("snapshot is null");
			}
			var sourceText = span.Snapshot.AsText();
			if (sourceText == null) {
				throw new InvalidOperationException("sourceText is null");
			}
			var docId = workspace.GetDocumentIdInCurrentContext(sourceText.Container);
			if (docId == null) {
				throw new InvalidOperationException("docId is null");
			}
			return solution.ContainsDocument(docId)
				? solution.GetDocument(docId)
				: solution.WithDocumentText(docId, sourceText, PreservationMode.PreserveIdentity).GetDocument(docId);
		}

		/// <summary>Gets all <see cref="Document"/>s from a given <see cref="Project"/> and referencing/referenced projects.</summary>
		public static IEnumerable<Document> GetRelatedProjectDocuments(this Project project) {
			foreach (var proj in GetRelatedProjects(project)) {
				foreach (var doc in proj.Documents) {
					yield return doc;
				}
			}
		}

		public static Document GetDocument(this Project project, string filePath) {
			return project.Documents.FirstOrDefault(d => String.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Gets a collection containing <paramref name="project"/> itself, and projects referenced by <paramref name="project"/> or referencing <paramref name="project"/>.
		/// </summary>
		/// <param name="project">The project to be examined.</param>
		static HashSet<Project> GetRelatedProjects(this Project project) {
			var projects = new HashSet<Project>();
			GetRelatedProjects(project, projects);
			var id = project.Id;
			foreach (var proj in project.Solution.Projects) {
				if (projects.Contains(proj) == false
					&& proj.AllProjectReferences.Any(p => p.ProjectId == id)) {
					projects.Add(proj);
				}
			}
			return projects;
		}

		static void GetRelatedProjects(Project project, HashSet<Project> projects) {
			if (project == null) {
				return;
			}
			projects.Add(project);
			foreach (var pr in project.AllProjectReferences) {
				GetRelatedProjects(project.Solution.GetProject(pr.ProjectId), projects);
			}
		}

	}
}
