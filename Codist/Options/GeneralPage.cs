using System;
using System.Drawing;
using System.Windows.Forms;
using AppHelpers;

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
		}
		private void MiscPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_SuperQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(Features.SuperQuickInfo, _SuperQuickInfoBox.Checked));
			_SyntaxHighlightBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(Features.SyntaxHighlight, _SyntaxHighlightBox.Checked));
			_ScrollbarMarkerBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(Features.ScrollbarMarkers, _ScrollbarMarkerBox.Checked));
			_SmartBarBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(Features.SmartBar, _SmartBarBox.Checked));
			_CodeBarBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(Features.NaviBar, _CodeBarBox.Checked));

			_TopMarginBox.ValueChanged += _UI.HandleEvent(() => {
				Config.Instance.TopSpace = (double)_TopMarginBox.Value;
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			});
			_BottomMarginBox.ValueChanged += _UI.HandleEvent(() => {
				Config.Instance.BottomSpace = (double)_BottomMarginBox.Value;
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			});
			_NoSpaceBetweenWrappedLinesBox.CheckedChanged += _UI.HandleEvent(() => {
				Config.Instance.NoSpaceBetweenWrappedLines = _NoSpaceBetweenWrappedLinesBox.Checked;
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			});
			_OptimizeMainWindowBox.CheckedChanged += _UI.HandleEvent(() => {
				Config.Instance.Set(DisplayOptimizations.MainWindow, _OptimizeMainWindowBox.Checked);
				WpfHelper.SetUITextRenderOptions(System.Windows.Application.Current.MainWindow, _OptimizeMainWindowBox.Checked);
			});
			_OptimizeCodeWindowBox.CheckedChanged += _UI.HandleEvent(() => {
				Config.Instance.Set(DisplayOptimizations.CodeWindow, _OptimizeCodeWindowBox.Checked);
			});

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
			//todo distinguish syntax theme loading and config file loading
			Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_ScrollbarMarkerBox.Checked = config.Features.MatchFlags(Features.ScrollbarMarkers);
				_SuperQuickInfoBox.Checked = config.Features.MatchFlags(Features.SuperQuickInfo);
				_SyntaxHighlightBox.Checked = config.Features.MatchFlags(Features.SyntaxHighlight);
				_SmartBarBox.Checked = config.Features.MatchFlags(Features.SmartBar);
				_CodeBarBox.Checked = config.Features.MatchFlags(Features.NaviBar);
				_OptimizeMainWindowBox.Checked = config.DisplayOptimizations.MatchFlags(DisplayOptimizations.MainWindow);
				_OptimizeCodeWindowBox.Checked = config.DisplayOptimizations.MatchFlags(DisplayOptimizations.CodeWindow);
				_NoSpaceBetweenWrappedLinesBox.Checked = config.NoSpaceBetweenWrappedLines;
				_TopMarginBox.Value = (decimal)config.TopSpace;
				_BottomMarginBox.Value = (decimal)config.BottomSpace;
			});
		}
	}
}
