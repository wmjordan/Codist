using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Xsl;
using Codist.Controls;
using Markdig;
using Markdig.Syntax;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Sgml;
using R = Codist.Properties.Resources;
using P = EnvDTE.Project;
using WinForm = System.Windows.Forms;
using Newtonsoft.Json;
using System.Diagnostics;

namespace Codist.Commands
{
	sealed class TransformDocumentWindow : Window
	{
		readonly P _Project;
		readonly ThemedTipText _TipText;
		readonly Dictionary<string, TransformSettings> _Settings;
		readonly TransformSettings _CurrentSettings;
		readonly string _ConfigPath, _ProjectPath, _SourcePath, _BasePath, _SourceName;
		readonly ITextSnapshot _SourceText;
		readonly TextBox _SourceBox, _TargetBox, _XsltBox;
		readonly Button _BrowseTargetButton, _BrowseXsltButton, _TransformButton, _CancelButton;
		readonly RadioButton _SaveHtmlFragmentButton, _SaveHtmlDocumentButton, _XsltTransformButton;
		readonly ComboBox _EncodingBox;

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		public TransformDocumentWindow(P project, ITextSnapshot source, string sourcePath) {
			_Project = project;
			_SourceText = source;
			_SourcePath = sourcePath;
			(_BasePath, _SourceName) = FileHelper.DeconstructPath(sourcePath);
			_SourceName = _SourceName != null ? Path.GetFileNameWithoutExtension(_SourceName) : String.Empty;
			if (project != null) {
				_ProjectPath = project.FullName;
				_ConfigPath = TransformSettings.GetConfigPath(project);
			}
			else {
				_ProjectPath = _BasePath;
				_ConfigPath = TransformSettings.GetConfigPath(_BasePath);
			}
			Title = "Transform Document";
			ShowInTaskbar = false;
			SnapsToDevicePixels = true;
			ResizeMode = ResizeMode.NoResize;
			SizeToContent = SizeToContent.WidthAndHeight;
			Content = new Grid {
				Margin = WpfHelper.MiddleMargin,
				ColumnDefinitions = {
					new ColumnDefinition { Width = GridLength.Auto },
					new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
					new ColumnDefinition { Width = new GridLength(100, GridUnitType.Pixel) }
				},
				RowDefinitions = {
					new RowDefinition { Height = GridLength.Auto },
					new RowDefinition { Height = GridLength.Auto },
					new RowDefinition { Height = GridLength.Auto },
					new RowDefinition { Height = GridLength.Auto },
					new RowDefinition { Height = GridLength.Auto },
					new RowDefinition { Height = GridLength.Auto },
					new RowDefinition { Height = GridLength.Auto },
				},
				Children = {
					new Label { Content = "Source document:" }.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey),
					new ThemedTextBox { Text = sourcePath, Margin = WpfHelper.MiddleMargin, IsReadOnly = true, MaxWidth = 600 }.Set(ref _SourceBox).SetProperty(Grid.ColumnProperty, 1).SetProperty(Grid.ColumnSpanProperty, 2),

					new Label { Content = "Target document:" }.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey).SetProperty(Grid.RowProperty, 1),
					new ThemedTextBox { Text = _SourceName + ".html", Margin = WpfHelper.MiddleMargin }.Set(ref _TargetBox).SetProperty(Grid.RowProperty, 1).SetProperty(Grid.ColumnProperty, 1),
					new ThemedButton (R.CMD_Browse, "Browse save location of the output document.", BrowseOutputDocument) { Width = 80, Margin = WpfHelper.MiddleMargin }.Set(ref _BrowseTargetButton).SetProperty(Grid.RowProperty, 1).SetProperty(Grid.ColumnProperty, 2),

					new Label { Content = "Transform mode:" }.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey).SetProperty(Grid.RowProperty, 2),
					new WrapPanel {
						Orientation = Orientation.Horizontal,
						VerticalAlignment = VerticalAlignment.Center,
						Margin = WpfHelper.MiddleMargin,
						Children = {
							new RadioButton { Content = "HTML Document", IsChecked = true, GroupName = "Mode", MinWidth = 100, Margin = WpfHelper.SmallHorizontalMargin }.Set(ref _SaveHtmlDocumentButton).ReferenceStyle(VsResourceKeys.ThemedDialogRadioButtonStyleKey),
							new RadioButton { Content = "HTML Fragment", GroupName = "Mode", MinWidth = 100, Margin = WpfHelper.SmallHorizontalMargin }.Set(ref _SaveHtmlFragmentButton).ReferenceStyle(VsResourceKeys.ThemedDialogRadioButtonStyleKey),
							new RadioButton { Content = "XSLT", GroupName = "Mode", MinWidth = 100, Margin = WpfHelper.SmallHorizontalMargin }.Set(ref _XsltTransformButton).ReferenceStyle(VsResourceKeys.ThemedDialogRadioButtonStyleKey),
						}
					}.SetProperty(Grid.RowProperty, 2).SetProperty(Grid.ColumnProperty, 1).SetProperty(Grid.ColumnSpanProperty, 2),

