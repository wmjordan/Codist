using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using EnvDTE;
using Newtonsoft.Json;

namespace Codist.AutoBuildVersion
{
	/// <summary>
	/// Contains settings to automate assembly versioning after successful build.
	/// </summary>
	public sealed class BuildSetting : Dictionary<string, BuildConfigSetting>
	{
		public BuildSetting() : base(StringComparer.OrdinalIgnoreCase) {
		}

		public static string GetConfigPath(Project project) {
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			return Path.Combine(Path.GetDirectoryName(project.FullName), "obj", project.Name + ".autoversion.json");
		}
		public static BuildSetting Load(Project project) {
			return Load(GetConfigPath(project));
		}
		public static BuildSetting Load(string configPath) {
			try {
				return File.Exists(configPath)
					? JsonConvert.DeserializeObject<BuildSetting>(File.ReadAllText(configPath), new JsonSerializerSettings {
						DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
						NullValueHandling = NullValueHandling.Ignore,
						Error = (sender, args) => {
							args.ErrorContext.Handled = true; // ignore json error
						}
					})
					: null;
			}
			catch (Exception ex) {
				Debug.Write("Error loading " + nameof(BuildSetting) + " from " + configPath);
				Debug.WriteLine(ex.ToString());
				return null;
			}
		}

		public void RewriteVersion(Project project, string buildConfig) {
			var i = buildConfig.IndexOf('|');
			if (i != -1) {
				buildConfig = buildConfig.Substring(0, i);
			}
			var setting = Merge("<Any>", buildConfig);
			if (setting?.ShouldRewrite == true) {
				setting.RewriteVersion(project);
			}
		}

		/// <summary>Merge two <see cref="BuildConfigSetting"/>s named <paramref name="baseConfig"/> and <paramref name="specificConfig"/>. The latter one has higher precedence.</summary>
		/// <param name="baseConfig">The base configuration name.</param>
		/// <param name="specificConfig">The specific configuration name.</param>
		/// <returns>The merged configuration. If neither <paramref name="baseConfig"/> or <paramref name="specificConfig"/> exists, returns <see langword="null"/>.</returns>
		public BuildConfigSetting Merge(string baseConfig, string specificConfig) {
			TryGetValue(specificConfig, out var s);
			if (TryGetValue(baseConfig, out var b)) {
				return s != null
					? new BuildConfigSetting {
						AssemblyVersion = s.AssemblyVersion?.ShouldRewrite == true ? s.AssemblyVersion : b.AssemblyVersion,
						AssemblyFileVersion = s.AssemblyFileVersion?.ShouldRewrite == true ? s.AssemblyFileVersion : b.AssemblyFileVersion,
						UpdateLastYearNumberInCopyright = s.UpdateLastYearNumberInCopyright || b.UpdateLastYearNumberInCopyright
					}
					: b;
			}
			return s;
		}
	}
}
