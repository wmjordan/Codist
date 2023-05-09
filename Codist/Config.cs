using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using AppHelpers;
using Codist.Margins;
using Codist.SyntaxHighlight;
using Codist.Taggers;
using Newtonsoft.Json;

namespace Codist
{
	sealed class Config
	{
		internal const string CurrentVersion = "7.3.0";
		const string ThemePrefix = "res:";
		const int DefaultIconSize = 20;
		internal const string LightTheme = ThemePrefix + "Light",
			PaleLightTheme = ThemePrefix + "PaleLight",
			DarkTheme = ThemePrefix + "Dark",
			PaleDarkTheme = ThemePrefix + "PaleDark",
			SimpleTheme = ThemePrefix + "Simple";

		#if DEBUG
		static DateTime __LastSaved, __LastLoaded;
		#endif
		static int __LoadingConfig;
		static Action<Config> __Loaded;
		static Action<ConfigUpdatedEventArgs> __Updated;

		ConfigManager _ConfigManager;

		public static readonly string ConfigPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\{Constants.NameOfMe}\\Config.json";
		public static Config Instance = InitConfig();

		public string Version { get; set; }
		internal InitStatus InitStatus { get; private set; }
		public string LogPath { get => LogHelper.LogPath; set => LogHelper.LogPath = value; }
		public bool ShouldSerializeLogPath() => String.IsNullOrEmpty(LogPath) == false;

		[DefaultValue(Features.All)]
		public Features Features { get; set; } = Features.All;

		[DefaultValue(DisplayOptimizations.None)]
		public DisplayOptimizations DisplayOptimizations { get; set; } = DisplayOptimizations.None;

		[DefaultValue(SpecialHighlightOptions.Default)]
		public SpecialHighlightOptions SpecialHighlightOptions { get; set; } = SpecialHighlightOptions.Default;

		[DefaultValue(MarkerOptions.Default)]
		public MarkerOptions MarkerOptions { get; set; } = MarkerOptions.Default;

		[DefaultValue(QuickInfoOptions.Default)]
		public QuickInfoOptions QuickInfoOptions { get; set; } = QuickInfoOptions.Default;

		[DefaultValue(SmartBarOptions.Default)]
		public SmartBarOptions SmartBarOptions { get; set; } = SmartBarOptions.Default;

		[DefaultValue(NaviBarOptions.Default)]
		public NaviBarOptions NaviBarOptions { get; set; } = NaviBarOptions.Default;

		[DefaultValue(BuildOptions.Default)]
		public BuildOptions BuildOptions { get; set; } = BuildOptions.Default;

		[DefaultValue(DeveloperOptions.Default)]
		public DeveloperOptions DeveloperOptions { get; set; } = DeveloperOptions.Default;

		[DefaultValue(SymbolToolTipOptions.Default)]
		public SymbolToolTipOptions SymbolToolTipOptions { get; set; } = SymbolToolTipOptions.Default;

		[DefaultValue(JumpListOptions.Default)]
		public JumpListOptions JumpListOptions { get; set; } = JumpListOptions.Default;

		[DefaultValue(0d)]
		public double TopSpace { get; set; }
		[DefaultValue(0d)]
		public double BottomSpace { get; set; }
		[DefaultValue(0d)]
		public double QuickInfoMaxWidth { get; set; }
		[DefaultValue(0d)]
		public double QuickInfoMaxHeight { get; set; }
		[DefaultValue(0d)]
		public double QuickInfoXmlDocExtraHeight { get; set; }
		[DefaultValue(false)]
		public bool NoSpaceBetweenWrappedLines { get; set; }
		[JsonIgnore]
		public bool SuppressAutoBuildVersion { get; set; }
		[DefaultValue(DefaultIconSize)]
		public int SmartBarButtonSize { get; set; } = DefaultIconSize;
		public List<CommentLabel> Labels { get; } = new List<CommentLabel>();
		public QuickInfoConfig QuickInfo { get; } = new QuickInfoConfig();

		#region Deprecated style containers
		public List<CommentStyle> CommentStyles { get; } = new List<CommentStyle>();
		public List<XmlCodeStyle> XmlCodeStyles { get; } = new List<XmlCodeStyle>();
		public List<CSharpStyle> CodeStyles { get; } = new List<CSharpStyle>();
		public List<CppStyle> CppStyles { get; } = new List<CppStyle>();
		public List<MarkdownStyle> MarkdownStyles { get; } = new List<MarkdownStyle>();
		public List<CodeStyle> GeneralStyles { get; } = new List<CodeStyle>();
		public List<SymbolMarkerStyle> SymbolMarkerStyles { get; } = new List<SymbolMarkerStyle>();
		public bool ShouldSerializeCommentStyles() => false;
		public bool ShouldSerializeXmlCodeStyles() => false;
		public bool ShouldSerializeCodeStyles() => false;
		public bool ShouldSerializeCppStyles() => false;
		public bool ShouldSerializeMarkdownStyles() => false;
		public bool ShouldSerializeGeneralStyles() => false;
		public bool ShouldSerializeSymbolMarkerStyles() => false;
		#endregion
		public List<SyntaxStyle> Styles { get; set; } // for serialization only
		public List<MarkerStyle> MarkerSettings { get; } = new List<MarkerStyle>();
		public List<SearchEngine> SearchEngines { get; } = new List<SearchEngine>();
		public List<WrapText> WrapTexts { get; } = new List<WrapText>();
		public SymbolReferenceMarkerStyle SymbolReferenceMarkerSettings { get; } = new SymbolReferenceMarkerStyle();
		public string BrowserPath { get; set; }
		public string BrowserParameter { get; set; }
		public string TaskManagerPath { get; set; }
		public string TaskManagerParameter { get; set; }
		internal bool IsChanged => _ConfigManager?.IsChanged ?? false;

