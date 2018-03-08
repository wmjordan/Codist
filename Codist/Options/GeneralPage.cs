using System;
using System.Drawing;
using System.Windows.Forms;

namespace Codist.Options
{
	public partial class GeneralPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public GeneralPage() {
			InitializeComponent();
		}
		internal GeneralPage(ConfigPage page) : this() {
			_UI.CommonEventAction += Config.Instance.FireConfigChangedEvent;
		}
		private void MiscPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			_TopMarginBox.Value = (decimal)LineTransformers.LineHeightTransformProvider.TopSpace;
			_BottomMarginBox.Value = (decimal)LineTransformers.LineHeightTransformProvider.BottomSpace;
			LoadConfig(Config.Instance);

			_TopMarginBox.ValueChanged += _UI.HandleEvent(() => LineTransformers.LineHeightTransformProvider.TopSpace = (double)_TopMarginBox.Value);
			_BottomMarginBox.ValueChanged += _UI.HandleEvent(() => LineTransformers.LineHeightTransformProvider.BottomSpace = (double)_BottomMarginBox.Value);
			_NoSpaceBetweenWrappedLinesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.NoSpaceBetweenWrappedLines = _NoSpaceBetweenWrappedLinesBox.Checked);
			_SaveConfigButton.Click += (s, args) => {
				using (var d = new SaveFileDialog {
					Title = "Save Codist configuration file...",
					FileName = "Codist.json",
					DefaultExt = "json",
					Filter = "Codist configuration file|*.json"
				}) {
					if (d.ShowDialog() != DialogResult.OK) {
						return;
					}
					Config.Instance.SaveConfig(d.FileName);
				}
			};
			_LoadConfigButton.Click += (s, args) => {
				_ThemeMenu.Show(_LoadConfigButton, new Point(0, _LoadConfigButton.Height));
			};
			_ResetConfigButton.Click += (s, args) => {
				if (MessageBox.Show("Do you want to reset the syntax highlight settings to default?", nameof(Codist), MessageBoxButtons.YesNo) == DialogResult.Yes) {
					Config.ResetStyles();
				}
			};
			_ThemeMenu.ItemClicked += (s, args) => {
				switch (args.ClickedItem.Tag) {
					case "Light": Config.LoadConfig(Config.LightTheme); return;
					case "Dark": Config.LoadConfig(Config.DarkTheme); return;
				}
				_ThemeMenu.Hide();
				using (var d = new OpenFileDialog {
					Title = "Load Codist configuration file...",
					FileName = "Codist.json",
					DefaultExt = "json",
					Filter = "Codist configuration file|*.json"
				}) {
					if (d.ShowDialog() != DialogResult.OK) {
						return;
					}
					try {
						Config.LoadConfig(d.FileName);
						System.IO.File.Copy(d.FileName, Config.ConfigPath, true);
					}
					catch (Exception ex) {
						MessageBox.Show("Error occured while loading config file: " + ex.Message, nameof(Codist));
					}
				}
			};
			Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_NoSpaceBetweenWrappedLinesBox.Checked = config.NoSpaceBetweenWrappedLines;
			});
		}
	}
}
