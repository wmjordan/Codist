using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codist
{
	static class VsShellHelper
	{
		public const string CSharpProjectKind = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}",
			ProjectFolderKind = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}",
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
				catch (System.Runtime.InteropServices.COMException) {
					v = null;
				}
				catch (NotImplementedException) {
					v = null;
				}
				catch (System.Reflection.TargetParameterCountException) {
					v = null;
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
			return GetActiveBuildConfiguration(CodistPackage.DTE.ActiveDocument);
		}
		public static void SetActiveBuildConfiguration(string configName) {
			SetActiveBuildConfiguration(CodistPackage.DTE.ActiveDocument, configName);
		}

		public static Chain<string> GetBuildConfigNames() {
			ThreadHelper.ThrowIfNotOnUIThread();
			var configs = new Chain<string>();
			foreach (EnvDTE80.SolutionConfiguration2 c in CodistPackage.DTE.Solution.SolutionBuild.SolutionConfigurations) {
				foreach (EnvDTE.SolutionContext context in c.SolutionContexts) {
					if (configs.Contains(context.ConfigurationName) == false) {
						configs.Add(context.ConfigurationName);
					}
				}
			}
			return configs;
		}

		public static (string platformName, string configName) GetActiveBuildConfiguration(EnvDTE.Document document) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var pn =  document.ProjectItem.ContainingProject.UniqueName;
			foreach (EnvDTE80.SolutionConfiguration2 c in CodistPackage.DTE.Solution.SolutionBuild.SolutionConfigurations) {
				foreach (EnvDTE.SolutionContext context in c.SolutionContexts) {
					if (context.ProjectName == pn) {
						return (c.PlatformName, context.ConfigurationName);
					}
				}
			}
			return default;
		}

		public static void SetActiveBuildConfiguration(EnvDTE.Document document, string configName) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var pn = document.ProjectItem.ContainingProject.UniqueName;
			foreach (EnvDTE80.SolutionConfiguration2 c in CodistPackage.DTE.Solution.SolutionBuild.SolutionConfigurations) {
				foreach (EnvDTE.SolutionContext context in c.SolutionContexts) {
					if (context.ProjectName == pn) {
						context.ConfigurationName = configName;
						return;
					}
				}
			}
		}

		public static EnvDTE.Project GetProject(string projectName) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var projects = CodistPackage.DTE.Solution.Projects;
			var projectPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(CodistPackage.DTE.Solution.FullName), projectName));
			for (int i = 1; i <= projects.Count; i++) {
				var project = projects.Item(i);
				if (project.FullName.Length == 0 && project.Kind == ProjectFolderKind) {
					if ((project = FindProject(project.ProjectItems, projectPath)) != null) {
						return project;
					}
				}
				else if (String.Equals(project.FullName, projectPath, StringComparison.OrdinalIgnoreCase)) {
					return project;
				}
			}
			return CodistPackage.DTE.Solution.Projects.Item(projectName);

			EnvDTE.Project FindProject(EnvDTE.ProjectItems items, string pp) {
				for (int i = 1; i <= items.Count; i++) {
					var item = items.Item(i);
					if (item.Object is EnvDTE.Project p
						&& String.Equals(p.FullName, pp, StringComparison.OrdinalIgnoreCase)) {
						return p;
					}
				}
				return null;
			}
		}

		public static bool IsVsixProject(this EnvDTE.Project project) {
			return project?.ExtenderNames is string[] extenders && Array.IndexOf(extenders, VsixProjectExtender) != -1;
		}

		public static bool IsCSharpProject(this EnvDTE.Project project) {
			return project?.Kind == CSharpProjectKind;
		}

		public static EnvDTE.ProjectItem FindItem(this EnvDTE.Project project, params string[] itemNames) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = project.ProjectItems;
			var count = items.Count;
			EnvDTE.ProjectItem p = null;
			foreach (var name in itemNames) {
				bool match = false;
				for (int i = 1; i <= count; i++) {
					p = items.Item(i);
					if (String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) {
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
				return CodistPackage.DTE.ActiveDocument?.ProjectItem?.ContainingProject?.ExtenderNames is string[] extenders && Array.IndexOf(extenders, VsixProjectExtender) != -1;
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
}
