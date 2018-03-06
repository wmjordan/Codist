using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Classifiers
{
    /// <summary>
    /// Classifier provider. It adds the classifier to the set of classifiers.
    /// </summary>
    [Export(typeof(IClassifierProvider))]
    [ContentType("CSharp")]
    sealed class CSharpClassifierProvider : IClassifierProvider
    {
        /// <summary>
        /// Classification registry to be used for getting a reference to the custom classification
        /// type later.
        /// </summary>
        [Import]
        IClassificationTypeRegistryService _classificationRegistry = null;

        /// <summary>
        /// Text document factory to be used for getting a event of text document disposed.
        /// </summary>
        [Import]
        ITextDocumentFactoryService _textDocumentFactoryService = null;

        /// <summary>
        /// Gets a classifier for the given text buffer.
        /// </summary>
        /// <param name="textBuffer">The <see cref="ITextBuffer"/> to classify.</param>
        /// <returns>
        /// A classifier for the text buffer, or null if the provider cannot do so in its current state.
        /// </returns>
        public IClassifier GetClassifier(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(() =>
                new CSharpClassifier(_classificationRegistry, _textDocumentFactoryService, textBuffer));
        }
    }
}