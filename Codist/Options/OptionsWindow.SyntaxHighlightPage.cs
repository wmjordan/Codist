using System;
using System.Windows.Controls;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class SyntaxHighlightPage : OptionPageFactory
		{
			public override string Name => R.T_SyntaxHighlight;
			public override Features RequiredFeature => Features.SyntaxHighlight;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly OptionBox<SpecialHighlightOptions> _CommentTaggerBox, _SearchResultBox;

				public PageControl() {
					var o = Config.Instance.SpecialHighlightOptions;
					SetContents(new TextBlock { TextWrapping = System.Windows.TextWrapping.Wrap, Margin = WpfHelper.MiddleVerticalMargin }
						.Append(R.OT_ConfigSyntaxNote)
						.AppendLink(R.CMD_ConfigureSyntaxHighlight, _ => Commands.SyntaxCustomizerWindowCommand.Execute(null, EventArgs.Empty), R.CMDT_ConfigureSyntaxHighlight),
						new TitleBox(R.OT_ExtraHighlight),
						_CommentTaggerBox = o.CreateOptionBox(SpecialHighlightOptions.SpecialComment, UpdateConfig, R.OT_EnableCommentTagger),
						_SearchResultBox = o.CreateOptionBox(SpecialHighlightOptions.SearchResult, UpdateConfig, R.OT_HighlightSearchResults),
						new DescriptionBox("*: The highlight search results feature is under development and may not work as expected")
					);
				}

				void UpdateConfig(SpecialHighlightOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
				}

				protected override void LoadConfig(Config config) {
					_CommentTaggerBox.UpdateWithOption(config.SpecialHighlightOptions);
					_SearchResultBox.UpdateWithOption(config.SpecialHighlightOptions);
				}
			}
		}
	}
}
