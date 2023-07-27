using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	static class XmlClassificationDefinitions
	{
#pragma warning disable 169, IDE0044
		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.XmlName)]
		static ClassificationTypeDefinition XmlName;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.XmlAttributeName)]
		static ClassificationTypeDefinition XmlAttributeName;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.XmlAttributeQuotes)]
		static ClassificationTypeDefinition XmlAttributeQuotes;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.XmlAttributeValue)]
		static ClassificationTypeDefinition XmlAttributeValue;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.XmlCData)]
		static ClassificationTypeDefinition XmlCData;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.XmlComment)]
		static ClassificationTypeDefinition XmlComment;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.XmlDelimiter)]
		static ClassificationTypeDefinition XmlDelimiter;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.XmlProcessingInstruction)]
		static ClassificationTypeDefinition XmlProcessingInstruction;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.XmlText)]
		static ClassificationTypeDefinition XmlText;
#pragma warning restore 169
	}
}
