using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codist;

	static class VsShellHelper
	{
		public const string CSharpProjectKind = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}",
			ProjectFolderKind = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}",
		MiscKind = "{66A2671D-8FB5-11D2-AA7E-00C04F688DDE}",
		UnloadedProjectKind = "{67294A52-A4F0-11D2-AA88-00C04F688DDE}",
		MiscFilesKind = "{66A2671F-8FB5-11D2-AA7E-00C04F688DDE}",
			VsixProjectExtender = "VsixProjectExtender";

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		public static IEnumerable<KeyValuePair<string, object>> Enumerate(this EnvDTE.Properties properties) {
			if (properties == null) {
				yield break;
			}
			var c = properties.Count;
			for (int i = 1; i <= c; i++) {
				var p = properties.Item(i);
				object v;
				try {
					v = p.Value;
				}
				catch (System.Runtime.InteropServices.COMException ex) {
					v = ex;
				}
				catch (NotImplementedException ex) {
					v = ex;
				}
				catch (System.Reflection.TargetParameterCountException ex) {
					v = ex;
				}
				catch (ArgumentException ex) {
					v = ex;
				}
				yield return new KeyValuePair<string, object>(p.Name, v);
			}
		}

		public static T GetExtObjectAs<T>(this IVsHierarchy item) where T : class {
			ThreadHelper.ThrowIfNotOnUIThread();
			return item.GetProperty(VSConstants.VSITEMID_ROOT, (int)VsHierarchyPropID.ExtObject, out var name) != 0
				? null
				: name as T;
		}

		public static (string platformName, string configName) GetActiveBuildConfiguration() {
			return GetActiveBuildConfiguration(ServicesHelper.Instance.DTE.ActiveDocument);
		}
		public static void SetActiveBuildConfiguration(string configName) {
			SetActiveBuildConfiguration(ServicesHelper.Instance.DTE.ActiveDocument, configName);
		}

		public static Chain<string> GetBuildConfigNames() {
			ThreadHelper.ThrowIfNotOnUIThread();
			var configs = new Chain<string>();
			foreach (EnvDTE80.SolutionConfiguration2 c in ServicesHelper.Instance.DTE.Solution.SolutionBuild.SolutionConfigurations) {
			foreach (SolutionContext context in c.SolutionContexts) {
					if (configs.Contains(context.ConfigurationName) == false) {
						configs.Add(context.ConfigurationName);
					}
				}
			}
			return configs;
		}

	public static (string platformName, string configName) GetActiveBuildConfiguration(Document document) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var pn =  document.ProjectItem.ContainingProject.UniqueName;
			foreach (EnvDTE80.SolutionConfiguration2 c in ServicesHelper.Instance.DTE.Solution.SolutionBuild.SolutionConfigurations) {
			foreach (SolutionContext context in c.SolutionContexts) {
					if (context.ProjectName == pn) {
						return (c.PlatformName, context.ConfigurationName);
					}
				}
			}
			return default;
		}

	public static void SetActiveBuildConfiguration(Document document, string configName) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var pn = document.ProjectItem.ContainingProject.UniqueName;
			foreach (EnvDTE80.SolutionConfiguration2 c in ServicesHelper.Instance.DTE.Solution.SolutionBuild.SolutionConfigurations) {
			foreach (SolutionContext context in c.SolutionContexts) {
					if (context.ProjectName == pn) {
						context.ConfigurationName = configName;
						return;
					}
				}
			}
		}

	public static Project GetActiveProjectInSolutionExplorer() {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (ServicesHelper.Instance.DTE.ToolWindows.SolutionExplorer.SelectedItems is object[] selectedObjects) {
			foreach (UIHierarchyItem hi in selectedObjects.OfType<UIHierarchyItem>()) {
				if (hi.Object is Project item) {
						return item;
					}
				}
			}
			return null;
		}

	public static bool IsMiscOrProjectFolder(this Project project) {
		return project.Kind switch {
			MiscKind or ProjectFolderKind => true,
			_ => false,
		};
	}

		public static TObj GetFirstSelectedItemInSolutionExplorer<TObj>(Predicate<TObj> predicate)
			where TObj : class {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (ServicesHelper.Instance.DTE.ToolWindows.SolutionExplorer.SelectedItems is object[] selectedObjects) {
			foreach (UIHierarchyItem hi in selectedObjects.OfType<UIHierarchyItem>()) {
					if (hi.Object is TObj item
						&& predicate(item)) {
						return item;
					}
				}
			}
			return null;
		}

	public static Project GetProject(string projectName) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var projects = ServicesHelper.Instance.DTE.Solution.Projects;
			var projectPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ServicesHelper.Instance.DTE.Solution.FullName), projectName));
			for (int i = 1; i <= projects.Count; i++) {
				var project = projects.Item(i);
				if (project.FullName.Length == 0 && project.Kind == ProjectFolderKind) {
					if ((project = FindProject(project.ProjectItems, projectPath)) != null) {
						return project;
					}
				}
			else if (FileHelper.AreFileNamesEqual(project.FullName, projectPath)) {
					return project;
				}
			}
			return ServicesHelper.Instance.DTE.Solution.Projects.Item(projectName);

		Project FindProject(ProjectItems items, string pp) {
				for (int i = 1; i <= items.Count; i++) {
					var item = items.Item(i);
				if (item.Object is Project p
					&& FileHelper.AreFileNamesEqual(p.FullName, pp)) {
						return p;
					}
				}
				return null;
			}
		}

	public static bool IsVsixProject(this Project project) {
			return project?.ExtenderNames is string[] extenders && Array.IndexOf(extenders, VsixProjectExtender) != -1;
		}

	public static bool IsCSharpProject(this Project project) {
			return project?.Kind == CSharpProjectKind;
		}

	public static string GetDefaultProjectLocation() {
		ThreadHelper.ThrowIfNotOnUIThread();
		try {
			return ServicesHelper.Instance.DTE.get_Properties("Environment", "ProjectsAndSolution")
				?.Item("ProjectsLocation")
				?.Value?.ToString();
		}
		catch (Exception ex) {
			ex.Log();
			return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		}
	}

	public static ProjectItem FindItem(this Project project, params string[] itemNames) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = project.ProjectItems;
			var count = items.Count;
		ProjectItem p = null;
			foreach (var name in itemNames) {
				bool match = false;
				for (int i = 1; i <= count; i++) {
					p = items.Item(i);
				if (FileHelper.AreFileNamesEqual(p.Name, name)) {
						items = p.ProjectItems;
						count = items.Count;
						match = true;
						break;
					}
				}
				if (match == false) {
					return null;
				}
			}
			return p;
		}

		public static bool IsVsixProject() {
			ThreadHelper.ThrowIfNotOnUIThread();
			try {
				return ServicesHelper.Instance.DTE.ActiveDocument?.ProjectItem?.ContainingProject?.ExtenderNames is string[] extenders && Array.IndexOf(extenders, VsixProjectExtender) != -1;
			}
			catch (ArgumentException) {
				// hack: for https://github.com/wmjordan/Codist/issues/124
				return false;
			}
		}

		[Conditional("LOG")]
		public static void Log(string text) {
			OutputPane.OutputLine(text);
		}
		public static int OutputLine(string text) {
			return OutputPane.OutputLine(text);
		}
		public static void ClearOutputPane() {
			OutputPane.ClearOutputPane();
		}

		static class OutputPane
		{
			static IVsOutputWindowPane __OutputPane;

			static public int OutputLine(string text) {
				ThreadHelper.ThrowIfNotOnUIThread();
				return (__OutputPane ?? (__OutputPane = CreateOutputPane()))
					.OutputString(text + Environment.NewLine);
			}

			static public void ClearOutputPane() {
				ThreadHelper.ThrowIfNotOnUIThread();
				__OutputPane?.Clear();
			}

			[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
			static IVsOutputWindowPane CreateOutputPane() {
				var window = ServicesHelper.Get<IVsOutputWindow, SVsOutputWindow>();
				var guid = new Guid(CodistPackage.PackageGuidString);
				if (window.CreatePane(ref guid, nameof(Codist), 1, 0) == VSConstants.S_OK
					&& window.GetPane(ref guid, out var pane) == VSConstants.S_OK) {
					return pane;
				}
				return null;
			}
		}
	}
