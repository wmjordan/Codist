using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using System.ComponentModel;

namespace Codist
{
	sealed class Config
	{
		static DateTime LastSaved;

		public static readonly string ConfigPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + Constants.NameOfMe + "\\Config.json";
		public static Config Instance = InitConfig();

		[DefaultValue(false)]
		public bool HighlightXmlDocCData { get; set; }
		[DefaultValue(false)]
		public bool MarkAbstractions { get; set; }
		[DefaultValue(true)]
		public bool MarkComments { get; set; } = true;
		[DefaultValue(true)]
		public bool MarkDeclarations { get; set; } = true;
		[DefaultValue(true)]
		public bool MarkDirectives { get; set; } = true;
		[DefaultValue(true)]
		public bool MarkLineNumbers { get; set; } = true;
		public bool ShowQuickInfo => ShowAttributesQuickInfo || ShowBaseTypeQuickInfo || ShowExtensionMethodQuickInfo || ShowInterfacesQuickInfo || ShowNumericQuickInfo || ShowStringQuickInfo;
		[DefaultValue(true)]
		public bool ShowAttributesQuickInfo { get; set; } = true;
		[DefaultValue(true)]
		public bool ShowBaseTypeQuickInfo { get; set; } = true;
		[DefaultValue(true)]
		public bool ShowBaseTypeInheritenceQuickInfo { get; set; } = true;
		[DefaultValue(false)]
		public bool ShowExtensionMethodQuickInfo { get; set; }
		[DefaultValue(false)]
		public bool ShowInterfacesQuickInfo { get; set; }
		[DefaultValue(false)]
		public bool ShowInterfacesInheritenceQuickInfo { get; set; }
		[DefaultValue(true)]
		public bool ShowNumericQuickInfo { get; set; } = true;
		[DefaultValue(true)]
		public bool ShowStringQuickInfo { get; set; } = true;

		public double TopSpace {
			get => LineTransformers.LineHeightTransformProvider.TopSpace;
			set => LineTransformers.LineHeightTransformProvider.TopSpace = value;
		}
		public double BottomSpace {
			get => LineTransformers.LineHeightTransformProvider.BottomSpace;
			set => LineTransformers.LineHeightTransformProvider.BottomSpace = value;
		}
		public bool NoSpaceBetweenWrappedLines { get; set; }
		public List<CommentLabel> Labels { get; private set; } = new List<CommentLabel>();
		public List<CommentStyle> CommentStyles { get; private set; } = new List<CommentStyle>();
		public List<XmlCodeStyle> XmlCodeStyles { get; private set; } = new List<XmlCodeStyle>();
		public List<CodeStyle> CodeStyles { get; private set; } = new List<CodeStyle>();

		public static event EventHandler ConfigLoaded;
		public static event EventHandler ConfigUpdated;

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
			Instance = InternalLoadConfig(configPath);
			ConfigLoaded?.Invoke(Instance, EventArgs.Empty);
			ConfigUpdated?.Invoke(Instance, EventArgs.Empty);
		}

		static Config InternalLoadConfig(string configPath) {
			Config config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath), new JsonSerializerSettings {
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
			for (int i = s.Count - 1; i >= 0; i--) {
				if (s[i] == null || Enum.IsDefined(typeof(CommentStyleTypes), s[i].StyleID) == false) {
					s.RemoveAt(i);
				}
			}
			MergeDefaultStyles(s);
			var cs = config.CodeStyles;
			for (int i = cs.Count - 1; i >= 0; i--) {
				if (cs[i] == null || Enum.IsDefined(typeof(CodeStyleTypes), cs[i].StyleID) == false) {
					cs.RemoveAt(i);
				}
			}
			MergeDefaultCodeStyles(cs);
			var xcs = config.XmlCodeStyles;
			for (int i = xcs.Count - 1; i >= 0; i--) {
				if (xcs[i] == null || Enum.IsDefined(typeof(XmlStyleTypes), xcs[i].StyleID) == false) {
					xcs.RemoveAt(i);
				}
			}
			MergeDefaultXmlCodeStyles(xcs);
			return config;
		}

		public void Reset() {
			Labels.Clear();
			InitDefaultLabels(Labels);
			CommentStyles.Clear();
			CommentStyles.AddRange(GetDefaultCommentStyles());
			CodeStyles.Clear();
			CodeStyles.AddRange(GetDefaultCodeStyles());
			XmlCodeStyles.Clear();
			XmlCodeStyles.AddRange(GetDefaultXmlCodeStyles());
		}

