using System;
using System.ComponentModel;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[ToolboxItem(false)]
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
			_UI.PostEventAction += () => Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
		}

		void CSharpSpecialHighlightPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_OptionTabs.AddPage("Symbol marker", new SyntaxStyleOptionPage(_ServicePage, () => Config.Instance.SymbolMarkerStyles, Config.GetDefaultCodeStyles<Codist.SyntaxHighlight.SymbolMarkerStyle, SymbolMarkerStyleTypes>), true);
			_OptionTabs.AddPage("C# Syntax", new SyntaxStyleOptionPage(_ServicePage, () => Config.Instance.CodeStyles, Config.GetDefaultCodeStyles<Codist.SyntaxHighlight.CSharpStyle, CSharpStyleTypes>), true);

			_MarkSpecialPunctuationBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.SpecialPunctuation, _MarkSpecialPunctuationBox.Checked));
			_HighlightDeclarationBracesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.DeclarationBrace, _HighlightDeclarationBracesBox.Checked));
			_HighlightParameterBracesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.ParameterBrace, _HighlightParameterBracesBox.Checked));
			_HighlightCastParenthesesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.CastBrace, _HighlightCastParenthesesBox.Checked));
			_HighlightBranchBracesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.BranchBrace, _HighlightBranchBracesBox.Checked));
			_HighlightLoopBracesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.LoopBrace, _HighlightLoopBracesBox.Checked));
			_HighlightResourceBracesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.ResourceBrace, _HighlightResourceBracesBox.Checked));

			_HighlightLocalFunctionDeclarationBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.LocalFunctionDeclaration, _HighlightLocalFunctionDeclarationBox.Checked));
			_HighlightNonPrivateFieldDeclarationBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.NonPrivateField, _HighlightNonPrivateFieldDeclarationBox.Checked));

			Config.Loaded += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_MarkSpecialPunctuationBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialPunctuation);

				_HighlightDeclarationBracesBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.DeclarationBrace);
				_HighlightParameterBracesBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.ParameterBrace);
				_HighlightCastParenthesesBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.CastBrace);
				_HighlightBranchBracesBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.BranchBrace);
				_HighlightLoopBracesBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.LoopBrace);
				_HighlightResourceBracesBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.ResourceBrace);

				_HighlightLocalFunctionDeclarationBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.LocalFunctionDeclaration);
				_HighlightNonPrivateFieldDeclarationBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.NonPrivateField);
			});
		}
	}
}
