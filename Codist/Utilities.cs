using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using GdiColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfText = System.Windows.Media.FormattedText;

namespace Codist
{
	static class Utilities
	{
		public static string GetClassificationType(this Type type, string field) {
			var f = type.GetField(field);
			var d = f.GetCustomAttribute<ClassificationTypeAttribute>();
			return d?.ClassificationTypeNames;
		}
		public static Document GetDocument(this Workspace workspace, SnapshotSpan span) {
			var solution = workspace.CurrentSolution;
			var sourceText = span.Snapshot.AsText();
			var docId = workspace.GetDocumentIdInCurrentContext(sourceText.Container);
			return solution.ContainsDocument(docId)
				? solution.GetDocument(docId)
				: solution.WithDocumentText(docId, sourceText, PreservationMode.PreserveIdentity).GetDocument(docId);
		}

		public static GdiColor ChangeTrasparency(this GdiColor color, byte alpha) {
			return GdiColor.FromArgb(alpha, color.R, color.G, color.B);
		}

		public static WpfColor ParseColor(string colorText) {
			if (String.IsNullOrEmpty(colorText) || colorText[0] != '#' || colorText.Length != 7 && colorText.Length != 9) {
				return WpfColors.Transparent;
			}
			try {
				byte a = 0xFF, r, g, b;
				if (colorText.Length == 7
					&& ParseByte(colorText, 1, out r)
					&& ParseByte(colorText, 3, out g)
					&& ParseByte(colorText, 5, out b)) {
					return WpfColor.FromArgb(a, r, g, b);
				}
				if (colorText.Length == 9
					&& ParseByte(colorText, 1, out a)
					&& ParseByte(colorText, 3, out r)
					&& ParseByte(colorText, 5, out g)
					&& ParseByte(colorText, 7, out b)) {
					return WpfColor.FromArgb(a, r, g, b);
				}
			}
			catch (Exception ex) {
				Debug.WriteLine(ex);
			}
			return WpfColors.Transparent;
		}

		public static bool ParseByte(string text, int index, out byte value) {
			var h = text[index];
			var l = text[++index];
			var b = 0;
			if (h >= '0' && h <= '9') {
				b = (h - '0') << 4;
			}
			else if (h >= 'A' && h <= 'F') {
				b = (h - ('A' - 10)) << 4;
			}
			else if (h >= 'a' && h <= 'f') {
				b = (h - ('a' - 10)) << 4;
			}
			else {
				value = 0;
				return false;
			}
			if (l >= '0' && l <= '9') {
				b |= (l - '0');
			}
			else if (l >= 'A' && l <= 'F') {
				b |= (l - ('A' - 10));
			}
			else if (l >= 'a' && l <= 'f') {
				b |= (l - ('a' - 10));
			}
			else {
				value = 0;
				return false;
			}
			value = (byte)b;
			return true;
		}