					new Label { Content = "XSLT document:" }.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey).SetProperty(Grid.RowProperty, 3),
					new ThemedTextBox { Margin = WpfHelper.MiddleMargin }.Set(ref _XsltBox).SetProperty(Grid.RowProperty, 3).SetProperty(Grid.ColumnProperty, 1),
					new ThemedButton (R.CMD_Browse, "Browse location of XSLT document.", BrowseXsltDocument){ Width = 80, Margin = WpfHelper.MiddleMargin }.Set (ref _BrowseXsltButton).SetProperty(Grid.RowProperty, 3).SetProperty(Grid.ColumnProperty, 2),

					new Label { Content = "Output file encoding:" }.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey).SetProperty(Grid.RowProperty, 4),
					new ComboBox { Items = { "UTF-8", "System Encoding (ANSI)", "Unicode", "GB 18030" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 200, Margin = WpfHelper.MiddleMargin }.ReferenceStyle(VsResourceKeys.ComboBoxStyleKey).Set(ref _EncodingBox).SetProperty(Grid.RowProperty, 4).SetProperty(Grid.ColumnProperty, 1),

					new StackPanel {
						Orientation = Orientation.Horizontal,
						Margin = WpfHelper.MiddleMargin,
						Children = {
							new ThemedButton(R.CMD_OK, null, DoTransform) { IsDefault = true, MinWidth = 80, Margin = WpfHelper.MiddleMargin },
							new ThemedButton(R.CMD_Cancel, null, Close) { IsCancel = true, MinWidth = 80, Margin = WpfHelper.MiddleMargin },
							new ThemedButton("Save settings", "Save current settings for next time", SaveSettings) { MinWidth = 80, Margin = WpfHelper.MiddleMargin },
						}
					}.SetProperty(Grid.ColumnSpanProperty, 2).SetProperty(Grid.ColumnProperty, 1).SetProperty(Grid.RowProperty, 5),

					new ThemedTipText() { MaxWidth = 600 }.SetProperty(Grid.ColumnSpanProperty, 3).SetProperty(Grid.RowProperty, 6).Set(ref _TipText)
				}
			}.ReferenceProperty(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey);
			this.ReferenceProperty(Border.BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
			_XsltBox.IsEnabled = _BrowseXsltButton.IsEnabled = false;
			_XsltTransformButton.Checked += (s, args) => _XsltBox.IsEnabled = _BrowseXsltButton.IsEnabled = true;
			_XsltTransformButton.Unchecked += (s, args) => _XsltBox.IsEnabled = _BrowseXsltButton.IsEnabled = false;

			_Settings = TransformSettings.Load(_ConfigPath) ?? new Dictionary<string, TransformSettings>(StringComparer.OrdinalIgnoreCase);
			if (_Settings.TryGetValue(PackageUtilities.MakeRelative(_ConfigPath, _SourcePath), out _CurrentSettings)) {
				LoadSettings();
			}
		}

		void LoadSettings() {
			var s = _CurrentSettings;
			_TargetBox.Text = s.TargetFile;
			_XsltBox.Text = s.XsltFile;
			switch (s.Mode) {
				case 0: _SaveHtmlDocumentButton.IsChecked = true; break;
				case 1: _SaveHtmlFragmentButton.IsChecked = true; break;
				case 2: _XsltTransformButton.IsChecked = true; break;
			}
			if (s.TargetEncoding < _EncodingBox.Items.Count) {
				_EncodingBox.SelectedIndex = s.TargetEncoding;
			}
		}

		void SaveSettings() {
			var s = MakeSettings();
			try {
				var k = PackageUtilities.MakeRelative(_ConfigPath, _SourcePath);
				if (_Settings.ContainsKey(k) == false || s != _CurrentSettings) {
					_Settings[k] = s;
					File.WriteAllText(_ConfigPath, JsonConvert.SerializeObject(_Settings, Newtonsoft.Json.Formatting.None, new Newtonsoft.Json.Converters.StringEnumConverter()));
					_TipText.Text = "Settings saved to \"<PATH>\".".Replace("<PATH>", _ConfigPath);
				}
			}
			catch (Exception ex) {
				MessageWindow.Error(ex, "Failed to save config file to <NAME>".Replace("<NAME>", _ConfigPath), null);
			}
		}

		TransformSettings MakeSettings() {
			return new TransformSettings {
				TargetFile = _TargetBox.Text,
				XsltFile = _XsltBox.Text,
				Mode = _SaveHtmlDocumentButton.IsChecked == true ? 0 :
					_SaveHtmlFragmentButton.IsChecked == true ? 1 :
					_SaveHtmlFragmentButton.IsChecked == true ? 2 : 0,
				TargetEncoding = _EncodingBox.SelectedIndex,
			};
		}

		void BrowseXsltDocument() {
			using (var f = new WinForm.OpenFileDialog {
				Filter = "XSLT Documents|*.xslt;*.xsl",
				AddExtension = true,
				Title = "Specify location of XSLT document",
				InitialDirectory = _BasePath,
				FileName = _SourceName + ".xslt",
				ValidateNames = true,
			}) {
				if (f.ShowDialog() == WinForm.DialogResult.OK) {
					_XsltBox.Text = PackageUtilities.MakeRelative(_SourcePath, f.FileName);
				}
			}
		}

		void BrowseOutputDocument() {
			using (var f = new WinForm.SaveFileDialog {
				Filter = R.F_Html,
				AddExtension = true,
				Title = R.T_SpecifyLocation,
				InitialDirectory = _BasePath,
				FileName = _SourceName + ".html",
				ValidateNames = true,
			}) {
				if (f.ShowDialog() == WinForm.DialogResult.OK) {
					_TargetBox.Text = PackageUtilities.MakeRelative(_SourcePath, f.FileName);
				}
			}
		}

		void DoTransform() {
			try {
				var md = Markdown.Parse(_SourceText.GetText(), new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
				if (_TargetBox.Text.Length == 0) {
					ExportToNewWindow(md);
					return;
				}
				var t = Path.Combine(_BasePath, _TargetBox.Text);
				if (_SaveHtmlFragmentButton.IsChecked == true) {
					SaveHtmlFragment(md, t);
				}
				else if (_SaveHtmlDocumentButton.IsChecked == true) {
					SaveHtmlDocument(md, t);
				}
				else {
					if (File.Exists(_XsltBox.Text) == false) {
						MessageWindow.Error("XSLT file does not exist.");
						return;
					}
					TransformHtmlDocument(md, t);
				}
			}
			catch (Exception ex) {
				MessageWindow.Error(ex, R.T_TransformFailed.Replace("<NAME>", _SourcePath), null, new Source());
				return;
			}
			//if (_Project != null) {
			//	SaveSettings();
			//}
			Close();
		}

		private void ExportToNewWindow(MarkdownDocument md) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var w = CodistPackage.DTE.ItemOperations.NewFile("General\\HTML Page", _SourceName);
			var view = w.Document.GetActiveWpfDocumentView();
			using (var edit = view.TextBuffer.CreateEdit()) {
				edit.Replace(new Span(0, view.TextSnapshot.Length), md.ToHtml());
				edit.Apply();
			}
			w.Document.Saved = true;
		}

		void SaveHtmlFragment(MarkdownDocument md, string targetPath) {
			File.WriteAllText(targetPath, md.ToHtml(), GetEncoding());
		}

		void SaveHtmlDocument(MarkdownDocument md, string targetPath) {
			var html = TransformToHtml(md);
			using (var xw = XmlWriter.Create(targetPath, new XmlWriterSettings {
				OmitXmlDeclaration = true,
				Indent = true,
				IndentChars = "\t",
				Encoding = GetEncoding(),
			})) {
				xw.WriteDocType("html", null, null, null);
				html.Save(xw);
			}
		}

		XmlDocument TransformToHtml(MarkdownDocument md) {
			var html = new XmlDocument();
			var root = html.AppendChild(html.CreateElement("html"));
			var head = root.AppendChild(html.CreateElement("head"));
			var meta = html.CreateElement("meta");
			meta.SetAttribute("charset", GetEncoding().WebName);
			head.AppendChild(meta);
			head.AppendChild(html.CreateElement("title")).InnerText = _SourceName;
			var body = root.AppendChild(html.CreateElement("body"));
			using (var ms = new MemoryStream())
			using (var w = new StreamWriter(ms, Encoding.UTF8)) {
				md.ToHtml(w);
				w.Flush();
				ms.Position = 0;
				using (var r = new StreamReader(ms, Encoding.UTF8)) {
					var sgml = new SgmlReader(new XmlReaderSettings {
						ConformanceLevel = ConformanceLevel.Fragment,
						ValidationType = ValidationType.None
					}) {
						IgnoreDtd = true,
						StripDocType = true,
						InputStream = r,
						WhitespaceHandling = WhitespaceHandling.Significant
					};
					var nav = body.CreateNavigator();
					while (sgml.ReadState < ReadState.Error) {
						nav.AppendChild(sgml);
					}
				}
			}
			return html;
		}

		void TransformHtmlDocument(MarkdownDocument md, string targetPath) {
			var html = TransformToHtml(md);
			var xslt = new XslCompiledTransform();
			try {
				xslt.Load(_XsltBox.Text);
			}
			catch (XmlException ex) {
				MessageWindow.Error("XSLT document is malformed: " + ex.Message);
				return;
			}
			catch (XsltException ex) {
				MessageWindow.Error("XSLT document is invalid: " + ex.Message);
				return;
			}
			using (var xw = XmlWriter.Create(targetPath, new XmlWriterSettings {
				OmitXmlDeclaration = true,
				Indent = true,
				IndentChars = "\t",
				Encoding = GetEncoding(),
			})) {
				xslt.Transform(html, xw);
			}
		}

		Encoding GetEncoding() {
			switch (_EncodingBox.SelectedIndex) {
				case 1: return Encoding.Default;
				case 2: return Encoding.Unicode;
				case 3: return Encoding.GetEncoding("GB18030");
				default: return Encoding.UTF8;
			}
		}

		struct TransformSettings : IEquatable<TransformSettings>
		{
			public string TargetFile { get; set; }
			public int Mode { get; set; }
			public string XsltFile { get; set; }
			public int TargetEncoding { get; set; }

			public static string GetConfigPath(P project) {
				ThreadHelper.ThrowIfNotOnUIThread();
				return Path.Combine(Path.GetDirectoryName(project.FullName), "obj", project.Name + ".transform.json");
			}
			public static string GetConfigPath(string basePath) {
				return Path.Combine(basePath, "codist.transform.json");
			}
			public static Dictionary<string, TransformSettings> Load(string configPath) {
				try {
					var d = File.Exists(configPath)
						? JsonConvert.DeserializeObject<Dictionary<string, TransformSettings>>(File.ReadAllText(configPath), new JsonSerializerSettings {
							DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
							NullValueHandling = NullValueHandling.Ignore,
							Error = (sender, args) => {
								args.ErrorContext.Handled = true; // ignore json error
							}
						})
						: null;
					if (d != null) {
						var r = new Dictionary<string, TransformSettings>(StringComparer.OrdinalIgnoreCase);
						foreach (var item in d) {
							r[item.Key] = item.Value;
						}
						return r;
					}
					return null;
				}
				catch (Exception ex) {
					$"Error loading {nameof(TransformSettings)} from {configPath}".Log();
					ex.Log();
					return null;
				}
			}

			public bool Equals(TransformSettings other) {
				return this == other;
			}

			public override bool Equals(object obj) {
				return obj is TransformSettings t && this == t;
			}

			public override int GetHashCode() {
				int hashCode = 1143690159;
				hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(TargetFile);
				hashCode = hashCode * -1521134295 + Mode.GetHashCode();
				hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(XsltFile);
				hashCode = hashCode * -1521134295 + TargetEncoding.GetHashCode();
				return hashCode;
			}

			public static bool operator == (TransformSettings x, TransformSettings y) {
				return !(x != y);
			}

			public static bool operator != (TransformSettings x, TransformSettings y) {
				return x.TargetFile != y.TargetFile
					|| x.Mode != y.Mode
					|| x.TargetEncoding != y.TargetEncoding
					|| x.XsltFile != y.XsltFile;
			}
		}

		struct Source { }
	}
}
