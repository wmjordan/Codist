using System.ComponentModel;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

// note:
// enums defined in this file are used in
// 1. grouped syntax styles used in SyntaxHighlightCustomizationWindow
// 2. extra `IClassificationType`s that used by syntax highlighter, exported via `ServiceHelper`
// 3. style configuration names in old versions of Codist, prior to version 5
//
// enum fields can have the following attributes
// 1. Category: group styles in SyntaxHighlightCustomizationWindow
// 2. ClassificationType.ClassificationTypeNames: name the exported IClassificationType
// 3. BaseDefinition: define base classification types
// 4. Inheritance: denote the field is built-in, no need to export
// 5. Description: information about the field
// 6. Order: specify ordering of IClassificationType
// 7. Style: specify default styles for denoted IClassificationType
namespace Codist.SyntaxHighlight
{
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
		//[Category(Constants.SyntaxCategory.General)]
		//[ClassificationType(ClassificationTypeNames = Constants.CodeFormalLanguage)]
		//FormalLanguage,
		//[Category(Constants.SyntaxCategory.General)]
		//[ClassificationType(ClassificationTypeNames = Constants.CodeIdentifier)]
		//[Description("A base style shared by type, type member, local, parameter, etc.")]
		//Identifier,
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

	[Category(Constants.CodeTypes.CPlusPlus)]
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

