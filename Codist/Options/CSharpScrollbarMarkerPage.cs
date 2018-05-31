using System;
using System.ComponentModel;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[Browsable(false)]
	public partial class CSharpScrollbarMarkerPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public CSharpScrollbarMarkerPage() {
			InitializeComponent();
		}
		internal CSharpScrollbarMarkerPage(ConfigPage page) : this() {
			//_UI.CommonEventAction += Config.Instance.FireConfigChangedEvent;
		}

		void CSharpScrollbarMarkerPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_DirectivesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.CompilerDirective, _DirectivesBox.Checked));
			_SpecialCommentsBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.SpecialComment, _SpecialCommentsBox.Checked));
			_MemberDeclarationBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.MemberDeclaration, _LongMethodBox.Enabled = _TypeDeclarationBox.Enabled = _MethodDeclarationBox.Enabled = _MemberDeclarationBox.Checked));
			_LongMethodBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.LongMemberDeclaration, _LongMethodBox.Checked));
			_TypeDeclarationBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.TypeDeclaration, _TypeDeclarationBox.Checked));
			_MethodDeclarationBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.MethodDeclaration, _MethodDeclarationBox.Checked));

			Config.Loaded += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_DirectivesBox.Checked = config.MarkerOptions.MatchFlags(MarkerOptions.CompilerDirective);
				_SpecialCommentsBox.Checked = config.MarkerOptions.MatchFlags(MarkerOptions.SpecialComment);
				_MemberDeclarationBox.Checked = _LongMethodBox.Enabled = config.MarkerOptions.MatchFlags(MarkerOptions.MemberDeclaration);
				_LongMethodBox.Checked = config.MarkerOptions.MatchFlags(MarkerOptions.LongMemberDeclaration);
				_MethodDeclarationBox.Checked = config.MarkerOptions.MatchFlags(MarkerOptions.MethodDeclaration);
				_TypeDeclarationBox.Checked = _LongMethodBox.Enabled = _MethodDeclarationBox.Enabled = config.MarkerOptions.MatchFlags(MarkerOptions.TypeDeclaration);
			});
		}
	}
}