		public static void RegisterLoadHandler(Action<Config> handler) {
			if (__Loaded != null) {
				foreach (var h in __Loaded.GetInvocationList()) {
					if (h.Equals(handler)) {
						$"Error: {handler} has already been registered as load handler".Log();
						return;
					}
				}
			}
			__Loaded += handler;
		}
		public static void UnregisterLoadHandler(Action<Config> handler) {
			if (__Loaded != null) {
				foreach (var h in __Loaded.GetInvocationList()) {
					if (h.Equals(handler)) {
						__Loaded -= handler;
						return;
					}
				}
			}
			$"Error: {handler} has not been registered as load handler".Log();
		}
		public static void RegisterUpdateHandler(Action<ConfigUpdatedEventArgs> handler) {
			if (__Updated != null) {
				foreach (var h in __Updated.GetInvocationList()) {
					if (h.Equals(handler)) {
						$"Error: {handler} has already been registered as update handler".Log();
						return;
					}
				}
			}
			__Updated += handler;
		}
		public static void UnregisterUpdateHandler(Action<ConfigUpdatedEventArgs> handler) {
			if (__Updated != null) {
				foreach (var h in __Updated.GetInvocationList()) {
					if (h.Equals(handler)) {
						__Updated -= handler;
						return;
					}
				}
			}
			$"Error: {handler} has not been registered as update handler".Log();
		}

		public static Config InitConfig() {
			if (File.Exists(ConfigPath) == false) {
				var config = GetDefaultConfig();
				config.InitStatus = InitStatus.FirstLoad;
				return config;
			}
			try {
				var config = InternalLoadConfig(ConfigPath, StyleFilters.None);
				if (System.Version.TryParse(config.Version, out var v) == false
					|| v < System.Version.Parse(CurrentVersion)) {
					config.InitStatus = InitStatus.Upgraded;
					UpgradeConfig(config, v);
				}
				return config;
			}
			catch (Exception ex) {
				ex.Log();
				return GetDefaultConfig();
			}
		}

		static void UpgradeConfig(Config config, Version oldVersion) {
			if (oldVersion < new Version(7, 4)
				&& config.QuickInfoOptions.MatchFlags(QuickInfoOptions.NodeRange) == false) {
				config.QuickInfoOptions |= QuickInfoOptions.NodeRange;
				__Updated?.Invoke(new ConfigUpdatedEventArgs(config, Features.SuperQuickInfo));
			}
		}

		public static void LoadConfig(string configPath, StyleFilters styleFilter = StyleFilters.None) {
			if (Interlocked.Exchange(ref __LoadingConfig, 1) != 0) {
				return;
			}
			$"Load config: {configPath}".Log();
			try {
				Instance = InternalLoadConfig(configPath, styleFilter);
				__Loaded?.Invoke(Instance);
				__Updated?.Invoke(new ConfigUpdatedEventArgs(Instance, styleFilter != StyleFilters.None ? Features.SyntaxHighlight : Features.All));
			}
			catch(Exception ex) {
				ex.Log();
				Instance = GetDefaultConfig();
			}
			finally {
				__LoadingConfig = 0;
			}
		}

