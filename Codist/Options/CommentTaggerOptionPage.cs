using System;
using System.Windows.Forms;

namespace Codist.Options
{
	public partial class CommentTaggerOptionControl : UserControl
	{
		CommentLabel _activeLabel;
		bool _uiLock;
		bool _loaded;

		public CommentTaggerOptionControl() {
			InitializeComponent();
		}

		protected override void OnLoad(EventArgs e) {
			base.OnLoad(e);
			if (_loaded) {
				return;
			}

			foreach (var item in Config.Instance.Labels) {
				_SyntaxListBox.Items.Add(new ListViewItem(item.Label) { Tag = item });
			}
			foreach (var item in Enum.GetNames(typeof(CommentStyle))) {
				_StyleBox.Items.Add(item);
			}
			_SyntaxListBox.ItemSelectionChanged += _SyntaxListBox_ItemSelectionChanged;
			_ApplyContentBox.CheckedChanged += StyleApplicationChanged;
			_ApplyTagBox.CheckedChanged += StyleApplicationChanged;
			_IgnoreCaseBox.CheckedChanged += (s, args) => { if (_uiLock == false) { _activeLabel.IgnoreCase = _IgnoreCaseBox.Checked; } };
			_EndWithPunctuationBox.CheckedChanged += (s, args) => { if (_uiLock == false) { _activeLabel.AllowPunctuationDelimiter = _EndWithPunctuationBox.Checked; } };
			_StyleBox.SelectedIndexChanged += (s, args) => { if (_uiLock == false) { _activeLabel.StyleID = (CommentStyle)_StyleBox.SelectedIndex; } };
			_TagTextBox.TextChanged += (s, args) => { if (_uiLock == false) { _activeLabel.Label = _TagTextBox.Text; } };
			_loaded = true;
		}

		void StyleApplicationChanged(object sender, EventArgs e) {
			if (_uiLock) {
				return;
			}
			if (_ApplyContentBox.Checked) {
				_activeLabel.StyleApplication = CommentStyleApplication.Content;
			}
			else if (_ApplyTagBox.Checked) {
				_activeLabel.StyleApplication = CommentStyleApplication.Tag;
			}
		}

		void _SyntaxListBox_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e) {
			if (e.ItemIndex == -1) {
				return;
			}
			var i = e.Item.Tag as CommentLabel;
			if (i == null) {
				return;
			}
			_uiLock = true;
			_activeLabel = i;
			_ApplyContentBox.Checked = i.StyleApplication == CommentStyleApplication.Content;
			_ApplyTagBox.Checked = i.StyleApplication == CommentStyleApplication.Tag;
			_EndWithPunctuationBox.Checked = i.AllowPunctuationDelimiter;
			_IgnoreCaseBox.Checked = i.IgnoreCase;
			_StyleBox.SelectedIndex = (int)i.StyleID;
			_TagTextBox.Text = i.Label;
			UpdatePreview();
			_uiLock = false;
		}

		void UpdatePreview() {

		}
	}
}
