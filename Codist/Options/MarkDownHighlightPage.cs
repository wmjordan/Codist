using System;
using System.ComponentModel;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[ToolboxItem(false)]
	public partial class MarkdownHighlightPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		readonly ConfigPage _ServicePage;
		bool _Loaded;

		public MarkdownHighlightPage() {
			InitializeComponent();
		}
		internal MarkdownHighlightPage(ConfigPage page) : this() {
			_ServicePage = page;
			_UI.CommonEventAction += () => Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
		}

		void MarkdownHighlightPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_OptionTabs.AddPage("Markdown marker", new SyntaxStyleOptionPage(_ServicePage, () => Config.Instance.MarkdownStyles, Config.GetDefaultCodeStyles<Codist.SyntaxHighlight.MarkdownStyle, MarkdownStyleTypes>), true);
			_OptionTabs.TabPages.RemoveAt(1);

			Config.Loaded += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
			});
		}
	}
}
