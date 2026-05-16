using System;
using R = Codist.Properties.Resources;

namespace Codist.Options;

sealed partial class OptionsWindow
{
	sealed class FileBrowserPage : OptionPageFactory
	{
		public override string Name => R.T_FileBrowser;
		public override Features RequiredFeature => Features.FileBrowser;

		protected override OptionPage CreatePage() {
			return new PageControl();
		}

		sealed class PageControl : OptionPage
		{
			readonly OptionBox<FileBrowserOptions> _UsePreview, _UseCodeWindow, _DimNonSolutionItems, _ShowLabelsBox, _ShowSolutionProjectsBox, _ShowSolutionFolderBox, _ShowProjectFolderBox, _ShowDocumentFolderBox, _ShowOpenedDocumentsBox;

			public PageControl() {
				var o = Config.Instance.FileBrowserOptions;
				SetContents(new Note(R.OT_ConfigFileBrowserNote),
					new TitleBox(R.OT_Behavior),
					_UsePreview = o.CreateOptionBox(FileBrowserOptions.UseProvisional, UpdateConfig, R.OT_UseProvisionalWindow),
					_UseCodeWindow = o.CreateOptionBox(FileBrowserOptions.UseCodeWindow, UpdateConfig, R.OT_PreferCodeWindow),
					_DimNonSolutionItems = o.CreateOptionBox(FileBrowserOptions.DimNonSolutionItems, UpdateConfig, R.OT_DimNonSolutionItems),
					new TitleBox(R.OT_ExtraHighlight),
					_ShowSolutionProjectsBox = o.CreateOptionBox(FileBrowserOptions.ShowSolutionProjects, UpdateConfig, R.OT_ShowSolutionProjects),
					_ShowSolutionFolderBox = o.CreateOptionBox(FileBrowserOptions.ShowSolutionFolder, UpdateConfig, R.OT_ShowSolutionFolder),
					_ShowProjectFolderBox = o.CreateOptionBox(FileBrowserOptions.ShowCurrentProjectFolder, UpdateConfig, R.OT_ShowProjectFolder),
					_ShowDocumentFolderBox = o.CreateOptionBox(FileBrowserOptions.ShowCurrentDocumentFolder, UpdateConfig, R.OT_ShowDocumentFolder),
					_ShowOpenedDocumentsBox = o.CreateOptionBox(FileBrowserOptions.ShowOpenedDocuments, UpdateConfig, R.OT_ShowOpenedDocuments),
					_ShowLabelsBox = o.CreateOptionBox(FileBrowserOptions.ShowLabels, UpdateConfig, R.OT_ShowLabels)
				);
			}

			void UpdateConfig(FileBrowserOptions options, bool set) {
				if (IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.FileBrowser);
			}

			protected override void LoadConfig(Config config) {
				var options = config.FileBrowserOptions;
				_UsePreview.UpdateWithOption(options);
				_UseCodeWindow.UpdateWithOption(options);
				_DimNonSolutionItems.UpdateWithOption(options);
				_ShowLabelsBox.UpdateWithOption(options);
				_ShowSolutionProjectsBox.UpdateWithOption(options);
				_ShowSolutionFolderBox.UpdateWithOption(options);
				_ShowProjectFolderBox.UpdateWithOption(options);
				_ShowDocumentFolderBox.UpdateWithOption(options);
				_ShowOpenedDocumentsBox.UpdateWithOption(options);
			}
		}
	}
}
