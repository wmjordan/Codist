using System;
using System.Drawing;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[System.ComponentModel.ToolboxItem(false)]
	public partial class SmartBarPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public SmartBarPage() {
			InitializeComponent();
		}
		internal SmartBarPage(ConfigPage page) : this() {
			_UI.PostEventAction += () => Config.Instance.FireConfigChangedEvent(Features.SmartBar);
		}
		private void SmartBarPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_AutoShowSmartBarBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SmartBarOptions.ManualDisplaySmartBar, _AutoShowSmartBarBox.Checked == false));
			_ToggleSmartBarBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SmartBarOptions.ShiftToggleDisplay, _ToggleSmartBarBox.Checked));
			_BrowserPathBox.TextChanged += _UI.HandleEvent(() => Config.Instance.BrowserPath = _BrowserPathBox.Text);
			_BrowserParameterBox.TextChanged += _UI.HandleEvent(() => Config.Instance.BrowserParameter = _BrowserParameterBox.Text);
			_BrowseBrowserButton.Click += (s, args) => {
				if (_BrowseBrowserDialog.ShowDialog() == DialogResult.OK) {
					_BrowserPathBox.Text = _BrowseBrowserDialog.FileName;
				}
			};
			_SearchEngineBox.SelectedIndexChanged += _UI.HandleEvent(RefreshSearchEngineUI);
			_AddButton.Click += _UI.HandleEvent(() => {
				_SearchEngineBox.Items.Add(new ListViewItem(new[] { "New item", String.Empty }));
				Config.Instance.SearchEngines.Add(new SearchEngine("New Item", String.Empty));
				_SearchEngineBox.Items[_SearchEngineBox.Items.Count - 1].Selected = true;
				RefreshSearchEngineUI();
				_NameBox.Focus();
			});
			_RemoveButton.Click += _UI.HandleEvent(() => {
				var i = _SearchEngineBox.SelectedItems[0].SubItems;
				if (MessageBox.Show("Are you sure to remove search engine " + i[0].Text + "?", nameof(Codist), MessageBoxButtons.YesNo) == DialogResult.Yes) {
					var p = _SearchEngineBox.SelectedItems[0].Index;
					_SearchEngineBox.Items.RemoveAt(p);
					Config.Instance.SearchEngines.RemoveAt(p);
					RefreshSearchEngineUI();
				}
			});
			_MoveUpButton.Click += _UI.HandleEvent(() => {
				var i = _SearchEngineBox.SelectedItems[0];
				var p = i.Index;
				if (p > 0) {
					var se = Config.Instance.SearchEngines[p];
					_SearchEngineBox.Items.RemoveAt(p);
					Config.Instance.SearchEngines.RemoveAt(p);
					_SearchEngineBox.Items.Insert(--p, i);
					Config.Instance.SearchEngines.Insert(p, se);
				}
				_MoveUpButton.Enabled = p > 0;
			});
			_SaveButton.Click += _UI.HandleEvent(() => {
				var i = _SearchEngineBox.SelectedItems[0];
				i.SubItems[0].Text = _NameBox.Text;
				i.SubItems[1].Text = _UrlBox.Text;
				Config.Instance.SearchEngines[i.Index] = new SearchEngine(_NameBox.Text, _UrlBox.Text);
			});
			_ResetButton.Click += _UI.HandleEvent(() => {
				if (MessageBox.Show("Do you want to reset search engines to default ones?", nameof(Codist), MessageBoxButtons.YesNo) == DialogResult.Yes) {
					Config.Instance.ResetSearchEngines();
					ResetSearchEngines(Config.Instance.SearchEngines);
				}
			});
			RefreshSearchEngineUI();
			Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void RefreshSearchEngineUI() {
			if (_RemoveButton.Enabled = _ResetButton.Enabled = _SaveButton.Enabled = _NameBox.Enabled = _UrlBox.Enabled = _SearchEngineBox.SelectedIndices.Count > 0) {
				_MoveUpButton.Enabled = _SearchEngineBox.SelectedIndices[0] > 0;
				_NameBox.Text = _SearchEngineBox.SelectedItems[0].SubItems[0].Text;
				_UrlBox.Text = _SearchEngineBox.SelectedItems[0].SubItems[1].Text;
			}
			else {
				_MoveUpButton.Enabled = false;
			}
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_AutoShowSmartBarBox.Checked = config.SmartBarOptions.MatchFlags(SmartBarOptions.ManualDisplaySmartBar) == false;
				_ToggleSmartBarBox.Checked = config.SmartBarOptions.MatchFlags(SmartBarOptions.ShiftToggleDisplay);
				_BrowserPathBox.Text = config.BrowserPath;
				_BrowserParameterBox.Text = config.BrowserParameter;
				ResetSearchEngines(config.SearchEngines);
			});
		}

		void ResetSearchEngines(System.Collections.Generic.List<SearchEngine> searchEngines) {
			_SearchEngineBox.Items.Clear();
			_SearchEngineBox.Items.AddRange(searchEngines.ConvertAll(i => new ListViewItem(new[] { i.Name, i.Pattern })).ToArray());
		}
	}
}
