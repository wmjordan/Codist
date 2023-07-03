using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist
{
	sealed class ServicesHelper
	{
		// We initialize the SolutionDocumentEvents instance here since ServicesHelper is called by many places in Codist, usually sooner than CodistPackage.InitializeAsync
		readonly SolutionDocumentEvents _DocumentEvents = new SolutionDocumentEvents();

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
		public IClassifierAggregatorService ClassifierAggregator { get; private set; }

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

		[Import]
		public IRtfBuilderService RtfService { get; private set; }

		[Import]
		public ITextUndoHistoryRegistry TextUndoHistoryService { get; private set; }

		public static TInterface Get<TInterface, VSInterface>() where TInterface : class {
			ThreadHelper.ThrowIfNotOnUIThread();
			return ServiceProvider.GlobalProvider.GetService(typeof(VSInterface)) as TInterface;
		}
	}
}
