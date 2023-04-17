using System;
using System.ComponentModel;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist
{
	/// <summary>
	/// Code style constants
	/// </summary>
	static class Constants
	{
		public const string NameOfMe = nameof(Codist);

		public static class CodeTypes
		{
			public const string CPlusPlus = "C/C++";
			public const string Code = "Code";
			public const string CSharp = "CSharp";
			public const string HtmlxProjection = "HTMLXProjection";
			public const string Text = "Text";
			/// <summary>
			/// From VS 17.5 on, 'vs-markdown' is used instead of 'Markdown'
			/// </summary>
			public const string Markdown = "Markdown";
			public const string VsMarkdown = "vs-markdown";
			public const string Xml = "XML";
			public const string Projection = "projection";
			public const string FindResults = "FindResults";
			public const string Output = "Output";
			public const string InteractiveContent = "Interactive Content";
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
			public const string Markup = nameof(Markup);
			public const string Member = nameof(Member);
			public const string Xml = "XML";
			public const string Highlight = nameof(Highlight);
			public const string Heading = nameof(Heading);
			public const string Task = nameof(Task);
			public const string GeneralTask = "General Task";
			public const string Source = nameof(Source);
			public const string Style = nameof(Style);
		}

		public static class EditorProperties
		{
			public const string TextViewBackground = "TextView Background";
			public const string Text = "Text";
			public const string PlainText = "Plain Text";
			public const string Caret = "Caret";
			public const string OverwriteCaret = "Overwrite Caret";
			public const string SelectedText = "Selected Text";
			public const string InactiveSelectedText = "Inactive Selected Text";
			public const string VisibleWhitespace = "Visible Whitespace";
		}

		public const string CodeKeyword = "Keyword";// it is weird that there are two keyword with different cases in the resource dictionary: a Keyword and a PredefinedClassificationTypeNames.Keyword;
		public const string CodeComment = PredefinedClassificationTypeNames.Comment;
		public const string CodeText = "text";

		public const string CSharpAbstractionKeyword = "C#: Abstraction keyword";
		public const string CSharpBranchingKeyword = "C#: Branching keyword";
		public const string CSharpControlFlowKeyword = "C#: Control flow keyword";
		public const string CSharpLoopKeyword = "C#: Loop keyword";
		public const string CSharpTypeCastKeyword = "C#: Type cast keyword";
		public const string CodeBold = CodistPrefix + "Bold";

		public const string CodeClassName = "class name";
		public const string CodeRecordClassName = "record class name";
		public const string CodeStructName = "struct name";
		public const string CodeRecordStructName = "record struct name";
		public const string CodeEnumName = "enum name";
		public const string CodeInterfaceName = "interface name";
		public const string CodeDelegateName = "delegate name";
		public const string CodeModuleName = "module name";
		public const string CodeTypeParameterName = "type parameter name";
		public const string CodePreprocessorText = "preprocessor text";
		public const string CodePreprocessorKeyword = PredefinedClassificationTypeNames.PreprocessorKeyword;
		public const string CodeExcluded = PredefinedClassificationTypeNames.ExcludedCode;
		public const string CodeUnnecessary = "unnecessary code";
		public const string CodeIdentifier = PredefinedClassificationTypeNames.Identifier;
		public const string CodeLiteral = PredefinedClassificationTypeNames.Literal;
		public const string CodeNumber = "Number"; // it is weird that there are two keyword with different cases in the resource dictionary: a Number and PredefinedClassificationTypeNames.Number;
		public const string CodeOperator = PredefinedClassificationTypeNames.Operator;
		public const string CodePlainText = "Plain Text";
		public const string CodePunctuation = "punctuation";
		public const string CodeBraceMatching = "brace matching";
		public const string CodeInlineRenameField = "inline rename field";
		public const string CodeString = "String";// it is weird that there are two keyword with different cases in the resource dictionary: a String and a PredefinedClassificationTypeNames.String;
		public const string CodeStringVerbatim = "string - verbatim";
		public const string CodeSymbolDefinition = PredefinedClassificationTypeNames.SymbolDefinition;
		public const string CodeSymbolReference = PredefinedClassificationTypeNames.SymbolReference;
		public const string CodeUrl = "url";
		public const string CodeFormalLanguage = PredefinedClassificationTypeNames.FormalLanguage;
		public const string CodeOverloadedOperator = "operator - overloaded";
		public const string CodeStringEscapeCharacter = "string - escape character";
		public const string CodeKeywordControl = "keyword - control";
		public const string CodeConstantName = "constant name";
		public const string CodeEnumMemberName = "enum member name";
		public const string CodeExtensionMethodName = "extension method name";
		public const string CodeEventName = "event name";
		public const string CodeFieldName = "field name";
		public const string CodePropertyName = "property name";
		public const string CodeNamespaceName = "namespace name";
		public const string CodeLocalName = "local name";
		public const string CodeMethodName = "method name";
		public const string CodeParameterName = "parameter name";
		public const string CodeReassignedVariable = "reassigned variable";
		public const string CodeStaticSymbol = "static symbol";
		public const string CodeLabelName = "label name";
		public const string CodeNavigableSymbol = "navigableSymbol";

		public const string XmlDocAttributeName = "xml doc comment - attribute name";
		public const string XmlDocAttributeQuotes = "xml doc comment - attribute quotes";
		public const string XmlDocAttributeValue = "xml doc comment - attribute value";
		public const string XmlDocComment = "xml doc comment - text";
		public const string XmlDocCData = "xml doc comment - cdata section";
		public const string XmlDocDelimiter = "xml doc comment - delimiter";
		public const string XmlDocEntity = "xml doc comment - entity reference";
		public const string XmlDocTag = "xml doc comment - name";

		public const string MarkupAttribute = PredefinedClassificationTypeNames.MarkupAttribute;
		public const string MarkupAttributeValue = PredefinedClassificationTypeNames.MarkupAttributeValue;
		public const string MarkupNode = PredefinedClassificationTypeNames.MarkupNode;

		public const string CSharpLocalVariableName = "C#: Local variable";
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
		public const string CSharpReadOnlyStructName = "C#: Read-only struct";
		public const string CSharpRefStructName = "C#: Ref struct";
		public const string CSharpEnumFieldName = "C#: Enum field";
		public const string CSharpResourceKeyword = "C#: Resource keyword";
		public const string CSharpAliasNamespaceName = "C#: Alias namespace";
		public const string CSharpConstructorMethodName = "C#: Constructor method";
		public const string CSharpDeclarationName = "C#: Declaration";
		public const string CSharpMemberDeclarationName = "C#: Member declaration";
		public const string CSharpLocalDeclarationName = "C#: Local declaration";
		public const string CSharpTypeParameterName = "C#: Type parameter";
		public const string CSharpStaticMemberName = "C#: Static member";
		public const string CSharpOverrideMemberName = "C#: Override member";
		public const string CSharpVirtualMemberName = "C#: Virtual member";
		public const string CSharpVolatileFieldName = "C#: Volatile field";
		public const string CSharpAbstractMemberName = "C#: Abstract member";
		public const string CSharpSealedMemberName = "C#: Sealed class or member";
		public const string CSharpPrivateMemberName = "C#: Private member";
		public const string CSharpAttributeName = "C#: Attribute name";
		public const string CSharpAttributeNotation = "C#: Attribute notation";
		public const string CSharpLabel = "C#: Label";
		public const string CSharpDeclarationBrace = "C#: Declaration brace";
		public const string CSharpMethodBody = "C#: Method body";
		public const string CSharpXmlDoc = "C#: XML Doc";
		public const string CSharpUserSymbol = "C#: User symbol";
		public const string CSharpMetadataSymbol = "C#: Metadata symbol";

		public const string CppFunction = "cppFunction";
		public const string CppClassTemplate = "cppClassTemplate";
		public const string CppControlKeyword = "cppControlKeyword";
		public const string CppEnumerator = "cppEnumerator";
		public const string CppFunctionTemplate = "cppFunctionTemplate";
		public const string CppEvent = "cppEvent";
		public const string CppGenericType = "cppGenericType";
		public const string CppGlobalVariable = "cppGlobalVariable";
		public const string CppInactiveCodeClassification = "cppInactiveCodeClassification";
		public const string CppInlineHint = "cppInlineHint";
		public const string CppLabel = "cppLabel";
		public const string CppLocalVariable = "cppLocalVariable";
		public const string CppMacro = "cppMacro";
		public const string CppMemberField = "cppMemberField";
		public const string CppMemberFunction = "cppMemberFunction";
		public const string CppMemberOperator = "cppMemberOperator";
		public const string CppNamespace = "cppNamespace";
		public const string CppNewDelete = "cppNewDelete";
		public const string CppParameter = "cppParameter";
		public const string CppOperator = "cppOperator";
		public const string CppProperty = "cppProperty";
		public const string CppRefType = "cppRefType"; // not mapped
		public const string CppSolidCodeClassification = "cppSolidCodeClassification";
		public const string CppStaticMemberField = "cppStaticMemberField"; // not mapped
		public const string CppStaticMemberFunction = "cppStaticMemberFunction"; // not mapped
		public const string CppStringDelimiterCharacter = "cppStringDelimiterCharacter";
		public const string CppStringEscapeCharacter = "cppStringEscapeCharacter";
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
		public const string XsltKeyword = "XSLT Keyword";

		public const string MarkdownHeading1 = "Markdown: Heading 1";
		public const string MarkdownHeading2 = "Markdown: Heading 2";
		public const string MarkdownHeading3 = "Markdown: Heading 3";
		public const string MarkdownHeading4 = "Markdown: Heading 4";
		public const string MarkdownHeading5 = "Markdown: Heading 5";
		public const string MarkdownHeading6 = "Markdown: Heading 6";
		public const string MarkdownVsBold = "vsMarkdown_bold";
		public const string MarkdownVsItalic = "vsMarkdown_italic";
		public const string MarkdownVsStrikethrough = "vsMarkdown_strikethrough";
		public const string MarkdownVsSubscript = "vsMarkdown_subscript";
		public const string MarkdownVsSuperscript = "vsMarkdown_superscript";
		public const string MarkdownVsUrl = "vsMarkdown_url";

		internal const string CodistPrefix = "Codist: ";
		//! Important
		//# Notice
		public const string EmphasisComment = CodistPrefix + "Emphasis";
		//? Question
		public const string QuestionComment = CodistPrefix + "Question";
		//!? Exclamation
		public const string ExclamationComment = CodistPrefix + "Exclamation";
		//x Removed
		public const string DeletionComment = CodistPrefix + "Deletion";

		//TODO: This does not need work
		public const string TodoComment = CodistPrefix + "Task - ToDo";
		//NOTE: Watch-out!
		public const string NoteComment = CodistPrefix + "Task - Note";
		//Hack: B-) We are in the Matrix now!!!
		public const string HackComment = CodistPrefix + "Task - Hack";
		//Undone: The revolution has not yet succeeded. Comrades still need to strive hard.
		public const string UndoneComment = CodistPrefix + "Task - Undone";

		//+++ heading 1
		public const string Heading1Comment = CodistPrefix + "Heading 1";
		//++ heading 2
		public const string Heading2Comment = CodistPrefix + "Heading 2";
		//+ heading 3
		public const string Heading3Comment = CodistPrefix + "Heading 3";
		//- heading 4
		public const string Heading4Comment = CodistPrefix + "Heading 4";
		//-- heading 5
		public const string Heading5Comment = CodistPrefix + "Heading 5";
		//--- heading 6
		public const string Heading6Comment = CodistPrefix + "Heading 6";

		public const string Task1Comment = CodistPrefix + "Task 1";
		public const string Task2Comment = CodistPrefix + "Task 2";
		public const string Task3Comment = CodistPrefix + "Task 3";
		public const string Task4Comment = CodistPrefix + "Task 4";
		public const string Task5Comment = CodistPrefix + "Task 5";
		public const string Task6Comment = CodistPrefix + "Task 6";
		public const string Task7Comment = CodistPrefix + "Task 7";
		public const string Task8Comment = CodistPrefix + "Task 8";
		public const string Task9Comment = CodistPrefix + "Task 9";

		public const string Highlight1 = CodistPrefix + "Highlight 1";
		public const string Highlight2 = CodistPrefix + "Highlight 2";
		public const string Highlight3 = CodistPrefix + "Highlight 3";
		public const string Highlight4 = CodistPrefix + "Highlight 4";
		public const string Highlight5 = CodistPrefix + "Highlight 5";
		public const string Highlight6 = CodistPrefix + "Highlight 6";
		public const string Highlight7 = CodistPrefix + "Highlight 7";
		public const string Highlight8 = CodistPrefix + "Highlight 8";
		public const string Highlight9 = CodistPrefix + "Highlight 9";

		public static readonly Color CommentColor = Colors.Green;
		public static readonly Color QuestionColor = Colors.MediumPurple;
		public static readonly Color ExclamationColor = Colors.IndianRed;
		public static readonly Color DeletionColor = Colors.Gray;
		public static readonly Color ToDoColor = Colors.DarkBlue;
		public static readonly Color NoteColor = Colors.Orange;
		public static readonly Color HackColor = Colors.Black;
		public static readonly Color UndoneColor = Color.FromRgb(113, 65, 54);
		public static readonly Color TaskColor = Colors.Red;
		public static readonly Color ControlFlowColor = Colors.MediumBlue;

		public const string EmptyColor = "#00000000";
	}

	public static class Suppression
	{
		public const string VSTHRD010 = "VSTHRD010:Invoke single-threaded types on Main thread";
		public const string CheckedInCaller = "Checked in caller";
		public const string VSTHRD100 = "VSTHRD100:Avoid async void methods";
		public const string EventHandler = "Event handler";
	}

	enum CommentStyleTypes
	{
		[ClassificationType(ClassificationTypeNames = Constants.CodeComment)]
		Default,
		[Category(Constants.SyntaxCategory.Task)]
		[ClassificationType(ClassificationTypeNames = Constants.EmphasisComment)]
		Emphasis,
		[Category(Constants.SyntaxCategory.Task)]
		[ClassificationType(ClassificationTypeNames = Constants.QuestionComment)]
		Question,
		[Category(Constants.SyntaxCategory.Task)]
		[ClassificationType(ClassificationTypeNames = Constants.ExclamationComment)]
		Exclamation,
		[Category(Constants.SyntaxCategory.Task)]
		[ClassificationType(ClassificationTypeNames = Constants.DeletionComment)]
		Deletion,
		[Category(Constants.SyntaxCategory.Task)]
		[ClassificationType(ClassificationTypeNames = Constants.TodoComment)]
		ToDo,
		[Category(Constants.SyntaxCategory.Task)]
		[ClassificationType(ClassificationTypeNames = Constants.NoteComment)]
		Note,
		[Category(Constants.SyntaxCategory.Task)]
		[ClassificationType(ClassificationTypeNames = Constants.HackComment)]
		Hack,
		[Category(Constants.SyntaxCategory.Task)]
		[ClassificationType(ClassificationTypeNames = Constants.UndoneComment)]
		Undone,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading1Comment)]
		Heading1,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading2Comment)]
		Heading2,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading3Comment)]
		Heading3,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading4Comment)]
		Heading4,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading5Comment)]
		Heading5,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading6Comment)]
		Heading6,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[ClassificationType(ClassificationTypeNames = Constants.Task1Comment)]
		Task1,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[ClassificationType(ClassificationTypeNames = Constants.Task2Comment)]
		Task2,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[ClassificationType(ClassificationTypeNames = Constants.Task3Comment)]
		Task3,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[ClassificationType(ClassificationTypeNames = Constants.Task4Comment)]
		Task4,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[ClassificationType(ClassificationTypeNames = Constants.Task5Comment)]
		Task5,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[ClassificationType(ClassificationTypeNames = Constants.Task6Comment)]
		Task6,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[ClassificationType(ClassificationTypeNames = Constants.Task7Comment)]
		Task7,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[ClassificationType(ClassificationTypeNames = Constants.Task8Comment)]
		Task8,
		[Category(Constants.SyntaxCategory.GeneralTask)]
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
		[ClassificationType(ClassificationTypeNames = Constants.CodeNamespaceName)]
		NamespaceName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeModuleName)]
		ModuleName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeClassName)]
		ClassName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeRecordClassName)]
		RecordClassName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeStructName)]
		StructName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeRecordStructName)]
		RecordStructName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeInterfaceName)]
		InterfaceName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeEnumName)]
		EnumName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeEnumMemberName)]
		EnumMemberName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeDelegateName)]
		DelegateName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeEventName)]
		EventName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodePropertyName)]
		PropertyName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeFieldName)]
		FieldName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeMethodName)]
		MethodName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeParameterName)]
		ParameterName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeTypeParameterName)]
		TypeParameterName,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeFormalLanguage)]
		FormalLanguage,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeIdentifier)]
		[Description("A base style shared by type, type member, local, parameter, etc.")]
		Identifier,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeNumber)]
		Number,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeString)]
		[Description("Literal string")]
		String,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeStringVerbatim)]
		[Description("Multiline literal string")]
		StringVerbatim,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeStringEscapeCharacter)]
		[Description("String escape character")]
		StringEscapeCharacter,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeOperator)]
		Operator,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeLocalName)]
		Local,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeReassignedVariable)]
		ReassignedVariable,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodePunctuation)]
		Punctuation,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeUrl)]
		Url,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeLabelName)]
		LabelName,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeStaticSymbol)]
		StaticSymbol,
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
		[Category(Constants.SyntaxCategory.CompilerMarked)]
		[ClassificationType(ClassificationTypeNames = Constants.CodeUnnecessary)]
		UnnecessaryCode,
	}
	enum CppStyleTypes
	{
		None,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CppMacro)]
		Macro,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CppNewDelete)]
		NewDelete,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CppControlKeyword)]
		ControlKeyword,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CppOperator)]
		Operator,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CppStringEscapeCharacter)]
		StringEscapeCharacter,
		[Category(Constants.SyntaxCategory.General)]
		[ClassificationType(ClassificationTypeNames = Constants.CppStringDelimiterCharacter)]
		StringDelimiterCharacter,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CppNamespace)]
		Namespace,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CppClassTemplate)]
		ClassTemplate,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CppFunctionTemplate)]
		FunctionTemplate,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CppGenericType)]
		GenericType,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CppLocalVariable)]
		LocalVariable,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CppLabel)]
		Label,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CppUserDefinedLiteralNumber)]
		UserDefinedLiteralNumber,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CppUserDefinedLiteralRaw)]
		UserDefinedLiteralRaw,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CppUserDefinedLiteralString)]
		UserDefinedLiteralString,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CppType)]
		Type,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CppValueType)]
		ValueType,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CppRefType)]
		RefType,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CppFunction)]
		Function,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CppEvent)]
		Event,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CppParameter)]
		Parameter,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CppProperty)]
		Property,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CppGlobalVariable)]
		GlobalVariable,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CppMemberField)]
		MemberField,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CppMemberFunction)]
		MemberFunction,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CppMemberOperator)]
		MemberOperator,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CppStaticMemberField)]
		StaticMemberField,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CppStaticMemberFunction)]
		StaticMemberFunction,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CppEnumerator)]
		Enumerator,
		[Category(Constants.SyntaxCategory.CompilerMarked)]
		[ClassificationType(ClassificationTypeNames = Constants.CppInactiveCodeClassification)]
		InactiveCodeClassification,
		[Category(Constants.SyntaxCategory.CompilerMarked)]
		[ClassificationType(ClassificationTypeNames = Constants.CppSolidCodeClassification)]
		SolidCodeClassification,
		[Category(Constants.SyntaxCategory.CompilerMarked)]
		[ClassificationType(ClassificationTypeNames = Constants.CppInlineHint)]
		InlineHint,
	}
	enum CSharpStyleTypes
	{
		None,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpControlFlowKeyword)]
		[BaseDefinition(Constants.CodeKeyword)]
		[Description("Keyword: break, continue, yield, return, throw, inheriting from Keyword")]
		BreakAndReturnKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpAbstractionKeyword)]
		[BaseDefinition(Constants.CodeKeyword)]
		[Description("Keyword: abstract, override, sealed, virtual, inheriting from Keyword")]
		AbstractionKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpBranchingKeyword)]
		[BaseDefinition(Constants.CodeKeyword)]
		[Description("Keyword: switch, case, default, if, else, inheriting from Keyword")]
		BranchingKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLoopKeyword)]
		[BaseDefinition(Constants.CodeKeyword)]
		[Description("Keyword: for, foreach in, do, while, inheriting from Keyword")]
		LoopKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpTypeCastKeyword)]
		[BaseDefinition(Constants.CodeKeyword)]
		[Description("Keyword: as, is, in, ref, out, inheriting from Keyword")]
		TypeCastKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpResourceKeyword)]
		[BaseDefinition(Constants.CodeKeyword)]
		[Description("Keyword: using, lock, try catch finally, fixed, unsafe, stackalloc, inheriting from Keyword")]
		ResourceAndExceptionKeyword,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpDeclarationName)]
		[Description("Declaration of non-nested type: class, struct, interface, enum, delegate and event, inheriting from Identifier")]
		Declaration,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpMemberDeclarationName)]
		[Description("Declaration of type member: property, method, event, delegate, nested type, etc. (excluding fields), inheriting from Declaration")]
		MemberDeclaration,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpDeclarationBrace)]
		[BaseDefinition(Constants.CodePunctuation)]
		[Description("Braces {} for declaration, inheriting from Punctuation")]
		DeclarationBrace,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpStaticMemberName)]
		[BaseDefinition(Constants.CodeStaticSymbol)]
		[Description("Name of static member, inheriting from static symbol")]
		StaticMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpOverrideMemberName)]
		[Description("Name of overriding member, inheriting from Identifier")]
		OverrideMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpAbstractMemberName)]
		[Description("Name of abstract member, inheriting from Identifier")]
		AbstractMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpVirtualMemberName)]
		[Description("Name of virtual member, inheriting from Identifier")]
		VirtualMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpPrivateMemberName)]
		[Description("Name of private member, inheriting from Identifier")]
		PrivateMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLocalDeclarationName)]
		[Description("Declaration of local variable")]
		LocalDeclaration,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLocalVariableName)]
		[Description("Name of local variable, inheriting from Identifier")]
		LocalVariableName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLabel)]
		[BaseDefinition(Constants.CodeLabelName)]
		[Description("Name of label, inheriting from Identifier")]
		Label,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpAttributeName)]
		[Description("Name of attribute annotation, inheriting from Class Name")]
		[BaseDefinition(Constants.CodeClassName)]
		[BaseDefinition(Constants.CSharpAttributeNotation)]
		AttributeName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpAttributeNotation)]
		[Description("Whole region of attribute annotation")]
		AttributeNotation,

		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpNamespaceName)]
		[BaseDefinition(Constants.CodeNamespaceName)]
		NamespaceName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpSealedMemberName)]
		[Description("Name of sealed class or sealed member")]
		SealedClassName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpReadOnlyStructName)]
		[BaseDefinition(Constants.CodeStructName)]
		[Description("Name of readonly struct")]
		ReadOnlyStructName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpEventName)]
		[BaseDefinition(Constants.CodeEventName)]
		[Description("Name of event, inheriting from Identifier")]
		EventName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpTypeParameterName)]
		[BaseDefinition(Constants.CodeTypeParameterName)]
		[Description("Name of type parameter, inheriting from Identifier")]
		TypeParameterName,

		//[ClassificationType(ClassificationTypeNames = Constants.CodeModuleName)]
		//ModuleDeclaration,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpConstructorMethodName)]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Description("Name of constructor, inheriting from Method Name")]
		ConstructorMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpFieldName)]
		[BaseDefinition(Constants.CodeFieldName)]
		[Description("Name of field, inheriting from Identifier")]
		FieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpConstFieldName)]
		[BaseDefinition(Constants.CSharpStaticMemberName)]
		[BaseDefinition(Constants.CSharpReadOnlyFieldName)]
		[BaseDefinition(Constants.CodeConstantName)]
		[Description("Name of constant field, inheriting from Read Only Field Name")]
		ConstFieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpReadOnlyFieldName)]
		[BaseDefinition(Constants.CSharpFieldName)]
		[Description("Name of read-only field, inheriting from Field Name")]
		ReadOnlyFieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpVolatileFieldName)]
		[BaseDefinition(Constants.CSharpFieldName)]
		[Description("Name of volatile field, inheriting from Field Name")]
		VolatileFieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpEnumFieldName)]
		[BaseDefinition(Constants.CodeEnumMemberName)]
		[BaseDefinition(Constants.CSharpConstFieldName)]
		[Description("Name of enum field, inheriting from Const Field Name")]
		EnumFieldName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpPropertyName)]
		[BaseDefinition(Constants.CodePropertyName)]
		[Description("Name of property, inheriting from Identifier")]
		PropertyName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpMethodName)]
		[BaseDefinition(Constants.CodeMethodName)]
		[Description("Name of method, inheriting from Identifier")]
		MethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpExtensionMethodName)]
		[BaseDefinition(Constants.CSharpMethodName)]
		[BaseDefinition(Constants.CSharpStaticMemberName)]
		[BaseDefinition(Constants.CodeExtensionMethodName)]
		[Description("Name of extension method, inheriting from Method Name and Static Member Name")]
		ExtensionMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpExternMethodName)]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Description("Name of extern method, inheriting from Method Name")]
		ExternMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpParameterName)]
		[BaseDefinition(Constants.CodeParameterName)]
		[Description("Name of parameter, inheriting from Identifier")]
		ParameterName,

		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpXmlDoc)]
		[Description("Whole region of XML Documentation")]
		XmlDoc,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocComment)]
		[Description("Comment text of XML Documentation")]
		XmlDocComment,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocTag)]
		[Description("Tag of XML Documentation")]
		XmlDocTag,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocAttributeName)]
		[Description("Attribute name of XML Documentation")]
		XmlDocAttributeName,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocAttributeValue)]
		[Description("Attribute value of XML Documentation")]
		XmlDocAttributeValue,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocDelimiter)]
		[Description("Tag characters of XML Documentation")]
		XmlDocDelimiter,
		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDocCData)]
		[Description("CData content of XML Documentation")]
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
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XsltKeyword)]
		XsltKeyword,
		[Category(Constants.SyntaxCategory.Markup)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkupAttribute)]
		MarkupAttribute,
		[Category(Constants.SyntaxCategory.Markup)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkupAttributeValue)]
		MarkupAttributeValue,
		[Category(Constants.SyntaxCategory.Markup)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkupNode)]
		MarkupNode,
	}

	enum SymbolMarkerStyleTypes
	{
		None,
		[Category(Constants.SyntaxCategory.Highlight)]
		[ClassificationType(ClassificationTypeNames = Constants.Highlight1)]
		Highlight1,
		[Category(Constants.SyntaxCategory.Highlight)]
		[ClassificationType(ClassificationTypeNames = Constants.Highlight2)]
		Highlight2,
		[Category(Constants.SyntaxCategory.Highlight)]
		[ClassificationType(ClassificationTypeNames = Constants.Highlight3)]
		Highlight3,
		[Category(Constants.SyntaxCategory.Highlight)]
		[ClassificationType(ClassificationTypeNames = Constants.Highlight4)]
		Highlight4,
		[Category(Constants.SyntaxCategory.Highlight)]
		[ClassificationType(ClassificationTypeNames = Constants.Highlight5)]
		Highlight5,
		[Category(Constants.SyntaxCategory.Highlight)]
		[ClassificationType(ClassificationTypeNames = Constants.Highlight6)]
		Highlight6,
		[Category(Constants.SyntaxCategory.Highlight)]
		[ClassificationType(ClassificationTypeNames = Constants.Highlight7)]
		Highlight7,
		[Category(Constants.SyntaxCategory.Highlight)]
		[ClassificationType(ClassificationTypeNames = Constants.Highlight8)]
		Highlight8,
		[Category(Constants.SyntaxCategory.Highlight)]
		[ClassificationType(ClassificationTypeNames = Constants.Highlight9)]
		Highlight9,

		[Category(Constants.SyntaxCategory.Source)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpUserSymbol)]
		[Description("Type and member defined in source code")]
		MyTypeAndMember,
		[Category(Constants.SyntaxCategory.Source)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpMetadataSymbol)]
		[Description("Type and member imported via referencing assembly")]
		ReferencedTypeAndMember,
	}

	enum MarkdownStyleTypes
	{
		None,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading1)]
		Heading1,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading2)]
		Heading2,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading3)]
		Heading3,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading4)]
		Heading4,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading5)]
		Heading5,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading6)]
		Heading6,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsBold)]
		Bold,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsItalic)]
		Italic,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsStrikethrough)]
		Strikethrough,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsSuperscript)]
		Superscript,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsSubscript)]
		Subscript,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsUrl)]
		Url,
	}
	enum MarkerStyleTypes
	{
		None,
		SymbolReference,
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