		static Config InternalLoadConfig(string configPath, StyleFilters styleFilter) {
			var configContent = configPath == LightTheme ? Properties.Resources.Light
				: configPath == DarkTheme ? Properties.Resources.Dark
				: configPath == PaleLightTheme ? Properties.Resources.PaleLight
				: configPath == PaleDarkTheme ? Properties.Resources.PaleDark
				: configPath == SimpleTheme ? Properties.Resources.Simple
				: File.ReadAllText(configPath);
			var config = JsonConvert.DeserializeObject<Config>(configContent, new JsonSerializerSettings {
				DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
				NullValueHandling = NullValueHandling.Ignore,
				Error = (sender, args) => {
					args.ErrorContext.Handled = true; // ignore json error
				}
			});
			if (styleFilter == StyleFilters.None) {
				config.Labels.RemoveAll(i => String.IsNullOrWhiteSpace(i.Label));
				config.SearchEngines.RemoveAll(i => String.IsNullOrWhiteSpace(i.Name) || String.IsNullOrWhiteSpace(i.Pattern));
				config.WrapTexts.RemoveAll(i => String.IsNullOrWhiteSpace(i.Pattern));
			}
			var removeFontNames = System.Windows.Forms.Control.ModifierKeys == System.Windows.Forms.Keys.Control;
			LoadStyleEntries<CodeStyle, CodeStyleTypes>(config.GeneralStyles, removeFontNames);
			LoadStyleEntries<CommentStyle, CommentStyleTypes>(config.CommentStyles, removeFontNames);
			LoadStyleEntries<CppStyle, CppStyleTypes>(config.CppStyles, removeFontNames);
			LoadStyleEntries<CSharpStyle, CSharpStyleTypes>(config.CodeStyles, removeFontNames);
			LoadStyleEntries<XmlCodeStyle, XmlStyleTypes>(config.XmlCodeStyles, removeFontNames);
			LoadStyleEntries<MarkdownStyle, MarkdownStyleTypes>(config.MarkdownStyles, removeFontNames);
			LoadStyleEntries<SymbolMarkerStyle, SymbolMarkerStyleTypes>(config.SymbolMarkerStyles, removeFontNames);
			if (styleFilter == StyleFilters.All) {
				// don't override other settings if loaded from predefined themes or syntax config file
				ResetCodeStyle(Instance.GeneralStyles, config.GeneralStyles);
				ResetCodeStyle(Instance.CommentStyles, config.CommentStyles);
				ResetCodeStyle(Instance.CodeStyles, config.CodeStyles);
				ResetCodeStyle(Instance.CppStyles, config.CppStyles);
				ResetCodeStyle(Instance.XmlCodeStyles, config.XmlCodeStyles);
				ResetCodeStyle(Instance.MarkdownStyles, config.MarkdownStyles);
				ResetCodeStyle(Instance.SymbolMarkerStyles, config.SymbolMarkerStyles);
				ResetCodeStyle(Instance.MarkerSettings, config.MarkerSettings);
				Instance.Styles = config.Styles;
				#if DEBUG
				__LastLoaded = DateTime.Now;
				#endif
				return Instance;
			}
			else if (styleFilter != StyleFilters.None) {
				MergeCodeStyle(Instance.GeneralStyles, config.GeneralStyles, styleFilter);
				MergeCodeStyle(Instance.CommentStyles, config.CommentStyles, styleFilter);
				MergeCodeStyle(Instance.CodeStyles, config.CodeStyles, styleFilter);
				MergeCodeStyle(Instance.CppStyles, config.CppStyles, styleFilter);
				MergeCodeStyle(Instance.XmlCodeStyles, config.XmlCodeStyles, styleFilter);
				MergeCodeStyle(Instance.SymbolMarkerStyles, config.SymbolMarkerStyles, styleFilter);
				ResetCodeStyle(Instance.MarkerSettings, config.MarkerSettings);
				Instance.Styles = config.Styles;
				#if DEBUG
				__LastLoaded = DateTime.Now;
				#endif
				return Instance;
			}
			Display.LayoutOverride.Reload(config.DisplayOptimizations);
			Display.ResourceMonitor.Reload(config.DisplayOptimizations);
			#if DEBUG
			__LastLoaded = DateTime.Now;
			#endif
			"Config loaded".Log();
			return config;
		}

		public static void ResetStyles() {
			FormatStore.Reset();
			__Updated?.Invoke(new ConfigUpdatedEventArgs(Instance, Features.SyntaxHighlight));
		}

		public void ResetSearchEngines() {
			ResetSearchEngines(SearchEngines);
		}
		public static void ResetSearchEngines(List<SearchEngine> engines) {
			engines.Clear();
			engines.AddRange(new[] {
				new SearchEngine("Bing", "https://www.bing.com/search?q=%s"),
				new SearchEngine("Google", "https://www.google.com/search?q=%s"),
				new SearchEngine("StackOverflow", "https://stackoverflow.com/search?q=%s"),
				new SearchEngine("GitHub", "https://github.com/search?q=%s"),
				new SearchEngine("CodeProject", "https://www.codeproject.com/search.aspx?q=%s&x=0&y=0&sbo=kw"),
				new SearchEngine(".NET Core Source", "https://source.dot.net/#q=%s"),
				new SearchEngine(".NET Framework Source", "https://referencesource.microsoft.com/#q=%s"),
			});
		}

		public void ResetWrapTexts() {
			ResetWrapTexts(WrapTexts);
		}
		public static void ResetWrapTexts(List<WrapText> wrapTexts) {
			wrapTexts.Clear();
			wrapTexts.AddRange(new[] {
				new WrapText("\"$\"", "\"\""),
				new WrapText("($)", "()"),
				new WrapText("'$'", "''"),
				new WrapText("[$]", "[]"),
				new WrapText("{$}", "{}"),
				new WrapText("<$>", "<>"),
				new WrapText("%$%", "%%"),
			});
		}

