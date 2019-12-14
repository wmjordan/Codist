using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using AppHelpers;
using Codist.Taggers;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codist.Options
{
	[ToolboxItem(false)]
	public partial class CommentTaggerOptionPage : UserControl
	{
		readonly CommentStyle _ServicePage;
		readonly UiLock _UI = new UiLock();
		CommentLabel _ActiveLabel;
		TabPage _HighlightPage;
		bool _Loaded;

		public CommentTaggerOptionPage() {
			InitializeComponent();
		}
		internal CommentTaggerOptionPage(CommentStyle service) : this() {
			_ServicePage = service;
		}

		protected override void OnLoad(EventArgs e) {
			base.OnLoad(e);
			if (_Loaded) {
				return;
			}

			_CommentTaggerTabs.AddPage("Comment Highlight", new SyntaxStyleOptionPage(_ServicePage, () => Config.Instance.CommentStyles, Config.GetDefaultCommentStyles), false);
			_HighlightPage = _CommentTaggerTabs.TabPages[2];

			LoadStyleList();

			_AddTagButton.Click += (s, args) => {
				Enum.TryParse<CommentStyleTypes>(_StyleBox.Text, out var style);
				var label = new CommentLabel(_TagTextBox.Text.Length > 0 ? _TagTextBox.Text : "tag", style);
				Config.Instance.Labels.Add(label);
				_SyntaxListBox.Items.Add(new CommentTaggerListViewItem(label.Label, label) { Selected = true });
				_ActiveLabel = label;
			};
			_RemoveTagButton.Click += (s, args) => {
				if (_ActiveLabel == null) {
					return;
				}
				var i = Config.Instance.Labels.IndexOf(_ActiveLabel);
				if (i == -1) {
					return;
				}
				Config.Instance.Labels.RemoveAt(i);
				_SyntaxListBox.Items.RemoveAt(i);
				_ActiveLabel = null;
			};
			_IgnoreCaseBox.CheckedChanged += _UI.HandleEvent(() => {
				if (_ActiveLabel != null) {
					_ActiveLabel.IgnoreCase = _IgnoreCaseBox.Checked;
				}
			});
			_EndWithPunctuationBox.CheckedChanged += _UI.HandleEvent(() => {
				if (_ActiveLabel != null) {
					_ActiveLabel.AllowPunctuationDelimiter = _EndWithPunctuationBox.Checked;
				}
			});
			_StyleBox.SelectedIndexChanged += _UI.HandleEvent(() => {
				if (_ActiveLabel == null) {
					return;
				}
				Enum.TryParse<CommentStyleTypes>(_StyleBox.Text, out var style);
				_ActiveLabel.StyleID = style;
				MarkChanged(_StyleBox, EventArgs.Empty);
			});
			_TagTextBox.TextChanged += _UI.HandleEvent(() => {
				if (_ActiveLabel != null) {
					_ActiveLabel.Label = _TagTextBox.Text;
					foreach (CommentTaggerListViewItem item in _SyntaxListBox.Items) {
						if (item.CommentLabel == _ActiveLabel) {
							item.Text = _TagTextBox.Text;
						}
					}
				}
			});
			foreach (var item in new Control[] { _TagTextBox }) {
				item.Click += MarkChanged;
			}
			foreach (var item in new[] { _ApplyContentBox, _ApplyTagBox, _ApplyContentTagBox }) {
				item.CheckedChanged += StyleApplicationChanged;
				item.CheckedChanged += MarkChanged;
			}
			foreach (var item in new[] { _IgnoreCaseBox, _EndWithPunctuationBox, _HighlightSpecialCommentBox }) {
				item.CheckStateChanged += MarkChanged;
			}
			_HighlightSpecialCommentBox.CheckedChanged += _UI.HandleEvent(() => {
				var c = _HighlightSpecialCommentBox.Checked;
				Config.Instance.Set(SpecialHighlightOptions.SpecialComment, c);
				if (c) {
					_CommentTaggerTabs.TabPages.Add(_TagsPage);
					_CommentTaggerTabs.TabPages.Add(_HighlightPage);
				}
				else if (_CommentTaggerTabs.TabCount == 3) {
					_CommentTaggerTabs.TabPages.RemoveAt(2);
					_CommentTaggerTabs.TabPages.RemoveAt(1);
				}
			});

			_PreviewBox.SizeChanged += (s, args) => UpdatePreview();
			_SyntaxListBox.ItemSelectionChanged += _SyntaxListBox_ItemSelectionChanged;
			_UI.PostEventAction += () => Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			Config.Loaded += (s, args) => LoadStyleList();
			Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_HighlightSpecialCommentBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialComment);
			});
		}

		void LoadStyleList() {
			_UI.Lock();
			_HighlightSpecialCommentBox.Checked = Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialComment);
			_SyntaxListBox.Items.Clear();
			foreach (var item in Config.Instance.Labels) {
				_SyntaxListBox.Items.Add(new CommentTaggerListViewItem(item.Label, item));
			}
			_StyleBox.Items.Clear();
			var t = typeof(CommentStyleTypes);
			foreach (var item in Enum.GetNames(t)) {
				var d = t.GetField(item).GetCustomAttribute<Microsoft.VisualStudio.Text.Classification.ClassificationTypeAttribute>()?.ClassificationTypeNames;
				if (d == null || d.StartsWith(Constants.CodistPrefix, StringComparison.Ordinal) == false) {
					continue;
				}
				_StyleBox.Items.Add(item);
			}
			if (_StyleBox.Visible) {
				_StyleBox.SelectedIndex = 0;
			}
			_UI.Unlock();
		}

		void MarkChanged(object sender, EventArgs args) {
			if (_UI.IsLocked || _ActiveLabel == null) {
				return;
			}
			UpdatePreview();
			var style = FindStyle(_ActiveLabel);
			if (style != null) {
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight, style.ClassificationType);
			}
		}

		void StyleApplicationChanged(object sender, EventArgs e) {
			if (_UI.IsLocked || _ActiveLabel == null) {
				return;
			}
			if (_ApplyContentBox.Checked) {
				_ActiveLabel.StyleApplication = CommentStyleApplication.Content;
			}
			else if (_ApplyTagBox.Checked) {
				_ActiveLabel.StyleApplication = CommentStyleApplication.Tag;
			}
			else if (_ApplyContentTagBox.Checked) {
				_ActiveLabel.StyleApplication = CommentStyleApplication.TagAndContent;
			}
		}

		void _SyntaxListBox_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e) {
			if (e.ItemIndex == -1) {
				return;
			}
			var i = (e.Item as CommentTaggerListViewItem)?.CommentLabel;
			if (i == null) {
				return;
			}
			_UI.DoWithLock(() => {
				_ActiveLabel = i;
				_ApplyContentBox.Checked = i.StyleApplication == CommentStyleApplication.Content;
				_ApplyTagBox.Checked = i.StyleApplication == CommentStyleApplication.Tag;
				_ApplyContentTagBox.Checked = i.StyleApplication == CommentStyleApplication.TagAndContent;
				_EndWithPunctuationBox.Checked = i.AllowPunctuationDelimiter;
				_IgnoreCaseBox.Checked = i.IgnoreCase;
				var s = i.StyleID.ToString();
				for (int n = 0; n < _StyleBox.Items.Count; n++) {
					if ((string)_StyleBox.Items[n] == s) {
						_StyleBox.SelectedIndex = n;
						break;
					}
				}
				_TagTextBox.Text = i.Label;
				UpdatePreview();
			});
		}

		void UpdatePreview() {
			if (_ActiveLabel == null) {
				return;
			}
			var bmp = new Bitmap(_PreviewBox.Width, _PreviewBox.Height);
			ThemeHelper.GetFontSettings(FontsAndColorsCategory.TextEditor, out var fontName, out var fontSize);
			RenderPreview(bmp, fontName, fontSize, _ActiveLabel);
			_PreviewBox.Image = bmp;
		}

		static void RenderPreview(Bitmap bmp, string fontName, int fontSize, CommentLabel label) {
			var style = FindStyle(label);
			if (style == null || String.IsNullOrEmpty(label.Label)) {
				return;
			}
			var size = (float)(fontSize + style.FontSize);
			if (size < 2) {
				return;
			}
			Codist.SyntaxHighlight.FormatStore.MixStyle(style, out var fs, out var fc, out var bc);
			using (var g = Graphics.FromImage(bmp))
			using (var f = new Font(String.IsNullOrEmpty(style.Font) ? fontName : style.Font, size, fs))
			using (var b = new SolidBrush(fc))
			using (var bg = new SolidBrush(ThemeHelper.DocumentPageColor)) {
				g.FillRectangle(bg, 0, 0, bmp.Width, bmp.Height);
				var t = label.StyleApplication == CommentStyleApplication.Tag ? label.Label
					: label.StyleApplication == CommentStyleApplication.TagAndContent ? label.Label + " Preview 01ioIOlLWM"
					: "Preview 01ioIOlLWM";
				var m = g.MeasureString(t, f, bmp.Size);
				g.SmoothingMode = SmoothingMode.HighQuality;
				g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
				g.CompositingQuality = CompositingQuality.HighQuality;
				using (var bb = ConfigPage.GetPreviewBrush(style.BackgroundEffect, bc, ref m)) {
					g.FillRectangle(bb, new Rectangle(0, 0, (int)m.Width, (int)m.Height));
				}
				g.DrawString(t, f, b, new RectangleF(PointF.Empty, bmp.PhysicalDimension));
			}
		}

		static Codist.SyntaxHighlight.CommentStyle FindStyle(CommentLabel label) {
			return Config.Instance.CommentStyles.Find(i => i.StyleID == label.StyleID);
		}
	}
}
