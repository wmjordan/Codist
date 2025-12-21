using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using CLR;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Newtonsoft.Json;
using SharpYaml.Serialization;
using R = Codist.Properties.Resources;

namespace Codist.Taggers
{
	sealed class CustomTagger : CachedTaggerBase
	{
		const string ConfigFileName = "codist.ct", JsonExtension = ".json", YamlExtension = ".yaml";

		readonly bool _FullParse;
		readonly ITextDocument _TextDocument;
		TaggerConfig _Config;
		string _Path;
		TextTaggerBase[] _Taggers;

		CustomTagger(TaggerConfig config, string path, ITextDocument doc, ITextView view) : base(view) {
			_Config = config;
			_Path = path;
			_TextDocument = doc;
			_Taggers = config.GetTaggers(path);
			config.AddTagger(this);
			doc.FileActionOccurred += FileActionOccurred;
		}

		public static CustomTagger Get(ITextView view, ITextDocument doc) {
			return TagDefinitionProvider.CreateCustomTagger(view, doc);
		}

		public void Drop() {
			_TextDocument.FileActionOccurred -= FileActionOccurred;
			_Config?.RemoveTagger(this);
		}

		protected override bool DoFullParseAtFirstLoad => _FullParse;
		protected override void Parse(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
			TextTaggerBase[] taggers;
			if ((taggers = _Taggers) == null || taggers.Length == 0) {
				return;
			}
			var ctx = new TaggerContext(span, results);
			foreach (var tagger in taggers) {
				tagger.GetTags(ctx);
			}
		}

		public static void ConvertJsonConfigToYaml(string jsonConfigPath) {
			LoadAndConvertJsonConfigFile(jsonConfigPath, JsonExtension, File.ReadAllText(jsonConfigPath));
		}

		static List<TextTaggerBase> CreateTaggers(CustomTagDefinitionSet defSet) {
			var taggers = new List<TextTaggerBase>();
			var cts = ServicesHelper.Instance.ClassificationTypeRegistry;
			foreach (var item in defSet.Items) {
				TextTaggerBase tagger;
				if (String.IsNullOrEmpty(item.Match) == false) {
					tagger = MakeRegexTagger(cts, item);
				}
				else if (String.IsNullOrEmpty(item.Contains) == false) {
					tagger = MakeContainsTagger(cts, item);
				}
				else {
					continue;
				}
				if (tagger != null) {
					taggers.Add(tagger);
				}
			}
			return taggers;
		}

		static RegexTagger MakeRegexTagger(IClassificationTypeRegistryService cts, DefinitionItem item) {
			if (item.Tag is null || cts.TryGetClassificationTag(item.Tag, out ClassificationTag ct) == false) {
				ct = null;
			}
			var ts = item.GroupTags?.Length > 0
				? Array.ConvertAll(item.GroupTags, i => String.IsNullOrWhiteSpace(i) == false && cts.TryGetClassificationTag(i, out var t) ? t : null)
				: null;
			return ct == null && ts == null
				? null
				: new RegexTagger {
					Pattern = item.Match,
					IgnoreCase = item.IgnoreCase,
					Tag = ct,
					GroupTags = ts,
					WholeLine = item.TagLine
				};
		}

		static ContainsTextTagger MakeContainsTagger(IClassificationTypeRegistryService cts, DefinitionItem item) {
			return item.Tag != null
				&& cts.TryGetClassificationTag(item.Tag, out ClassificationTag ct)
				? new ContainsTextTagger {
					Content = item.Contains,
					IgnoreCase = item.IgnoreCase,
					Tag = ct,
					WholeLine = item.TagLine
				}
				: null;
		}

		void FileActionOccurred(object sender, TextDocumentFileActionEventArgs e) {
			switch (e.FileActionType) {
				case FileActionTypes.ContentSavedToDisk:
				case FileActionTypes.DocumentRenamed:
					_Path = e.FilePath;
					var config = TagDefinitionProvider.GetTaggerConfig(_Path);
					if (_Config != config) {
						_Config?.RemoveTagger(this);
						_Config = config;
						config?.AddTagger(this);
					}
					_Taggers = config?.GetTaggers(_Path);
					break;
			}
		}