		public static string ToHexString(this GdiColor color) {
			return "#" + color.A.ToString("X2") + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
		}
		public static string ToHexString(this WpfColor color) {
			return "#" + color.A.ToString("X2") + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
		}
		public static GdiColor ToGdiColor(this WpfColor color) {
			return GdiColor.FromArgb(color.A, color.R, color.G, color.B);
		}
		public static WpfColor ToWpfColor (this GdiColor color) {
			return WpfColor.FromArgb(color.A, color.R, color.G, color.B);
		}
		public static WpfColor Alpha(this WpfColor color, byte a) {
			return WpfColor.FromArgb(a, color.R, color.G, color.B);
		}
		public static TBrush Alpha<TBrush>(this TBrush brush, double opacity)
			where TBrush : WpfBrush {
			if (brush != null) {
				brush.Opacity = opacity;
			}
			return brush;
		}
		public static TPanel Add<TPanel>(this TPanel panel, UIElement control)
			where TPanel : Panel {
			panel.Children.Add(control);
			return panel;
		}
		public static TPanel AddReadOnlyTextBox<TPanel>(this TPanel panel, string text)
			where TPanel : Panel {
			panel.Children.Add(new TextBox {
				Text = text,
				IsReadOnly = true,
				TextAlignment = TextAlignment.Right,
				MinWidth = 180,
				BorderBrush = WpfBrushes.Transparent
			});
			return panel;
		}
		public static TextBlock AddText(this TextBlock block, string text) {
			block.Inlines.Add(new System.Windows.Documents.Run(text));
			return block;
		}
		public static TextBlock AddText(this TextBlock block, string text, bool bold) {
			return block.AddText(text, bold, false, null);
		}
		public static TextBlock AddText(this TextBlock block, string text, WpfBrush brush) {
			return block.AddText(text, false, false, brush);
		}
		public static TextBlock AddText(this TextBlock block, string text, bool bold, bool italic, WpfBrush brush) {
			var run = new System.Windows.Documents.Run(text);
			if (bold) {
				run.FontWeight = FontWeights.Bold;
			}
			if (italic) {
				run.FontStyle = FontStyles.Italic;
			}
			if (brush != null) {
				run.Foreground = brush;
			}
			block.Inlines.Add(run);
			return block;
		}
		public static TPanel AddText<TPanel>(this TPanel parent, string text)
			where TPanel : Panel {
			return parent.AddText(text, false, false, null);
		}
		public static TPanel AddText<TPanel>(this TPanel parent, string text, bool bold)
			where TPanel : Panel {
			return parent.AddText(text, bold, false, null);
		}
		public static TPanel AddText<TPanel>(this TPanel parent, string text, bool bold, bool italic)
			where TPanel : Panel {
			return parent.AddText(text, bold, italic, null);
		}
		public static TPanel AddText<TPanel>(this TPanel parent, string text, WpfBrush brush)
			where TPanel : Panel {
			return parent.AddText(text, false, false, brush);
		}
		public static TPanel AddText<TPanel>(this TPanel parent, string text, bool bold, bool italic, WpfBrush foregroundBrush)
			where TPanel : Panel {
			var t = new TextBlock { Text = text };
			if (bold) {
				t.FontWeight = FontWeight.FromOpenTypeWeight(bold ? 800 : 400);
			}
			if (italic) {
				t.FontStyle = FontStyles.Italic;
			}
			if (foregroundBrush != null) {
				t.Foreground = foregroundBrush;
			}
			parent.Children.Add(t);
			return parent;
		}
		public static StackPanel MakeHorizontal(this StackPanel panel) {
			panel.Orientation = Orientation.Horizontal;
			return panel;
		}
		public static string GetText<TPanel>(this TPanel panel)
			where TPanel : Panel {
			var sb = new System.Text.StringBuilder(50);
			GetText(panel, sb);
			return sb.ToString();
		}

		static void GetText<TPanel>(TPanel panel, System.Text.StringBuilder sb)
			where TPanel : Panel {
			foreach (var item in panel.Children) {
				var t = item as TextBlock;
				if (t != null) {
					sb.Append(t.Text);
				}
				else {
					var p = item as Panel;
					if (p != null) {
						GetText(p, sb);
					}
				}
			}
		}

		public static void GoToSymbol(this ISymbol symbol) {
			if (symbol.Locations.Length > 0) {
				var openDoc = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
				var loc = symbol.Locations[0];
				var path = loc.SourceTree?.FilePath;
				if (path == null) {
					return;
				}
				var pos = loc.GetLineSpan().StartLinePosition;
				openDoc.OpenFile(path, pos.Line + 1, pos.Character + 1);
			}
		}
		// not used at this moment. It is weird that __VSNEWDOCUMENTSTATE was not discovered by the compiler
		public static void OpenFile(this EnvDTE.DTE dte, string file, int line, int column) {
			if (file == null) {
				return;
			}
			file = System.IO.Path.GetFullPath(file);
			if (System.IO.File.Exists(file) == false) {
				return;
			}
			//using (new NewDocumentStateScope(__VSNEWDOCUMENTSTATE.NDS_Provisional | __VSNEWDOCUMENTSTATE.NDS_NoActivate, Microsoft.VisualStudio.VSConstants.NewDocumentStateReason.Navigation)) {
				dte.ItemOperations.OpenFile(file);
				((EnvDTE.TextSelection)dte.ActiveDocument.Selection).MoveTo(line, column);
			//}
		}

		public static WpfBrush GetBrush(this IEditorFormatMap map, string formatName, string resourceId = EditorFormatDefinition.ForegroundBrushId) {
			var p = map.GetProperties(formatName);
			return p != null && p.Contains(resourceId)
				? (p[resourceId] as WpfBrush).Clone()
				: null;
		}

		static readonly System.Windows.Media.Typeface StatusText = SystemFonts.StatusFontFamily.GetTypefaces().First();
		public static WpfText ToFormattedText(string text, double size, WpfBrush brush) {
			return new WpfText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, StatusText, size, brush);
		}
		public static WpfText SetItalic(this WpfText text) {
			text.SetFontStyle(FontStyles.Italic);
			return text;
		}
		public static WpfText SetBold(this WpfText text) {
			text.SetFontWeight(FontWeights.Bold);
			return text;
		}
		public static void ScreenShot(FrameworkElement control, string path) {
			var s = (control).RenderSize;
			var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)s.Width, (int)s.Height, 96, 96, System.Windows.Media.PixelFormats.Default);
			bmp.Render(control);
			var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
			enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
			using (var f = System.IO.File.Create(path)) {
				enc.Save(f);
			}
		}
	}
}
