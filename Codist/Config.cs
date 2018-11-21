using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Media;
using Newtonsoft.Json;
using Codist.Classifiers;
using Codist.SyntaxHighlight;
using Codist.Margins;
using AppHelpers;

namespace Codist
{
	sealed class Config
	{
		const string ThemePrefix = "res:";
		const int DefaultIconSize = 20;
		internal const string LightTheme = ThemePrefix + "Light", DarkTheme = ThemePrefix + "Dark", SimpleTheme = ThemePrefix + "Simple";

		static DateTime _LastSaved, _LastLoaded;
		static int _LoadingConfig;

		ConfigManager _ConfigManager;

		public static readonly string ConfigPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + Constants.NameOfMe + "\\Config.json";
		public static Config Instance = InitConfig();

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

		public double TopSpace { get; set; }
		public double BottomSpace { get; set; }
		public double QuickInfoMaxWidth { get; set; }
		public double QuickInfoMaxHeight { get; set; }
		public bool NoSpaceBetweenWrappedLines { get; set; }
		[DefaultValue(DefaultIconSize)]
		public int SmartBarButtonSize { get; set; } = DefaultIconSize;
		public List<CommentLabel> Labels { get; } = new List<CommentLabel>();
		public List<CommentStyle> CommentStyles { get; } = new List<CommentStyle>();
		public List<XmlCodeStyle> XmlCodeStyles { get; } = new List<XmlCodeStyle>();
		public List<CSharpStyle> CodeStyles { get; } = new List<CSharpStyle>();
		public List<CppStyle> CppStyles { get; } = new List<CppStyle>();
		public List<CodeStyle> GeneralStyles { get; } = new List<CodeStyle>();
		public List<SymbolMarkerStyle> SymbolMarkerStyles { get; } = new List<SymbolMarkerStyle>();
		public List<MarkerStyle> MarkerSettings { get; } = new List<MarkerStyle>();

		public static event EventHandler Loaded;
		public static event EventHandler<ConfigUpdatedEventArgs> Updated;

		public static Config InitConfig() {
			if (File.Exists(ConfigPath) == false) {
				var config = GetDefaultConfig();
				config.SaveConfig(ConfigPath);
				return config;
			}
			try {
				return InternalLoadConfig(ConfigPath, false);
			}
			catch (Exception ex) {
				Debug.WriteLine(ex.ToString());
				return GetDefaultConfig();
			}
		}

		public static void LoadConfig(string configPath, bool stylesOnly = false) {
			if (Interlocked.Exchange(ref _LoadingConfig, 1) != 0) {
				return;
			}
			Debug.WriteLine("Load config: " + configPath);
			try {
				Instance = InternalLoadConfig(configPath, stylesOnly);
				//TextEditorHelper.ResetStyleCache();
				Loaded?.Invoke(Instance, EventArgs.Empty);
				Updated?.Invoke(Instance, new ConfigUpdatedEventArgs(stylesOnly ? Features.SyntaxHighlight : Features.All));
			}
			catch(Exception ex) {
				Debug.WriteLine(ex.ToString());
				Instance = GetDefaultConfig();
			}
			finally {
				_LoadingConfig = 0;
			}
		}

		static Config InternalLoadConfig(string configPath, bool stylesOnly) {
			var configContent = configPath == LightTheme ? Properties.Resources.Light
				: configPath == DarkTheme ? Properties.Resources.Dark
				: configPath == SimpleTheme ? Properties.Resources.Simple
				: File.ReadAllText(configPath);
			var config = JsonConvert.DeserializeObject<Config>(configContent, new JsonSerializerSettings {
				DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
				NullValueHandling = NullValueHandling.Ignore,
				Error = (sender, args) => {
					args.ErrorContext.Handled = true; // ignore json error
				}
			});
			if (stylesOnly == false) {
				var l = config.Labels;
				for (var i = l.Count - 1; i >= 0; i--) {
					if (String.IsNullOrWhiteSpace(l[i].Label)) {
						l.RemoveAt(i);
					}
				}
				if (l.Count == 0) {
					InitDefaultLabels(l);
				}
			}
			var removeFontNames = System.Windows.Forms.Control.ModifierKeys == System.Windows.Forms.Keys.Control;
			LoadStyleEntries<CodeStyle, CodeStyleTypes>(config.GeneralStyles, removeFontNames);
			LoadStyleEntries<CommentStyle, CommentStyleTypes>(config.CommentStyles, removeFontNames);
			LoadStyleEntries<CppStyle, CppStyleTypes>(config.CppStyles, removeFontNames);
			LoadStyleEntries<CSharpStyle, CSharpStyleTypes>(config.CodeStyles, removeFontNames);
			LoadStyleEntries<XmlCodeStyle, XmlStyleTypes>(config.XmlCodeStyles, removeFontNames);
			LoadStyleEntries<SymbolMarkerStyle, SymbolMarkerStyleTypes>(config.SymbolMarkerStyles, removeFontNames);
			if (stylesOnly) {
				// don't override other settings if loaded from predefined themes or syntax config file
				ResetCodeStyle(Instance.GeneralStyles, config.GeneralStyles);
				ResetCodeStyle(Instance.CommentStyles, config.CommentStyles);
				ResetCodeStyle(Instance.CodeStyles, config.CodeStyles);
				ResetCodeStyle(Instance.CppStyles, config.CppStyles);
				ResetCodeStyle(Instance.XmlCodeStyles, config.XmlCodeStyles);
				ResetCodeStyle(Instance.SymbolMarkerStyles, config.SymbolMarkerStyles);
				ResetCodeStyle(Instance.MarkerSettings, config.MarkerSettings);
				_LastLoaded = DateTime.Now;
				return Instance;
			}
			_LastLoaded = DateTime.Now;
			Debug.WriteLine("Config loaded");
			return config;
		}

