using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Codist
{
	#region Demo area
	/////////////////////////////////////////
	////COPYRIGHT
	//The quick brown fox jumps over the lazy dog

	/*? hallo for en kommentar!? */
	/*! A long comment - will it get bold!? 
     * Should this be bold as well?
     * Another line
	*/
	/*!? Normal comment - here '*/
	#endregion

	/// <summary>
	/// Comment style constants
	/// </summary>
	static class Constants
	{
		public const string CodeKeyword = "Keyword";
		public const string CodeComment = "Comment";

		public const string ReturnKeyword = "Keyword: Return";
		public const string CodeExitKeyword = "Keyword: Exit";

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

		public const string XmlDocAttributeName = "xml doc comment - attribute name";
		public const string XmlDocAttributeQuotes = "xml doc comment - attribute quotes";
		public const string XmlDocAttributeValue = "xml doc comment - attribute value";
		public const string XmlDocComment = "xml doc comment - text";
		public const string XmlDocCData = "xml doc comment - cdata section";
		public const string XmlDocDelimiter = "xml doc comment - delimiter";
		public const string XmlDocEntity = "xml doc comment - entity reference";
		public const string XmlDocTag = "xml doc comment - name";

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
		//Hack: B-)
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
		public static readonly Color ExitColor = Colors.MediumBlue;
		public static readonly Color ReturnColor = Colors.Blue;
	}

	enum CommentStyles
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

	enum CodeStyles
	{
		None,
		[Description(Constants.CodeClassName)]
		ClassDeclaration,
		[Description(Constants.CodeStructName)]
		StructDeclaration,
		[Description(Constants.CodeInterfaceName)]
		InterfaceDeclaration,
		[Description(Constants.CodeEnumName)]
		EnumDeclaration,
		[Description(Constants.CodeDelegateName)]
		DelegateDeclaration,
		[Description(Constants.CodeTypeParameterName)]
		TypeParameter,
		[Description(Constants.CodeModuleName)]
		ModuleDeclaration,
		[Description(Constants.CodeKeyword)]
		Keyword,
		[Description(Constants.CodeExitKeyword)]
		ExitKeyword,
		[Description(Constants.CodeExcluded)]
		ExcludedCode,
		[Description(Constants.CodeUnnecessary)]
		UnnecessaryCode,
		[Description(Constants.CodePreprocessorText)]
		PreprocessorText,
		[Description(Constants.CodePreprocessorKeyword)]
		PreprocessorKeyword,
		[Description(Constants.CodeSymbolDefinition)]
		SymbolDefinition,
		[Description(Constants.CodeSymbolReference)]
		SymbolReference,
		[Description(Constants.CodeIdentifier)]
		Identifier,
		[Description(Constants.CodeNumber)]
		Number,
		[Description(Constants.CodeString)]
		String,
		[Description(Constants.CodeStringVerbatim)]
		StringVerbatim,
		[Description(Constants.CodeOperator)]
		Operator,
		[Description(Constants.CodePunctuation)]
		Punctuation,
		[Description(Constants.CodeUrl)]
		Url,
		[Description(Constants.CodeComment)]
		Comment,
		[Description(Constants.XmlDocComment)]
		XmlDocComment,
		[Description(Constants.XmlDocTag)]
		XmlDocTag,
		[Description(Constants.XmlDocAttributeName)]
		XmlDocAttributeName,
		[Description(Constants.XmlDocAttributeValue)]
		XmlDocAttributeValue,
		[Description(Constants.XmlDocDelimiter)]
		XmlDocDelimiter,
	}
	enum CommentStyleApplication
	{
		Content,
		Tag,
		TagAndContent
	}
}
