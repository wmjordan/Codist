using System;
using System.ComponentModel;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[Browsable(false)]
	public partial class CSharpSpecialHighlightPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public CSharpSpecialHighlightPage() {
			InitializeComponent();
		}
		internal CSharpSpecialHighlightPage(ConfigPage page) : this() {
			//_UI.CommonEventAction += Config.Instance.FireConfigChangedEvent;
		}

		void CSharpSpecialHighlightPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_HighlightDeclarationBracesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.DeclarationBrace, _HighlightDeclarationBracesBox.Checked));
			_HighlightParameterBracesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.ParameterBrace, _HighlightParameterBracesBox.Checked));
			_HighlightSpecialCommentBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.SpecialComment, _HighlightSpecialCommentBox.Checked));

			Config.Loaded += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_HighlightDeclarationBracesBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.DeclarationBrace);
				_HighlightParameterBracesBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.ParameterBrace);
				_HighlightSpecialCommentBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialComment);
			});
		}
	}
}
