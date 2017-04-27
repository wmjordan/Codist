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
		public static readonly string Path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Codist\\Config.json";
		public static readonly Config Instance = LoadConfig();

		public List<CommentLabel> Labels { get; private set; } = new List<CommentLabel>();
		public List<CommentStyleOption> Styles { get; private set; } = new List<CommentStyleOption>();

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
					if (s[i].StyleID < CommentStyle.Default || s[i].StyleID > CommentStyle.Heading6) {
						l.RemoveAt(i);
					}
				}
				MergeDefaultStyles(s);
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
		}

		public void SaveConfig() {
			try {
				var d = System.IO.Path.GetDirectoryName(Path);
				if (Directory.Exists(d) == false) {
					Directory.CreateDirectory(d);
				}
				File.WriteAllText(Path, JsonConvert.SerializeObject(this, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter()));
			}
			catch (Exception ex) {
				Debug.WriteLine(ex.ToString());
			}
		}

		static Config GetDefaultConfig() {
			var c = new Config();
			InitDefaultLabels(c.Labels);
			c.Styles.AddRange(GetDefaultStyles());
			return c;
		}

		static void InitDefaultLabels(List<CommentLabel> labels) {
			labels.AddRange (new CommentLabel[] {
				new CommentLabel("!", CommentStyle.Emphasis),
				new CommentLabel("#", CommentStyle.Emphasis),
				new CommentLabel("?", CommentStyle.Question),
				new CommentLabel("!?", CommentStyle.Exclaimation),
				new CommentLabel("x", CommentStyle.Deletion, true),
				new CommentLabel("+++", CommentStyle.Heading1),
				new CommentLabel("!!", CommentStyle.Heading1),
				new CommentLabel("++", CommentStyle.Heading2),
				new CommentLabel("+", CommentStyle.Heading3),
				new CommentLabel("-", CommentStyle.Heading4),
				new CommentLabel("--", CommentStyle.Heading5),
				new CommentLabel("---", CommentStyle.Heading6),
				new CommentLabel("TODO", CommentStyle.ToDo, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("TO-DO", CommentStyle.ToDo, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("NOTE", CommentStyle.Note, true) { AllowPunctuationDelimiter = true },
				new CommentLabel("HACK", CommentStyle.Hack, true) { AllowPunctuationDelimiter = true },
			});
		}
		static void MergeDefaultStyles(List<CommentStyleOption> styles) {
			foreach (var s in GetDefaultStyles()) {
				if (styles.FindIndex(i=> i.StyleID == s.StyleID) == -1) {
					styles.Add(s);
				}
			}
		}
		static CommentStyleOption[] GetDefaultStyles() {
			return new CommentStyleOption[] {
				new CommentStyleOption(CommentStyle.Emphasis, Constants.CommentColor) { Bold = true, FontSize = 10 },
				new CommentStyleOption(CommentStyle.Exclaimation, Constants.ExclaimationColor),
				new CommentStyleOption(CommentStyle.Question, Constants.QuestionColor),
				new CommentStyleOption(CommentStyle.Deletion, Constants.DeletionColor) { StrikeThrough = true },
				new CommentStyleOption(CommentStyle.ToDo, Colors.White) { BackgroundColor = Constants.ToDoColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyleOption(CommentStyle.Note, Colors.White) { BackgroundColor = Constants.NoteColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyleOption(CommentStyle.Hack, Colors.White) { BackgroundColor = Constants.HackColor.ToHexString(), UseScrollBarMarker = true },
				new CommentStyleOption(CommentStyle.Heading1, Constants.CommentColor) { FontSize = 12 },
				new CommentStyleOption(CommentStyle.Heading2, Constants.CommentColor) { FontSize = 8 },
				new CommentStyleOption(CommentStyle.Heading3, Constants.CommentColor) { FontSize = 4 },
				new CommentStyleOption(CommentStyle.Heading4, Constants.CommentColor) { FontSize = -1 },
				new CommentStyleOption(CommentStyle.Heading5, Constants.CommentColor) { FontSize = -2 },
				new CommentStyleOption(CommentStyle.Heading6, Constants.CommentColor) { FontSize = -3 },
			};
		}
	}

	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	class CommentStyleOption
	{
		public CommentStyleOption() {
		}
		public CommentStyleOption(CommentStyle styleID, Color foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor.ToHexString();
		}
		public CommentStyleOption(CommentStyle styleID, string foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor;
		}

		/// <summary>Gets or sets the comment style.</summary>
		public CommentStyle StyleID { get; set; }
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
		public string ForegroundColor { get; set; }
		/// <summary>Gets or sets the foreground color to render the comment text. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		public string BackgroundColor { get; set; }
		/// <summary>Gets or sets whether the comment is marked on the scrollbar.</summary>
		public bool UseScrollBarMarker { get; set; }
		/// <summary>Gets or sets the font.</summary>
		public string Font { get; internal set; }

		internal Color ForeColor {
			get { return Utilities.ParseColor(ForegroundColor); }
		}
		internal Color BackColor {
			get { return Utilities.ParseColor(BackgroundColor); }
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

		public CommentLabel(string label, CommentStyle styleID) {
			Label = label;
			StyleID = styleID;
		}
		public CommentLabel(string label, CommentStyle styleID, bool ignoreCase) {
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
		public CommentStyle StyleID { get; set; }
	}

	static class Utilities
	{
		public static Color ParseColor(string colorText) {
			if (String.IsNullOrEmpty(colorText) || colorText[0] != '#' || colorText.Length != 7 && colorText.Length != 9) {
				return Colors.Transparent;
			}
			try {
				byte a = 0xFF, r, g, b;
				if (colorText.Length == 7
					&& ParseByte(colorText, 1, out r)
					&& ParseByte(colorText, 3, out g)
					&& ParseByte(colorText, 5, out b)) {
					return Color.FromArgb(a, r, g, b);
				}
				if (colorText.Length == 9
					&& ParseByte(colorText, 1, out a)
					&& ParseByte(colorText, 3, out r)
					&& ParseByte(colorText, 5, out g)
					&& ParseByte(colorText, 7, out b)) {
					return Color.FromArgb(a, r, g, b);
				}
			}
			catch (Exception ex) {
				Debug.WriteLine(ex);
			}
			return Colors.Transparent;
		}

		public static bool ParseByte(string text, int index, out byte value) {
			var h = text[index];
			var l = text[++index];
			var b = 0;
			if (h >= '0' && h <= '9') {
				b = (h - '0') << 4;
			}
			else if (h >= 'A' && h <= 'F') {
				b = (h - 'A' + 10) << 4;
			}
			else if (h >= 'a' && h <= 'f') {
				b = (h - 'a' + 10) << 4;
			}
			else {
				value = 0;
				return false;
			}
			if (l >= '0' && l <= '9') {
				b |= (l - '0');
			}
			else if (l >= 'A' && l <= 'F') {
				b |= (l - 'A' + 10);
			}
			else if (l >= 'a' && l <= 'f') {
				b |= (l - 'a' + 10);
			}
			else {
				value = 0;
				return false;
			}
			value = (byte)b;
			return true;
		}
		public static string ToHexString(this Color color) {
			return "#" + color.A.ToString("X2") + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
		}
	}
}