		static bool NameEquals(string a, string b) {
			return String.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}

		static List<CustomTagDefinitionSet> LoadAndConvertJsonConfigFile(string configPath, string extension, string t) {
			var r = JsonConvert.DeserializeObject<List<CustomTagDefinitionSet>>(t);
			YamlLoader.Save(configPath.Substring(0, configPath.Length - extension.Length) + YamlExtension, r);
			File.Delete(configPath);
			return r;
		}

		sealed class TaggerContext
		{
			readonly ITextSnapshot _Snapshot;
			string _SpanText;

			public TaggerContext(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
				Span = span;
				Results = results;
				_Snapshot = span.Snapshot;
			}

			public SnapshotSpan Span { get; }
			public ICollection<TaggedContentSpan> Results { get; }
			public string Text => _SpanText != null ? _SpanText : (_SpanText = Span.GetText());

			public void AddTag(IClassificationTag tag) {
				Results.Add(new TaggedContentSpan(tag, Span, 0, 0));
			}

			public void AddTag(IClassificationTag tag, int startOffset, int length) {
				Results.Add(new TaggedContentSpan(tag, _Snapshot, Span.Start.Position + startOffset, length, 0, 0));
			}

			public void AddTagAndContentOffset(IClassificationTag tag, int startOffset, int length) {
				Results.Add(new TaggedContentSpan(tag, Span, startOffset, length));
			}
		}

		abstract class TextTaggerBase
		{
			public IClassificationTag Tag { get; set; }
			public bool IgnoreCase { get; set; }
			public bool WholeLine { get; set; }

			public abstract void GetTags(TaggerContext ctx);
		}

		sealed class StartWithTagger : TextTaggerBase
		{
			string _Prefix;
			int _PrefixLength;

			public bool SkipLeadingWhitespace { get; set; }
			public string Prefix {
				get => _Prefix;
				set { _Prefix = value; _PrefixLength = value.Length; }
			}

			public override void GetTags(TaggerContext ctx) {
				var span = ctx.Span;
				if (SkipLeadingWhitespace) {
					var l = span.Length;
					for (int i = 0; i < l; i++) {
						if (Char.IsWhiteSpace(span.CharAt(i)) == false) {
							if (span.HasTextAtOffset(_Prefix, IgnoreCase, i)) {
								if (WholeLine) {
									ctx.AddTag(Tag);
									return;
								}
								ctx.AddTagAndContentOffset(Tag, i += _PrefixLength, l - i);
							}
							return;
						}
					}
				}
				else if (span.StartsWith(_Prefix, IgnoreCase)) {
					if (WholeLine) {
						ctx.AddTag(Tag);
						return;
					}
					ctx.AddTagAndContentOffset(Tag, _PrefixLength, span.Length - _PrefixLength);
				}
			}
		}

		sealed class ContainsTextTagger : TextTaggerBase
		{
			string _Content;
			int _ContentLength;

			public string Content {
				get => _Content;
				set { _Content = value; _ContentLength = value.Length; }
			}

			public override void GetTags(TaggerContext ctx) {
				int i, p;
				if (WholeLine) {
					if (ctx.Span.IndexOf(_Content, 0, IgnoreCase) >= 0) {
						ctx.AddTag(Tag);
					}
				}
				else {
					i = 0;
					while ((p = ctx.Span.IndexOf(_Content, i, IgnoreCase)) >= 0) {
						ctx.AddTag(Tag, p, _ContentLength);
						i += p + _ContentLength;
					}
				}
			}
		}

		sealed class RegexTagger : TextTaggerBase
		{
			Regex _Expression;
			string _Pattern;

			public string Pattern {
				get => _Pattern;
				set { _Pattern = value; _Expression = null; }
			}
			public IClassificationTag[] GroupTags { get; set; }

