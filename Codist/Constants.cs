using System;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.StandardClassification;

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
			public const string PlainText = "plaintext";
			public const string Text = "Text";
			/// <summary>
			/// From VS 17.5 on, 'vs-markdown' is used instead of 'Markdown'
			/// </summary>
			public const string Markdown = "Markdown";
			public const string VsMarkdown = "vs-markdown";
			public const string Xml = "XML";
			public const string Xaml = "XAML";
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
			public const string Xaml = "XAML";
			public const string Xml = "XML";
			public const string Highlight = nameof(Highlight);
			public const string Heading = nameof(Heading);
			public const string Block = nameof(Block);
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

		public const string CodeKeyword = PredefinedClassificationTypeNames.Keyword;
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
		public const string CodeNumber = PredefinedClassificationTypeNames.Number;
		public const string CodeOperator = PredefinedClassificationTypeNames.Operator;
		public const string CodePlainText = "Plain Text";
		public const string CodePunctuation = "punctuation";
		public const string CodeBraceMatching = "brace matching";
		public const string CodeInlineRenameField = "inline rename field";
		public const string CodeString = PredefinedClassificationTypeNames.String;
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
		public const string CSharpLocalFunctionDeclarationName = "C#: Local function declaration";
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
		public const string CSharpNestedTypeName = "C#: Nested type member";
		public const string CSharpVariableCapturedExpression = "C#: Variable captured expression";
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

		public const string XamlAttributeName = "XAML Attribute";
		public const string XamlAttributeQuotes = "XAML Attribute Quotes";
		public const string XamlAttributeValue = "XAML Attribute Value";
		public const string XamlCData = "XAML CData Section";
		public const string XamlComment = "XAML Comment";
		public const string XamlDelimiter = "XAML Delimiter";
		public const string XamlName = "XAML Name";
		public const string XamlProcessingInstruction = "XAML Processing Instruction";
		public const string XamlText = "XAML Text";
		public const string XamlMarkupExtensionClass = "XAML Markup Extension Class";
		public const string XamlMarkupExtensionParameterName = "XAML Markup Extension Parameter Name";
		public const string XamlMarkupExtensionParameterValue = "XAML Markup Extension Parameter Value";
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
		public const string MarkdownQuotation = "Markdown: Quotation";
		public const string MarkdownOrderedList = "Markdown: Ordered List";
		public const string MarkdownUnorderedList = "Markdown: Unordered List";
		public const string MarkdownCodeBlock = "Markdown: Indented Code Block";
		public const string MarkdownFencedCodeBlock = "Markdown: Fenced Code Block";
		public const string MarkdownHtmlCodeBlock = "Markdown: HTML Code Block";
		public const string MarkdownThematicBreak = "Markdown: Thematic Break (Horizontal Line)";
		public const string MarkdownUnderline = "Markdown: Underline";
		public const string MarkdownVsBold = "vsMarkdown_bold";
		public const string MarkdownVsItalic = "vsMarkdown_italic";
		public const string MarkdownVsStrikethrough = "vsMarkdown_strikethrough";
		public const string MarkdownVsSubscript = "vsMarkdown_subscript";
		public const string MarkdownVsSuperscript = "vsMarkdown_superscript";
		public const string MarkdownVsUrl = "vsMarkdown_url";

		internal const string CodistPrefix = "Codist: ";
		public const string CodistComment = CodistPrefix + "Comment";
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
