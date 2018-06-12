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

namespace Codist
{
	sealed class Config
	{
		const string ThemePrefix = "res:";
		internal const string LightTheme = ThemePrefix + "Light", DarkTheme = ThemePrefix + "Dark";

		static DateTime _LastSaved, _LastLoaded;
		static int _LoadingConfig;

		public static readonly string ConfigPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + Constants.NameOfMe + "\\Config.json";
		public static Config Instance = InitConfig();

		[DefaultValue(Features.All)]
		public Features Features { get; set; }

		[DefaultValue(SpecialHighlightOptions.None)]
		public SpecialHighlightOptions SpecialHighlightOptions { get; set; }

		[DefaultValue(MarkerOptions.Default)]
		public MarkerOptions MarkerOptions { get; set; } = MarkerOptions.Default;

		[DefaultValue(QuickInfoOptions.Default)]
		public QuickInfoOptions QuickInfoOptions { get; set; } = QuickInfoOptions.Default;

		public double TopSpace { get; set; }
		public double BottomSpace { get; set; }
		public double QuickInfoMaxWidth { get; set; }
		public double QuickInfoMaxHeight { get; set; }
		public bool NoSpaceBetweenWrappedLines { get; set; }
		public List<CommentLabel> Labels { get; } = new List<CommentLabel>();
		public List<CommentStyle> CommentStyles { get; } = new List<CommentStyle>();
		public List<XmlCodeStyle> XmlCodeStyles { get; } = new List<XmlCodeStyle>();
		public List<CSharpStyle> CodeStyles { get; } = new List<CSharpStyle>();
		public List<CodeStyle> GeneralStyles { get; } = new List<CodeStyle>();

		public static event EventHandler Loaded;
		public static event EventHandler<ConfigUpdatedEventArgs> Updated;

		public static Config InitConfig() {
			//AppHelpers.LogHelper.UseLogMethod(i => Debug.WriteLine(i));
			if (File.Exists(ConfigPath) == false) {
				Config config = GetDefaultConfig();
				config.SaveConfig(ConfigPath);
				return config;
			}
			try {
				return InternalLoadConfig(ConfigPath);
			}
			catch (Exception ex) {
				Debug.WriteLine(ex.ToString());
				return GetDefaultConfig();
			}
		}

		public static void LoadConfig(string configPath) {
			//HACK: prevent redundant load operations issued by configuration pages
			if (_LastLoaded.AddSeconds(2) > DateTime.Now
				|| Interlocked.Exchange(ref _LoadingConfig, 1) != 0) {
				return;
			}
			try {
				Instance = InternalLoadConfig(configPath);
				Loaded?.Invoke(Instance, EventArgs.Empty);
				Updated?.Invoke(Instance, new ConfigUpdatedEventArgs(Features.All));
			}
			catch(Exception ex) {
				Debug.WriteLine(ex.ToString());
				Instance = GetDefaultConfig();
			}
			finally {
				_LoadingConfig = 0;
			}
		}

