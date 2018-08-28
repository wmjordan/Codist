using System;
using System.ComponentModel;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist
{
	/// <summary>
	/// Code style constants
	/// </summary>
	static partial class Constants
	{
		public const string NameOfMe = nameof(Codist);

		public static class CodeTypes
		{
			public const string Code = nameof(Code);
			public const string CSharp = nameof(CSharp);
			public const string Text = nameof(Text);
		}

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
			public const string Xml = "XML";
		}

		public static class EditorProperties
		{
			public const string TextViewBackground = "TextView Background";
			public const string Text = "Text";
			public const string Caret = "Caret";
			public const string OverwriteCaret = "Overwrite Caret";
			public const string SelectedText = "Selected Text";
			public const string InactiveSelectedText = "Inactive Selected Text";
			public const string VisibleWhitespace = "Visible Whitespace";
		}

		public const string CodeKeyword = "Keyword";
		public const string CodeComment = "Comment";

		public const string CodeAbstractionKeyword = "Keyword: Abstraction";
		public const string CodeBranchingKeyword = "Keyword: Branching";
		public const string CodeControlFlowKeyword = "Keyword: Control flow";
		public const string CodeLoopKeyword = "Keyword: Loop";
		public const string CodeSpecialPuctuation = "Special Puctuation";

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

		public const string CSharpLocalVariableName = "C#: Local field";
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
		public const string CSharpResourceKeyword = "C#: Resource keyword";
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
		public const string CSharpAttributeName = "C#: Attribute name";
		public const string CSharpAttributeNotation = "C#: Attribute notation";
		public const string CSharpLabel = "C#: Label";
		public const string CSharpDeclarationBrace = "C#: Declaration brace";
		public const string CSharpMethodBody = "C#: Method body";
		public const string CSharpXmlDoc = "C#: XML Doc";

		public const string CppFunction = "cppFunction";
		public const string CppClassTemplate = "cppClassTemplate";
		public const string CppFunctionTemplate = "cppFunctionTemplate";
		public const string CppEvent = "cppEvent";
		public const string CppGenericType = "cppGenericType";
		public const string CppGlobalVariable = "cppGlobalVariable";
		public const string CppLabel = "cppLabel";
		public const string CppLocalVariable = "cppLocalVariable";
		public const string CppMacro = "cppMacro"; // not mapped
		public const string CppMemberField = "cppMemberField";
		public const string CppMemberFunction = "cppMemberFunction";
		public const string CppMemberOperator = "cppMemberOperator";
		public const string CppNamespace = "cppNamespace";
		public const string CppNewDelete = "cppNewDelete"; // not mapped
		public const string CppParameter = "cppParameter";
		public const string CppOperator = "cppOperator";
		public const string CppProperty = "cppProperty";
		public const string CppRefType = "cppRefType"; // not mapped
		public const string CppStaticMemberField = "cppStaticMemberField"; // not mapped
		public const string CppStaticMemberFunction = "cppStaticMemberFunction"; // not mapped
		public const string CppType = "cppType";
		public const string CppUserDefinedLiteralNumber = "cppUserDefinedLiteralNumber"; // not mapped
		public const string CppUserDefinedLiteralRaw = "cppUserDefinedLiteralRaw"; // not mapped
		public const string CppUserDefinedLiteralString = "cppUserDefinedLiteralString"; // not mapped
		public const string CppValueType = "cppValueType";

		public const string XmlAttributeName = "XML Attribute";
		public const string XmlAttributeQuotes = "XML Attribute Quotes";
		public const string XmlAttributeValue = "XML Attribute Value";
		public const string XmlCData = "XML CData Section";
		public const string XmlComment = "XML Comment";
		public const string XmlDelimiter = "XML Delimiter";
		public const string XmlName = "XML Name";
		public const string XmlProcessingInstruction = "XML Processing Instruction";
		public const string XmlText = "XML Text";

		//public const string EditorIntellisense = "intellisense";
		//public const string EditorSigHelp = "sighelp";
		//public const string EditorSigHelpDoc = "sighelp-doc";

		internal const string CommentPrefix = "CdstComment: ";
		//! Important
		//# Notice
		public const string EmphasisComment = CommentPrefix + "Emphasis";
		//? Question
		public const string QuestionComment = CommentPrefix + "Question";
		//!? Exclaimation
		public const string ExclaimationComment = CommentPrefix + "Exclaimation";
		//x Removed
		public const string DeletionComment = CommentPrefix + "Deletion";

		//TODO: This does not need work
		public const string TodoComment = CommentPrefix + "Task - ToDo";
		//NOTE: Watch-out!
		public const string NoteComment = CommentPrefix + "Task - Note";
		//Hack: B-) We are in the Matrix now!!!
		public const string HackComment = CommentPrefix + "Task - Hack";
		//Undone: The revolution has not yet succeeded. Comrades still need to strive hard.
		public const string UndoneComment = CommentPrefix + "Task - Undone";

		//+++ heading 1
		public const string Heading1Comment = CommentPrefix + "Heading 1";
		//++ heading 2
		public const string Heading2Comment = CommentPrefix + "Heading 2";
		//+ heading 3
		public const string Heading3Comment = CommentPrefix + "Heading 3";
		//- heading 4
		public const string Heading4Comment = CommentPrefix + "Heading 4";
		//-- heading 5
		public const string Heading5Comment = CommentPrefix + "Heading 5";
		//--- heading 6
		public const string Heading6Comment = CommentPrefix + "Heading 6";

		public const string Task1Comment = CommentPrefix + "Task 1";
		public const string Task2Comment = CommentPrefix + "Task 2";
		public const string Task3Comment = CommentPrefix + "Task 3";
		public const string Task4Comment = CommentPrefix + "Task 4";
		public const string Task5Comment = CommentPrefix + "Task 5";
		public const string Task6Comment = CommentPrefix + "Task 6";
		public const string Task7Comment = CommentPrefix + "Task 7";
		public const string Task8Comment = CommentPrefix + "Task 8";
		public const string Task9Comment = CommentPrefix + "Task 9";

		public static readonly Color CommentColor = Colors.Green;
		public static readonly Color QuestionColor = Colors.MediumPurple;
		public static readonly Color ExclaimationColor = Colors.IndianRed;
		public static readonly Color DeletionColor = Colors.Gray;
		public static readonly Color ToDoColor = Colors.DarkBlue;
		public static readonly Color NoteColor = Colors.Orange;
		public static readonly Color HackColor = Colors.Black;
		public static readonly Color UndoneColor = Color.FromRgb(113, 65, 54);
		public static readonly Color TaskColor = Colors.Red;
		public static readonly Color ControlFlowColor = Colors.MediumBlue;
	}

	enum CommentStyleTypes
	{
		[ClassificationType(ClassificationTypeNames = Constants.CodeComment)]
		Default,
		[ClassificationType(ClassificationTypeNames = Constants.EmphasisComment)]
		Emphasis,
		[ClassificationType(ClassificationTypeNames = Constants.QuestionComment)]
		Question,
		[ClassificationType(ClassificationTypeNames = Constants.ExclaimationComment)]
		Exclaimation,
		[ClassificationType(ClassificationTypeNames = Constants.DeletionComment)]
		Deletion,
		[ClassificationType(ClassificationTypeNames = Constants.TodoComment)]
		ToDo,
		[ClassificationType(ClassificationTypeNames = Constants.NoteComment)]
		Note,
		[ClassificationType(ClassificationTypeNames = Constants.HackComment)]
		Hack,
		[ClassificationType(ClassificationTypeNames = Constants.UndoneComment)]
		Undone,
		[ClassificationType(ClassificationTypeNames = Constants.Heading1Comment)]
		Heading1,
		[ClassificationType(ClassificationTypeNames = Constants.Heading2Comment)]
		Heading2,
		[ClassificationType(ClassificationTypeNames = Constants.Heading3Comment)]
		Heading3,
		[ClassificationType(ClassificationTypeNames = Constants.Heading4Comment)]
		Heading4,
		[ClassificationType(ClassificationTypeNames = Constants.Heading5Comment)]
		Heading5,
		[ClassificationType(ClassificationTypeNames = Constants.Heading6Comment)]
		Heading6,
		[ClassificationType(ClassificationTypeNames = Constants.Task1Comment)]
		Task1,
		[ClassificationType(ClassificationTypeNames = Constants.Task2Comment)]
		Task2,
		[ClassificationType(ClassificationTypeNames = Constants.Task3Comment)]
		Task3,
		[ClassificationType(ClassificationTypeNames = Constants.Task4Comment)]
		Task4,
		[ClassificationType(ClassificationTypeNames = Constants.Task5Comment)]
		Task5,
		[ClassificationType(ClassificationTypeNames = Constants.Task6Comment)]
		Task6,
		[ClassificationType(ClassificationTypeNames = Constants.Task7Comment)]
		Task7,
		[ClassificationType(ClassificationTypeNames = Constants.Task8Comment)]
		Task8,
		[ClassificationType(ClassificationTypeNames = Constants.Task9Comment)]
		Task9,
	}

	enum CodeStyleTypes
	{
		None,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeKeyword)]
		Keyword,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpNamespaceName)]
		[ClassificationType(ClassificationTypeNames = Constants.CppNamespace)]
		NamespaceName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeClassName)]
		[ClassificationType(ClassificationTypeNames = Constants.CppType)]
		ClassName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeStructName)]
		[ClassificationType(ClassificationTypeNames = Constants.CppValueType)]
		StructName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeInterfaceName)]
		InterfaceName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeEnumName)]
		EnumName,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeIdentifier)]
		Identifier,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeNumber)]
		Number,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeString)]
		String,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeStringVerbatim)]
		StringVerbatim,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeOperator)]
		[ClassificationType(ClassificationTypeNames = Constants.CppMemberOperator)]
		[ClassificationType(ClassificationTypeNames = Constants.CppOperator)]
		Operator,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodePunctuation)]
		Punctuation,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeUrl)]
		Url,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLabel)]
		[ClassificationType(ClassificationTypeNames = Constants.CppLabel)]
		Label,
		[Category(Constants.SyntaxCategory.Preprocessor)]
		[ClassificationType(ClassificationTypeNames = Constants.CodePreprocessorText)]
		PreprocessorText,
		[Category(Constants.SyntaxCategory.Preprocessor)]
		[ClassificationType(ClassificationTypeNames = Constants.CodePreprocessorKeyword)]
		PreprocessorKeyword,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeComment)]
		Comment,
		[Category(Constants.SyntaxCategory.CompilerMarked)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeExcluded)]
		ExcludedCode,
		//[ClassificationType(ClassificationTypeNames = Constants.CodeBraceMatching)]
		//BraceMatching,
		[Category(Constants.SyntaxCategory.CompilerMarked)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeUnnecessary)]
		UnnecessaryCode,
	}
	enum CSharpStyleTypes
	{
		None,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeControlFlowKeyword)]
		BreakAndReturnKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeAbstractionKeyword)]
		AbstractionKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeBranchingKeyword)]
		BranchingKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeLoopKeyword)]
		LoopKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpResourceKeyword)]
		ResourceAndExceptionKeyword,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpDeclarationName)]
		TypeDeclaration,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpNestedDeclarationName)]
		NestedTypeDeclaration,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpStaticMemberName)]
		[ClassificationType(ClassificationTypeNames = Constants.CppGlobalVariable)]
		StaticMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpOverrideMemberName)]
		OverrideMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpAbstractMemberName)]
		AbstractMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpVirtualMemberName)]
		VirtualMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLocalVariableName)]
		[ClassificationType(ClassificationTypeNames = Constants.CppLocalVariable)]
		LocalVariableName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpAttributeName)]
		AttributeName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpAttributeNotation)]
		AttributeNotation,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpDeclarationBrace)]
		DeclarationBrace,

		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpSealedClassName)]
		SealedClassName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeDelegateName)]
		DelegateName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpEventName)]
		[ClassificationType(ClassificationTypeNames = Constants.CppEvent)]
		EventName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpTypeParameterName)]
		[ClassificationType(ClassificationTypeNames = Constants.CppClassTemplate)]
		[ClassificationType(ClassificationTypeNames = Constants.CppFunctionTemplate)]
		[ClassificationType(ClassificationTypeNames = Constants.CppGenericType)]
		TypeParameterName,

		//[ClassificationType(ClassificationTypeNames = Constants.CodeModuleName)]
		//ModuleDeclaration,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpConstructorMethodName)]
		ConstructorMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpFieldName)]
		[ClassificationType(ClassificationTypeNames = Constants.CppMemberField)]
		FieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpConstFieldName)]
		ConstFieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpReadOnlyFieldName)]
		ReadOnlyFieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpPropertyName)]
		[ClassificationType(ClassificationTypeNames = Constants.CppProperty)]
		PropertyName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpMethodName)]
		[ClassificationType(ClassificationTypeNames = Constants.CppFunction)]
		[ClassificationType(ClassificationTypeNames = Constants.CppMemberFunction)]
		MethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpExtensionMethodName)]
		ExtensionMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpExternMethodName)]
		ExternMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpParameterName)]
		[ClassificationType(ClassificationTypeNames = Constants.CppParameter)]
		ParameterName,

		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpXmlDoc)]
		XmlDoc,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocComment)]
		XmlDocComment,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocTag)]
		XmlDocTag,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocAttributeName)]
		XmlDocAttributeName,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocAttributeValue)]
		XmlDocAttributeValue,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocDelimiter)]
		XmlDocDelimiter,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocCData)]
		XmlDocCData,
	}

	enum XmlStyleTypes
	{
		None,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeName)]
		XmlAttributeName,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeQuotes)]
		XmlAttributeQuotes,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeValue)]
		XmlAttributeValue,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlCData)]
		XmlCData,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlComment)]
		XmlComment,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDelimiter)]
		XmlDelimiter,
		//[Category(Constants.SyntaxCategory.Xml)]
		//[ClassificationType(ClassificationTypeNames = Constants.XmlEntityReference)]
		//XmlEntityReference,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlName)]
		XmlName,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlProcessingInstruction)]
		XmlProcessingInstruction,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlText)]
		XmlText,
	}
	enum CommentStyleApplication
	{
		Content,
		Tag,
		TagAndContent
	}
	enum DebuggerStatus
	{
		Design,
		Running,
		Break,
		EditAndContinue
	}
}
