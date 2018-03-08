using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Media;
using Newtonsoft.Json;

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

		[DefaultValue(SpecialHighlightOptions.None)]
		public SpecialHighlightOptions SpecialHighlightOptions { get; set; }

		[DefaultValue(MarkerOptions.Default)]
		public MarkerOptions MarkerOptions { get; set; } = MarkerOptions.Default;

		[DefaultValue(QuickInfoOptions.Default)]
		public QuickInfoOptions QuickInfoOptions { get; set; } = QuickInfoOptions.Default;

		public double TopSpace {
			get => LineTransformers.LineHeightTransformProvider.TopSpace;
			set => LineTransformers.LineHeightTransformProvider.TopSpace = value;
		}
		public double BottomSpace {
			get => LineTransformers.LineHeightTransformProvider.BottomSpace;
			set => LineTransformers.LineHeightTransformProvider.BottomSpace = value;
		}
		public bool NoSpaceBetweenWrappedLines { get; set; }
		public List<CommentLabel> Labels { get; } = new List<CommentLabel>();
		public List<CommentStyle> CommentStyles { get; } = new List<CommentStyle>();
		public List<XmlCodeStyle> XmlCodeStyles { get; } = new List<XmlCodeStyle>();
		public List<CodeStyle> CodeStyles { get; } = new List<CodeStyle>();

		public static event EventHandler Loaded;
		public static event EventHandler Updated;

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
				Updated?.Invoke(Instance, EventArgs.Empty);
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
			var s = config.CommentStyles;
			CleanUpStyleEntry(s, loadFromTheme);
			for (int i = s.Count - 1; i >= 0; i--) {
				if (s[i] == null || Enum.IsDefined(typeof(CommentStyleTypes), s[i].StyleID) == false) {
					s.RemoveAt(i);
				}
			}
			MergeDefaultStyles(s);
			var cs = config.CodeStyles;
			CleanUpStyleEntry(cs, loadFromTheme);
			for (int i = cs.Count - 1; i >= 0; i--) {
				if (cs[i] == null || Enum.IsDefined(typeof(CodeStyleTypes), cs[i].StyleID) == false) {
					cs.RemoveAt(i);
				}
			}
			MergeDefaultCodeStyles(cs);
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
			ResetCodeStyle(Instance.CodeStyles, GetDefaultCodeStyles());
			ResetCodeStyle(Instance.XmlCodeStyles, GetDefaultXmlCodeStyles());
			Updated?.Invoke(Instance, EventArgs.Empty);
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
					Updated?.Invoke(this, EventArgs.Empty);
				}
			}
			catch (Exception ex) {
				Debug.WriteLine(ex.ToString());
			}
		}

		internal void FireConfigChangedEvent() {
			Updated?.Invoke(this, EventArgs.Empty);
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
			c.CommentStyles.AddRange(GetDefaultCommentStyles());
			c.CodeStyles.AddRange(GetDefaultCodeStyles());
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
		static void MergeDefaultStyles(List<CommentStyle> styles) {
			foreach (var s in GetDefaultCommentStyles()) {
				if (s.Id > 0 && styles.FindIndex(i=> i.StyleID == s.StyleID) == -1) {
					styles.Add(s);
				}
			}
		}
		static void MergeDefaultCodeStyles(List<CodeStyle> styles) {
			foreach (var s in GetDefaultCodeStyles()) {
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
				new CommentStyle(CommentStyleTypes.ToDo, Colors.White) { BackgroundColor = Constants.ToDoColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Note, Colors.White) { BackgroundColor = Constants.NoteColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Hack, Colors.White) { BackgroundColor = Constants.HackColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Heading1, Constants.CommentColor) { FontSize = 12 },
				new CommentStyle(CommentStyleTypes.Heading2, Constants.CommentColor) { FontSize = 8 },
				new CommentStyle(CommentStyleTypes.Heading3, Constants.CommentColor) { FontSize = 4 },
				new CommentStyle(CommentStyleTypes.Heading4, Constants.CommentColor) { FontSize = -1 },
				new CommentStyle(CommentStyleTypes.Heading5, Constants.CommentColor) { FontSize = -2 },
				new CommentStyle(CommentStyleTypes.Heading6, Constants.CommentColor) { FontSize = -3 },
				new CommentStyle(CommentStyleTypes.Task1, Constants.CommentColor) { UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Task2, Constants.CommentColor) { UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Task3, Constants.CommentColor) { UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Task4, Constants.CommentColor) { UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Task5, Constants.CommentColor) { UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Task6, Constants.CommentColor) { UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Task7, Constants.CommentColor) { UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Task8, Constants.CommentColor) { UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Task9, Constants.CommentColor) { UseScrollBarMarker = true },
			};
		}
		internal static CodeStyle[] GetDefaultCodeStyles() {
			var r = new CodeStyle[Enum.GetValues(typeof(CodeStyleTypes)).Length];
			for (int i = 0; i < r.Length; i++) {
				r[i] = new CodeStyle { StyleID = (CodeStyleTypes)i };
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
		internal void Set(QuickInfoOptions options, bool set) {
			QuickInfoOptions = AppHelpers.EnumHelper.SetFlags(QuickInfoOptions, options, set);
		}
		internal void Set(MarkerOptions options, bool set) {
			MarkerOptions = AppHelpers.EnumHelper.SetFlags(MarkerOptions, options, set);
		}
		internal void Set(SpecialHighlightOptions options, bool set) {
			SpecialHighlightOptions = AppHelpers.EnumHelper.SetFlags(SpecialHighlightOptions, options, set);
		}
	}

	abstract class StyleBase
	{
		static protected readonly Regex FriendlyNamePattern = new Regex(@"([a-z])([A-Z])", RegexOptions.Singleline);

		public abstract int Id { get; }
		/// <summary>Gets or sets whether the content rendered in bold.</summary>
		public bool? Bold { get; set; }
		/// <summary>Gets or sets whether the content rendered in italic.</summary>
		public bool? Italic { get; set; }
		/// <summary>Gets or sets whether the content rendered with overline.</summary>
		public bool? OverLine { get; set; }
		/// <summary>Gets or sets whether the content rendered stricken-through.</summary>
		public bool? Strikethrough { get; set; }
		/// <summary>Gets or sets whether the content rendered with underline.</summary>
		public bool? Underline { get; set; }
		/// <summary>Gets or sets the font size. Font size number is relative to the editor text size.</summary>
		public double FontSize { get; set; }
		/// <summary>Gets or sets the foreground color to render the text. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		public string ForegroundColor {
			get { return ForeColor.ToHexString(); }
			set { ForeColor = Utilities.ParseColor(value); }
		}
		/// <summary>Gets or sets the foreground color to render the text. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		public string BackgroundColor {
			get { return BackColor.ToHexString(); }
			set { BackColor = Utilities.ParseColor(value); }
		}
		/// <summary>Gets or sets the brush effect to draw the background color.</summary>
		public BrushEffect BackgroundEffect { get; set; }
		/// <summary>Gets or sets whether the denoted element is marked on the scrollbar.</summary>
		public bool UseScrollBarMarker { get; set; }
		/// <summary>Gets or sets the font.</summary>
		public string Font { get; set; }

		internal Color ForeColor { get; set; }
		internal Color BackColor { get; set; }

		public abstract string Category { get; }
		internal StyleBase Clone() {
			return (StyleBase)MemberwiseClone();
		}
		internal void CopyTo(StyleBase style) {
			style.Bold = Bold;
			style.Italic = Italic;
			style.OverLine = OverLine;
			style.Underline = Underline;
			style.Strikethrough = Strikethrough;
			style.FontSize = FontSize;
			style.BackgroundEffect = BackgroundEffect;
			style.Font = Font;
			style.ForeColor = ForeColor;
			style.BackColor = BackColor;
		}
		internal void Reset() {
			Bold = Italic = OverLine = Underline = Strikethrough = null;
			FontSize = 0;
			BackgroundEffect = BrushEffect.Solid;
			Font = null;
			ForeColor = BackColor = default(Color);
		}
	}

	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	sealed class CommentStyle : StyleBase
	{
		public CommentStyle() {
		}
		public CommentStyle(CommentStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor.ToHexString();
		}

		public override int Id => (int)StyleID;

		/// <summary>Gets or sets the comment style.</summary>
		public CommentStyleTypes StyleID { get; set; }

		public override string Category => Constants.SyntaxCategory.Comment;

		internal new CommentStyle Clone() {
			return (CommentStyle)MemberwiseClone();
		}

		public override string ToString() {
			return StyleID.ToString();
		}
	}

	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	sealed class XmlCodeStyle : StyleBase
	{
		public XmlCodeStyle() {
		}
		public XmlCodeStyle(XmlStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor.ToHexString();
		}

		public override int Id => (int)StyleID;

		/// <summary>Gets or sets the comment style.</summary>
		public XmlStyleTypes StyleID { get; set; }

		public override string Category => Constants.SyntaxCategory.Xml;

		internal new CommentStyle Clone() {
			return (CommentStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}

	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	sealed class CodeStyle : StyleBase
	{
		string _Category;

		public override int Id => (int)StyleID;

		/// <summary>Gets or sets the code style.</summary>
		public CodeStyleTypes StyleID { get; set; }

		public override string Category {
			get {
				if (_Category != null) {
					return _Category;
				}
				var f = typeof(CodeStyleTypes).GetField(StyleID.ToString());
				if (f == null) {
					return _Category = String.Empty;
				}
				var c = f.GetCustomAttribute<CategoryAttribute>(false);
				if (c == null) {
					return _Category = String.Empty;
				}
				return _Category = c.Category;
			}
		}
		internal new CodeStyle Clone() {
			return (CodeStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}

	[DebuggerDisplay("{Label} IgnoreCase: {IgnoreCase} AllowPunctuationDelimiter: {AllowPunctuationDelimiter}")]
	sealed class CommentLabel
	{
		string _label;
		int _labelLength;
		StringComparison _stringComparison;

		public CommentLabel() {
		}

		public CommentLabel(string label, CommentStyleTypes styleID) {
			Label = label;
			StyleID = styleID;
		}
		public CommentLabel(string label, CommentStyleTypes styleID, bool ignoreCase) {
			Label = label;
			StyleID = styleID;
			IgnoreCase = ignoreCase;
		}

		public bool AllowPunctuationDelimiter { get; set; }

		/// <summary>Gets or sets the label to identifier the comment type.</summary>
		public string Label { get { return _label; } set { _label = value; _labelLength = (value ?? String.Empty).Length; } }
		internal int LabelLength => _labelLength;
		/// <summary>Gets or sets whether the label is case-sensitive.</summary>
		public bool IgnoreCase {
			get => _stringComparison == StringComparison.OrdinalIgnoreCase;
			set => _stringComparison = value ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		}
		public CommentStyleApplication StyleApplication { get; set; }
		internal StringComparison Comparison => _stringComparison;
		/// <summary>Gets or sets the comment style.</summary>
		public CommentStyleTypes StyleID { get; set; }

		public CommentLabel Clone() {
			return (CommentLabel)MemberwiseClone();
		}
		public void CopyTo(CommentLabel label) {
			label.AllowPunctuationDelimiter = AllowPunctuationDelimiter;
			label.StyleApplication = StyleApplication;
			label.StyleID = StyleID;
			label._label = _label;
			label._labelLength = _labelLength;
			label._stringComparison = _stringComparison;
		}
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
	public enum QuickInfoOptions
	{
		None,
		Attributes = 1,
		BaseType = 1 << 1,
		BaseTypeInheritence = 1 << 2,
		Declaration = 1 << 3,
		ExtensionMethod = 1 << 4,
		Interfaces = 1 << 5,
		InterfacesInheritence = 1 << 6,
		NumericValues = 1 << 7,
		String = 1 << 8,
		Parameter = 1 << 9,
		InterfaceImplementations = 1 << 10,
		Default = Attributes | BaseType | Interfaces | NumericValues | InterfaceImplementations
	}

	[Flags]
	public enum SpecialHighlightOptions
	{
		None,
		XmlDocCode = 1,
		DeclarationBrace = 1 << 1
	}

	[Flags]
	public enum MarkerOptions
	{
		None,
		SpecialComment = 1,
		TypeDeclaration = 1 << 1,
		CompilerDirective = 1 << 2,
		LineNumber = 1 << 3,
		CodeRange = 1 << 4,
		LongMemberDeclaration = 1 << 5,
		Default = SpecialComment | TypeDeclaration | LineNumber | CodeRange | LongMemberDeclaration
	}
}
