using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CLR;
using Codist.SymbolCommands;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	sealed class CSharpSymbolContextMenu : ContextMenu, IDisposable
	{
		static readonly string[] __UnitTestingNamespace = new[] { "UnitTesting", "TestTools", "VisualStudio", "Microsoft" };
		UIHost _Host;

		public CSharpSymbolContextMenu(ISymbol symbol, SyntaxNode node, SemanticContext semanticContext) {
			Resources = SharedDictionaryManager.ContextMenu;
			Foreground = ThemeCache.ToolWindowTextBrush;
			this.ReferenceCrispImageBackground(VsColors.CommandBarMenuBackgroundGradientBeginKey);
			_Host = new UIHost(symbol, node, semanticContext);
		}

		public event EventHandler<RoutedEventArgs> CommandExecuted;

		public void AddTitleItem(string name) {
			Items.Add(new MenuItem {
				Header = name,
				IsEnabled = false,
				Icon = null,
				HorizontalContentAlignment = HorizontalAlignment.Right
			});
		}

		public void AddNodeCommands() {
			if (_Host.Node != null) {
				AddCommand(CommandId.GoToNode);
				AddCommand(CommandId.SelectNode);
			}
		}
		public void AddSymbolNodeCommands() {
			var symbol = _Host.Symbol;
			if (symbol.HasSource()) {
				AddCommand(CommandId.GoToSymbolDefinition);
				if (symbol.Kind != SymbolKind.Namespace && _Host.Node == null) {
					AddCommand(CommandId.SelectSymbolNode);
				}
			}
			else if (_Host.Node != null) {
				AddCommand(CommandId.SelectNode);
			}
			AddCopyAndSearchSymbolCommands();
		}

		public void AddCopyAndSearchSymbolCommands() {
			var symbol = _Host.Symbol;
			AddCommand(CommandId.CopySymbolName);
			if (symbol.Kind == SymbolKind.Field && ((IFieldSymbol)symbol).HasConstantValue) {
				AddCommand(CommandId.CopyConstantValue);
			}
			if (symbol.CanBeReferencedByName) {
				AddCommand(CommandId.WebSearch);
			}
		}

		public void AddAnalysisCommands() {
			if (_Host.Context.Document == null) {
				return;
			}
			switch (_Host.Symbol.Kind) {
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.Event:
				case SymbolKind.Field:
					CreateCommandForMembers();
					break;
				case SymbolKind.Parameter:
					CreateCommandForParameter();
					goto case SymbolKind.Local;
				case SymbolKind.Local:
					CreateCommandsForReturnTypeCommand();
					break;
				case SymbolKind.NamedType:
					CreateCommandForNamedType(_Host.Symbol as INamedTypeSymbol);
					break;
				case SymbolKind.Namespace:
					AddCommand(CommandId.ListSymbolMembers);
					AddCommand(CommandId.ListSymbolLocations);
					break;
				case SymbolKind.ErrorType:
					return;
			}
			if (_Host.Context.SemanticModel != null) {
				if (_Host.Node?.Kind().IsDeclaration() == true
					&& _Host.Node.SyntaxTree == _Host.Context.SemanticModel.SyntaxTree
					&& _Host.Symbol.Kind != SymbolKind.Namespace) {
					AddCommand(CommandId.ListReferencedSymbols);
				}

				if (String.IsNullOrEmpty(_Host.Symbol.Name) == false) {
					AddCommand(CommandId.FindSymbolsWithName);
				}
			}
		}

		public void AddFindAllReferencesCommand() {
			Items.Add(CreateItem(IconIds.FindReference, R.CMD_FindAllReferences, FindAllReferences));
		}

		public void AddGoToAnyCommands() {
			Items.Add(CreateItem(IconIds.GoToMember, R.CMD_GotoMember, GoToMember));
			Items.Add(CreateItem(IconIds.GoToType, R.CMD_GotoType, GoToType));
			Items.Add(CreateItem(IconIds.GoToSymbol, R.CMD_GotoSymbol, GoToSymbol));
		}

		public void AddUnitTestCommands() {
			var symbol = _Host.Symbol;
			if (symbol.HasSource() && symbol.IsPublicConcreteInstance()) {
				bool isTest = false;
				if (symbol.Kind == SymbolKind.Method) {
					if (symbol.GetContainingTypes().All(t => t.IsPublicConcreteInstance())
						&& ((IMethodSymbol)symbol).ReturnsVoid
						&& symbol.GetAttributes().Any(a => a.AttributeClass.MatchTypeName("TestMethodAttribute", __UnitTestingNamespace))) {
						isTest = true;
					}
				}
				else if (symbol.Kind == SymbolKind.NamedType && ((ITypeSymbol)symbol).TypeKind == TypeKind.Class) {
					if (symbol.GetAttributes().Any(a => a.AttributeClass.MatchTypeName("TestClassAttribute", __UnitTestingNamespace))) {
						isTest = true;
					}
				}

				if (isTest) {
					AddCommand(CommandId.DebugUnitTest);
					AddCommand(CommandId.RunUnitTest);
				}
			}
		}

		public void DisposeOnClose() {
			Closed += CSharpSymbolContextMenu_Closed;
		}

		void AddCommand(CommandId commandId) {
			AddCommand(_Host.CreateCommand(commandId));
		}
		void AddCommand(CommandId commandId, string substitution) {
			AddCommand(_Host.CreateCommand(commandId, substitution));
		}
		void AddCommand(CustomMenuItem command) {
			command.AddClickHandler(OnCommandExecuted);
			Items.Add(command);
		}
		void OnCommandExecuted(object sender, RoutedEventArgs args) {
			CommandExecuted?.Invoke(this, args);
		}

		void CreateCommandForMembers() {
			if (_Host.Symbol.Kind != SymbolKind.Method
				|| IsExternallyCallable(((IMethodSymbol)_Host.Symbol).MethodKind)) {
				AddCommand(CommandId.FindReferrers);
			}
			if (_Host.Symbol.MayHaveOverride()) {
				AddCommand(CommandId.FindOverrides);
			}
			var st = _Host.Symbol.ContainingType;
			if (st?.TypeKind == TypeKind.Interface) {
				AddCommand(CommandId.FindImplementations);
			}
			if (_Host.Symbol.Kind != SymbolKind.Event) {
				CreateCommandsForReturnTypeCommand();
			}
			else {
				CreateCommandForEventArgs();
			}
			if (_Host.Symbol.Kind == SymbolKind.Method) {
				switch (((IMethodSymbol)_Host.Symbol).MethodKind) {
					case MethodKind.Constructor:
						if (st.SpecialType == SpecialType.None) {
							AddCommand(CommandId.FindTypeReferrers);
							CreateInstanceCommandsForContainingType();
						}
						break;
					case MethodKind.StaticConstructor:
					case MethodKind.Destructor:
						break;
					default:
						AddCommand(CommandId.FindMethodsBySignature);
						break;
				}
			}

			bool IsExternallyCallable(MethodKind methodKind) {
				switch (methodKind) {
					case MethodKind.AnonymousFunction:
					case MethodKind.LocalFunction:
					case MethodKind.Destructor:
					case MethodKind.StaticConstructor:
						return false;
				}
				return true;
			}
		}

		void CreateCommandForNamedType(INamedTypeSymbol t) {
			var isExtensionType = t.GetExtensionParameter() != null;
			if (isExtensionType) {
				AddCommand(CommandId.ListSymbolMembers);
				return;
			}
			if (t.IsAnyKind(TypeKind.Class, TypeKind.Struct)) {
				var ctor = _Host.Node?.GetObjectCreationNode();
				if (ctor != null) {
					var symbol = _Host.Context.SemanticModel.GetSymbolOrFirstCandidate(ctor);
					if (symbol != null) {
						AddCommand(CommandId.FindConstructorReferrers);
					}
				}
				else if (t.InstanceConstructors.Length > 0) {
					AddCommand(CommandId.FindObjectInitializers);
				}
			}
			AddCommand(CommandId.FindTypeReferrers);
			AddCommand(CommandId.ListSymbolMembers);
			if (t.IsStatic) {
				return;
			}
			if (t.IsSealed == false) {
				if (t.TypeKind == TypeKind.Class) {
					AddCommand(CommandId.FindDerivedClasses);
				}
				else if (t.TypeKind == TypeKind.Interface) {
					AddCommand(CommandId.FindImplementations);
					AddCommand(CommandId.FindSubInterfaces);
				}
			}
			if (t.TypeKind == TypeKind.Delegate) {
				AddCommand(CommandId.FindMethodsBySignature);
			}
			AddCommand(CommandId.FindExtensionMethods);
			if (t.SpecialType == SpecialType.None) {
				CreateInstanceCommandsForType();
			}
		}

		void CreateCommandsForReturnTypeCommand() {
			var rt = _Host.Symbol.GetReturnType();
			if (rt.SpecialType.CeqAny(SpecialType.System_Void, SpecialType.System_Object)
				|| rt.IsAnyKind(TypeKind.TypeParameter, TypeKind.Error, TypeKind.Dynamic)
				|| rt.IsTupleType) {
				return;
			}
			var et = rt.ResolveElementType();
			var ga = et.ResolveSingleGenericTypeArgument();
			string typeName = et.Name + et.GetParameterString();
			AddCommand(CommandId.ListReturnTypeMembers, typeName);
			if (rt.IsStatic == false) {
				AddCommand(CommandId.FindReturnTypeExtensionMethods, typeName);
			}
			if (et.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
				AddCommand(CommandId.GoToSymbolReturnType, typeName);
			}
			if (ReferenceEquals(ga, et) == false) {
				typeName = ga.Name + ga.GetParameterString();
				AddCommand(CommandId.ListSpecialGenericReturnTypeMembers, typeName);
				if (ga.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
					AddCommand(CommandId.GoToSpecialGenericSymbolReturnType, typeName);
				}
			}
		}

		void CreateCommandForEventArgs() {
			var t = ((IEventSymbol)_Host.Symbol).GetEventArgsType();
			if (t?.GetBaseTypes().Any(i => i.MatchTypeName(nameof(EventArgs), nameof(System))) == true) {
				AddCommand(CommandId.ListEventArgsMembers, t.GetOriginalName());
			}
		}

		void CreateCommandForParameter() {
			var p = (IParameterSymbol)_Host.Symbol;
			if (p.ContainingSymbol is IMethodSymbol m
				&& m.MethodKind.CeqAny(MethodKind.Ordinary, MethodKind.Constructor, MethodKind.LocalFunction, MethodKind.ReducedExtension)) {
				AddCommand(p.HasExplicitDefaultValue ? CommandId.FindOptionalParameterAssignments : CommandId.FindParameterAssignments, _Host.Symbol.Name);
			}
		}

		void CreateInstanceCommandsForType() {
			AddCommand(CommandId.FindInstanceProducers);
			AddCommand(CommandId.FindInstanceConsumers);
		}

		void CreateInstanceCommandsForContainingType() {
			AddCommand(CommandId.FindContainingTypeInstanceProducers);
			AddCommand(CommandId.FindContainingTypeInstanceConsumers);
		}

		static CustomMenuItem CreateItem(int imageId, string title, string substitutions, RoutedEventHandler clickHandler, string tooltip) {
			return new CustomMenuItem(imageId, title, substitutions).AddClickHandler(clickHandler).SetToolTip(tooltip);
		}

		static CustomMenuItem CreateItem(int imageId, string title, RoutedEventHandler clickHandler, string tooltip = null) {
			return new CustomMenuItem(imageId, title).AddClickHandler(clickHandler).SetToolTip(tooltip);
		}

		void FireCommandExecuted(RoutedEventArgs e) {
			CommandExecuted?.Invoke(this, e);
			IsOpen = false;
		}

		void CSharpSymbolContextMenu_Closed(object sender, RoutedEventArgs e) {
			Closed -= CSharpSymbolContextMenu_Closed;
			Dispose();
		}

		public void Dispose() {
			_Host = null;
			DataContext = null;
			this.DisposeCollection();
		}

		#region Menu event handlers
		void FindAllReferences(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.FindAllReferences");
		}
		void GoToMember(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.GoToMember");
		}
		void GoToType(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.GoToType");
		}
		void GoToSymbol(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.GoToSymbol");
		}
		#endregion

		sealed class CustomMenuItem : MenuItem
		{
			readonly int _ImageId;
			RoutedEventHandler _ClickHandler;
			string _ToolTip;
			bool _Handled;
			bool _hasSubCommand;
			Chain<System.Windows.Controls.Primitives.ButtonBase> _SubCommands;
			CommandOptions _Option;

			public CustomMenuItem(int imageId, string title) {
				Icon = VsImageHelper.GetImage(_ImageId = imageId);
				Header = new ThemedMenuText { Text = title };
			}
			public CustomMenuItem(int imageId, string title, string titleSubstitution) {
				Icon = VsImageHelper.GetImage(_ImageId = imageId);

				var i = title.IndexOf('<');
				if (i < -1) {
					goto FALLBACK;
				}
				var i2 = title.IndexOf('>', i);
				if (i2 < 0) {
					goto FALLBACK;
				}
				Header = new ThemedMenuText().Append(title.Substring(0, i))
					.Append(new System.Windows.Documents.Run(String.IsNullOrEmpty(titleSubstitution) ? "?" : titleSubstitution) { TextDecorations = { TextDecorations.Underline } })
					.Append(title.Substring(i2 + 1));
				return;
			FALLBACK:
				Header = new ThemedMenuText { Text = title };
			}

			public CommandOptions Option => _Option;

			public CustomMenuItem AddClickHandler(RoutedEventHandler clickHandler) {
				_ClickHandler += clickHandler;
				return this;
			}
			public CustomMenuItem AddSubCommand(string tooltip, RoutedEventHandler handler, int imageId = 0) {
				handler += HideMenu;
				var button = new ThemedButton(imageId > 0 ? imageId : _ImageId, tooltip, handler);
				(_SubCommands ??= []).Add(button);
				_hasSubCommand = true;
				return this;
			}
			public CustomMenuItem AddOptionButton(string tooltip, CommandOptions option, int imageId = 0, Action<ThemedToggleButton> buttonConfigurator = null) {
				var button = new ThemedToggleButton(imageId > 0 ? imageId : _ImageId, tooltip, SetCommandOption) { Tag = option };
				buttonConfigurator?.Invoke(button);
				(_SubCommands ??= []).Add(button);
				_hasSubCommand = true;
				return this;
			}
			public CustomMenuItem HasExtractMatchOption(ISymbol symbol) {
				if (symbol == null
					|| symbol.IsStatic
					|| symbol is IMethodSymbol m && m.MethodKind != MethodKind.Ordinary
					|| symbol is INamedTypeSymbol nt && !nt.IsBoundedGenericType()) {
					return this;
				}
				var t = symbol.GetReturnType() ?? symbol as INamedTypeSymbol;
				return t?.IsAnyKind(TypeKind.Class, TypeKind.Structure, TypeKind.Interface, TypeKind.Delegate) == true
					? AddOptionButton(R.CMDT_FindExtract, CommandOptions.ExtractMatch, IconIds.CurrentSymbolOnly)
					: this;
			}
			public CustomMenuItem HasMatchTypeArgumentOption(ISymbol symbol) {
				return symbol != null && (symbol is IMethodSymbol m && m.IsBoundedGenericMethod()
						|| symbol is INamedTypeSymbol t && t.IsBoundedGenericType()
						|| symbol.ContainingType?.IsBoundedGenericType() == true)
					? AddOptionButton(R.CMDT_MatchTypeArgument, CommandOptions.MatchTypeArgument, IconIds.MatchTypeArgument)
					: this;
			}
			public CustomMenuItem HasDirectDeriveOption() {
				return AddOptionButton(R.CMDT_FindDirectlyDerived, CommandOptions.DirectDerive, IconIds.DirectDerive);
			}
			public CustomMenuItem HasFileScopeOptions() {
				return AddOptionButton(R.CMDT_ScopeToCurrentFile, CommandOptions.CurrentFile, IconIds.File, ExclusiveButtonConfigurator.FileProject)
					.AddOptionButton(R.CMDT_ScopeToCurrentProject, CommandOptions.CurrentProject, IconIds.Project, ExclusiveButtonConfigurator.FileProject)
					.AddOptionButton(R.CMDT_ScopeToRelatedProjects, CommandOptions.RelatedProjects, IconIds.RelatedProjects, ExclusiveButtonConfigurator.Projects);
			}
			public CustomMenuItem HasProjectScopeOptions() {
				return AddOptionButton(R.CMDT_ScopeToCurrentProject, CommandOptions.CurrentProject, IconIds.Project,  ExclusiveButtonConfigurator.Projects)
					.AddOptionButton(R.CMDT_ScopeToRelatedProjects, CommandOptions.RelatedProjects, IconIds.RelatedProjects, ExclusiveButtonConfigurator.Projects);
			}
			public CustomMenuItem HasSourceCodeScopeOption() {
				return AddOptionButton(R.CMDT_ScopeToSourceCode, CommandOptions.SourceCode, IconIds.SourceCode, ExclusiveButtonConfigurator.SourceCode)
					.AddOptionButton(R.CMDT_ScopeToExternal, CommandOptions.External, IconIds.ExternalSymbol, ExclusiveButtonConfigurator.SourceCode);
			}

			void HideMenu(object sender, RoutedEventArgs e) {
				this.GetParent<CSharpSymbolContextMenu>()?.FireCommandExecuted(e);
			}
			void SetCommandOption(object sender, RoutedEventArgs e) {
				var b = sender as ThemedToggleButton;
				_Option = _Option.SetFlags((CommandOptions)b.Tag, b.IsChecked == true);
			}

			public CustomMenuItem SetToolTip(string tooltip) {
				if (tooltip == null) {
					return this;
				}
				_ToolTip = tooltip;
				return this.SetLazyToolTip(CreateToolTip).SetTipOptions();
			}

			CommandToolTip CreateToolTip() {
				return new CommandToolTip(_ImageId, ((ThemedMenuText)Header).GetText(), new ThemedTipText(_ToolTip));
			}

			protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e) {
				base.OnPreviewMouseRightButtonUp(e);
				_Handled = false;
				HandleExecuteEvent(e);
			}
			public override void OnApplyTemplate() {
				base.OnApplyTemplate();
				if (!_hasSubCommand) {
					return;
				}
				if (GetTemplateChild("SubCommands") is ContentPresenter c) {
					c.Content = new ThemedControlGroup().AddRange(_SubCommands);
				}
			}

			protected override void OnClick() {
				base.OnClick();
				if (_Handled == false) {
					HandleExecuteEvent(new RoutedEventArgs(ClickEvent));
				}
				_Handled = false;
			}

			void HandleExecuteEvent(RoutedEventArgs e) {
				var h = _ClickHandler;
				if (h != null && _Handled == false) {
					try {
						h.Invoke(this, e);
					}
					catch (Exception ex) {
						MessageWindow.Error(ex, R.T_ErrorExecutingCommand + ((ThemedMenuText)Header).GetText(), null, this);
					}
					_Handled = true;
				}
			}
		}

		sealed class UIHost
		{
			readonly ISymbol _Symbol;
			readonly SyntaxNode _Node;
			readonly SemanticContext _SemanticContext;

			public UIHost(ISymbol symbol, SyntaxNode node, SemanticContext semanticContext) {
				_Symbol = symbol?.Kind == SymbolKind.Alias ? ((IAliasSymbol)symbol).Target : symbol;
				_Node = node;
				_SemanticContext = semanticContext;
			}

			public ISymbol Symbol => _Symbol;
			public SyntaxNode Node => _Node;
			public SemanticContext Context => _SemanticContext;


			#region Command event handlers
			public CustomMenuItem CreateCommand(CommandId commandId) {
				switch (commandId) {
					case CommandId.GoToNode:
						return CreateItem(IconIds.GoToDefinition, R.CMD_GoToDefinition, GoToNode);
					case CommandId.SelectNode:
						return CreateItem(IconIds.SelectCode, R.CMD_SelectCode, SelectNode)
							.AddSubCommand(R.CMDT_SelectCodeWithoutTrivia, SelectNodeWithoutTrivia, IconIds.SelectCodeWithoutTrivia);
					case CommandId.SelectSymbolNode:
						return CreateItem(IconIds.SelectCode, R.CMD_SelectCode, SelectSymbolNode)
							.AddSubCommand(R.CMDT_SelectCodeWithoutTrivia, SelectSymbolNodeWithoutTrivia, IconIds.SelectCodeWithoutTrivia);
					case CommandId.GoToSymbolDefinition:
						return CreateItem(IconIds.GoToDefinition, R.CMD_GoToDefinition, GoToSymbolDefinition);
					case CommandId.CopySymbolName: {
						var cmd = CreateItem(IconIds.Copy, R.CMD_CopySymbol, CopySymbolName, R.CMDT_CopySymbol)
							.AddSubCommand(R.CMDT_CopyQualifiedName, CopyTypeQualifiedSymbolName, IconIds.Class);
						if (_Symbol != null) {
							if (_Symbol.IsQualifiable()) {
								cmd.AddSubCommand(R.CMDT_CopyFullyQualifiedName, CopyFullyQualifiedSymbolName, IconIds.Namespace);
							}
							if (_Symbol.Kind != SymbolKind.Namespace) {
								cmd.AddSubCommand(R.CMDT_CopyDefinition, CopyDefinition, IconIds.Definition);
							}
						}
						return cmd;
					}
					case CommandId.CopyConstantValue:
						return CreateItem(IconIds.Constant, R.CMD_CopyConstantValue, CopyConstantValue);
					case CommandId.FindExtensionMethods:
						return CreateItem(IconIds.ExtensionMethod, R.CMD_FindExtensions, FindExtensionMethods)
							.HasExtractMatchOption(_Symbol)
							.HasSourceCodeScopeOption();
					case CommandId.FindSubInterfaces:
						return CreateItem(IconIds.FindDerivedTypes, R.CMD_FindInheritedInterfaces, FindSubInterfaces, R.CMDT_FindInheritedInterfaces)
							.HasDirectDeriveOption()
							.HasSourceCodeScopeOption();
					case CommandId.FindImplementations:
						return CreateItem(IconIds.FindImplementations, R.CMD_FindImplementations, FindImplementations, R.CMDT_FindImplementations)
							.AddOptionButton(R.CMDT_FindDirectImplementations, CommandOptions.DirectDerive, IconIds.DirectDerive)
							.HasProjectScopeOptions()
							.HasSourceCodeScopeOption();
					case CommandId.FindDerivedClasses:
						return CreateItem(IconIds.FindDerivedTypes, R.CMD_FindDerivedClasses, FindDerivedClasses, R.CMDT_FindDerivedClasses)
							.HasDirectDeriveOption()
							.HasProjectScopeOptions()
							.HasSourceCodeScopeOption();
					case CommandId.FindOverrides:
						return CreateItem(IconIds.FindOverloads, R.CMD_FindOverrides, FindOverrides)
							.HasProjectScopeOptions()
							.HasSourceCodeScopeOption();
					case CommandId.FindReferrers:
						return CreateItem(IconIds.FindReferrers, R.CMD_FindReferrers, FindReferrers, R.CMDT_FindReferrers)
							.HasExtractMatchOption(_Symbol)
							.HasMatchTypeArgumentOption(_Symbol)
							.HasFileScopeOptions();
					case CommandId.FindSymbolsWithName:
						return CreateItem(IconIds.FindSymbolsWithName, R.CMD_FindSymbolwithName, _Symbol.Name, FindSymbolWithName, R.CMDT_FindSymbolwithName)
							.AddOptionButton(R.CMDT_FindSymbolWithFullName, CommandOptions.ExtractMatch, IconIds.SameName)
							.AddOptionButton(R.CMDT_MatchCase, CommandOptions.MatchCase, IconIds.MatchCase)
							.HasSourceCodeScopeOption();
					case CommandId.FindMethodsBySignature:
						return CreateItem(IconIds.FindMethodsMatchingSignature, R.CMD_FindMethodsSameSignature, FindMethodsBySignature, R.CMDT_FindMethodsSameSignature)
							.AddOptionButton(R.CMDT_ExcludeGenerics, CommandOptions.NoTypeArgument, IconIds.ExcludeGeneric)
							.HasSourceCodeScopeOption();
					case CommandId.FindConstructorReferrers:
						return CreateItem(IconIds.FindReferrers, R.CMD_FindCallers, FindConstructorReferrers, R.CMDT_FindCallers)
							.AddOptionButton(R.CMDT_FindDirectCallers, CommandOptions.ExtractMatch, IconIds.EditMatches)
							.HasMatchTypeArgumentOption(_Symbol)
							.HasFileScopeOptions();
					case CommandId.FindObjectInitializers:
						return CreateItem(IconIds.FindReferrers, R.CMD_FindConstructorCallers, FindObjectInitializers, R.CMDT_FindConstructorCallers)
							.HasExtractMatchOption(_Symbol)
							.HasMatchTypeArgumentOption(_Symbol)
							.HasFileScopeOptions();
					case CommandId.FindInstanceProducers:
						return CreateItem(IconIds.InstanceProducer, R.CMD_FindInstanceProducer, FindInstanceProducers, R.CMDT_FindInstanceProducer)
							.HasExtractMatchOption(_Symbol)
							.HasSourceCodeScopeOption();
					case CommandId.FindInstanceConsumers:
						return CreateItem(IconIds.Argument, R.CMD_FindInstanceAsParameter, FindInstanceConsumers, R.CMDT_FindInstanceAsParameter)
							.HasExtractMatchOption(_Symbol)
							.HasSourceCodeScopeOption();
					case CommandId.FindContainingTypeInstanceProducers:
						return CreateItem(IconIds.InstanceProducer, R.CMD_FindInstanceProducer, FindContainingTypeInstanceProducers, R.CMDT_FindContainingTypeInstanceProducer)
							.HasExtractMatchOption(_Symbol)
							.HasSourceCodeScopeOption();
					case CommandId.FindContainingTypeInstanceConsumers:
						return CreateItem(IconIds.Argument, R.CMD_FindInstanceAsParameter, FindContainingTypeInstanceConsumers, R.CMDT_FindContainingTypeInstanceAsParameter)
							.HasExtractMatchOption(_Symbol)
							.HasSourceCodeScopeOption();
					case CommandId.FindTypeReferrers:
						return CreateItem(IconIds.FindTypeReferrers, R.CMD_FindTypeReferrers, FindTypeReferrers, R.CMDT_FindTypeReferrers)
							.HasExtractMatchOption(_Symbol)
							.HasMatchTypeArgumentOption(_Symbol)
							.HasFileScopeOptions();
					case CommandId.ListReferencedSymbols:
						return CreateItem(IconIds.ListReferencedSymbols, R.CMD_ListReferencedSymbols, ListReferencedSymbols, R.CMDT_ListReferencedSymbols);
					case CommandId.ListSymbolMembers:
						return CreateItem(IconIds.ListMembers, R.CMD_ListMembers, ListSymbolMembers, R.CMDT_ListTypeMembers);
					case CommandId.ListSymbolLocations:
						return CreateItem(IconIds.FileLocations, R.CMD_ListSymbolLocations, ListSymbolLocations);
					case CommandId.DebugUnitTest:
						return CreateItem(IconIds.DebugTest, R.CMD_DebugUnitTest, DebugUnitTest);
					case CommandId.RunUnitTest:
						return CreateItem(IconIds.RunTest, R.CMD_RunUnitTest, RunUnitTest);
					case CommandId.WebSearch:
						return CreateWebSearchCommand();
				}
				return null;
			}
			public CustomMenuItem CreateCommand(CommandId commandId, string substitution) {
				switch (commandId) {
					case CommandId.ListReturnTypeMembers:
						return CreateItem(IconIds.ListMembers, R.CMD_ListMembersOf, substitution, ListReturnTypeMembers, R.CMDT_ListTypeMembers);
					case CommandId.FindReturnTypeExtensionMethods:
						return CreateItem(IconIds.ExtensionMethod, R.CMD_FindExtensionsFor, substitution, FindReturnTypeExtensionMethods, R.CMDT_FindTypeExtensionMethods)
							.HasExtractMatchOption(_Symbol)
							.HasSourceCodeScopeOption();
					case CommandId.GoToSymbolReturnType:
						return CreateItem(IconIds.GoToReturnType, R.CMD_GoTo, substitution, GoToSymbolReturnType, R.CMDT_GoToTypeDefinition);
					case CommandId.ListSpecialGenericReturnTypeMembers:
						return CreateItem(IconIds.ListMembers, R.CMD_ListMembersOf, substitution, ListSpecialGenericReturnTypeMembers, R.CMDT_ListTypeMembers);
					case CommandId.FindParameterAssignments:
						return CreateItem(IconIds.FindParameterAssignment, R.CMD_FindAssignmentsFor, substitution, FindParameterAssignments, R.CMDT_FindAssignmentsFor);
					case CommandId.FindOptionalParameterAssignments:
						return CreateItem(IconIds.FindParameterAssignment, R.CMD_FindAssignmentsFor, substitution, FindOptionalParameterAssignments, R.CMDT_FindAssignmentsFor)
							.AddOptionButton(R.CMDT_ExplicitAssignment, CommandOptions.Explicit, IconIds.ExplicitAssignment, ExclusiveButtonConfigurator.ExplicitImplicit)
							.AddOptionButton(R.CMDT_DefaultAssignment, CommandOptions.Implicit, IconIds.DefaultAssignment, ExclusiveButtonConfigurator.ExplicitImplicit);
					case CommandId.GoToSpecialGenericSymbolReturnType:
						return CreateItem(IconIds.GoToReturnType, R.CMD_GoTo, substitution, GoToSpecialGenericSymbolReturnType, R.CMDT_GoToTypeDefinition);
					case CommandId.ListEventArgsMembers:
						return CreateItem(IconIds.ListMembers, R.CMD_ListMembersOf, substitution, ListEventArgsMembers, R.CMDT_ListEventArgumentMember);
				}
				return null;
			}

			void GoToNode(object sender, RoutedEventArgs args) {
				_Node.GetReference().GoToSource();
			}
			void SelectNode(object sender, RoutedEventArgs args) {
				_Node.SelectNode(true);
			}
			void SelectNodeWithoutTrivia(object sender, RoutedEventArgs args) {
				_Node.SelectNode(false);
			}
			void SelectSymbolNode(object sender, RoutedEventArgs args) {
				_Symbol.GetSyntaxNode().SelectNode(true);
			}
			void SelectSymbolNodeWithoutTrivia(object sender, RoutedEventArgs args) {
				_Symbol.GetSyntaxNode().SelectNode(false);
			}
			void RunUnitTest(object sender, RoutedEventArgs args) {
				if (_SemanticContext.Node.FirstAncestorOrSelf<SyntaxNode>(n => n.IsAnyKind(SyntaxKind.ClassDeclaration, SyntaxKind.MethodDeclaration)) != _Node) {
					_SemanticContext.View.MoveCaret(_Node.SpanStart);
				}
				TextEditorHelper.ExecuteEditorCommand("TestExplorer.RunAllTestsInContext");
			}
			void DebugUnitTest(object sender, RoutedEventArgs args) {
				if (_SemanticContext.Node.FirstAncestorOrSelf<SyntaxNode>(n => n.IsAnyKind(SyntaxKind.ClassDeclaration, SyntaxKind.MethodDeclaration)) != _Node) {
					_SemanticContext.View.MoveCaret(_Node.SpanStart);
				}
				TextEditorHelper.ExecuteEditorCommand("TestExplorer.DebugAllTestsInContext");
			}
			void GoToSymbolDefinition(object sender, RoutedEventArgs args) {
				var locs = _Symbol.GetSourceReferences();
				if (locs.Length == 1) {
					locs[0].GoToSource();
				}
				else {
					_SemanticContext.ShowLocations(_Symbol, locs, (sender as UIElement).GetParent<ListBoxItem>());
				}
			}
			void ListSymbolLocations(object sender, RoutedEventArgs args) {
				var symbol = _Symbol;
				if (symbol is INamespaceSymbol ns) {
					symbol = ns.GetCompilationNamespace(_SemanticContext.SemanticModel);
				}
				_SemanticContext.ShowLocations(symbol, symbol.GetSourceReferences(), (sender as UIElement).GetParent<ListBoxItem>());
			}
			void GoToSymbolReturnType(object sender, RoutedEventArgs args) {
				_Symbol.GetReturnType().ResolveElementType().GoToSource();
			}
			void GoToSpecialGenericSymbolReturnType(object sender, RoutedEventArgs args) {
				_Symbol.GetReturnType().ResolveElementType().ResolveSingleGenericTypeArgument().GoToSource();
			}
			static void TryCopy(string content) {
				try {
					Clipboard.SetDataObject(content);
				}
				catch (SystemException) {
					// ignore failure
				}
			}
			void CopySymbolName(object sender, RoutedEventArgs args) {
				TryCopy(_Symbol.GetOriginalName());
			}
			void CopyDefinition(object sender, RoutedEventArgs args) {
				var s = _Symbol.OriginalDefinition;
				TryCopy(s.Kind == SymbolKind.NamedType
					? ((INamedTypeSymbol)s).GetDefinition(CodeAnalysisHelper.DefinitionNameFormat)
					: s.ToDisplayString(CodeAnalysisHelper.DefinitionNameFormat));
			}
			void CopyTypeQualifiedSymbolName(object sender, RoutedEventArgs args) {
				CopyQualifiedSymbolName(false);
			}
			void CopyFullyQualifiedSymbolName(object sender, RoutedEventArgs args) {
				CopyQualifiedSymbolName(true);
			}

			void CopyQualifiedSymbolName(bool fullyQualified) {
				var s = _Symbol.OriginalDefinition;
				string t;
				switch (s.Kind) {
					case SymbolKind.Namespace:
					case SymbolKind.NamedType:
						t = _Symbol.ToDisplayString(CodeAnalysisHelper.QualifiedTypeNameFormat);
						break;
					case SymbolKind.Method:
						var m = s as IMethodSymbol;
						if (m.ReducedFrom != null) {
							s = m.ReducedFrom;
						}
						if (m.MethodKind == MethodKind.Constructor) {
							s = m.ContainingType;
							goto case SymbolKind.NamedType;
						}
						else if (m.MethodKind == MethodKind.ExplicitInterfaceImplementation) {
							t = m.Name;
							break;
						}
						goto default;
					default:
						t = s.ToDisplayString(fullyQualified ? CodeAnalysisHelper.QualifiedTypeNameFormat : CodeAnalysisHelper.TypeMemberNameFormat);
						break;
				}
				TryCopy(t);
			}

			void CopyConstantValue(object sender, RoutedEventArgs args) {
				var f = _Symbol as IFieldSymbol;
				if (f.HasConstantValue == false) {
					return;
				}
				TryCopy(f.ConstantValue?.ToString() ?? "null");
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void ListSymbolMembers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindMembersAsync(_Symbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void ListReturnTypeMembers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindMembersAsync(_Symbol.GetReturnType().ResolveElementType());
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void ListEventArgsMembers(object sender, RoutedEventArgs e) {
				var t = ((IEventSymbol)_Symbol).GetEventArgsType();
				if (t != null) {
					await _SemanticContext.FindMembersAsync(t);
				}
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void ListSpecialGenericReturnTypeMembers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindMembersAsync(_Symbol.GetReturnType().ResolveElementType().ResolveSingleGenericTypeArgument());
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindParameterAssignments(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var docs = MakeDocumentListFromOption(options);
				await _SemanticContext.FindParameterAssignmentsAsync(_Symbol as IParameterSymbol, docs);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindOptionalParameterAssignments(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var docs = MakeDocumentListFromOption(options);
				var o = options.MatchFlags(CommandOptions.Explicit) ? ArgumentAssignmentFilter.ExplicitValue
					: options.MatchFlags(CommandOptions.Implicit) ? ArgumentAssignmentFilter.DefaultValue
					: ArgumentAssignmentFilter.Undefined;
				await _SemanticContext.FindParameterAssignmentsAsync(_Symbol as IParameterSymbol, docs, false, o);
			}

			void ListReferencedSymbols(object sender, RoutedEventArgs e) {
				var m = new SymbolMenu(_SemanticContext);
				var c = 0;
				var containerType = _Symbol.ContainingType ?? _Symbol;
				var loc = _Node.SyntaxTree.FilePath;
				foreach (var sr in _Node.FindReferencingSymbols(_SemanticContext.SemanticModel, true)) {
					var s = sr.Key;
					var sl = s.GetSourceReferences()[0];
					SymbolItem i;
					if (sl.SyntaxTree.FilePath != loc) {
						i = m.Add(sl.ToLocation());
						i.Content.FontWeight = FontWeights.Bold;
						i.Content.HorizontalAlignment = HorizontalAlignment.Center;
						loc = sl.SyntaxTree.FilePath;
					}
					i = m.Add(s, false);
					if (s.ContainingType.Equals(containerType) == false) {
						i.Hint = (s.ContainingType ?? s).ToDisplayString(CodeAnalysisHelper.MemberNameFormat);
					}
					if (sr.Value > 1) {
						i.Hint += " @" + sr.Value.ToText();
					}
					++c;
				}
				var symbol = _Symbol;
				m.Title.SetGlyph(symbol.GetImageId())
					.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
					.Append(R.T_ReferencedSymbols)
					.Append(c.ToString());
				m.Show();
			}

			IEnumerable<Document> MakeDocumentListFromOption(CommandOptions options) {
				return options.MatchFlags(CommandOptions.CurrentProject)
					? _SemanticContext.Document.Project.Documents
					: options.MatchFlags(CommandOptions.CurrentFile)
					? [_SemanticContext.Document]
					: options.MatchFlags(CommandOptions.RelatedProjects)
					? _SemanticContext.Document.Project.GetRelatedProjectDocuments()
					: null;
			}
			IEnumerable<Project> MakeProjectListFromOption(CommandOptions options) {
				return options.MatchFlags(CommandOptions.CurrentProject)
					? [_SemanticContext.Document.Project]
					: options.MatchFlags(CommandOptions.RelatedProjects)
					? _SemanticContext.Document.Project.GetRelatedProjects()
					: null;
			}

			static SymbolSourceFilter MakeSourceFlagFromOption(CommandOptions options) {
				return options.MatchFlags(CommandOptions.SourceCode) ? SymbolSourceFilter.RequiresSource
					: options.MatchFlags(CommandOptions.External) ? SymbolSourceFilter.ExcludesSource
					: default;
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindReferrers(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var docs = MakeDocumentListFromOption(options);
				var m = options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
				var a = options.MatchFlags(CommandOptions.MatchTypeArgument) || UIHelper.IsCtrlDown;
				await _SemanticContext.FindReferrersAsync(_Symbol, m, a, docs);
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindTypeReferrers(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var docs = MakeDocumentListFromOption(options);
				var m = options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
				var a = options.MatchFlags(CommandOptions.MatchTypeArgument) || UIHelper.IsCtrlDown;
				await _SemanticContext.FindReferrersAsync(_Symbol.Kind == SymbolKind.Method ? _Symbol.ContainingType : _Symbol, m, a, docs, s => s.Kind == SymbolKind.NamedType, n => IsTypeReference(n));
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindOverrides(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var p = MakeProjectListFromOption(options);
				var s = MakeSourceFlagFromOption(options);
				await _SemanticContext.FindOverridesAsync(_Symbol, p, s);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindDerivedClasses(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var i = options.MatchFlags(CommandOptions.DirectDerive) || UIHelper.IsCtrlDown;
				var p = MakeProjectListFromOption(options);
				var s = MakeSourceFlagFromOption(options);
				await _SemanticContext.FindDerivedClassesAsync((INamedTypeSymbol)_Symbol, i, p, s, UIHelper.IsShiftDown == false);
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindImplementations(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var d = options.MatchFlags(CommandOptions.DirectDerive) || UIHelper.IsCtrlDown;
				var p = MakeProjectListFromOption(options);
				var s = MakeSourceFlagFromOption(options);
				await _SemanticContext.FindImplementationsAsync(_Symbol, d, p, s);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindSubInterfaces(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var o = options.MatchFlags(CommandOptions.DirectDerive) || UIHelper.IsCtrlDown;
				var s = MakeSourceFlagFromOption(options);
				await _SemanticContext.FindSubInterfacesAsync(_Symbol, o, s);
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindMethodsBySignature(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var a = options.MatchFlags(CommandOptions.NoTypeArgument);
				var s = MakeSourceFlagFromOption(options);
				await _SemanticContext.FindMethodsBySignatureAsync(_Symbol, a, s);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindExtensionMethods(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var m = options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
				var s = MakeSourceFlagFromOption(options);
				await _SemanticContext.FindExtensionMethodsAsync(_Symbol, m, s);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindReturnTypeExtensionMethods(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var m = options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
				var s = MakeSourceFlagFromOption(options);
				await _SemanticContext.FindExtensionMethodsAsync(_Symbol.GetReturnType(), m, s);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindSymbolWithName(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var m = options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
				var c = options.MatchFlags(CommandOptions.MatchCase);
				var s = MakeSourceFlagFromOption(options);
				await _SemanticContext.FindSymbolWithNameAsync(_Symbol, m, c, s);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindConstructorReferrers(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var d = options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
				var a = options.MatchFlags(CommandOptions.MatchTypeArgument) || UIHelper.IsCtrlDown;
				var docs = MakeDocumentListFromOption(options);
				await _SemanticContext.FindReferrersAsync(_SemanticContext.SemanticModel.GetSymbolOrFirstCandidate(_Node.GetObjectCreationNode()), d, a, docs);
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindObjectInitializers(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var m = options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
				var a = options.MatchFlags(CommandOptions.MatchTypeArgument) || UIHelper.IsCtrlDown;
				var docs = MakeDocumentListFromOption(options);
				if (_Symbol is INamedTypeSymbol t && t.GetPrimaryConstructor() != null) {
					await _SemanticContext.FindReferrersAsync(_Symbol, m, a, docs, null, n => !IsTypeReference(n));
				}
				else {
					await _SemanticContext.FindReferrersAsync(_Symbol, m, a, docs, s => s.Kind == SymbolKind.Method);
				}
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindInstanceProducers(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var m = options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
				var s = options.MatchFlags(CommandOptions.SourceCode);
				await _SemanticContext.FindInstanceProducerAsync(_Symbol, m, s);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindContainingTypeInstanceProducers(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var m = options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
				var s = options.MatchFlags(CommandOptions.SourceCode);
				await _SemanticContext.FindInstanceProducerAsync(_Symbol.ContainingType, m, s);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindInstanceConsumers(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var m = options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
				var s = options.MatchFlags(CommandOptions.SourceCode);
				await _SemanticContext.FindInstanceAsParameterAsync(_Symbol, m, s);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindContainingTypeInstanceConsumers(object sender, RoutedEventArgs e) {
				var options = GetOptions(sender);
				var m = options.MatchFlags(CommandOptions.ExtractMatch) || UIHelper.IsCtrlDown;
				var s = options.MatchFlags(CommandOptions.SourceCode);
				await _SemanticContext.FindInstanceAsParameterAsync(_Symbol.ContainingType, m, s);
			}

			CustomMenuItem CreateWebSearchCommand() {
				var symbolName = _Symbol.GetOriginalName();
				if (String.IsNullOrEmpty(symbolName)) {
					return null;
				}
				var symbolFullName = _Symbol.GetQualifiedName();
				var search = new CustomMenuItem(IconIds.SearchWebSite, R.OT_WebSearch);
				search.Items.AddRange(
					Config.Instance.SearchEngines.ConvertAll(s => {
						var item = CreateItem(
							IconIds.SearchWebSite,
							R.CMD_SearchWith.Replace("<NAME>", s.Name),
							(sender, args) => {
								var m = (MenuItem)sender;
								var keyword = UIHelper.IsShiftDown ? m.GetAlternativeSearchParameter() : m.GetSearchParameter();
								ExternalCommand.OpenWithWebBrowser(m.GetSearchUrl(), keyword);
							});
						item.SetLazyToolTip(() => new CommandToolTip(IconIds.SearchWebSite, R.CMD_WebSearchWithSymbolName + "\n" + R.CMDT_WebSearchWithSymbolName)).SetTipOptions();
						item.SetSearchUrlPattern(s.Pattern, symbolName, symbolFullName);
						return item;
					})
				);
				search.Items.Add(CreateItem(IconIds.CustomizeWebSearch, R.CMD_Customize, (sender, args) => Commands.OptionsWindowCommand.ShowOptionPage(R.OT_WebSearch))
					.SetLazyToolTip(() => new CommandToolTip(IconIds.CustomizeWebSearch, R.CMD_Customize + "\n" + R.CMDT_CustomizeSearchEngines))
					.SetTipOptions());
				return search;
			}
			#endregion

			static CommandOptions GetOptions(object sender) {
				return (sender as UIElement)?.GetParentOrSelf<CustomMenuItem>()?.Option ?? 0;
			}

			static bool IsTypeReference(SyntaxNode node) {
				var p = node.Parent.UnqualifyExceptNamespace();
				switch (p.Kind()) {
					case SyntaxKind.TypeOfExpression:
					case SyntaxKind.SimpleMemberAccessExpression:
					case SyntaxKind.CatchDeclaration:
					case SyntaxKind.CastExpression:
					case SyntaxKind.IsExpression:
					case SyntaxKind.IsPatternExpression:
					case SyntaxKind.AsExpression:
					case SyntaxKind.InvocationExpression:
						return true;
					case SyntaxKind.GenericName:
						return p.Parent.IsKind(SyntaxKind.ObjectCreationExpression) || IsTypeReference(p);
					case SyntaxKind.TypeArgumentList:
						p = p.Parent;
						goto case SyntaxKind.GenericName;
					case SyntaxKind.QualifiedName:
						return IsTypeReference(p);
					case SyntaxKind.DeclarationPattern:
						return p.Parent.IsAnyKind(SyntaxKind.IsPatternExpression, SyntaxKind.CasePatternSwitchLabel);
				}
				return false;
			}
		}

		sealed class ExclusiveButtonConfigurator
		{
			readonly CommandOptions _ExclusiveOptions;

			ExclusiveButtonConfigurator(CommandOptions exclusiveOptions) {
				_ExclusiveOptions = exclusiveOptions;
			}

			public readonly static Action<ThemedToggleButton> ExplicitImplicit = new ExclusiveButtonConfigurator(CommandOptions.Explicit | CommandOptions.Implicit).ConfigureExclusiveAssignmentButton;
			public readonly static Action<ThemedToggleButton> FileProject = new ExclusiveButtonConfigurator(CommandOptions.CurrentFile | CommandOptions.CurrentProject | CommandOptions.RelatedProjects).ConfigureExclusiveAssignmentButton;
			public readonly static Action<ThemedToggleButton> Projects = new ExclusiveButtonConfigurator(CommandOptions.CurrentProject | CommandOptions.RelatedProjects).ConfigureExclusiveAssignmentButton;
			public readonly static Action<ThemedToggleButton> SourceCode = new ExclusiveButtonConfigurator(CommandOptions.SourceCode | CommandOptions.External).ConfigureExclusiveAssignmentButton;

			void ConfigureExclusiveAssignmentButton(ThemedToggleButton button) {
				button.Checked += ExclusiveToggleButton;
			}
			void ExclusiveToggleButton(object sender, RoutedEventArgs e) {
				var b = (ThemedToggleButton)sender;
				if (b.IsChecked != true) {
					return;
				}
				foreach (var c in b.GetParent<ThemedControlGroup>().Controls) {
					if (c != b
						&& c is ThemedToggleButton tb
						&& ((CommandOptions)c.Tag).HasAnyFlag(_ExclusiveOptions)) {
						tb.IsChecked = false;
					}
				}
			}
		}
	}
}