		public void SaveConfig(string path, bool stylesOnly = false) {
			path = path ?? ConfigPath;
			try {
				var d = Path.GetDirectoryName(path);
				if (Directory.Exists(d) == false) {
					Directory.CreateDirectory(d);
				}
				object o;
				if (stylesOnly) {
					o = new {
						Version = CurrentVersion,
						Styles = GetCustomizedStyles()
					};
				}
				else {
					o = this;
					Version = CurrentVersion;
					Styles = GetCustomizedStyles().ToList();
				}
				File.WriteAllText(path, JsonConvert.SerializeObject(
					o,
					Formatting.Indented,
					new JsonSerializerSettings {
						DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
						NullValueHandling = NullValueHandling.Ignore,
						Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
					}));
				if (path == ConfigPath) {
					#if DEBUG
					__LastSaved = __LastLoaded = DateTime.Now;
					#endif
					"Config saved".Log();
				}
			}
			catch (Exception ex) {
				ex.Log();
			}
			finally {
				Styles = null;
			}

			IEnumerable<SyntaxStyle> GetCustomizedStyles() {
				return FormatStore.GetStyles()
					.Where(i => i.Value?.IsSet == true)
					.Select(i => { var s = new SyntaxStyle(i.Key); i.Value.CopyTo(s); return s; });
			}
		}

		internal void BeginUpdate() {
			if (_ConfigManager == null) {
				_ConfigManager = new ConfigManager();
			}
		}
		internal void EndUpdate(bool apply) {
			Interlocked.Exchange(ref _ConfigManager, null)?.Quit(apply);
		}
		internal void FireConfigChangedEvent(Features updatedFeature) {
			__Updated?.Invoke(new ConfigUpdatedEventArgs(this, updatedFeature));
		}
		internal void FireConfigChangedEvent(Features updatedFeature, object parameter) {
			__Updated?.Invoke(new ConfigUpdatedEventArgs(this, updatedFeature) { Parameter = parameter });
		}
		internal static TStyle[] GetDefaultCodeStyles<TStyle, TStyleType>()
			where TStyle : StyleBase<TStyleType>, new()
			where TStyleType : struct, Enum {
			var r = new TStyle[Enum.GetValues(typeof(TStyleType)).Length];
			for (var i = 0; i < r.Length; i++) {
				r[i] = new TStyle { StyleID = ClrHacker.DirectCast<int, TStyleType>(i) };
			}
			return r;
		}
		internal static MarkerStyle[] GetDefaultMarkerStyles() {
			return new MarkerStyle[] {
				new MarkerStyle(MarkerStyleTypes.SymbolReference, Colors.Cyan)
			};
		}
		internal static List<TStyle> GetDefinedStyles<TStyle>(List<TStyle> styles)
			where TStyle : StyleBase {
			return styles.FindAll(s => s.IsSet);
		}
		internal void Set(Features options, bool set) {
			Features = Features.SetFlags(options, set);
		}
		internal void Set(DisplayOptimizations options, bool set) {
			DisplayOptimizations = DisplayOptimizations.SetFlags(options, set);
		}
		internal void Set(QuickInfoOptions options, bool set) {
			QuickInfoOptions = QuickInfoOptions.SetFlags(options, set);
		}
		internal void Set(NaviBarOptions options, bool set) {
			NaviBarOptions = NaviBarOptions.SetFlags(options, set);
		}
		internal void Set(SmartBarOptions options, bool set) {
			SmartBarOptions = SmartBarOptions.SetFlags(options, set);
		}
		internal void Set(MarkerOptions options, bool set) {
			MarkerOptions = MarkerOptions.SetFlags(options, set);
		}
		internal void Set(SpecialHighlightOptions options, bool set) {
			SpecialHighlightOptions = SpecialHighlightOptions.SetFlags(options, set);
		}
		internal void Set(BuildOptions options, bool set) {
			BuildOptions = BuildOptions.SetFlags(options, set);
		}
		internal void Set(DeveloperOptions options, bool set) {
			DeveloperOptions = DeveloperOptions.SetFlags(options, set);
		}
		internal void Set(SymbolToolTipOptions options, bool set) {
			SymbolToolTipOptions = SymbolToolTipOptions.SetFlags(options, set);
		}
		internal void Set(JumpListOptions options, bool set) {
			JumpListOptions = JumpListOptions.SetFlags(options, set);
		}

		static void LoadStyleEntries<TStyle, TStyleType> (List<TStyle> styles, bool removeFontNames)
			where TStyle : StyleBase<TStyleType>, new()
			where TStyleType : struct, Enum {
			styles.RemoveAll(i => i.Id < 1);
			if (removeFontNames) {
				styles.ForEach(i => i.Font = null);
			}
			for (var i = styles.Count - 1; i >= 0; i--) {
				if (styles[i] == null || EnumHelper.IsDefined(styles[i].StyleID) == false) {
					styles.RemoveAt(i);
				}
			}
			MergeDefaultCodeStyles<TStyle, TStyleType>(styles);
			styles.Sort((x, y) => x.Id - y.Id);
		}

