using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using Newtonsoft.Json;

namespace Codist
{
	class Config
	{
		static DateTime LastSaved;

		public static readonly string Path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Codist\\Config.json";
		public static readonly Config Instance = LoadConfig();

		public List<CommentLabel> Labels { get; private set; } = new List<CommentLabel>();
		public List<CommentStyle> Styles { get; private set; } = new List<CommentStyle>();
		public List<CodeStyle> CodeStyles { get; private set; } = new List<CodeStyle>();

		public event EventHandler ConfigUpdated;

		public static Config LoadConfig() {
			//AppHelpers.LogHelper.UseLogMethod(i => Debug.WriteLine(i));
			Config config;
			if (File.Exists(Path) == false) {
				config = GetDefaultConfig();
				config.SaveConfig();
				return config;
			}
			try {
				config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Path));
				var l = config.Labels;
				for (int i = l.Count - 1; i >= 0; i--) {
					if (String.IsNullOrWhiteSpace(l[i].Label)) {
						l.RemoveAt(i);
					}
				}
				if (l.Count == 0) {
					InitDefaultLabels(l);
				}
				var s = config.Styles;
				for (int i = s.Count - 1; i >= 0; i--) {
					if (s[i].StyleID < CommentStyles.Default || s[i].StyleID > CommentStyles.Task9) {
						s.RemoveAt(i);
					}
				}
				MergeDefaultStyles(s);
				var cs = config.CodeStyles;
				for (int i = cs.Count - 1; i >= 0; i--) {
					if (cs[i].StyleID < Codist.CodeStyles.None || cs[i].StyleID > Codist.CodeStyles.XmlDocTag) {
						cs.RemoveAt(i);
					}
				}
				MergeDefaultCodeStyles(cs);
				return config;
			}
			catch (Exception ex) {
				Debug.WriteLine(ex.ToString());
				return GetDefaultConfig();
			}
		}

		public void Reset() {
			Labels.Clear();
			InitDefaultLabels(Labels);
			Styles.Clear();
			Styles.AddRange(GetDefaultStyles());
			CodeStyles.Clear();
			CodeStyles.AddRange(GetDefaultCodeStyles());
		}

		public void SaveConfig() {
			//HACK: prevent redundant save operations issued by configuration pages
			if (LastSaved.AddSeconds(2) > DateTime.Now) {
				return;
			}
			try {
				var d = System.IO.Path.GetDirectoryName(Path);
				if (Directory.Exists(d) == false) {
					Directory.CreateDirectory(d);
				}
				File.WriteAllText(Path, JsonConvert.SerializeObject(this, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter()));
				LastSaved = DateTime.Now;
				if (ConfigUpdated != null) {
					ConfigUpdated(this, EventArgs.Empty);
				}
			}
			catch (Exception ex) {
				Debug.WriteLine(ex.ToString());
			}
		}

		static Config GetDefaultConfig() {
			var c = new Config();
			InitDefaultLabels(c.Labels);
			c.Styles.AddRange(GetDefaultStyles());
			c.CodeStyles.AddRange(GetDefaultCodeStyles());
			return c;
		}

		static void InitDefaultLabels(List<CommentLabel> labels) {
			labels.AddRange (new CommentLabel[] {
				new CommentLabel("!", CommentStyles.Emphasis),
				new CommentLabel("#", CommentStyles.Emphasis),
				new CommentLabel("?", CommentStyles.Question),
				new CommentLabel("!?", CommentStyles.Exclaimation),
				new CommentLabel("x", CommentStyles.Deletion, true),
				new CommentLabel("+++", CommentStyles.Heading1),
				new CommentLabel("!!", CommentStyles.Heading1),
				new CommentLabel("++", CommentStyles.Heading2),
				new CommentLabel("+", CommentStyles.Heading3),
				new CommentLabel("-", CommentStyles.Heading4),
				new CommentLabel("--", CommentStyles.Heading5),
				new CommentLabel("---", CommentStyles.Heading6),
				new CommentLabel("TODO", CommentStyles.ToDo, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("TO-DO", CommentStyles.ToDo, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("undone", CommentStyles.ToDo, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("NOTE", CommentStyles.Note, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("HACK", CommentStyles.Hack, true) { AllowPunctuationDelimiter = true },
			});
		}
		static void MergeDefaultStyles(List<CommentStyle> styles) {
			foreach (var s in GetDefaultStyles()) {
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
		static CommentStyle[] GetDefaultStyles() {
			return new CommentStyle[] {
				new CommentStyle(CommentStyles.Emphasis, Constants.CommentColor) { Bold = true, FontSize = 10 },
				new CommentStyle(CommentStyles.Exclaimation, Constants.ExclaimationColor),
				new CommentStyle(CommentStyles.Question, Constants.QuestionColor),
				new CommentStyle(CommentStyles.Deletion, Constants.DeletionColor) { StrikeThrough = true },
				new CommentStyle(CommentStyles.ToDo, Colors.White) { BackgroundColor = Constants.ToDoColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyle(CommentStyles.Note, Colors.White) { BackgroundColor = Constants.NoteColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyle(CommentStyles.Hack, Colors.White) { BackgroundColor = Constants.HackColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyle(CommentStyles.Heading1, Constants.CommentColor) { FontSize = 12 },
				new CommentStyle(CommentStyles.Heading2, Constants.CommentColor) { FontSize = 8 },
				new CommentStyle(CommentStyles.Heading3, Constants.CommentColor) { FontSize = 4 },
				new CommentStyle(CommentStyles.Heading4, Constants.CommentColor) { FontSize = -1 },
				new CommentStyle(CommentStyles.Heading5, Constants.CommentColor) { FontSize = -2 },
				new CommentStyle(CommentStyles.Heading6, Constants.CommentColor) { FontSize = -3 },
				new CommentStyle(CommentStyles.Task1, Constants.CommentColor),
				new CommentStyle(CommentStyles.Task2, Constants.CommentColor),
				new CommentStyle(CommentStyles.Task3, Constants.CommentColor),
				new CommentStyle(CommentStyles.Task4, Constants.CommentColor),
				new CommentStyle(CommentStyles.Task5, Constants.CommentColor),
				new CommentStyle(CommentStyles.Task6, Constants.CommentColor),
				new CommentStyle(CommentStyles.Task7, Constants.CommentColor),
				new CommentStyle(CommentStyles.Task8, Constants.CommentColor),
				new CommentStyle(CommentStyles.Task9, Constants.CommentColor),
			};
		}
		static CodeStyle[] GetDefaultCodeStyles() {
			var r = new CodeStyle[(int)Codist.CodeStyles.XmlDocTag + 1];
			for (int i = 0; i < r.Length; i++) {
				r[i] = new CodeStyle { StyleID = (Codist.CodeStyles)i };
			}
			return r;
		}
	}

	abstract class StyleBase
	{
		Color _backColor, _foreColor;

		/// <summary>Gets or sets whether the content rendered in bold.</summary>
		public bool? Bold { get; set; }
		/// <summary>Gets or sets whether the content rendered in italic.</summary>
		public bool? Italic { get; set; }
		/// <summary>Gets or sets whether the content rendered stricken-through.</summary>
		public bool? StrikeThrough { get; set; }
		/// <summary>Gets or sets whether the content rendered with underline.</summary>
		public bool? Underline { get; set; }
		/// <summary>Gets or sets the font size of the comment. Font size number is relative to the editor text size.</summary>
		public double FontSize { get; set; }
		/// <summary>Gets or sets the foreground color to render the comment text. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		public string ForegroundColor {
			get { return _foreColor.ToHexString(); }
			set { _foreColor = Utilities.ParseColor(value); }
		}
		/// <summary>Gets or sets the foreground color to render the comment text. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		public string BackgroundColor {
			get { return _backColor.ToHexString(); }
			set { _backColor = Utilities.ParseColor(value); }
		}
		/// <summary>Gets or sets whether the comment is marked on the scrollbar.</summary>
		public bool UseScrollBarMarker { get; set; }
		/// <summary>Gets or sets the font.</summary>
		public string Font { get; internal set; }

		internal Color ForeColor {
			get { return _foreColor; }
			set { _foreColor = value; }
		}
		internal Color BackColor {
			get { return _backColor; }
			set { _backColor = value; }
		}

	}
	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	class CommentStyle : StyleBase
	{
		public CommentStyle() {
		}
		public CommentStyle(CommentStyles styleID, Color foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor.ToHexString();
		}
		public CommentStyle(CommentStyles styleID, string foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor;
		}

		/// <summary>Gets or sets the comment style.</summary>
		public CommentStyles StyleID { get; set; }

		public CommentStyle Clone() {
			return (CommentStyle)MemberwiseClone();
		}

		public override string ToString() {
			return StyleID.ToString();
		}
	}

	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	class CodeStyle : StyleBase
	{
		/// <summary>Gets or sets the code style.</summary>
		public CodeStyles StyleID { get; set; }

		public CodeStyle Clone() {
			return (CodeStyle)MemberwiseClone();
		}

		public override string ToString() {
			return StyleID.ToString();
		}
	}

	[DebuggerDisplay("{Label} IgnoreCase: {IgnoreCase} AllowPunctuationDelimiter: {AllowPunctuationDelimiter}")]
	class CommentLabel
	{
		string _label;
		int _labelLength;
		StringComparison _stringComparison;

		public CommentLabel() {
		}

		public CommentLabel(string label, CommentStyles styleID) {
			Label = label;
			StyleID = styleID;
		}
		public CommentLabel(string label, CommentStyles styleID, bool ignoreCase) {
			Label = label;
			StyleID = styleID;
			IgnoreCase = ignoreCase;
		}

		public bool AllowPunctuationDelimiter { get; set; }

		/// <summary>Gets or sets the label to identifier the comment type.</summary>
		public string Label { get { return _label; } set { _label = value; _labelLength = (value ?? String.Empty).Length; } }
		internal int LabelLength { get { return _labelLength; } }
		/// <summary>Gets or sets whether the label is case-sensitive.</summary>
		public bool IgnoreCase {
			get { return _stringComparison == StringComparison.OrdinalIgnoreCase; }
			set { _stringComparison = value ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; }
		}
		public CommentStyleApplication StyleApplication { get; set; }
		internal StringComparison Comparison { get { return _stringComparison; } }
		/// <summary>Gets or sets the comment style.</summary>
		public CommentStyles StyleID { get; set; }

		public CommentLabel Clone() {
			return (CommentLabel)MemberwiseClone();
		}
	}
}
