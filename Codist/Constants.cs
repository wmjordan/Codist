using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Codist
{
	/// <summary>
	/// Code style constants
	/// </summary>
	static partial class Constants
	{
		public static class SyntaxCategory
		{
			public const string Keyword = nameof(Keyword);
			public const string Preprocessor = nameof(Preprocessor);
			public const string General = nameof(General);
			public const string Comment = nameof(Comment);
			public const string CompilerMarked = "Compiler Marked";
			public const string Declaration = nameof(Declaration);
			public const string TypeDefinition = "Type Definition";
			public const string Member = nameof(Member);
		}

		public const string CodeKeyword = "Keyword";
		public const string CodeComment = "Comment";

		public const string CodeAbstractionKeyword = "Keyword: Abstraction";
		public const string CodeReturnKeyword = "Keyword: return, throw";

		public const string CodeClassName = "class name";
		public const string CodeStructName = "struct name";
		public const string CodeEnumName = "enum name";
		public const string CodeInterfaceName = "interface name";
		public const string CodeDelegateName = "delegate name";
		public const string CodeModuleName = "module name";
		public const string CodeTypeParameterName = "type parameter name";
		public const string CodePreprocessorText = "preprocessor text";
		public const string CodePreprocessorKeyword = "preprocessor keyword";
		public const string CodeExcluded = "excluded code";
		public const string CodeUnnecessary = "unnecessary code";
		public const string CodeIdentifier = "identifier";
		public const string CodeLiteral = "literal";
		public const string CodeNumber = "number";
		public const string CodeOperator = "operator";
		public const string CodePunctuation = "punctuation";
		public const string CodeBraceMatching = "brace matching";
		public const string CodeInlineRenameField = "inline rename field";
		public const string CodeString = "string";
		public const string CodeStringVerbatim = "string - verbatim";
		public const string CodeSymbolDefinition = "symbol definition";
		public const string CodeSymbolReference = "symbol reference";
		public const string CodeUrl = "url";
		public const string CodeFormalLanguage = "formal language";

		public const string XmlDocAttributeName = "xml doc comment - attribute name";
		public const string XmlDocAttributeQuotes = "xml doc comment - attribute quotes";
		public const string XmlDocAttributeValue = "xml doc comment - attribute value";
		public const string XmlDocComment = "xml doc comment - text";
		public const string XmlDocCData = "xml doc comment - cdata section";
		public const string XmlDocDelimiter = "xml doc comment - delimiter";
		public const string XmlDocEntity = "xml doc comment - entity reference";
		public const string XmlDocTag = "xml doc comment - name";

		public const string CSharpLocalFieldName = "C#: Local field";
		public const string CSharpParameterName = "C#: Parameter";
		public const string CSharpNamespaceName = "C#: Namespace";
		public const string CSharpExtensionMethodName = "C#: Extension method";
		public const string CSharpExternMethodName = "C#: Extern method";
		public const string CSharpMethodName = "C#: Method";
		public const string CSharpEventName = "C#: Event";
		public const string CSharpPropertyName = "C#: Property";
		public const string CSharpFieldName = "C#: Field";
		public const string CSharpConstFieldName = "C#: Const field";
		public const string CSharpReadOnlyFieldName = "C#: Read-only field";
		public const string CSharpAliasNamespaceName = "C#: Alias namespace";
		public const string CSharpConstructorMethodName = "C#: Constructor method";
		public const string CSharpDeclarationName = "C#: Type declaration";
		public const string CSharpNestedDeclarationName = "C#: Nested type declaration";
		public const string CSharpTypeParameterName = "C#: Type parameter";
		public const string CSharpStaticMemberName = "C#: Static member";
		public const string CSharpOverrideMemberName = "C#: Override member";
		public const string CSharpVirtualMemberName = "C#: Virtual member";
		public const string CSharpAbstractMemberName = "C#: Abstract member";
		public const string CSharpSealedClassName = "C#: Sealed class";
		public const string CSharpAttributeNotation = "C#: Attribute notation";
		public const string CSharpLabel = "C#: Label";

		//public const string EditorIntellisense = "intellisense";
		//public const string EditorSigHelp = "sighelp";
		//public const string EditorSigHelpDoc = "sighelp-doc";

		//! Important
		//# Notice
		public const String EmphasisComment = "Comment: Emphasis";
		//? Question
		public const String QuestionComment = "Comment: Question";
		//!? Exclaimation
		public const String ExclaimationComment = "Comment: Exclaimation";
		//x Removed
		public const String DeletionComment = "Comment: Deletion";

		//TODO: This does not need work
		public const String TodoComment = "Comment: Task - ToDo";
		//NOTE: Watch-out!
		public const String NoteComment = "Comment: Task - Note";
		//Hack: B-) We are in the Matrix now!!!
		public const String HackComment = "Comment: Task - Hack";

		//+++ heading 1
		public const string Heading1Comment = "Comment: Heading 1";
		//++ heading 2
		public const string Heading2Comment = "Comment: Heading 2";
		//+ heading 3
		public const string Heading3Comment = "Comment: Heading 3";
		//- heading 4
		public const string Heading4Comment = "Comment: Heading 4";
		//-- heading 5
		public const string Heading5Comment = "Comment: Heading 5";
		//--- heading 6
		public const string Heading6Comment = "Comment: Heading 6";

		public const string Task1Comment = "Comment: Task 1";
		public const string Task2Comment = "Comment: Task 2";
		public const string Task3Comment = "Comment: Task 3";
		public const string Task4Comment = "Comment: Task 4";
		public const string Task5Comment = "Comment: Task 5";
		public const string Task6Comment = "Comment: Task 6";
		public const string Task7Comment = "Comment: Task 7";
		public const string Task8Comment = "Comment: Task 8";
		public const string Task9Comment = "Comment: Task 9";

		public static readonly Color CommentColor = Colors.Green;
		public static readonly Color QuestionColor = Colors.MediumPurple;
		public static readonly Color ExclaimationColor = Colors.IndianRed;
		public static readonly Color DeletionColor = Colors.Gray;
		public static readonly Color ToDoColor = Colors.DarkBlue;
		public static readonly Color NoteColor = Colors.Orange;
		public static readonly Color HackColor = Colors.Black;
		public static readonly Color ReturnColor = Colors.MediumBlue;
		public static readonly Color AbstractionColor = Colors.DarkOrange;
	}

	enum CommentStyleTypes
	{
		[Description(Constants.CodeComment)]
		Default,
		[Description(Constants.EmphasisComment)]
		Emphasis,
		[Description(Constants.QuestionComment)]
		Question,
		[Description(Constants.ExclaimationComment)]
		Exclaimation,
		[Description(Constants.DeletionComment)]
		Deletion,
		[Description(Constants.TodoComment)]
		ToDo,
		[Description(Constants.NoteComment)]
		Note,
		[Description(Constants.HackComment)]
		Hack,
		[Description(Constants.Heading1Comment)]
		Heading1,
		[Description(Constants.Heading2Comment)]
		Heading2,
		[Description(Constants.Heading3Comment)]
		Heading3,
		[Description(Constants.Heading4Comment)]
		Heading4,
		[Description(Constants.Heading5Comment)]
		Heading5,
		[Description(Constants.Heading6Comment)]
		Heading6,
		[Description(Constants.Task1Comment)]
		Task1,
		[Description(Constants.Task2Comment)]
		Task2,
		[Description(Constants.Task3Comment)]
		Task3,
		[Description(Constants.Task4Comment)]
		Task4,
		[Description(Constants.Task5Comment)]
		Task5,
		[Description(Constants.Task6Comment)]
		Task6,
		[Description(Constants.Task7Comment)]
		Task7,
		[Description(Constants.Task8Comment)]
		Task8,
		[Description(Constants.Task9Comment)]
		Task9,
	}

	enum CodeStyleTypes
	{
		None,
		[Category(Constants.SyntaxCategory.Keyword)]
		[Description(Constants.CodeKeyword)]
		Keyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[Description(Constants.CodeReturnKeyword)]
		MethodReturnKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[Description(Constants.CodeAbstractionKeyword)]
		AbstractionKeyword,
		//[Description(Constants.CodeSymbolDefinition)]
		//SymbolDefinition,
		//[Description(Constants.CodeSymbolReference)]
		//SymbolReference,

		[Category(Constants.SyntaxCategory.Declaration)]
		[Description(Constants.CSharpDeclarationName)]
		TypeDeclaration,
		[Category(Constants.SyntaxCategory.Declaration)]
		[Description(Constants.CSharpNestedDeclarationName)]
		NestedTypeDeclaration,
		[Category(Constants.SyntaxCategory.Declaration)]
		[Description(Constants.CSharpStaticMemberName)]
		StaticMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[Description(Constants.CSharpOverrideMemberName)]
		OverrideMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[Description(Constants.CSharpAbstractMemberName)]
		AbstractMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[Description(Constants.CSharpVirtualMemberName)]
		VirtualMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[Description(Constants.CSharpAttributeNotation)]
		AttributeNotation,

		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[Description(Constants.CSharpNamespaceName)]
		NamespaceName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[Description(Constants.CodeClassName)]
		ClassName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[Description(Constants.CSharpSealedClassName)]
		SealedClassName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[Description(Constants.CodeStructName)]
		StructName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[Description(Constants.CodeInterfaceName)]
		InterfaceName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[Description(Constants.CodeEnumName)]
		EnumName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[Description(Constants.CodeDelegateName)]
		DelegateName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[Description(Constants.CSharpEventName)]
		EventName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[Description(Constants.CSharpTypeParameterName)]
		TypeParameterName,

		//[Description(Constants.CodeModuleName)]
		//ModuleDeclaration,
		[Category(Constants.SyntaxCategory.Member)]
		[Description(Constants.CSharpConstructorMethodName)]
		ConstructorMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[Description(Constants.CSharpFieldName)]
		FieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[Description(Constants.CSharpLocalFieldName)]
		LocalFieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[Description(Constants.CSharpConstFieldName)]
		ConstFieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[Description(Constants.CSharpReadOnlyFieldName)]
		ReadOnlyFieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[Description(Constants.CSharpPropertyName)]
		PropertyName,
		[Category(Constants.SyntaxCategory.Member)]
		[Description(Constants.CSharpMethodName)]
		MethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[Description(Constants.CSharpExtensionMethodName)]
		ExtensionMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[Description(Constants.CSharpExternMethodName)]
		ExternMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[Description(Constants.CSharpParameterName)]
		ParameterName,

		[Category(Constants.SyntaxCategory.General)]
		[Description(Constants.CodeIdentifier)]
		Identifier,
		[Category(Constants.SyntaxCategory.General)]
		[Description(Constants.CodeNumber)]
		Number,
		[Category(Constants.SyntaxCategory.General)]
		[Description(Constants.CodeString)]
		String,
		[Category(Constants.SyntaxCategory.General)]
		[Description(Constants.CodeStringVerbatim)]
		StringVerbatim,
		[Category(Constants.SyntaxCategory.General)]
		[Description(Constants.CodeOperator)]
		Operator,
		[Category(Constants.SyntaxCategory.General)]
		[Description(Constants.CodePunctuation)]
		Punctuation,
		[Category(Constants.SyntaxCategory.General)]
		[Description(Constants.CodeUrl)]
		Url,
		[Category(Constants.SyntaxCategory.General)]
		[Description(Constants.CSharpLabel)]
		Label,
		[Category(Constants.SyntaxCategory.Preprocessor)]
		[Description(Constants.CodePreprocessorText)]
		PreprocessorText,
		[Category(Constants.SyntaxCategory.Preprocessor)]
		[Description(Constants.CodePreprocessorKeyword)]
		PreprocessorKeyword,
		[Category(Constants.SyntaxCategory.Comment)]
		[Description(Constants.CodeComment)]
		Comment,
		[Category(Constants.SyntaxCategory.Comment)]
		[Description(Constants.XmlDocComment)]
		XmlDocComment,
		[Category(Constants.SyntaxCategory.Comment)]
		[Description(Constants.XmlDocTag)]
		XmlDocTag,
		[Category(Constants.SyntaxCategory.Comment)]
		[Description(Constants.XmlDocAttributeName)]
		XmlDocAttributeName,
		[Category(Constants.SyntaxCategory.Comment)]
		[Description(Constants.XmlDocAttributeValue)]
		XmlDocAttributeValue,
		[Category(Constants.SyntaxCategory.Comment)]
		[Description(Constants.XmlDocDelimiter)]
		XmlDocDelimiter,
		[Category(Constants.SyntaxCategory.CompilerMarked)]
		[Description(Constants.CodeExcluded)]
		ExcludedCode,
		//[Description(Constants.CodeBraceMatching)]
		//BraceMatching,
		[Category(Constants.SyntaxCategory.CompilerMarked)]
		[Description(Constants.CodeUnnecessary)]
		UnnecessaryCode,
	}
	enum CommentStyleApplication
	{
		Content,
		Tag,
		TagAndContent
	}
}