		public static void ResetStyles() {
			ResetCodeStyle(Instance.GeneralStyles, GetDefaultCodeStyles<CodeStyle, CodeStyleTypes>());
			ResetCodeStyle(Instance.CommentStyles, GetDefaultCommentStyles());
			ResetCodeStyle(Instance.CodeStyles, GetDefaultCodeStyles<CSharpStyle, CSharpStyleTypes>());
			ResetCodeStyle(Instance.CppStyles, GetDefaultCodeStyles<CppStyle, CppStyleTypes>());
			ResetCodeStyle(Instance.XmlCodeStyles, GetDefaultCodeStyles<XmlCodeStyle, XmlStyleTypes>());
			ResetCodeStyle(Instance.SymbolMarkerStyles, GetDefaultCodeStyles<SymbolMarkerStyle, SymbolMarkerStyleTypes>());
			ResetCodeStyle(Instance.MarkerSettings, GetDefaultMarkerStyles());
			Loaded?.Invoke(Instance, EventArgs.Empty);
			Updated?.Invoke(Instance, new ConfigUpdatedEventArgs(Features.SyntaxHighlight));
		}

		public void SaveConfig(string path, bool stylesOnly = false) {
			path = path ?? ConfigPath;
			try {
				var d = Path.GetDirectoryName(path);
				if (Directory.Exists(d) == false) {
					Directory.CreateDirectory(d);
				}
				File.WriteAllText(path, JsonConvert.SerializeObject(
					stylesOnly ? (object)new StyleConfig(this) : this,
					Formatting.Indented,
					new JsonSerializerSettings {
						DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
						NullValueHandling = NullValueHandling.Ignore,
						Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
					}));
				if (path == ConfigPath) {
					_LastSaved = _LastLoaded = DateTime.Now;
					Debug.WriteLine("Config saved");
				}
			}
			catch (Exception ex) {
				Debug.WriteLine(ex.ToString());
			}
		}

		internal void BeginUpdate() {
			if (_ConfigManager == null) {
				_ConfigManager = new ConfigManager();
			}
		}
		internal void EndUpdate(bool apply) {
			var m = Interlocked.Exchange(ref _ConfigManager, null);
			if (m != null) {
				m.Quit(apply);
			}
		}
		internal void FireConfigChangedEvent(Features updatedFeature) {
			Updated?.Invoke(this, new ConfigUpdatedEventArgs(updatedFeature));
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
			Features = EnumHelper.SetFlags(Features, options, set);
		}
		internal void Set(DisplayOptimizations options, bool set) {
			DisplayOptimizations = EnumHelper.SetFlags(DisplayOptimizations, options, set);
		}
		internal void Set(QuickInfoOptions options, bool set) {
			QuickInfoOptions = EnumHelper.SetFlags(QuickInfoOptions, options, set);
		}
		internal void Set(NaviBarOptions options, bool set) {
			NaviBarOptions = EnumHelper.SetFlags(NaviBarOptions, options, set);
		}
		internal void Set(SmartBarOptions options, bool set) {
			SmartBarOptions = EnumHelper.SetFlags(SmartBarOptions, options, set);
			FireConfigChangedEvent(Features.SmartBar);
		}
		internal void Set(MarkerOptions options, bool set) {
			MarkerOptions = EnumHelper.SetFlags(MarkerOptions, options, set);
			FireConfigChangedEvent(Features.ScrollbarMarkers);
		}
		internal void Set(SpecialHighlightOptions options, bool set) {
			SpecialHighlightOptions = EnumHelper.SetFlags(SpecialHighlightOptions, options, set);
			FireConfigChangedEvent(Features.SyntaxHighlight);
		}

