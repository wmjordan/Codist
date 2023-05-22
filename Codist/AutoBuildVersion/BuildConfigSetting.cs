using System;
using System.Diagnostics.CodeAnalysis;
using CLR;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;

namespace Codist.AutoBuildVersion
{
	public sealed class BuildConfigSetting
	{
		const string ProjectAssemblyVersionName = "AssemblyVersion";
		const string ProjectFileVersionName = "AssemblyFileVersion";
		const string SdkProjectFileVersionName = "FileVersion";
		const string SdkCopyrightName = "Copyright";

		public VersionSetting AssemblyVersion { get; set; }
		public VersionSetting AssemblyFileVersion { get; set; }
		public bool UpdateLastYearNumberInCopyright { get; set; }

		[JsonIgnore]
		public bool ShouldRewrite => ShouldSerializeAssemblyVersion() || ShouldSerializeAssemblyFileVersion() || UpdateLastYearNumberInCopyright;

		public bool ShouldSerializeAssemblyVersion() {
			return AssemblyVersion?.ShouldRewrite == true;
		}
		public bool ShouldSerializeAssemblyFileVersion() {
			return AssemblyFileVersion?.ShouldRewrite == true;
		}

		public void RewriteVersion(Project project) {
			var f = RewriteFlags.None
				.SetFlags(RewriteFlags.AssemblyVersion, AssemblyVersion?.ShouldRewrite == true)
				.SetFlags(RewriteFlags.AssemblyFileVersion, AssemblyFileVersion?.ShouldRewrite == true)
				.SetFlags(RewriteFlags.CopyrightYear, UpdateLastYearNumberInCopyright);
			RewriteSdkProjectFile(project, ref f);
			//var r = RewriteSdkProjectFile(project, ref f)
			//	|| RewriteAssemblyInfoFile(project, project.FindItem("Properties", "AssemblyInfo.cs"), ref f);
		}

		public static bool TryGetAssemblyAttributeValues(Project project, out string[] version, out string[] fileVersion, out string copyright) {
			ThreadHelper.ThrowIfNotOnUIThread();
			version = AttributePattern.GetMatchedVersion(project, ProjectAssemblyVersionName);
			fileVersion = AttributePattern.GetMatchedVersion(project, ProjectFileVersionName)
				?? AttributePattern.GetMatchedVersion(project, SdkProjectFileVersionName);
			copyright = AttributePattern.GetProperty(project, SdkCopyrightName);
			return version != null || fileVersion != null || copyright != null;
		}

		bool RewriteSdkProjectFile(Project project, ref RewriteFlags f) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var rf = RewriteFlags.None;
			if (f.MatchFlags(RewriteFlags.AssemblyVersion) && RewriteVersion(project, AssemblyVersion, ProjectAssemblyVersionName)) {
				rf = rf.SetFlags(RewriteFlags.AssemblyVersion, true);
			}
			if (f.MatchFlags(RewriteFlags.AssemblyFileVersion) && (RewriteVersion(project, AssemblyFileVersion, SdkProjectFileVersionName) || RewriteVersion(project, AssemblyFileVersion, ProjectFileVersionName))) {
				rf = rf.SetFlags(RewriteFlags.AssemblyFileVersion, true);
			}
			if (f.MatchFlags(RewriteFlags.CopyrightYear) && RewriteCopyrightYear(project, SdkCopyrightName)) {
				rf = rf.SetFlags(RewriteFlags.CopyrightYear, true);
			}
			f = f.SetFlags(rf, false);
			var r = rf != RewriteFlags.None;
			if (r && project.Saved == false) {
				project.Save();
			}
			return r;
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		bool RewriteVersion(Project project, VersionSetting setting, string versionName) {
			Property ver;
			if (setting is null) {
				return false;
			}
			try {
				if ((ver = project.Properties.Item(versionName)) == null) {
					return false;
				}
			}
			catch (ArgumentException) {
				return false;
			}
			var t = ver.Value.ToString();
			if (Version.TryParse(t, out var v)) {
				var n = setting.Rewrite(v.Major.ToText(), v.Minor.ToText(), v.Build.ToText(), v.Revision.ToText());
				if (n != t) {
					ver.Value = n;
					WriteBuildOutput($"{project.Name} {versionName} => {n}");
				}
				return true;
			}
			return false;
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		bool RewriteCopyrightYear(Project project, string propertyName) {
			Property property;
			try {
				if ((property = project.Properties.Item(propertyName)) == null) {
					return false;
				}
			}
			catch (ArgumentException) {
				return false;
			}
			var t = property.Value.ToString();
			int i = t.Length;
			while ((i = t.LastIndexOf("20", i, StringComparison.Ordinal)) >= 0) {
				if (i + 4 <= t.Length && IsDigit(t[i + 2]) && IsDigit(t[i + 3])
					&& (i == 0 || IsDigit(t[i - 1]) == false)
					&& (i + 5 > t.Length || IsDigit(t[i + 4]) == false)) {
					var n = DateTime.Now.Year.ToText();
					if (String.CompareOrdinal(t, i, n, 0, 4) != 0) {
						property.Value = t = t.Substring(0, i) + n + t.Substring(i + 4);
						WriteBuildOutput($"{project.Name} {propertyName} => {t}");
						return true;
					}
					return false;
				}
			}
			return false;
		}

		static bool IsDigit(char c) {
			return c >= '0' && c <= '9';
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		void WriteBuildOutput(string text) {
			CodistPackage.Instance?.GetOutputPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, "Build")?.OutputString(nameof(Codist) + ": " + text + Environment.NewLine);
		}

		[Flags]
		enum RewriteFlags
		{
			None,
			AssemblyVersion = 1,
			AssemblyFileVersion = 1 << 1,
			CopyrightYear = 1 << 2
		}

		static class AttributePattern
		{
			public static string[] GetMatchedVersion(Version v) {
				return new[] { v.Major.ToText(), v.Minor.ToText(), v.Build.ToText(), v.Revision.ToText() };
			}

			[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
			public static string[] GetMatchedVersion(Project project, string propertyName) {
				try {
					var ver = project.Properties.Item(propertyName);
					return ver != null && Version.TryParse(ver.Value.ToString(), out var v)
						? GetMatchedVersion(v)
						: null;
				}
				catch (ArgumentException) {
					return null;
				}
			}

			[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
			public static string GetProperty(Project project, string propertyName) {
				try {
					return project.Properties.Item(propertyName)?.Value is string s ? s : null;
				}
				catch (ArgumentException) {
					return null;
				}
			}
		}
	}
}