		static Config GetDefaultConfig() {
			#if DEBUG
			__LastLoaded = DateTime.Now;
			#endif
			var c = new Config();
			InitDefaultLabels(c.Labels);
			ResetSearchEngines(c.SearchEngines);
			ResetWrapTexts(c.WrapTexts);
			c.GeneralStyles.AddRange(GetDefaultCodeStyles<CodeStyle, CodeStyleTypes>());
			c.CommentStyles.AddRange(GetDefaultCodeStyles<CommentStyle, CommentStyleTypes>());
			c.CodeStyles.AddRange(GetDefaultCodeStyles<CSharpStyle, CSharpStyleTypes>());
			c.CppStyles.AddRange(GetDefaultCodeStyles<CppStyle, CppStyleTypes>());
			c.XmlCodeStyles.AddRange(GetDefaultCodeStyles<XmlCodeStyle, XmlStyleTypes>());
			c.MarkdownStyles.AddRange(GetDefaultCodeStyles<MarkdownStyle, MarkdownStyleTypes>());
			c.SymbolMarkerStyles.AddRange(GetDefaultCodeStyles<SymbolMarkerStyle, SymbolMarkerStyleTypes>());
			c.MarkerSettings.AddRange(GetDefaultMarkerStyles());
			c.SymbolReferenceMarkerSettings.Reset();
			return c;
		}