	[Category(Constants.CodeTypes.CSharp)]
	enum CSharpStyleTypes
	{
		None,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpControlFlowKeyword)]
		[BaseDefinition(Constants.CodeKeyword)]
		[BaseDefinition(Constants.CodeKeywordControl)]
		[Style(Bold = true)]
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
		[BaseDefinition(Constants.CodeKeywordControl)]
		[Description("Keyword: switch, case, default, if, else, inheriting from Keyword")]
		BranchingKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLoopKeyword)]
		[BaseDefinition(Constants.CodeKeyword)]
		[BaseDefinition(Constants.CodeKeywordControl)]
		[Description("Keyword: for, foreach in, do, while, inheriting from Keyword")]
		LoopKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpTypeCastKeyword)]
		[BaseDefinition(Constants.CodeKeyword)]
		[Description("Keyword: as, is, in, ref, out, inheriting from Keyword")]
		[Order(After = Constants.CSharpLoopKeyword)]
		TypeCastKeyword,
		[Category(Constants.SyntaxCategory.Keyword)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpResourceKeyword)]
		[BaseDefinition(Constants.CodeKeyword)]
		[BaseDefinition(Constants.CodeKeywordControl)]
		[Description("Keyword: using, lock, try catch finally, fixed, unsafe, stackalloc, inheriting from Keyword")]
		ResourceAndExceptionKeyword,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpDeclarationName)]
		[Order(After = Constants.CSharpUserSymbol)]
		[Style(Bold = true)]
		[Description("Declaration of non-nested type: class, struct, interface, enum, delegate and event, inheriting from Identifier")]
		Declaration,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpMemberDeclarationName)]
		[BaseDefinition(Constants.CSharpDeclarationName)]
		[Style(Bold = true)]
		[Description("Declaration of type member: property, method, event, delegate, nested type, etc. (excluding fields), inheriting from Declaration")]
		MemberDeclaration,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpAliasNamespaceName)]
		[BaseDefinition(Constants.CSharpNamespaceName)]
		[Description("Declaration of alias namespace")]
		AliasNamespace,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLocalFunctionDeclarationName)]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Order(After = Constants.CSharpStaticMemberName)]
		[Description("Declaration of local function, inheriting from method name")]
		LocalFunctionDeclaration,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpVariableCapturedExpression)]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Order(After = Constants.CodeOperator)]
		[Order(After = Constants.CSharpLocalFunctionDeclarationName)]
		[Style(Bold = true)]
		[Description("Declaration of expression which captures external variable")]
		VariableCapturedExpression,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpDeclarationBrace)]
		[BaseDefinition(Constants.CodePunctuation)]
		[Description("Braces {} for declaration, inheriting from Punctuation")]
		DeclarationBrace,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpStaticMemberName)]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[BaseDefinition(Constants.CodeStaticSymbol)]
		[Description("Name of static member, inheriting from static symbol")]
		StaticMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpOverrideMemberName)]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Description("Name of overriding member, inheriting from formal language")]
		OverrideMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpAbstractMemberName)]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Description("Name of abstract member, inheriting from formal language")]
		AbstractMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpVirtualMemberName)]
		[Description("Name of virtual member, inheriting from formal language")]
		VirtualMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[BaseDefinition(Constants.CodeIdentifier)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpPrivateMemberName)]
		[Description("Name of private member, inheriting from formal language")]
		PrivateMemberName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpExtensionMemberName)]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Order(After = Constants.CodeMethodName)]
		[Order(After = Constants.CodePropertyName)]
		[Order(After = Constants.CSharpMethodName)]
		[Order(After = Constants.CSharpPropertyName)]
        [Description("Name of extension methods or properties")]
		ExtensionMemberName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLocalDeclarationName)]
		[BaseDefinition(Constants.CSharpLocalVariableName)]
		[Description("Declaration of local variable")]
		LocalDeclaration,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLocalVariableName)]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeLocalName)]
		[Description("Name of local variable, inheriting from Identifier")]
		LocalVariableName,
		[Category(Constants.SyntaxCategory.Declaration)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLabel)]
		[BaseDefinition(Constants.CodeIdentifier)]
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
		[Order(Before = PredefinedClassificationTypeNames.NaturalLanguage)]
		AttributeNotation,

		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpNamespaceName)]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeNamespaceName)]
		NamespaceName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpSealedMemberName)]
		[Order(After = Constants.CodeClassName)]
		[Order(After = Constants.CSharpMethodName)]
		[Description("Name of sealed class or sealed member")]
		SealedClassName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpNestedTypeName)]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Order(After = Constants.CodeClassName)]
		[Order(After = Constants.CodeStructName)]
		[Order(After = Constants.CodeInterfaceName)]
		[Order(After = Constants.CodeEnumName)]
		[Description("Name of nested type")]
		NestedType,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpReadOnlyStructName)]
		[BaseDefinition(Constants.CodeStructName)]
		[Description("Name of readonly struct")]
		ReadOnlyStructName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpRefStructName)]
		[BaseDefinition(Constants.CodeStructName)]
		[Description("Name of ref struct")]
		RefStructName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpEventName)]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeEventName)]
		[Description("Name of event, inheriting from Identifier")]
		EventName,
		[Category(Constants.SyntaxCategory.TypeDefinition)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpTypeParameterName)]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeTypeParameterName)]
		[Description("Name of type parameter, inheriting from Identifier")]
		TypeParameterName,

		//[ClassificationType(ClassificationTypeNames = Constants.CodeModuleName)]
		//ModuleDeclaration,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpConstructorMethodName)]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Order(After = Constants.CodeClassName)]
		[Order(After = Constants.CodeStructName)]
		[Order(After = Constants.CodeRecordClassName)]
		[Order(After = Constants.CodeRecordStructName)]
		[Description("Name of constructor, inheriting from Method Name")]
		ConstructorMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpFieldName)]
		[BaseDefinition(Constants.CodeIdentifier)]
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
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodePropertyName)]
		[Description("Name of property, inheriting from Identifier")]
		PropertyName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpMethodName)]
		[BaseDefinition(Constants.CodeMethodName)]
		[Order(After = Constants.CodeOperator)]
		[Description("Name of method, inheriting from Identifier")]
		MethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpExtensionMethodName)]
		[BaseDefinition(Constants.CSharpMethodName)]
		[BaseDefinition(Constants.CSharpStaticMemberName)]
		[BaseDefinition(Constants.CodeExtensionMethodName)]
		[BaseDefinition(Constants.CSharpExtensionMemberName)]
		[Description("Name of extension method, inheriting from Method Name and Static Member Name")]
		ExtensionMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpExternMethodName)]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Description("Name of extern method, inheriting from Method Name")]
		ExternMethodName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpParameterName)]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeParameterName)]
		[Description("Name of parameter, inheriting from Identifier")]
		ParameterName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpPrimaryConstructorParameterName)]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CSharpParameterName)]
		[Description("Name of primary constructor parameter, inheriting from C# Parameter")]
		PrimaryConstructorParameterName,
		[Category(Constants.SyntaxCategory.Member)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLocalFunctionParameterName)]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CSharpParameterName)]
		[Description("Parameter of local function or anonymous function, inheriting from C# Parameter")]
		LocalFunctionParameterName,

		[Category(Constants.SyntaxCategory.Comment)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpXmlDoc)]
		[Description("Whole region of XML Documentation")]
		[Order(Before = PredefinedClassificationTypeNames.NaturalLanguage)]
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

		[Category(Constants.SyntaxCategory.Url)]
		[ClassificationType(ClassificationTypeNames = Constants.UrlScheme)]
		[BaseDefinition(Constants.CodeUrl)]
		UrlScheme,
		[Category(Constants.SyntaxCategory.Url)]
		[ClassificationType(ClassificationTypeNames = Constants.UrlHost)]
		[BaseDefinition(Constants.CodeUrl)]
		UrlHost,
		[Category(Constants.SyntaxCategory.Url)]
		[ClassificationType(ClassificationTypeNames = Constants.UrlCredential)]
		[BaseDefinition(Constants.CodeUrl)]
		UrlCredential,
		[Category(Constants.SyntaxCategory.Url)]
		[ClassificationType(ClassificationTypeNames = Constants.UrlFile)]
		[BaseDefinition(Constants.CodeUrl)]
		UrlFile,
		[Category(Constants.SyntaxCategory.Url)]
		[ClassificationType(ClassificationTypeNames = Constants.UrlQueryName)]
		[BaseDefinition(Constants.CodeUrl)]
		UrlQueryName,
		[Category(Constants.SyntaxCategory.Url)]
		[ClassificationType(ClassificationTypeNames = Constants.UrlQueryValue)]
		[BaseDefinition(Constants.CodeUrl)]
		UrlQueryValue,
		[Category(Constants.SyntaxCategory.Url)]
		[ClassificationType(ClassificationTypeNames = Constants.UrlPunctuation)]
		[BaseDefinition(Constants.CodeUrl)]
		UrlPunctuation,
		[Category(Constants.SyntaxCategory.Url)]
		[ClassificationType(ClassificationTypeNames = Constants.UrlFragment)]
		[BaseDefinition(Constants.CodeUrl)]
		UrlFragment,
	}

	enum XmlStyleTypes
	{
		None,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeName)]
		[Order(After = Constants.CodeFormalLanguage)]
		XmlAttributeName,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeQuotes)]
		[Order(After = Constants.CodeFormalLanguage)]
		XmlAttributeQuotes,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeValue)]
		[Order(After = Constants.CodeFormalLanguage)]
		XmlAttributeValue,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlCData)]
		[Order(After = Constants.CodeFormalLanguage)]
		XmlCData,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlComment)]
		[Order(After = Constants.CodeFormalLanguage)]
		XmlComment,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlDelimiter)]
		[Order(After = Constants.CodeFormalLanguage)]
		XmlDelimiter,
		//[Category(Constants.SyntaxCategory.Xml)]
		//[ClassificationType(ClassificationTypeNames = Constants.XmlEntityReference)]
		//XmlEntityReference,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlName)]
		[Order(After = Constants.CodeFormalLanguage)]
		XmlName,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlProcessingInstruction)]
		[Order(After = Constants.CodeFormalLanguage)]
		XmlProcessingInstruction,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XmlText)]
		[Order(After = Constants.CodeFormalLanguage)]
		XmlText,
		[Category(Constants.SyntaxCategory.Xml)]
		[ClassificationType(ClassificationTypeNames = Constants.XsltKeyword)]
		[Order(After = Constants.CodeFormalLanguage)]
		XsltKeyword,
		[Category(Constants.SyntaxCategory.Markup)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkupAttribute)]
		[Order(After = Constants.CodeFormalLanguage)]
		MarkupAttribute,
		[Category(Constants.SyntaxCategory.Markup)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkupAttributeValue)]
		[Order(After = Constants.CodeFormalLanguage)]
		MarkupAttributeValue,
		[Category(Constants.SyntaxCategory.Markup)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkupNode)]
		[Order(After = Constants.CodeFormalLanguage)]
		MarkupNode,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlAttributeName)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlAttributeName,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlAttributeQuotes)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlAttributeQuotes,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlAttributeValue)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlAttributeValue,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlCData)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlCData,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlComment)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlComment,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlDelimiter)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlDelimiter,
		//[Category(Constants.SyntaxCategory.Xaml)]
		//[ClassificationType(ClassificationTypeNames = Constants.XamlEntityReference)]
		//XamlEntityReference,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlName)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlName,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlProcessingInstruction)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlProcessingInstruction,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlText)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlText,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlMarkupExtensionClass)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlMarkupExtensionClass,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlMarkupExtensionParameterName)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlMarkupExtensionParameterName,
		[Category(Constants.SyntaxCategory.Xaml)]
		[ClassificationType(ClassificationTypeNames = Constants.XamlMarkupExtensionParameterValue)]
		[Order(After = Constants.CodeFormalLanguage)]
		XamlMarkupExtensionParameterValue,
	}

	enum MarkdownStyleTypes
	{
		None,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading1)]
		[Order(Before = Constants.MarkdownVsBold)]
		[Style(Bold = true, Size = 28)]
		Heading1,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading2)]
		[Order(Before = Constants.MarkdownVsBold)]
		[Style(Bold = true, Size = 24)]
		Heading2,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading3)]
		[Order(Before = Constants.MarkdownVsBold)]
		[Style(Bold = true, Size = 20)]
		Heading3,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading4)]
		[Order(Before = Constants.MarkdownVsBold)]
		[Style(Bold = true, Size = 18)]
		Heading4,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading5)]
		[Order(Before = Constants.MarkdownVsBold)]
		[Style(Bold = true, Size = 16)]
		Heading5,
		[Category(Constants.SyntaxCategory.Heading)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading6)]
		[Order(Before = Constants.MarkdownVsBold)]
		[Style(Bold = true, Size = 14)]
		Heading6,
		[Category(Constants.SyntaxCategory.Block)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownQuotation)]
		[Order(Before = Constants.MarkdownVsBold)]
		Quotation,
		[Category(Constants.SyntaxCategory.Block)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownOrderedList)]
		[Order(Before = Constants.MarkdownVsBold)]
		OrderedList,
		[Category(Constants.SyntaxCategory.Block)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownUnorderedList)]
		[Order(Before = Constants.MarkdownVsBold)]
		UnorderedList,
		[Category(Constants.SyntaxCategory.Block)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownCodeBlock)]
		CodeBlock,
		[Category(Constants.SyntaxCategory.Block)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownThematicBreak)]
		ThematicBreak,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsBold)]
		[Inheritance]
		Bold,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsItalic)]
		[Inheritance]
		Italic,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsStrikethrough)]
		[Inheritance]
		Strikethrough,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsSuperscript)]
		[Inheritance]
		Superscript,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsSubscript)]
		[Inheritance]
		Subscript,
		[Category(Constants.SyntaxCategory.Style)]
		[ClassificationType(ClassificationTypeNames = Constants.MarkdownVsUrl)]
		[Inheritance]
		Url,
	}

	enum CommentStyleTypes
	{
		[Inheritance]
		[ClassificationType(ClassificationTypeNames = Constants.CodeComment)]
		Default,
		[Category(Constants.SyntaxCategory.Task)]
		[BaseDefinition(Constants.CodeComment)]
		[ClassificationType(ClassificationTypeNames = Constants.CodistComment)]
		[Order(After = Priority.High)]
		CodistComment,
		[Category(Constants.SyntaxCategory.Task)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.EmphasisComment)]
		[Style(Bold = true)]
		Emphasis,
		[Category(Constants.SyntaxCategory.Task)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.QuestionComment)]
		[Style("#9370DB")]
		Question,
		[Category(Constants.SyntaxCategory.Task)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.ExclamationComment)]
		[Style("#CD5C5C")]
		Exclamation,
		[Category(Constants.SyntaxCategory.Task)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.DeletionComment)]
		[Style("#808080")]
		Deletion,
		[Category(Constants.SyntaxCategory.Task)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.TodoComment)]
		[Style("#FFFFFF", "#00008B")]
		ToDo,
		[Category(Constants.SyntaxCategory.Task)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.NoteComment)]
		[Style("#000000", "#FFA500")]
		Note,
		[Category(Constants.SyntaxCategory.Task)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.HackComment)]
		[Style("#90EE90", "#000000")]
		Hack,
		[Category(Constants.SyntaxCategory.Task)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.UndoneComment)]
		[Style("#A4AFD1", "#714136")]
		Undone,
		[Category(Constants.SyntaxCategory.Heading)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading1Comment)]
		[Style(Bold = true)]
		Heading1,
		[Category(Constants.SyntaxCategory.Heading)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading2Comment)]
		[Style(Bold = true)]
		Heading2,
		[Category(Constants.SyntaxCategory.Heading)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading3Comment)]
		[Style(Bold = true)]
		Heading3,
		[Category(Constants.SyntaxCategory.Heading)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading4Comment)]
		[Style(Bold = true)]
		Heading4,
		[Category(Constants.SyntaxCategory.Heading)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading5Comment)]
		[Style(Bold = true)]
		Heading5,
		[Category(Constants.SyntaxCategory.Heading)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Heading6Comment)]
		[Style(Bold = true)]
		Heading6,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Task1Comment)]
		Task1,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Task2Comment)]
		Task2,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Task3Comment)]
		Task3,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Task4Comment)]
		Task4,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Task5Comment)]
		Task5,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Task6Comment)]
		Task6,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Task7Comment)]
		Task7,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Task8Comment)]
		Task8,
		[Category(Constants.SyntaxCategory.GeneralTask)]
		[BaseDefinition(Constants.CodistComment)]
		[ClassificationType(ClassificationTypeNames = Constants.Task9Comment)]
		Task9,
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
		[Order(After = Constants.CSharpMetadataSymbol)]
		MyTypeAndMember,
		[Category(Constants.SyntaxCategory.Source)]
		[ClassificationType(ClassificationTypeNames = Constants.CSharpMetadataSymbol)]
		[Description("Type and member imported via referencing assembly")]
		[Order(After = Constants.CodeClassName)]
		[Order(After = Constants.CodeStructName)]
		[Order(After = Constants.CodeInterfaceName)]
		[Order(After = Constants.CodeEnumName)]
		[Order(After = Constants.CodeDelegateName)]
		[Order(After = Constants.CodeEventName)]
		[Order(After = Constants.CodeRecordClassName)]
		[Order(After = Constants.CodeRecordStructName)]
		[Order(After = Constants.CSharpStaticMemberName)]
		[Order(After = Constants.CSharpSealedMemberName)]
		[Order(After = Constants.CSharpAbstractMemberName)]
		[Order(After = Constants.CSharpPropertyName)]
		[Order(After = Constants.CSharpMethodName)]
		[Order(After = Constants.CSharpExtensionMethodName)]
		[Order(After = Constants.CSharpExternMethodName)]
		[Order(After = Constants.CSharpEventName)]
		[Order(After = Constants.CSharpNestedTypeName)]
		[Order(After = Constants.CSharpFieldName)]
		[Order(After = Constants.CSharpEnumFieldName)]
		[Order(After = Constants.CSharpReadOnlyFieldName)]
		[Order(After = Constants.CSharpVolatileFieldName)]
		[Order(After = Constants.CSharpConstFieldName)]
		ReferencedTypeAndMember,
	}

	enum PrivateStyleTypes
	{
		[ClassificationType(ClassificationTypeNames = Constants.CodeBold)]
		[Style(Bold = true)]
		Bold
	}
}