		static void LoadStyleEntries<TStyle, TStyleType> (List<TStyle> styles, bool removeFontNames)
			where TStyle : StyleBase<TStyleType>, new()
			where TStyleType : struct, Enum {
			styles.RemoveAll(i => i.Id < 1);
			styles.Sort((x, y) => x.Id - y.Id);
			if (removeFontNames) {
				styles.ForEach(i => i.Font = null);
			}
			for (var i = styles.Count - 1; i >= 0; i--) {
				if (styles[i] == null || EnumHelper.IsDefined(styles[i].StyleID) == false) {
					styles.RemoveAt(i);
				}
			}
			MergeDefaultCodeStyles<TStyle, TStyleType>(styles);
		}

		static Config GetDefaultConfig() {
			_LastLoaded = DateTime.Now;
			var c = new Config();
			InitDefaultLabels(c.Labels);
			c.GeneralStyles.AddRange(GetDefaultCodeStyles<CodeStyle, CodeStyleTypes>());
			c.CommentStyles.AddRange(GetDefaultCodeStyles<CommentStyle, CommentStyleTypes>());
			c.CodeStyles.AddRange(GetDefaultCodeStyles<CSharpStyle, CSharpStyleTypes>());
			c.CppStyles.AddRange(GetDefaultCodeStyles<CppStyle, CppStyleTypes>());
			c.XmlCodeStyles.AddRange(GetDefaultCodeStyles<XmlCodeStyle, XmlStyleTypes>());
			c.SymbolMarkerStyles.AddRange(GetDefaultCodeStyles<SymbolMarkerStyle, SymbolMarkerStyleTypes>());
			c.MarkerSettings.AddRange(GetDefaultMarkerStyles());
			return c;
		}