		public void SaveConfig(string path) {
			//HACK: prevent redundant save operations issued by configuration pages
			if (LastSaved.AddSeconds(2) > DateTime.Now) {
				return;
			}
			path = path ?? ConfigPath;
			try {
				var d = Path.GetDirectoryName(path);
				if (Directory.Exists(d) == false) {
					Directory.CreateDirectory(d);
				}
				File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter()));
				if (path == ConfigPath) {
					LastSaved = DateTime.Now;
					ConfigUpdated?.Invoke(this, EventArgs.Empty);
				}
			}
			catch (Exception ex) {
				Debug.WriteLine(ex.ToString());
			}
		}

		static Config GetDefaultConfig() {
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
				if (styles.FindIndex(i=> i.StyleID == s.StyleID) == -1) {
					styles.Add(s);
				}
			}
		}
		static void MergeDefaultCodeStyles(List<CodeStyle> styles) {
			foreach (var s in GetDefaultCodeStyles()) {
				if (styles.FindIndex(i => i.StyleID == s.StyleID) == -1) {
					styles.Add(s);
				}
			}
		}
		static void MergeDefaultXmlCodeStyles(List<XmlCodeStyle> styles) {
			foreach (var s in GetDefaultXmlCodeStyles()) {
				if (styles.FindIndex(i => i.StyleID == s.StyleID) == -1) {
					styles.Add(s);
				}
			}
		}
		internal static CommentStyle[] GetDefaultCommentStyles() {
			return new CommentStyle[] {
				new CommentStyle(CommentStyleTypes.Emphasis, Constants.CommentColor) { Bold = true, FontSize = 10 },
				new CommentStyle(CommentStyleTypes.Exclaimation, Constants.ExclaimationColor),
				new CommentStyle(CommentStyleTypes.Question, Constants.QuestionColor),
				new CommentStyle(CommentStyleTypes.Deletion, Constants.DeletionColor) { StrikeThrough = true },
				new CommentStyle(CommentStyleTypes.ToDo, Colors.White) { BackgroundColor = Constants.ToDoColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Note, Colors.White) { BackgroundColor = Constants.NoteColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Hack, Colors.White) { BackgroundColor = Constants.HackColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyle(CommentStyleTypes.Heading1, Constants.CommentColor) { FontSize = 12 },
				new CommentStyle(CommentStyleTypes.Heading2, Constants.CommentColor) { FontSize = 8 },
				new CommentStyle(CommentStyleTypes.Heading3, Constants.CommentColor) { FontSize = 4 },
				new CommentStyle(CommentStyleTypes.Heading4, Constants.CommentColor) { FontSize = -1 },
				new CommentStyle(CommentStyleTypes.Heading5, Constants.CommentColor) { FontSize = -2 },
				new CommentStyle(CommentStyleTypes.Heading6, Constants.CommentColor) { FontSize = -3 },
				new CommentStyle(CommentStyleTypes.Task1, Constants.CommentColor),
				new CommentStyle(CommentStyleTypes.Task2, Constants.CommentColor),
				new CommentStyle(CommentStyleTypes.Task3, Constants.CommentColor),
				new CommentStyle(CommentStyleTypes.Task4, Constants.CommentColor),
				new CommentStyle(CommentStyleTypes.Task5, Constants.CommentColor),
				new CommentStyle(CommentStyleTypes.Task6, Constants.CommentColor),
				new CommentStyle(CommentStyleTypes.Task7, Constants.CommentColor),
				new CommentStyle(CommentStyleTypes.Task8, Constants.CommentColor),
				new CommentStyle(CommentStyleTypes.Task9, Constants.CommentColor),
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
	}

	abstract class StyleBase
	{
		static protected readonly Regex FriendlyNamePattern = new Regex(@"([a-z])([A-Z])", RegexOptions.Singleline);
		Color _backColor, _foreColor;
		public abstract int Id { get; }
		/// <summary>Gets or sets whether the content rendered in bold.</summary>
		public bool? Bold { get; set; }
		/// <summary>Gets or sets whether the content rendered in italic.</summary>
		public bool? Italic { get; set; }
		/// <summary>Gets or sets whether the content rendered stricken-through.</summary>
		public bool? OverLine { get; set; }
		/// <summary>Gets or sets whether the content rendered stricken-through.</summary>
		public bool? StrikeThrough { get; set; }
		/// <summary>Gets or sets whether the content rendered with underline.</summary>
		public bool? Underline { get; set; }
		/// <summary>Gets or sets the font size. Font size number is relative to the editor text size.</summary>
		public double FontSize { get; set; }
		/// <summary>Gets or sets the foreground color to render the text. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		public string ForegroundColor {
			get { return _foreColor.ToHexString(); }
			set { _foreColor = Utilities.ParseColor(value); }
		}
		/// <summary>Gets or sets the foreground color to render the text. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		public string BackgroundColor {
			get { return _backColor.ToHexString(); }
			set { _backColor = Utilities.ParseColor(value); }
		}
		/// <summary>Gets or sets the brush effect to draw the background color.</summary>
		public BrushEffect BackgroundEffect { get; set; }
		/// <summary>Gets or sets whether the denoted element is marked on the scrollbar.</summary>
		public bool UseScrollBarMarker { get; set; }
		/// <summary>Gets or sets the font.</summary>
		public string Font { get; set; }

		internal Color ForeColor {
			get { return _foreColor; }
			set { _foreColor = value; }
		}
		internal Color BackColor {
			get { return _backColor; }
			set { _backColor = value; }
		}

		internal StyleBase Clone() {
			return (StyleBase)MemberwiseClone();
		}
		public abstract string Category { get; }
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
		public CommentStyle(CommentStyleTypes styleID, string foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor;
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
		public XmlCodeStyle(XmlStyleTypes styleID, string foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor;
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
				var c = f.GetCustomAttribute<System.ComponentModel.CategoryAttribute>(false);
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
	}

	public enum BrushEffect
	{
		Solid,
		ToBottom,
		ToTop,
		ToRight,
		ToLeft
	}
}
