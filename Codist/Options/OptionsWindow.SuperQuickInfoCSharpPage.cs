using System;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class SuperQuickInfoCSharpPage : OptionPageFactory
		{
			public override string Name => R.OT_CSharp;
			public override Features RequiredFeature => Features.SuperQuickInfo;
			public override bool IsSubOption => true;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly OptionBox<QuickInfoOptions> _OverrideDefaultDocumentation, _DocumentationFromBaseType, _DocumentationFromInheritDoc, _TextOnlyDoc, _OrdinaryDoc, _ReturnsDoc, _RemarksDoc, _ExceptionDoc, _SeeAlsoDoc, _ExampleDoc, _AlternativeStyle, _ContainingType, _CodeFontForXmlDocSymbol;
				readonly OptionBox<QuickInfoOptions> _NodeRange, _Attributes, _BaseType, _Declaration, _SymbolLocation, _Interfaces, _NumericValues, _String, _Parameter, _InterfaceImplementations, _TypeParameters, _NamespaceTypes, _MethodOverload, _InterfaceMembers, _EnumMembers, _SymbolReassignment, _ControlFlow, _DataFlow, _SyntaxNodePath;
				readonly OptionBox<QuickInfoOptions>[] _Options;

				public PageControl() {
					var o = Config.Instance.QuickInfoOptions;
					SetContents(new Note(R.OT_CSharpNote),
						new TitleBox(R.OT_QuickInfoOverride),
						new DescriptionBox(R.OT_QuickInfoOverrideNote),
						_AlternativeStyle = o.CreateOptionBox(QuickInfoOptions.AlternativeStyle, UpdateConfig, R.OT_AlternativeStyle)
							.SetLazyToolTip(() => R.OT_AlternativeStyleTip),
						_NodeRange = o.CreateOptionBox(QuickInfoOptions.NodeRange, UpdateConfig, R.OT_NodeRange)
							.SetLazyToolTip(() => R.OT_NodeRangeTip),
						_OverrideDefaultDocumentation = o.CreateOptionBox(QuickInfoOptions.OverrideDefaultDocumentation, UpdateConfig, R.OT_OverrideXmlDoc)
							.SetLazyToolTip(() => R.OT_OverrideXmlDocTip),
						_DocumentationFromBaseType = o.CreateOptionBox(QuickInfoOptions.DocumentationFromBaseType, UpdateConfig, R.OT_InheritXmlDoc)
							.SetLazyToolTip(() => R.OT_InheritXmlDocTip),
						_DocumentationFromInheritDoc = o.CreateOptionBox(QuickInfoOptions.DocumentationFromInheritDoc, UpdateConfig, R.OT_InheritDoc)
							.SetLazyToolTip(() => R.OT_InheritDocTip),
						_ReturnsDoc = o.CreateOptionBox(QuickInfoOptions.ReturnsDoc, UpdateConfig, R.OT_ShowReturnsXmlDoc)
							.SetLazyToolTip(() => R.OT_ShowReturnsXmlDocTip),
						_RemarksDoc = o.CreateOptionBox(QuickInfoOptions.RemarksDoc, UpdateConfig, R.OT_ShowRemarksXmlDoc)
							.SetLazyToolTip(() => R.OT_ShowRemarksXmlDocTip),
						_ExceptionDoc = o.CreateOptionBox(QuickInfoOptions.ExceptionDoc, UpdateConfig, R.OT_ShowExceptionXmlDoc)
							.SetLazyToolTip(() => R.OT_ShowExceptionXmlDocTip),
						_SeeAlsoDoc = o.CreateOptionBox(QuickInfoOptions.SeeAlsoDoc, UpdateConfig, R.OT_ShowSeeAlsoXmlDoc)
							.SetLazyToolTip(() => R.OT_ShowSeeAlsoXmlDocTip),
						_ExampleDoc = o.CreateOptionBox(QuickInfoOptions.ExampleDoc, UpdateConfig, R.OT_ShowExampleXmlDoc)
							.SetLazyToolTip(() => R.OT_ShowExampleXmlDocTip),
						_TextOnlyDoc = o.CreateOptionBox(QuickInfoOptions.TextOnlyDoc, UpdateConfig, R.OT_TextOnlyXmlDoc)
							.SetLazyToolTip(() => R.OT_TextOnlyXmlDocTip),
						_OrdinaryDoc = o.CreateOptionBox(QuickInfoOptions.OrdinaryCommentDoc, UpdateConfig, R.OT_UseOrdinaryComment)
							.SetLazyToolTip(() => R.OT_UseOrdinaryCommentTip),
						_ContainingType = o.CreateOptionBox(QuickInfoOptions.ContainingType, UpdateConfig, R.OT_ShowSeeContainingType)
							.SetLazyToolTip(() => R.OT_ShowSeeContainingTypeTip),
						_CodeFontForXmlDocSymbol = o.CreateOptionBox(QuickInfoOptions.UseCodeFontForXmlDocSymbol, UpdateConfig, R.OT_UseCodeEditorFontForSee)
							.SetLazyToolTip(() => R.OT_UseCodeEditorFontForSeeTip),

						new TitleBox(R.OT_AdditionalQuickInfo),
						new DescriptionBox(R.OT_AdditionalQuickInfoNote),
						_Attributes = o.CreateOptionBox(QuickInfoOptions.Attributes, UpdateConfig, R.OT_Attributes)
							.SetLazyToolTip(() => R.OT_AttributesTip),
						_BaseType = o.CreateOptionBox(QuickInfoOptions.BaseType, UpdateConfig, R.OT_BaseType)
							.SetLazyToolTip(() => R.OT_BaseTypeTip),
						_Declaration = o.CreateOptionBox(QuickInfoOptions.Declaration, UpdateConfig, R.OT_Declaration)
							.SetLazyToolTip(() => R.OT_DesclarationTip),
						_EnumMembers = o.CreateOptionBox(QuickInfoOptions.Enum, UpdateConfig, R.OT_EnumMembers)
							.SetLazyToolTip(() => R.OT_EnumMembersTip),
						_Interfaces = o.CreateOptionBox(QuickInfoOptions.Interfaces, UpdateConfig, R.OT_Interfaces)
							.SetLazyToolTip(() => R.OT_InterfacesTip),
						_InterfaceImplementations = o.CreateOptionBox(QuickInfoOptions.InterfaceImplementations, UpdateConfig, R.OT_InterfaceImplementation)
							.SetLazyToolTip(() => R.OT_InterfaceImplementationTip),
						_InterfaceMembers = o.CreateOptionBox(QuickInfoOptions.InterfaceMembers, UpdateConfig, R.OT_InterfaceMembers)
							.SetLazyToolTip(() => R.OT_InterfaceMembersTip),
						_MethodOverload = o.CreateOptionBox(QuickInfoOptions.MethodOverload, UpdateConfig, R.OT_MethodOverloads)
							.SetLazyToolTip(() => R.OT_MethodOverloadsTip),
						_Parameter = o.CreateOptionBox(QuickInfoOptions.Parameter, UpdateConfig, R.OT_ParameterOfMethod)
							.SetLazyToolTip(() => R.OT_ParameterOfMethodTip),
						_TypeParameters = o.CreateOptionBox(QuickInfoOptions.TypeParameters, UpdateConfig, R.OT_TypeParameter)
							.SetLazyToolTip(() => R.OT_TypeParameterTip),
						_SymbolLocation = o.CreateOptionBox(QuickInfoOptions.SymbolLocation, UpdateConfig, R.OT_SymbolLocation)
							.SetLazyToolTip(() => R.OT_SymbolLocationTip),
						_NumericValues = o.CreateOptionBox(QuickInfoOptions.NumericValues, UpdateConfig, R.OT_NumericForms)
							.SetLazyToolTip(() => R.OT_NumericFormsTip),
						_String = o.CreateOptionBox(QuickInfoOptions.String, UpdateConfig, R.OT_StringInfo)
							.SetLazyToolTip(() => R.OT_StringInfoTip),
						_SymbolReassignment = o.CreateOptionBox(QuickInfoOptions.SymbolReassignment, UpdateConfig, R.OT_DenoteVariableReassignment)
							.SetLazyToolTip(() => R.OT_DenoteVariableReassignmentTip),

						_ControlFlow = o.CreateOptionBox(QuickInfoOptions.ControlFlow, UpdateConfig, R.OT_ControlFlow)
							.SetLazyToolTip(() => R.OT_ControlFlowTip),
						_DataFlow = o.CreateOptionBox(QuickInfoOptions.DataFlow, UpdateConfig, R.OT_DataFlow)
							.SetLazyToolTip(() => R.OT_DataFlowTip),
						_SyntaxNodePath = o.CreateOptionBox(QuickInfoOptions.SyntaxNodePath, UpdateConfig, R.OT_SyntaxNodePath)
							.SetLazyToolTip(() => R.OT_SyntaxNodePathTip)
					);
					_Options = new[] { _OverrideDefaultDocumentation, _DocumentationFromBaseType, _DocumentationFromInheritDoc, _TextOnlyDoc, _ReturnsDoc, _RemarksDoc, _ExceptionDoc, _SeeAlsoDoc, _ExampleDoc, _AlternativeStyle, _ContainingType, _CodeFontForXmlDocSymbol, _NodeRange, _Attributes, _BaseType, _Declaration, _EnumMembers, _SymbolLocation, _Interfaces, _NumericValues, _String, _Parameter, _InterfaceImplementations, _TypeParameters, /*_NamespaceTypes, */_MethodOverload, _InterfaceMembers, _SymbolReassignment, _ControlFlow, _DataFlow, _SyntaxNodePath };
					var docOptions = new OptionBox<QuickInfoOptions>[] { _DocumentationFromBaseType, _DocumentationFromInheritDoc, _TextOnlyDoc, _OrdinaryDoc, _ReturnsDoc, _RemarksDoc, _ExceptionDoc, _SeeAlsoDoc, _ExampleDoc, _ContainingType, _CodeFontForXmlDocSymbol };
					SubOptionMargin.ApplyMargin(docOptions);
					_OverrideDefaultDocumentation.BindDependentOptionControls(docOptions);
				}

				protected override void LoadConfig(Config config) {
					var o = config.QuickInfoOptions;
					Array.ForEach(_Options, i => i.UpdateWithOption(o));
				}

				void UpdateConfig(QuickInfoOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.SuperQuickInfo);
				}
			}
		}
	}
}
