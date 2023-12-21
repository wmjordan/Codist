using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Codist
{
	/// <summary>
	/// Initiates fundamental services and information for Codist to run.
	/// </summary>
	sealed class ServicesHelper
	{
		// We initialize the SolutionDocumentEvents instance here since ServicesHelper is called by many places in Codist, usually sooner than CodistPackage.InitializeAsync
		readonly SolutionDocumentEvents _DocumentEvents = new SolutionDocumentEvents();

		private ServicesHelper() {
			(Get<IComponentModel, SComponentModel>() ?? throw new TypeLoadException($"Could not load {nameof(SComponentModel)}"))
				.DefaultCompositionService
				.SatisfyImportsOnce(this);
			PostInitialization();
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
		public IContentTypeRegistryService ContentTypeRegistry { get; private set; }

		[Import]
		public IFileExtensionRegistryService FileExtensionRegistry { get; private set; }

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

		void PostInitialization() {
			#region Create classification types for syntax highlight
			var e = new ClassificationTypeExporter(ClassificationTypeRegistry, ContentTypeRegistry);
			e.FindClassificationTypes<SymbolMarkerStyleTypes>();
			e.FindClassificationTypes<CommentStyleTypes>();
			e.FindClassificationTypes<CSharpStyleTypes>();
			e.FindClassificationTypes<MarkdownStyleTypes>();
			e.FindClassificationTypes<XmlStyleTypes>();
			e.FindClassificationTypes<PrivateStyleTypes>();
			//note: do not add the following line, or highlight will get corrupted
			//e.FindClassificationDefinitions<CppStyleTypes>();
			e.ExportClassificationTypes();
			#endregion
		}

		sealed class ClassificationTypeExporter
		{
			readonly IClassificationTypeRegistryService _Classifications;
			readonly IContentTypeRegistryService _ContentTypes;
			readonly List<ExportEntry> _Entries = new List<ExportEntry>();

			public ClassificationTypeExporter(IClassificationTypeRegistryService classifications, IContentTypeRegistryService contentTypes) {
				_Classifications = classifications;
				_ContentTypes = contentTypes;
			}

			public void FindClassificationTypes<TStyle>() where TStyle : Enum {
				var t = typeof(TStyle);
				var r = _Classifications;

				// skip classification types which do not have a corresponding content type
				var c = t.GetCustomAttribute<CategoryAttribute>();
				if (c != null && _ContentTypes.GetContentType(c.Category) == null) {
					return;
				}

				foreach (var field in t.GetFields()) {
					var name = field.GetCustomAttribute<ClassificationTypeAttribute>()?.ClassificationTypeNames;
					if (String.IsNullOrEmpty(name)
						|| r.GetClassificationType(name) != null
						|| field.GetCustomAttribute<InheritanceAttribute>() != null) {
						continue;
					}
					var baseNames = new List<string>(field.GetCustomAttributes<BaseDefinitionAttribute>().Select(d => d.BaseDefinition).Where(i => String.IsNullOrEmpty(i) == false));
					_Entries.Add(new ExportEntry(name, baseNames.Count != 0 ? baseNames : null));
				}
			}

			public void ExportClassificationTypes() {
				var e = 0;
				int lastExported;
				var r = _Classifications;
				do {
					lastExported = e;
					foreach (var item in _Entries) {
						if (item.Exported || r.GetClassificationType(item.Name) != null) {
							continue;
						}
						if (item.BaseNames == null) {
							r.CreateClassificationType(item.Name, Enumerable.Empty<IClassificationType>());
							item.MarkExported();
							e++;
						}
						else {
							var cts = item.BaseNames.ConvertAll(r.GetClassificationType);
							cts.RemoveAll(i => i == null);
							r.CreateClassificationType(item.Name, cts);
							e++;
							item.MarkExported();
						}
					}
				}
				while (e < _Entries.Count && lastExported != e);
			}

			sealed class ExportEntry
			{
				public readonly string Name;
				public readonly List<string> BaseNames;
				public bool Exported;

				public ExportEntry(string name, List<string> baseNames) {
					Name = name;
					BaseNames = baseNames;
				}

				public void MarkExported() {
					Exported = true;
					$"Export classification type: {Name}".Log();
				}

				public override string ToString() {
					return $"{Name} ({(Exported ? "E" : "?")})";
				}
			}
		}
	}
}