		static Config InternalLoadConfig(string configPath) {
			var configContent = configPath == LightTheme ? Properties.Resources.Light
				: configPath == DarkTheme ? Properties.Resources.Dark
				: File.ReadAllText(configPath);
			var loadFromTheme = configPath.StartsWith(ThemePrefix, StringComparison.Ordinal);
			Config config = JsonConvert.DeserializeObject<Config>(configContent, new JsonSerializerSettings {
				DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
				NullValueHandling = NullValueHandling.Ignore,
				Error = (sender, args) => {
					args.ErrorContext.Handled = true; // ignore json error
				}
			});
			var l = config.Labels;
			for (int i = l.Count - 1; i >= 0; i--) {
				if (String.IsNullOrWhiteSpace(l[i].Label)) {
					l.RemoveAt(i);
				}
			}
			if (l.Count == 0) {
				InitDefaultLabels(l);
			}
			var cs = config.GeneralStyles;
			CleanUpStyleEntry(cs, loadFromTheme);
			for (int i = cs.Count - 1; i >= 0; i--) {
				if (cs[i] == null || Enum.IsDefined(typeof(CodeStyleTypes), cs[i].StyleID) == false) {
					cs.RemoveAt(i);
				}
			}
			MergeDefaultCodeStyles(cs);
			var s = config.CommentStyles;
			CleanUpStyleEntry(s, loadFromTheme);
			for (int i = s.Count - 1; i >= 0; i--) {
				if (s[i] == null || Enum.IsDefined(typeof(CommentStyleTypes), s[i].StyleID) == false) {
					s.RemoveAt(i);
				}
			}
			MergeDefaultCommentStyles(s);
			var css = config.CodeStyles;
			CleanUpStyleEntry(css, loadFromTheme);
			for (int i = css.Count - 1; i >= 0; i--) {
				if (css[i] == null || Enum.IsDefined(typeof(CSharpStyleTypes), css[i].StyleID) == false) {
					css.RemoveAt(i);
				}
			}
			MergeDefaultCSharpStyles(css);
			var xcs = config.XmlCodeStyles;
			CleanUpStyleEntry(xcs, loadFromTheme);
			for (int i = xcs.Count - 1; i >= 0; i--) {
				if (xcs[i] == null || Enum.IsDefined(typeof(XmlStyleTypes), xcs[i].StyleID) == false) {
					xcs.RemoveAt(i);
				}
			}
			MergeDefaultXmlCodeStyles(xcs);
			if (loadFromTheme) {
				// don't override other settings if loaded from predefined themes
				ResetCodeStyle(Instance.GeneralStyles, config.GeneralStyles);
				ResetCodeStyle(Instance.CommentStyles, config.CommentStyles);
				ResetCodeStyle(Instance.CodeStyles, config.CodeStyles);
				ResetCodeStyle(Instance.XmlCodeStyles, config.XmlCodeStyles);
				_LastLoaded = DateTime.Now;
				return Instance;
			}
			_LastLoaded = DateTime.Now;
			Debug.WriteLine("Config loaded");
			return config;
		}

		public static void ResetStyles() {
			ResetCodeStyle(Instance.CommentStyles, GetDefaultCommentStyles());
			ResetCodeStyle(Instance.CodeStyles, GetDefaultCSharpStyles());
			ResetCodeStyle(Instance.XmlCodeStyles, GetDefaultXmlCodeStyles());
			Updated?.Invoke(Instance, new ConfigUpdatedEventArgs(Features.SyntaxHighlight));
		}

		public void SaveConfig(string path) {
			//HACK: prevent redundant save operations issued by configuration pages
			if (_LastSaved.AddSeconds(2) > DateTime.Now) {
				return;
			}
			path = path ?? ConfigPath;
			try {
				var d = Path.GetDirectoryName(path);
				if (Directory.Exists(d) == false) {
					Directory.CreateDirectory(d);
				}
				File.WriteAllText(path, JsonConvert.SerializeObject(
					this,
					Formatting.Indented,
					new JsonSerializerSettings {
						DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
						NullValueHandling = NullValueHandling.Ignore,
						Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
					}));
				if (path == ConfigPath) {
					_LastSaved = _LastLoaded = DateTime.Now;
					Debug.WriteLine("Config saved");
					//Updated?.Invoke(this, EventArgs.Empty);
				}
			}
			catch (Exception ex) {
				Debug.WriteLine(ex.ToString());
			}
		}

		internal void FireConfigChangedEvent(Features updatedFeature) {
			Updated?.Invoke(this, new ConfigUpdatedEventArgs(updatedFeature));
		}

		static void CleanUpStyleEntry<TStyle> (List<TStyle> styles, bool removeFontNames)
			where TStyle : StyleBase {
			styles.RemoveAll(i => i.Id < 1);
			styles.Sort((x, y) => x.Id - y.Id);
			if (removeFontNames) {
				styles.ForEach(i => i.Font = null);
			}
		}