			public override void GetTags(TaggerContext ctx) {
				if (_Expression == null) {
					_Expression = new Regex(_Pattern, MakeRegexOptions());
				}

				var text = ctx.Text;
				int i = 0;
				Match m;
				while (i < text.Length && (m = _Expression.Match(text, i)).Success) {
					if (GroupTags?.Length > 0) {
						TagMatchedGroups(ctx, m);
					}
					if (!WholeLine && Tag != null) {
						ctx.AddTag(Tag, m.Index, m.Length);
					}
					i = m.Index + m.Length;
				}
				if (WholeLine && i != 0 && Tag != null) {
					ctx.AddTag(Tag);
				}
			}

			void TagMatchedGroups(TaggerContext ctx, Match m) {
				for (int i = Math.Min(GroupTags.Length, m.Groups.Count) - 1; i >= 0; i--) {
					var t = GroupTags[i];
					if (t != null) {
						var g = m.Groups[i + 1];
						if (g.Success) {
							ctx.AddTag(t, g.Index, g.Length);
						}
					}
				}
			}

			RegexOptions MakeRegexOptions() {
				return (IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)
					| RegexOptions.CultureInvariant
					| RegexOptions.Compiled;
			}
		}

		static class TagDefinitionProvider
		{
			static readonly Dictionary<string, TaggerConfig> __TaggerConfigs = new Dictionary<string, TaggerConfig>(StringComparer.OrdinalIgnoreCase);
			static readonly TaggerConfig __GlobalTaggerConfigs = GetTaggerConfig(GlobalConfigIdentifier);
			const string OutputWindowIdentifier = "<output>", GlobalConfigIdentifier = "<global>",
				JsonConfigExtension = ".ct.json",
				YamlConfigExtension = ".ct.yaml";
			const int MaxLookupParentDir = 5;

			static TagDefinitionProvider() {
				Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += ClearConfigsAfterCloseSolution;

				static void ClearConfigsAfterCloseSolution(object sender, EventArgs a) {
					var removes = new Chain<string>();
					foreach (var item in __TaggerConfigs) {
						var c = item.Value;
						c.CleanUp();
						if (c.AssociatedTaggerCount == 0) {
							removes.Add(item.Key);
							c.Dispose();
						}
					}
					foreach (var item in removes) {
						__TaggerConfigs.Remove(item);
					}
				}
			}

			public static CustomTagger CreateCustomTagger(ITextView view, ITextDocument doc) {
				if (view.Roles.Contains("OUTPUTWINDOW")) {
					return __GlobalTaggerConfigs is null
						? null
						: new CustomTagger(__GlobalTaggerConfigs, OutputWindowIdentifier, doc, view);
				}
				var path = doc.FilePath;
				var config = GetTaggerConfig(path) ?? __GlobalTaggerConfigs;
				return config == null
					? null
					: new CustomTagger(config, path, doc, view);
			}

			internal static TaggerConfig GetTaggerConfig(string path) {
				string dir;
				int level;
				bool isGlobal = false;
				if (String.IsNullOrEmpty(path)
					|| path.EndsWith(JsonConfigExtension, StringComparison.OrdinalIgnoreCase)
					|| path.EndsWith(YamlConfigExtension, StringComparison.OrdinalIgnoreCase)) {
					return null;
				}
				if (path == GlobalConfigIdentifier) {
					dir = Config.ConfigDirectory;
					level = MaxLookupParentDir; // disable up-dir lookup
					isGlobal = true;
				}
				else {
					dir = Path.GetDirectoryName(path);
					level = 0;
				}
				do {
					if (dir.Length == 0) {
						goto EXIT;
					}
					if (TryGetConfig(dir, YamlExtension, isGlobal, out var config)
						|| TryGetConfig(dir, JsonExtension, isGlobal, out config)) {
						return config;
					}
					dir = Path.GetDirectoryName(dir);
				}
				while (++level < MaxLookupParentDir && dir != null);
				EXIT:
				return path == GlobalConfigIdentifier
					? new TaggerConfig(Config.ConfigDirectory, ConfigFileName + YamlExtension, null)
					: null;
			}