		static void InitDefaultLabels(List<CommentLabel> labels) {
			labels.AddRange (new CommentLabel[] {
				new CommentLabel("!", CommentStyleTypes.Emphasis),
				new CommentLabel("#", CommentStyleTypes.Emphasis),
				new CommentLabel("?", CommentStyleTypes.Question),
				new CommentLabel("!?", CommentStyleTypes.Exclaimation),
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
				new CommentLabel("undone", CommentStyleTypes.ToDo, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("NOTE", CommentStyleTypes.Note, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("HACK", CommentStyleTypes.Hack, true) { AllowPunctuationDelimiter = true },
			});
		}
		static void MergeDefaultCodeStyles<TStyle, TStyleType> (List<TStyle> styles)
			where TStyle : StyleBase<TStyleType>, new()
			where TStyleType : struct, Enum {
			foreach (var s in GetDefaultCodeStyles<TStyle, TStyleType>()) {
				if (s.Id > 0 && styles.FindIndex(i => ClrHacker.DirectCompare(i.StyleID, s.StyleID)) == -1) {
					styles.Add(s);
				}
			}
		}
		static void ResetCodeStyle<TStyle>(List<TStyle> source, IEnumerable<TStyle> target) {
			source.Clear();
			source.AddRange(target);
		}
		internal static CommentStyle[] GetDefaultCommentStyles() {
			return new CommentStyle[] {
				new CommentStyle(CommentStyleTypes.Emphasis, Constants.CommentColor) { Bold = true, FontSize = 10 },
				new CommentStyle(CommentStyleTypes.Exclaimation, Constants.ExclaimationColor),
				new CommentStyle(CommentStyleTypes.Question, Constants.QuestionColor),
				new CommentStyle(CommentStyleTypes.Deletion, Constants.DeletionColor) { Strikethrough = true },
				new CommentStyle(CommentStyleTypes.ToDo, Colors.White) { BackgroundColor = Constants.ToDoColor.ToHexString(), ScrollBarMarkerStyle = ScrollbarMarkerStyle.Square },
				new CommentStyle(CommentStyleTypes.Note, Colors.White) { BackgroundColor = Constants.NoteColor.ToHexString(), ScrollBarMarkerStyle = ScrollbarMarkerStyle.Square },
				new CommentStyle(CommentStyleTypes.Hack, Colors.White) { BackgroundColor = Constants.HackColor.ToHexString(), ScrollBarMarkerStyle = ScrollbarMarkerStyle.Square },
				new CommentStyle(CommentStyleTypes.Undone, Color.FromRgb(164, 175, 209)) { BackgroundColor = Constants.UndoneColor.ToHexString(), ScrollBarMarkerStyle = ScrollbarMarkerStyle.Square },
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
			public List<CodeStyle> GeneralStyles => GetDefinedStyles(_Config.GeneralStyles);
			public List<SymbolMarkerStyle> SymbolMarkerStyles => GetDefinedStyles(_Config.SymbolMarkerStyles);
		}

		sealed class ConfigManager
		{
			int _version, _oldVersion;
			public ConfigManager() {
				Updated += MarkUpdated;
			}
			void MarkUpdated(object sender, ConfigUpdatedEventArgs e) {
				++_version;
			}
			internal void Quit(bool apply) {
				Updated -= MarkUpdated;
				if (apply) {
					if (_version != _oldVersion) {
						Instance.SaveConfig(null);
					}
					_oldVersion = _version;
				}
				else {
					if (_version != _oldVersion) {
						LoadConfig(ConfigPath);
						_version = _oldVersion;
					}
				}
			}
		}
	}

	sealed class ConfigUpdatedEventArgs : EventArgs
	{
		public ConfigUpdatedEventArgs(Features updatedFeature) {
			UpdatedFeature = updatedFeature;
		}

		public Features UpdatedFeature { get; }
	}

	public enum BrushEffect
	{
		Solid,
		ToBottom,
		ToTop,
		ToRight,
		ToLeft
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
		All = SyntaxHighlight | ScrollbarMarkers | SuperQuickInfo | SmartBar | NaviBar
	}

	[Flags]
	public enum DisplayOptimizations
	{
		None,
		MainWindow,
		CodeWindow,
	}

	[Flags]
	public enum QuickInfoOptions
	{
		None,
		Attributes = 1,
		BaseType = 1 << 1,
		BaseTypeInheritence = 1 << 2,
		Declaration = 1 << 3,
		SymbolLocation = 1 << 4,
		Interfaces = 1 << 5,
		InterfacesInheritence = 1 << 6,
		NumericValues = 1 << 7,
		String = 1 << 8,
		Parameter = 1 << 9,
		InterfaceImplementations = 1 << 10,
		TypeParameters = 1 << 11,
		NamespaceTypes = 1 << 12,
		Diagnostics = 1 << 13,
		OverrideDefaultDocumentation = 1 << 17,
		DocumentationFromBaseType = 1 << 18,
		DocumentationFromInheritDoc = 1 << 19,
		TextOnlyDoc = 1 << 22,
		ReturnsDoc = 1 << 23,
		RemarksDoc = 1 << 24,
		AlternativeStyle = 1 << 25,
		Color = 1 << 26,
		Selection = 1 << 27,
		ClickAndGo = 1 << 28,
		CtrlQuickInfo = 1 << 29,
		HideOriginalQuickInfo = 1 << 30,
		QuickInfoOverride = OverrideDefaultDocumentation | DocumentationFromBaseType | ClickAndGo,
		Default = Attributes | BaseType | Interfaces | NumericValues | InterfaceImplementations | ClickAndGo | OverrideDefaultDocumentation,
	}

	[Flags]
	public enum SmartBarOptions
	{
		None,
		ExpansionIncludeTrivia = 1 << 1,
		ShiftToggleDisplay = 1 << 2,
		ManualDisplaySmartBar = 1 << 3,
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
		SpecialPunctuation = 1 << 7,
		Default = SpecialComment,
		AllBraces = DeclarationBrace | ParameterBrace | BranchBrace | LoopBrace | ResourceBrace | SpecialPunctuation
	}

	[Flags]
	public enum MarkerOptions
	{
		None,
		SpecialComment = 1,
		MemberDeclaration = 1 << 1,
		LongMemberDeclaration = 1 << 2,
		CompilerDirective = 1 << 3,
		LineNumber = 1 << 4,
		TypeDeclaration = 1 << 5,
		MethodDeclaration = 1 << 6,
		SymbolReference = 1 << 7,
		CodeMarginMask = SpecialComment | CompilerDirective,
		MemberMarginMask = MemberDeclaration | SymbolReference,
		Default = SpecialComment | MemberDeclaration | LineNumber | LongMemberDeclaration | SymbolReference
	}

	[Flags]
	public enum NaviBarOptions
	{
		None,
		SyntaxDetail = 1,
		SymbolToolTip = 1 << 1,
		RangeHighlight = 1 << 2,
		ParameterList = 1 << 10,
		ParameterListShowParamName = 1 << 11,
		FieldValue = 1 << 12,
		PartialClassMember = 1 << 13,
		Region = 1 << 14,
		Default = SyntaxDetail | SymbolToolTip | RangeHighlight | ParameterList | FieldValue | PartialClassMember | Region
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
