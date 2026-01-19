using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class ExtensionDeveloperPage : OptionPageFactory
		{
			public override string Name => R.OT_ExtensionDevelopment;
			public override Features RequiredFeature => Features.None;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly OptionBox<BuildOptions> _BuildVsixAutoIncrement;
				readonly OptionBox<DeveloperOptions> _ShowActiveWindowProperties, _ShowSyntaxClassificationInfo, _OpenActivityLog;

				public PageControl() {
					var o = Config.Instance.SpecialHighlightOptions;
					SetContents(new Note(R.OT_ExtensionNote),

						new TitleBox(R.OT_SyntaxDiagnostics),
						new DescriptionBox(R.OT_SyntaxDiagnosticsNote),
						_ShowSyntaxClassificationInfo = Config.Instance.DeveloperOptions.CreateOptionBox(DeveloperOptions.ShowSyntaxClassificationInfo, UpdateConfig, R.OT_AddShowSyntaxClassifcationInfo)
							.SetLazyToolTip(() => R.OT_AddShowSyntaxClassifcationInfoTip),

						new TitleBox(R.OT_Build),
						_BuildVsixAutoIncrement = Config.Instance.BuildOptions.CreateOptionBox(BuildOptions.VsixAutoIncrement, UpdateConfig, R.OT_AutoIncrementVsixVersion)
							.SetLazyToolTip(() => R.OT_AutoIncrementVsixVersionTip),

						new TitleBox(R.OT_VisualStudio),
						_ShowActiveWindowProperties = Config.Instance.DeveloperOptions.CreateOptionBox(DeveloperOptions.ShowWindowInformer, UpdateConfig, R.OT_AddShowActiveWindowProperties)
							.SetLazyToolTip(() => R.OT_AddShowActiveWindowPropertiesTip),
						_OpenActivityLog = Config.Instance.DeveloperOptions.CreateOptionBox(DeveloperOptions.ShowActivityLog, UpdateConfig, R.OT_AddOpenActivityLog),

						new TitleBox(R.OT_Tools),
						new WrapPanel {
							Children = {
								MakeToolButton(DteCommandsExporter.Instance),
								MakeToolButton(ThemeColorsExporter.Instance),
								MakeToolButton(AppResourcesExporter.Instance)
							}
						}
					);
				}

				protected override void LoadConfig(Config config) {
					var o = config.BuildOptions;
					_BuildVsixAutoIncrement.UpdateWithOption(o);
				}

				void UpdateConfig(BuildOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.None);
				}

				void UpdateConfig(DeveloperOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.None);
				}

				static ThemedButton MakeToolButton(ExporterBase exporter) {
					return new ThemedButton(exporter.IconId, exporter.Title, exporter.Description, exporter.Export) { Margin = WpfHelper.SmallMargin };
				}
			}

			abstract class ExporterBase
			{
				public abstract int IconId { get; }
				public abstract string Title { get; }
				public abstract string Description { get; }
				protected abstract string DefaultFileName { get; }
				protected abstract void ExportContent(StreamWriter writer);

				public void Export() {
					string path;
					if ((path = GetSavePath(DefaultFileName, Title)) != null) {
						using (var writer = new StreamWriter(path)) {
							ExportContent(writer);
						}
						TextEditorHelper.OpenFile(path);
					}
				}

				static string GetSavePath(string defaultFileName, string title) {
					var saveDialog = new SaveFileDialog {
						Filter = R.F_Text,
						DefaultExt = ".txt",
						Title = title,
						FileName = defaultFileName,
						AddExtension = true,
						OverwritePrompt = true
					};

					return saveDialog.ShowDialog() == true
						? saveDialog.FileName
						: null;
				}
			}

			sealed class DteCommandsExporter : ExporterBase
			{
				public static readonly DteCommandsExporter Instance = new();

				DteCommandsExporter() { }

				public override int IconId => IconIds.UnorderedList;
				public override string Title => R.T_ExportDTECommands;
				public override string Description => R.T_ExportDTECommandsTip;
				protected override string DefaultFileName => "DTE.Commands.txt";

				[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
				protected override void ExportContent(StreamWriter writer) {
					var commands = CodistPackage.DTE.Commands;
					var c = commands.Count;
					var s = new List<CommandInfo>();
					var s2 = new List<CommandInfo>(c);
					for (int i = 0; i < c; i++) {
						var cmd = new CommandInfo(commands.Item(i + 1));
						if (cmd.Name.IndexOf('.') == -1) {
							s.Add(cmd);
						}
						else {
							s2.Add(cmd);
						}
					}
					s.Sort();
					s2.Sort();

					writer.WriteLine("Name\tLocalizedName\tGuid\tId\tBindings");
					foreach (var cmd in s.Concat(s2)) {
						writer.Write(cmd.Name);
						writer.Write('\t');
						writer.Write(cmd.LocalizedName);
						writer.Write('\t');
						writer.Write(cmd.Guid);
						writer.Write('\t');
						writer.Write(cmd.Id.ToText());
						writer.Write('\t');
						writer.WriteLine(cmd.Bindings);
					}
				}

				[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
				sealed class CommandInfo(EnvDTE.Command command) : IEquatable<CommandInfo>, IComparable<CommandInfo>
				{
					public string Name { get; } = command.Name;
					public string LocalizedName { get; } = command.LocalizedName;
					public string Guid { get; } = command.Guid;
					public int Id { get; } = command.ID;
					public string Bindings { get; } = GetBindingExpressions(command.Bindings);

					static string GetBindingExpressions(object bindings) {
						if (bindings == null) return String.Empty;

						try {
							if (bindings is object[] a) {
								return string.Join("; ", a.OfType<string>().Where(i => !string.IsNullOrWhiteSpace(i)));
							}
							if (bindings is string s) {
								return s;
							}
						}
						catch (Exception ex) {
							return $"Error: {ex.Message}";
						}
						return String.Empty;
					}

					public int CompareTo(CommandInfo other) {
						int c;
						return (c = Name.CompareTo(other.Name)) != 0
								? c
								: (c = Guid.CompareTo(other.Guid)) != 0
								? c
								: Id - other.Id;
					}

					public bool Equals(CommandInfo other) {
						return Guid == other.Guid;
					}
				}
			}

			sealed class ThemeColorsExporter : ExporterBase
			{
				public static readonly ThemeColorsExporter Instance = new();

				ThemeColorsExporter() { }

				public override int IconId => IconIds.SyntaxTheme;
				public override string Title => R.T_ExportThemeColors;
				public override string Description => R.T_ExportThemeColorsTip;
				protected override string DefaultFileName => "Theme Colors.txt";

				protected override void ExportContent(StreamWriter writer) {
					writer.WriteLine("Type\tName\tColor\tCategory");
					foreach (var type in new Type[] { typeof(EnvironmentColors), typeof(CommonControlsColors), typeof(SearchControlColors), typeof(CommonDocumentColors) }) {
						var typeName = type.Name;
						foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
							if (!prop.Name.EndsWith("Key", StringComparison.Ordinal)) {
								continue;
							}
							var value = prop.GetValue(null);
							if (value is not ThemeResourceKey key) {
								continue;
							}
							var color = VSColorTheme.GetThemedColor(key);
							writer.Write(typeName);
							writer.Write('\t');
							writer.Write(prop.Name);
							writer.Write('\t');
							writer.Write(color.ToHexString());
							writer.Write('\t');
							writer.Write(key.Category.ToString());
							writer.Write('\t');
							writer.Write(key.KeyType.Switch("ForegroundColor", "BackgroundColor", "ForegroundBrush", "BackgroundBrush", "?"));
							writer.WriteLine();
						}
					}
				}
			}

			sealed class AppResourcesExporter : ExporterBase
			{
				public static readonly AppResourcesExporter Instance = new();

				AppResourcesExporter() { }

				public override int IconId => IconIds.VisualStudio;
				public override string Title => R.T_ExportApplicationResources;
				public override string Description => R.T_ExportApplicationResourcesTip;
				protected override string DefaultFileName => "Application.Resources.txt";

				protected override void ExportContent(StreamWriter writer) {
					writer.WriteLine("ResourceKey\tKeyType\tValueType\tValueDescription");
					ProcessResourceDictionary(System.Windows.Application.Current.Resources, writer, []);
				}

				static void ProcessResourceDictionary(ResourceDictionary dict, StreamWriter writer, HashSet<ResourceDictionary> processed) {
					if (dict is null) return;
					if (!processed.Add(dict)) return;

					if (dict.Source != null) {
						writer.Write('#');
						writer.WriteLine(dict.Source.ToString());
					}

					var list = new List<ResourceEntryInfo>(dict.Count);
					foreach (DictionaryEntry entry in dict) {
						try {
							var e = new ResourceEntryInfo(entry);
							if (e.KeyString == "MainWindowActiveCaptionBrushKey") {

							}
							list.Add(e);
						}
						catch (Exception ex) {
							ex.Log();
						}
					}

					list.Sort();

					foreach (var item in list) {
						writer.Write(item.KeyString);
						writer.Write('\t');
						writer.Write(item.KeyType);
						writer.Write('\t');
						writer.Write(item.ValueType);
						writer.Write('\t');
						writer.WriteLine(item.ValueDescription);
					}

					if (dict.MergedDictionaries != null) {
						foreach (var mergedDict in dict.MergedDictionaries) {
							writer.WriteLine();
							ProcessResourceDictionary(mergedDict, writer, processed);
						}
					}
				}

				static string GetValueDescription(object value) {
					if (value == null) return String.Empty;

					if (value is Color color) {
						return color.ToHexString();
					}

					if (value is SolidColorBrush brush) {
						return brush.Color.ToHexString();
					}
					if (value is GradientBrush gradient) {
						return $"GradientBrush ({string.Join("; ", gradient.GradientStops)})";
					}
					if (value is TileBrush tile) {
						return $"TileBrush (Opacity:{tile.Opacity}, Stretch:{tile.Stretch})";
					}

					if (value is Style style) {
						var targetType = style.TargetType?.Name ?? "?";
						var basedOn = style.BasedOn != null ? $", BasedOn: {style.BasedOn.TargetType?.Name}" : String.Empty;
						return $"Style (@{targetType}{basedOn}, Setters: {style.Setters.Count})";
					}

					if (value is DataTemplate template) {
						return $"DataTemplate (DataType: {template.DataType?.ToString() ?? "<null>"})";
					}

					if (value is double d) return d.ToString("F2");
					if (value is int i) return i.ToString();
					if (value is bool b) return b.ToString();
					if (value is Thickness th) return $"L:{th.Left}, T:{th.Top}, R:{th.Right}, B:{th.Bottom}";
					if (value is CornerRadius cr) return $"TL:{cr.TopLeft}, TR:{cr.TopRight}, BR:{cr.BottomRight}, BL:{cr.BottomLeft}";
					if (value is GridLength gl) return gl.IsStar ? $"{gl.Value}*" : gl.Value.ToString();

					var str = value.ToString();
					return string.IsNullOrEmpty(str) ? String.Empty : (str.Length > 200 ? str.Substring(0, 200) + "..." : str);
				}

				sealed class ResourceEntryInfo(DictionaryEntry entry) : IComparable<ResourceEntryInfo>
				{
					public string KeyString { get; } = entry.Key is string k ? k
						: entry.Key is ThemeResourceKey rk ? rk.Name + "(KeyType: " + rk.KeyType + ", " + rk.Category + ")"
						: entry.Key?.ToString() ?? "<null>";
					public string KeyType { get; } = entry.Key?.GetType().Name ?? "<null>";
					public string ValueType { get; } = entry.Value?.GetType().Name ?? "<null>";
					public string ValueDescription { get; } = GetValueDescription(entry.Value);

					public int CompareTo(ResourceEntryInfo other) {
						return string.CompareOrdinal(KeyString, other.KeyString);
					}
				}
			}
		}
	}
}