		static void InitDefaultLabels(List<CommentLabel> labels) {
			labels.AddRange (new CommentLabel[] {
				new CommentLabel("!", CommentStyleTypes.Emphasis),
				new CommentLabel("#", CommentStyleTypes.Emphasis),
				new CommentLabel("?", CommentStyleTypes.Question),
				new CommentLabel("!?", CommentStyleTypes.Exclamation),
				new CommentLabel("x", CommentStyleTypes.Deletion, true),
				new CommentLabel("+++", CommentStyleTypes.Heading1),
				new CommentLabel("!!", CommentStyleTypes.Heading1),
				new CommentLabel("++", CommentStyleTypes.Heading2),
				new CommentLabel("+", CommentStyleTypes.Heading3),
				new CommentLabel("-", CommentStyleTypes.Heading4),
				new CommentLabel("--", CommentStyleTypes.Heading5),
				new CommentLabel("---", CommentStyleTypes.Heading6),
				new CommentLabel("TODO", CommentStyleTypes.ToDo, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("TO-DO", CommentStyleTypes.ToDo, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("undone", CommentStyleTypes.Undone, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("NOTE", CommentStyleTypes.Note, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("HACK", CommentStyleTypes.Hack, true) { AllowPunctuationDelimiter = true },
			});
		}
		static void MergeDefaultCodeStyles<TStyle, TStyleType> (List<TStyle> styles)
			where TStyle : StyleBase<TStyleType>, new()
			where TStyleType : struct, Enum {
			foreach (var s in GetDefaultCodeStyles<TStyle, TStyleType>()) {
				if (s.Id > 0 && styles.Find(i => ClrHacker.DirectCompare(i.StyleID, s.StyleID)) == null) {
					styles.Add(s);
				}
			}
		}
		static void ResetCodeStyle<TStyle>(List<TStyle> source, IEnumerable<TStyle> target) {
			source.Clear();
			source.AddRange(target);
		}
		static void MergeCodeStyle<TStyle>(List<TStyle> source, IEnumerable<TStyle> target, StyleFilters styleFilters) where TStyle : StyleBase {
			foreach (var item in target) {
				if (item.IsSet == false) {
					continue;
				}
				var s = source.Find(i => i.Id == item.Id);
				if (s != null) {
					item.CopyTo(s, styleFilters);
				}
			}
		}
		internal static CommentStyle[] GetDefaultCommentStyles() {
			return new CommentStyle[] {
				new CommentStyle(CommentStyleTypes.Emphasis, Constants.CommentColor) { Bold = true, FontSize = 10 },
				new CommentStyle(CommentStyleTypes.Exclamation, Constants.ExclamationColor),
				new CommentStyle(CommentStyleTypes.Question, Constants.QuestionColor),
				new CommentStyle(CommentStyleTypes.Deletion, Constants.DeletionColor) { Strikethrough = true },
				new CommentStyle(CommentStyleTypes.ToDo, Colors.White) { BackColor = Constants.ToDoColor, ScrollBarMarkerStyle = ScrollbarMarkerStyle.Square },
				new CommentStyle(CommentStyleTypes.Note, Colors.White) { BackColor = Constants.NoteColor, ScrollBarMarkerStyle = ScrollbarMarkerStyle.Square },
				new CommentStyle(CommentStyleTypes.Hack, Colors.LightGreen) { BackColor = Constants.HackColor, ScrollBarMarkerStyle = ScrollbarMarkerStyle.Square },
				new CommentStyle(CommentStyleTypes.Undone, Color.FromRgb(164, 175, 209)) { BackColor = Constants.UndoneColor, ScrollBarMarkerStyle = ScrollbarMarkerStyle.Square },
				new CommentStyle(CommentStyleTypes.Heading1) { FontSize = 12 },
				new CommentStyle(CommentStyleTypes.Heading2) { FontSize = 8 },
				new CommentStyle(CommentStyleTypes.Heading3) { FontSize = 4 },
				new CommentStyle(CommentStyleTypes.Heading4) { FontSize = -1 },
				new CommentStyle(CommentStyleTypes.Heading5) { FontSize = -2 },
				new CommentStyle(CommentStyleTypes.Heading6) { FontSize = -3 },
				new CommentStyle(CommentStyleTypes.Task1) { ScrollBarMarkerStyle = ScrollbarMarkerStyle.Number1 },
				new CommentStyle(CommentStyleTypes.Task2) { ScrollBarMarkerStyle = ScrollbarMarkerStyle.Number2 },
				new CommentStyle(CommentStyleTypes.Task3) { ScrollBarMarkerStyle = ScrollbarMarkerStyle.Number3 },
				new CommentStyle(CommentStyleTypes.Task4) { ScrollBarMarkerStyle = ScrollbarMarkerStyle.Number4 },
				new CommentStyle(CommentStyleTypes.Task5) { ScrollBarMarkerStyle = ScrollbarMarkerStyle.Number5 },
				new CommentStyle(CommentStyleTypes.Task6) { ScrollBarMarkerStyle = ScrollbarMarkerStyle.Number6 },
				new CommentStyle(CommentStyleTypes.Task7) { ScrollBarMarkerStyle = ScrollbarMarkerStyle.Number7 },
				new CommentStyle(CommentStyleTypes.Task8) { ScrollBarMarkerStyle = ScrollbarMarkerStyle.Number8 },
				new CommentStyle(CommentStyleTypes.Task9) { ScrollBarMarkerStyle = ScrollbarMarkerStyle.Number9 },
			};
		}

		sealed class StyleConfig
		{
			readonly Config _Config;

			public StyleConfig(Config config) {
				_Config = config;
			}

			public List<CommentStyle> CommentStyles => GetDefinedStyles(_Config.CommentStyles);
			public List<XmlCodeStyle> XmlCodeStyles => GetDefinedStyles(_Config.XmlCodeStyles);
			public List<CSharpStyle> CodeStyles => GetDefinedStyles(_Config.CodeStyles);
			public List<CppStyle> CppStyles => GetDefinedStyles(_Config.CppStyles);
			public List<MarkdownStyle> MarkdownStyles => GetDefinedStyles(_Config.MarkdownStyles);
			public List<CodeStyle> GeneralStyles => GetDefinedStyles(_Config.GeneralStyles);
			public List<SymbolMarkerStyle> SymbolMarkerStyles => GetDefinedStyles(_Config.SymbolMarkerStyles);
		}

		sealed class ConfigManager
		{
			int _Version, _OldVersion;
			public ConfigManager() {
				__Updated += MarkUpdated;
			}
			public bool IsChanged => _Version != _OldVersion;
			void MarkUpdated(ConfigUpdatedEventArgs e) {
				++_Version;
			}
			internal void MarkVersioned() {
				_OldVersion = _Version;
			}
			internal void Quit(bool apply) {
				__Updated -= MarkUpdated;
				if (apply) {
					if (_Version != _OldVersion) {
						Instance.SaveConfig(null);
						_OldVersion = _Version;
					}
				}
				else {
					if (_Version != _OldVersion) {
						LoadConfig(ConfigPath);
						_Version = _OldVersion;
					}
				}
			}
		}
	}

	public sealed class QuickInfoConfig
	{
		Color _BackColor;

		[DefaultValue(Constants.EmptyColor)]
		public string BackgroundColor {
			get => _BackColor.A == 0 ? Constants.EmptyColor : _BackColor.ToHexString();
			set => UIHelper.ParseColor(value, out _BackColor, out _);
		}

		internal Color BackColor { get => _BackColor; set => _BackColor = value; }
	}
	sealed class SearchEngine
	{
		public SearchEngine() {}
		public SearchEngine(string name, string pattern) {
			Name = name;
			Pattern = pattern;
		}
		public string Name { get; set; }
		public string Pattern { get; set; }
	}
	sealed class WrapText
	{
		string _Prefix, _Suffix, _Pattern, _Substitution;
		char _Indicator;
		public const char DefaultIndicator = '$';
		public WrapText(string pattern, string name = null, char indicator = DefaultIndicator) {
			Indicator = indicator;
			Pattern = pattern;
			Name = name;
		}

		public string Name { get; set; }
		public string Pattern {
			get => _Pattern;
			set {
				_Pattern = value;
				InternalUpdate();
			}
		}

		public char Indicator {
			get => _Indicator;
			set {
				_Indicator = value;
				InternalUpdate();
			}
		}

		internal string Prefix => _Prefix;
		internal string Suffix => _Suffix;
		internal string Substitution => _Substitution;

		public string Wrap(string text) {
			return _Prefix
				+ text
				+ (_Substitution != null ? _Suffix.Replace(_Substitution, text) : _Suffix);
		}

		void InternalUpdate() {
			int p;
			if (_Pattern != null && (p = _Pattern.IndexOf(Indicator)) >= 0) {
				_Prefix = _Pattern.Substring(0, p);
				_Suffix = _Pattern.Substring(p + 1);
				_Substitution = _Suffix.Contains(Indicator) ? Indicator.ToString() : null;
			}
			else {
				_Prefix = _Pattern;
				_Suffix = String.Empty;
				_Substitution = null;
			}
		}
	}

	sealed class ConfigUpdatedEventArgs : EventArgs
	{
		public ConfigUpdatedEventArgs(Config config, Features updatedFeature) {
			Config = config;
			UpdatedFeature = updatedFeature;
		}
		public Config Config { get; }
		public Features UpdatedFeature { get; }
		public object Parameter { get; set; }
	}

	enum InitStatus
	{
		Normal,
		FirstLoad,
		Upgraded,
	}

	public enum BrushEffect
	{
		Solid,
		ToBottom,
		ToTop,
		ToRight,
		ToLeft
	}

	public enum LineStyle
	{
		Solid,
		Dot,
		Dash,
		DashDot,
		Squiggle
	}

	[Flags]
	public enum Features
	{
		None,
		SyntaxHighlight = 1,
		ScrollbarMarkers = 1 << 1,
		SuperQuickInfo = 1 << 2,
		SmartBar = 1 << 3,
		NaviBar = 1 << 4,
		WebSearch = 1 << 5,
		WrapText = 1 << 6,
		JumpList = 1 << 7,
		All = SyntaxHighlight | ScrollbarMarkers | SuperQuickInfo | SmartBar | NaviBar | WebSearch | WrapText | JumpList
	}

	[Flags]
	public enum DisplayOptimizations
	{
		None,
		MainWindow,
		CodeWindow = 1 << 1,
		CompactMenu = 1 << 2,
		HideSearchBox = 1 << 3,
		HideFeedbackBox = 1 << 4,
		HideAccountBox = 1 << 5,
		ShowCpu = 1 << 6,
		ShowMemory = 1 << 7,
		ShowDrive = 1 << 8,
		ShowNetwork = 1	<< 9,
		ResourceMonitors = ShowCpu | ShowMemory | ShowDrive | ShowNetwork
	}

	[Flags]
	public enum QuickInfoOptions
	{
		None,
		[Obsolete]
		ClickAndGo = 0,
		[Obsolete]
		BaseTypeInheritance = 0,
		[Obsolete]
		InterfacesInheritance = 0,
		NodeRange = 1,
		Attributes = 1 << 1,
		BaseType = 1 << 2,
		Declaration = 1 << 3,
		SymbolLocation = 1 << 4,
		Interfaces = 1 << 5,
		Enum = 1 << 6,
		NumericValues = 1 << 7,
		String = 1 << 8,
		Parameter = 1 << 9,
		InterfaceImplementations = 1 << 10,
		TypeParameters = 1 << 11,
		NamespaceTypes = 1 << 12,
		Diagnostics = 1 << 13,
		MethodOverload = 1 << 14,
		InterfaceMembers = 1 << 15,
		ContainingType = 1 << 16,
		OverrideDefaultDocumentation = 1 << 17,
		DocumentationFromBaseType = 1 << 18,
		DocumentationFromInheritDoc = 1 << 19,
		SeeAlsoDoc = 1 << 20,
		TextOnlyDoc = 1 << 21,
		ExceptionDoc = 1 << 22,
		ReturnsDoc = 1 << 23,
		RemarksDoc = 1 << 24,
		ExampleDoc = 1 << 25,
		Color = 1 << 26,
		Selection = 1 << 27,
		CtrlSuppress = 1 << 28,
		[Obsolete]
		CtrlSupress = 1 << 28,
		CtrlQuickInfo = 1 << 29,
		AlternativeStyle = 1 << 30,
		UseCodeFontForXmlDocSymbol = 1 << 31,
		DocumentationOverride = OverrideDefaultDocumentation | DocumentationFromBaseType | DocumentationFromInheritDoc,
		QuickInfoOverride = DocumentationOverride | AlternativeStyle,
		Default = NodeRange | AlternativeStyle | Attributes | BaseType | Interfaces | Enum | NumericValues | InterfaceImplementations | MethodOverload | Parameter | OverrideDefaultDocumentation | DocumentationFromBaseType | DocumentationFromInheritDoc | SeeAlsoDoc | ExceptionDoc | ReturnsDoc | RemarksDoc,
	}

	[Flags]
	public enum SmartBarOptions
	{
		None,
		ExpansionIncludeTrivia = 1 << 1,
		ShiftToggleDisplay = 1 << 2,
		ManualDisplaySmartBar = 1 << 3,
		DoubleIndentRefactoring = 1 << 4,
		Default = ExpansionIncludeTrivia | ShiftToggleDisplay
	}

	[Flags]
	public enum SpecialHighlightOptions
	{
		None,
		SpecialComment = 1,
		DeclarationBrace = 1 << 1,
		ParameterBrace = 1 << 2,
		SymbolIdentifier = 1 << 3,
		BranchBrace = 1 << 4,
		LoopBrace = 1 << 5,
		ResourceBrace = 1 << 6,
		CastBrace = 1 << 7,
		SpecialPunctuation = 1 << 8,
		LocalFunctionDeclaration = 1 << 10,
		NonPrivateField = 1 << 11,
		UseTypeStyleOnConstructor = 1 << 12,
		CapturingLambdaExpression = 1 << 13,
		SearchResult = 1 << 20,
		Default = SpecialComment,
		AllParentheses = ParameterBrace | CastBrace | BranchBrace | LoopBrace | ResourceBrace,
		AllBraces = DeclarationBrace | ParameterBrace | CastBrace | BranchBrace | LoopBrace | ResourceBrace | SpecialPunctuation
	}

	[Flags]
	public enum StyleFilters
	{
		None,
		Color = 1,
		FontFamily = 1 << 1,
		FontSize = 1 << 2,
		FontStyle = 1 << 3,
		LineStyle = 1 << 4,
		All = Color | FontFamily | FontSize | FontStyle | LineStyle
	}

	[Flags]
	public enum MarkerOptions
	{
		None,
		SpecialComment = 1,
		MemberDeclaration = 1 << 1,
		LongMemberDeclaration = 1 << 2,
		CompilerDirective = 1 << 3,
		RegionDirective = 1 << 4,
		TypeDeclaration = 1 << 5,
		MethodDeclaration = 1 << 6,
		SymbolReference = 1 << 7,
		Selection = 1 << 8,
		LineNumber = 1 << 9,
		DisableChangeTracker = 1 << 10,
		CodeMarginMask = SpecialComment | CompilerDirective,
		MemberMarginMask = MemberDeclaration | SymbolReference,
		Default = SpecialComment | MemberDeclaration | LineNumber | Selection | LongMemberDeclaration | SymbolReference
	}

	[Flags]
	public enum NaviBarOptions
	{
		None,
		SyntaxDetail = 1,
		SymbolToolTip = 1 << 1,
		RangeHighlight = 1 << 2,
		RegionOnBar = 1 << 3,
		StripRegionNonLetter = 1 << 4,
		LineOfCode = 1 << 5,
		ReferencingTypes = 1 << 6,
		ParameterList = 1 << 10,
		ParameterListShowParamName = 1 << 11,
		FieldValue = 1 << 12,
		AutoPropertiesAsFields = 1 << 13,
		PartialClassMember = 1 << 14,
		Region = 1 << 15,
		RegionInMember = 1 << 16,
		BaseClassMember = 1 << 17,
		MemberType = 1 << 18,
		CtrlGoToSource = 1 << 19,
		Default = RangeHighlight | RegionOnBar | ParameterList | FieldValue | AutoPropertiesAsFields | PartialClassMember | Region
	}

	[Flags]
	public enum SymbolToolTipOptions
	{
		None,
		XmlDocSummary = 1,
		NumericValues = 1 << 1,
		Attributes = 1 << 2,
		Colors = 1 << 3,
		Default = XmlDocSummary | NumericValues | Attributes | Colors
	}

	[Flags]
	public enum BuildOptions
	{
		None,
		BuildTimestamp = 1,
		PrintSolutionProjectProperties = 1 << 1,
		ShowOutputPaneAfterBuild = 1 << 2,
		VsixAutoIncrement = 1 << 8,
		Default = None
	}

	[Flags]
	public enum DeveloperOptions
	{
		None,
		[Obsolete]
		ShowDocumentContentType = 1,
		ShowWindowInformer = 1,
		ShowSyntaxClassificationInfo = 1 << 1,
		Default = None
	}

	[Flags]
	public enum JumpListOptions
	{
		None,
		SafeMode = 1,
		DemonstrationMode = 1 << 1,
		[Obsolete]
		DemostrationMode = 1 << 1,
		NoScaling = 1 << 2,
		Default = SafeMode | DemonstrationMode | NoScaling
	}

	public enum ScrollbarMarkerStyle
	{
		None,
		Square,
		Circle,
		Number1,
		Number2,
		Number3,
		Number4,
		Number5,
		Number6,
		Number7,
		Number8,
		Number9,
	}
}