			static bool TryGetConfig(string dir, string extension, bool isGlobal, out TaggerConfig config) {
				var configPath = Path.Combine(dir, ConfigFileName + extension);
				if (isGlobal) {
					config = null;
				}
				else if (__TaggerConfigs.TryGetValue(configPath, out config)) {
					if (config.Items != null) {
						return true;
					}
					config.Dispose();
					__TaggerConfigs.Remove(configPath);
				}
				var s = LoadDefinitionSets(configPath, extension);
				if (s != null) {
					config = new TaggerConfig(dir, ConfigFileName + extension, s);
					if (!isGlobal) {
						__TaggerConfigs[configPath] = config;
					}
					return true;
				}
				return false;
			}

			static List<CustomTagDefinitionSet> LoadDefinitionSets(string configPath, string extension) {
				if (File.Exists(configPath) == false) {
					return null;
				}
				try {
					return LoadConfigFile(configPath, extension);
				}
				catch (Exception ex) {
					ex.Log();
					CodistPackage.ShowError(ex, R.T_FailedToLoadSyntaxCustomizationFile + "\n" + configPath);
				}
				return new List<CustomTagDefinitionSet>();
			}

			static List<CustomTagDefinitionSet> LoadConfigFile(string configPath, string extension) {
				var t = File.ReadAllText(configPath);
				return extension == YamlExtension
					? YamlLoader.Load(t)
					: JsonConvert.DeserializeObject<List<CustomTagDefinitionSet>>(t);
			}
		}

		// This class represents a config file for custom syntax highlight rules
		// It can be shared by multiple taggers
		// FileSystemWatcher is used to monitor file changes
		//   once the underlying config file is changed,
		//   it will reload the config file and signals linked _CustomTaggers about the change
		sealed class TaggerConfig : IDisposable
		{
			readonly FileSystemWatcher _Watcher;
			readonly object _Sync = new object();
			readonly string _Dir;
			bool _UsingYaml;
			string _MonitoringFileName, _ChangedFile;
			Timer _Timer;
			List<CustomTagDefinitionSet> _Items;
			List<CustomTagger> _CustomTaggers;

			public TaggerConfig(string dir, string fileName, List<CustomTagDefinitionSet> taggers) {
				var watcher = new FileSystemWatcher(dir, ConfigFileName + ".*");
				watcher.Changed += OnConfigFileChanged;
				watcher.Renamed += OnConfigFileChanged;
				watcher.Created += OnConfigFileChanged;
				watcher.Deleted += OnConfigFileChanged;
				watcher.EnableRaisingEvents = true;
				_Watcher = watcher;
				_Items = taggers?.Count != 0 ? taggers : null;
				_Dir = dir;
				_MonitoringFileName = fileName;
				_UsingYaml = NameEquals(Path.GetExtension(fileName), YamlExtension);
			}

			public List<CustomTagDefinitionSet> Items => _Items;

			public int AssociatedTaggerCount => _CustomTaggers?.Count ?? 0;

			public void AddTagger(CustomTagger tagger) {
				lock (_Sync) {
					if (_CustomTaggers == null) {
						_CustomTaggers = new List<CustomTagger>();
					}
					_CustomTaggers.Add(tagger);
				}
			}

			public void RemoveTagger(CustomTagger tagger) {
				lock (_Sync) {
					_CustomTaggers?.Remove(tagger);
				}
			}

			public void CleanUp() {
				lock (_Sync) {
					_CustomTaggers.RemoveAll(i => i.TextView.IsClosed);
				}
			}

			public TextTaggerBase[] GetTaggers(string path) {
				var definitions = _Items;
				if (definitions == null) {
					return null;
				}
				var r = new List<TextTaggerBase>();
				foreach (var item in definitions) {
					if (item.MatchPath(path)) {
						var taggers = item.GetTaggers();
						if (taggers != null) {
							r.AddRange(taggers);
						}
					}
				}
				return r.Count != 0 ? r.ToArray() : null;
			}

