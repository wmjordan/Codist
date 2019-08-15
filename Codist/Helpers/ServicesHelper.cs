using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist
{
	sealed class ServicesHelper
	{
		private ServicesHelper() {
			ThreadHelper.ThrowIfNotOnUIThread();
			(ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel ?? throw new TypeLoadException($"Could not load {nameof(SComponentModel)}"))
				.DefaultCompositionService
				.SatisfyImportsOnce(this);
		}

		public static ServicesHelper Instance { get; } = new ServicesHelper();

		[Import]
		public IBufferTagAggregatorFactoryService BufferTagAggregatorFactory { get; private set; }

		[Import]
		public IClassificationFormatMapService ClassificationFormatMap { get; private set; }

		[Import]
		public IClassificationTypeRegistryService ClassificationTypeRegistry { get; private set; }

		[Import]
		public Microsoft.VisualStudio.Utilities.IFileExtensionRegistryService FileExtensionRegistry { get; private set; }

		[Import]
		public IEditorFormatMapService EditorFormatMap { get; private set; }

		[Import]
		public IGlyphService Glyph { get; private set; }

		[Import]
		public Microsoft.VisualStudio.Text.Outlining.IOutliningManagerService OutliningManager { get; private set; }

		[Import]
		public ITextStructureNavigatorSelectorService TextStructureNavigator { get; private set; }

		[Import]
		public IViewTagAggregatorFactoryService ViewTagAggregatorFactory { get; private set; }

		[Import]
		public Microsoft.VisualStudio.LanguageServices.VisualStudioWorkspace VisualStudioWorkspace { get; private set; }
	}
}