		static Config GetDefaultConfig() {
			_LastLoaded = DateTime.Now;
			var c = new Config();
			InitDefaultLabels(c.Labels);
			c.GeneralStyles.AddRange(GetDefaultCodeStyles());
			c.CommentStyles.AddRange(GetDefaultCommentStyles());
			c.CodeStyles.AddRange(GetDefaultCSharpStyles());
			c.XmlCodeStyles.AddRange(GetDefaultXmlCodeStyles());
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
		static void MergeDefaultCodeStyles(List<CodeStyle> styles) {
			foreach (var s in GetDefaultCodeStyles()) {
				if (s.Id > 0 && styles.FindIndex(i => i.StyleID == s.StyleID) == -1) {
					styles.Add(s);
				}
			}
		}
		static void MergeDefaultCommentStyles(List<CommentStyle> styles) {
			foreach (var s in GetDefaultCommentStyles()) {
				if (s.Id > 0 && styles.FindIndex(i=> i.StyleID == s.StyleID) == -1) {
					styles.Add(s);
				}
			}
		}
		static void MergeDefaultCSharpStyles(List<CSharpStyle> styles) {
			foreach (var s in GetDefaultCSharpStyles()) {
				if (s.Id > 0 && styles.FindIndex(i => i.StyleID == s.StyleID) == -1) {
					styles.Add(s);
				}
			}
		}
		static void MergeDefaultXmlCodeStyles(List<XmlCodeStyle> styles) {
			foreach (var s in GetDefaultXmlCodeStyles()) {
				if (s.Id > 0 && styles.FindIndex(i => i.StyleID == s.StyleID) == -1) {
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
		internal static CodeStyle[] GetDefaultCodeStyles() {
			var r = new CodeStyle[Enum.GetValues(typeof(CodeStyleTypes)).Length];
			for (int i = 0; i < r.Length; i++) {
				r[i] = new CodeStyle { StyleID = (CodeStyleTypes)i };
			}
			return r;
		}
		internal static CSharpStyle[] GetDefaultCSharpStyles() {
			var r = new CSharpStyle[Enum.GetValues(typeof(CSharpStyleTypes)).Length];
			for (int i = 0; i < r.Length; i++) {
				r[i] = new CSharpStyle { StyleID = (CSharpStyleTypes)i };
			}
			return r;
		}
		internal static XmlCodeStyle[] GetDefaultXmlCodeStyles() {
			var r = new XmlCodeStyle[Enum.GetValues(typeof(XmlStyleTypes)).Length];
			for (int i = 0; i < r.Length; i++) {
				r[i] = new XmlCodeStyle { StyleID = (XmlStyleTypes)i };
			}
			return r;
		}
		internal void Set(Features options, bool set) {
			Features = AppHelpers.EnumHelper.SetFlags(Features, options, set);
		}
		internal void Set(QuickInfoOptions options, bool set) {
			QuickInfoOptions = AppHelpers.EnumHelper.SetFlags(QuickInfoOptions, options, set);
		}
		internal void Set(MarkerOptions options, bool set) {
			MarkerOptions = AppHelpers.EnumHelper.SetFlags(MarkerOptions, options, set);
			FireConfigChangedEvent(Features.ScrollbarMarkers);
		}
		internal void Set(SpecialHighlightOptions options, bool set) {
			SpecialHighlightOptions = AppHelpers.EnumHelper.SetFlags(SpecialHighlightOptions, options, set);
			FireConfigChangedEvent(Features.SyntaxHighlight);
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
		All = SyntaxHighlight | ScrollbarMarkers | SuperQuickInfo | SmartBar
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
		OverrideDefaultDocumentation = 1 << 20,
		DocumentationFromBaseType = 1 << 21,
		TextOnlyDoc = 1 << 22,
		ReturnsDoc = 1 << 23,
		Selection = 1 << 27,
		ClickAndGo = 1 << 28,
		CtrlQuickInfo = 1 << 29,
		HideOriginalQuickInfo = 1 << 30,
		QuickInfoOverride = OverrideDefaultDocumentation | DocumentationFromBaseType | ClickAndGo,
		Default = Attributes | BaseType | Interfaces | NumericValues | InterfaceImplementations | ClickAndGo,
	}

	[Flags]
	public enum SpecialHighlightOptions
	{
		None,
		SpecialComment = 1,
		DeclarationBrace = 1 << 1,
		ParameterBrace = 1 << 2,
		XmlDocCode = 1 << 3,
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
		CodeMarginMask = SpecialComment | CompilerDirective,
		Default = SpecialComment | MemberDeclaration | LineNumber | LongMemberDeclaration
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