			void OnConfigFileChanged(object sender, FileSystemEventArgs e) {
				var file = e.ChangeType == WatcherChangeTypes.Renamed ? ((RenamedEventArgs)e).OldName : e.Name;
				if (e.ChangeType.HasAnyFlag(WatcherChangeTypes.Deleted | WatcherChangeTypes.Renamed | WatcherChangeTypes.Created)) {
					if (NameEquals(_MonitoringFileName, file)) {
						_Items = null;
						_UsingYaml = false;
					}
				}
				var ext = Path.GetExtension(e.FullPath);
				var isYaml = NameEquals(ext, YamlExtension);
				if (!isYaml) {
					if (_UsingYaml) {
						// ignore JSON file change if using YAML
						return;
					}
					if (!NameEquals(ext, JsonExtension)) {
						// if file extension is not JSON or YAML
						return;
					}
				}
				_ChangedFile = e.FullPath;
				if (_Timer == null) {
					_Timer = new Timer(UpdateConfigFile, e.FullPath, 1000, -1);
				}
				else {
					_Timer.Change(1000, -1);
				}
			}

			void UpdateConfigFile(object filePath) {
				var f = _ChangedFile;
				try {
					if (!File.Exists(f)) {
						_Items = null;
						_UsingYaml = false;
					}
					else {
						var isYaml = NameEquals(Path.GetExtension(f), YamlExtension);
						var t = File.ReadAllText(f);
						_Items = isYaml
							? YamlLoader.Load(t)
							: JsonConvert.DeserializeObject<List<CustomTagDefinitionSet>>(t);
						_UsingYaml = isYaml;
						_MonitoringFileName = Path.GetFileName(f);
					}
					lock (_Sync) {
						foreach (var item in _CustomTaggers) {
							item._Taggers = GetTaggers(item._Path);
							item.OnTagsChanged(null);
						}
					}
				}
				catch (Exception ex) {
					_Items = null;
					ex.Log();
					CodistPackage.ShowError(ex, R.T_FailedToLoadSyntaxCustomizationFile + "\n" + f);
				}
			}

			public void Dispose() {
				_Watcher.Dispose();
				_Timer?.Dispose();
				Debug.Assert(_CustomTaggers == null || _CustomTaggers.Count == 0);
				_CustomTaggers = null;
				$"Unload tagger config: {_Dir}\\{_MonitoringFileName}".Log();
			}
		}

		sealed class CustomTagDefinitionSet
		{
			Regex _Pattern;
			TextTaggerBase[] _Taggers;

			[JsonProperty("file")]
			[YamlMember("file")]
			public string FilePattern {
				get => _Pattern?.ToString();
				set {
					_Pattern = new Regex(value, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
					_Taggers = null;
				}
			}

			[JsonProperty("items")]
			public List<DefinitionItem> Items { get; } = new List<DefinitionItem>();

			public bool MatchPath(string path) {
				return _Pattern?.IsMatch(path) != false;
			}

			public TextTaggerBase[] GetTaggers() {
				return _Taggers ?? (_Taggers = CreateTaggers(this).ToArray());
			}
		}

		sealed class DefinitionItem
		{
			[JsonProperty("match")]
			public string Match { get; set; }
			[JsonProperty("contains")]
			public string Contains { get; set; }
			[JsonProperty("ignoreCase")]
			[DefaultValue(false)]
			public bool IgnoreCase { get; set; }
			[JsonProperty("tag")]
			public string Tag { get; set; }
			[JsonProperty("groupTags")]
			public string[] GroupTags { get; set; }
			[JsonProperty("tagLine")]
			[DefaultValue(false)]
			public bool TagLine { get; set; }
		}

		static class YamlLoader
		{
			static readonly Serializer __Serializer = new Serializer(new SerializerSettings {
				NamingConvention = new CamelCaseNamingConvention(),
				IgnoreNulls = true,
			});
			internal static List<CustomTagDefinitionSet> Load(string yaml) {
				return String.IsNullOrWhiteSpace(yaml)
					? null
					: __Serializer.Deserialize<List<CustomTagDefinitionSet>>(yaml);
			}

			internal static void Save(string filePath, List<CustomTagDefinitionSet> r) {
				File.WriteAllText(filePath, __Serializer.Serialize(r));
			}
		}
	}
}
