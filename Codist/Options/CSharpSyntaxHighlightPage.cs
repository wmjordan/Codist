using System;
using System.ComponentModel;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[Browsable(false)]
	public partial class CSharpSyntaxHighlightPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		readonly ConfigPage _ServicePage;
		bool _Loaded;

		public CSharpSyntaxHighlightPage() {
			InitializeComponent();
		}
		internal CSharpSyntaxHighlightPage(ConfigPage page) : this() {
			_ServicePage = page;
		}

		void CSharpSpecialHighlightPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_OptionTabs.AddPage("C# Syntax", new SyntaxStyleOptionPage(_ServicePage, () => Config.Instance.CodeStyles, Config.GetDefaultCSharpStyles), true);

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
