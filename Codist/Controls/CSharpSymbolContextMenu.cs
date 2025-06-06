using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
			this.SetBackgroundForCrispImage(ThemeCache.TitleBackgroundColor);
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
			if (symbol.IsQualifiable()) {
				AddCommand(CommandId.CopyQualifiedSymbolName);
			}
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
					AddCommand(CommandId.FindReferencedSymbols);
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
				AddCommand(CommandId.FindSpecialGenericReturnTypeMembers, typeName);
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

		enum CommandId
		{
			GoToNode,
			SelectNode,
			SelectSymbolNode,
			GoToSymbolDefinition,
			CopySymbolName,
			CopyQualifiedSymbolName,
			CopyConstantValue,
			WebSearch,
			ListReturnTypeMembers,
			FindReturnTypeExtensionMethods,
			GoToSymbolReturnType,
			FindSpecialGenericReturnTypeMembers,
			GoToSpecialGenericSymbolReturnType,
			FindExtensionMethods,
			FindSubInterfaces,
			FindImplementations,
			FindDerivedClasses,
			FindOverrides,
			FindReferrers,
			FindReferencedSymbols,
			ListSymbolMembers,
			FindSymbolsWithName,
			FindMethodsBySignature,
			FindConstructorReferrers,
			FindObjectInitializers,
			FindParameterAssignments,
			FindOptionalParameterAssignments,
			DebugUnitTest,
			RunUnitTest,
			FindInstanceProducers,
			FindInstanceConsumers,
			FindContainingTypeInstanceProducers,
			FindContainingTypeInstanceConsumers,
			FindTypeReferrers,
			ListSymbolLocations,
			ListEventArgsMembers,
		}

		sealed class CustomMenuItem : MenuItem
		{
			readonly int _ImageId;
			RoutedEventHandler _ClickHandler;
			string _ToolTip;
			bool _Handled;

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

			public CustomMenuItem AddClickHandler(RoutedEventHandler clickHandler) {
				_ClickHandler += clickHandler;
				return this;
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
						return CreateItem(IconIds.SelectCode, R.CMD_SelectCode, SelectNode);
					case CommandId.SelectSymbolNode:
						return CreateItem(IconIds.SelectCode, R.CMD_SelectCode, SelectSymbolNode);
					case CommandId.GoToSymbolDefinition:
						return CreateItem(IconIds.GoToDefinition, R.CMD_GoToDefinition, GoToSymbolDefinition);
					case CommandId.CopySymbolName:
						return CreateItem(IconIds.Copy, R.CMD_CopySymbolName, CopySymbolName);
					case CommandId.CopyQualifiedSymbolName:
						return CreateItem(IconIds.Copy, R.CMD_CopyQualifiedSymbolName, CopyQualifiedSymbolName, R.CMDT_CopyQualifiedSymbolName);
					case CommandId.CopyConstantValue:
						return CreateItem(IconIds.Constant, R.CMD_CopyConstantValue, CopyConstantValue);
					case CommandId.FindExtensionMethods:
						return CreateItem(IconIds.ExtensionMethod, R.CMD_FindExtensions, FindExtensionMethods);
					case CommandId.FindSubInterfaces:
						return CreateItem(IconIds.FindDerivedTypes, R.CMD_FindInheritedInterfaces, FindSubInterfaces, R.CMDT_FindInheritedInterfaces);
					case CommandId.FindImplementations:
						return CreateItem(IconIds.FindImplementations, R.CMD_FindImplementations, FindImplementations, R.CMDT_FindImplementations);
					case CommandId.FindDerivedClasses:
						return CreateItem(IconIds.FindDerivedTypes, R.CMD_FindDerivedClasses, FindDerivedClasses, R.CMDT_FindDerivedClasses);
					case CommandId.FindOverrides:
						return CreateItem(IconIds.FindOverloads, R.CMD_FindOverrides, FindOverrides);
					case CommandId.FindReferrers:
						return CreateItem(IconIds.FindReferrers, R.CMD_FindReferrers, FindReferrers, R.CMDT_FindReferrers);
					case CommandId.FindReferencedSymbols:
						return CreateItem(IconIds.FindReferencingSymbols, R.CMD_FindReferencedSymbols, FindReferencedSymbols, R.CMDT_ListReferencedSymbols);
					case CommandId.ListSymbolMembers:
						return CreateItem(IconIds.ListMembers, R.CMD_ListMembers, ListSymbolMembers);
					case CommandId.FindSymbolsWithName:
						return CreateItem(IconIds.FindSymbolsWithName, R.CMD_FindSymbolwithName, _Symbol.Name, FindSymbolWithName, R.CMDT_FindSymbolwithName);
					case CommandId.FindMethodsBySignature:
						return CreateItem(IconIds.FindMethodsMatchingSignature, R.CMD_FindMethodsSameSignature, FindMethodsBySignature, R.CMDT_FindMethodsSameSignature);
					case CommandId.FindConstructorReferrers:
						return CreateItem(IconIds.FindReferrers, R.CMD_FindCallers, FindConstructorReferrers, R.CMDT_FindCallers);
					case CommandId.FindObjectInitializers:
						return CreateItem(IconIds.FindReferrers, R.CMD_FindConstructorCallers, FindObjectInitializers, R.CMDT_FindConstructorCallers);
					case CommandId.FindInstanceProducers:
						return CreateItem(IconIds.InstanceProducer, R.CMD_FindInstanceProducer, FindInstanceProducers, R.CMDT_FindInstanceProducer);
					case CommandId.FindInstanceConsumers:
						return CreateItem(IconIds.Argument, R.CMD_FindInstanceAsParameter, FindInstanceConsumers, R.CMDT_FindInstanceAsParameter);
					case CommandId.FindContainingTypeInstanceProducers:
						return CreateItem(IconIds.InstanceProducer, R.CMD_FindInstanceProducer, FindContainingTypeInstanceProducers, R.CMDT_FindContainingTypeInstanceProducer);
					case CommandId.FindContainingTypeInstanceConsumers:
						return CreateItem(IconIds.Argument, R.CMD_FindInstanceAsParameter, FindContainingTypeInstanceConsumers, R.CMDT_FindContainingTypeInstanceAsParameter);
					case CommandId.FindTypeReferrers:
						return CreateItem(IconIds.FindTypeReferrers, R.CMD_FindTypeReferrers, FindTypeReferrers, R.CMDT_FindTypeReferrers);
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
						return CreateItem(IconIds.ListMembers, R.CMD_ListMembersOf, substitution, ListReturnTypeMembers, R.CMDT_ListSymbolTypeMembers);
					case CommandId.FindReturnTypeExtensionMethods:
						return CreateItem(IconIds.ExtensionMethod, R.CMD_FindExtensionsFor, substitution, FindReturnTypeExtensionMethods, R.CMDT_FindSymbolTypeExtensionMethods);
					case CommandId.GoToSymbolReturnType:
						return CreateItem(IconIds.GoToReturnType, R.CMD_GoTo, substitution, GoToSymbolReturnType, R.CMDT_GoToSymbolTypeDefinition);
					case CommandId.FindSpecialGenericReturnTypeMembers:
						return CreateItem(IconIds.ListMembers, R.CMD_ListMembersOf, substitution, FindSpecialGenericReturnTypeMembers, R.CMDT_ListSymbolTypeMembers);
					case CommandId.FindParameterAssignments:
						return CreateItem(IconIds.FindParameterAssignment, R.CMD_FindAssignmentsFor, substitution, FindParameterAssignments, R.CMDT_FindAssignmentsFor);
					case CommandId.FindOptionalParameterAssignments:
						return CreateItem(IconIds.FindParameterAssignment, R.CMD_FindAssignmentsFor, substitution, FindOptionalParameterAssignments, R.CMDT_FindAssignmentsFor + Environment.NewLine + R.CMDT_FindAssignmentsForOption);
					case CommandId.GoToSpecialGenericSymbolReturnType:
						return CreateItem(IconIds.GoToReturnType, R.CMD_GoTo, substitution, GoToSpecialGenericSymbolReturnType, R.CMDT_GoToSymbolTypeDefinition);
					case CommandId.ListEventArgsMembers:
						return CreateItem(IconIds.ListMembers, R.CMD_ListMembersOf, substitution, ListEventArgsMembers, "List members of arguments of event");
				}
				return null;
			}

			void GoToNode(object sender, RoutedEventArgs args) {
				_Node.GetReference().GoToSource();
			}
			void SelectNode(object sender, RoutedEventArgs args) {
				_Node.SelectNode(true);
			}
			void SelectSymbolNode(object sender, RoutedEventArgs args) {
				_Symbol.GetSyntaxNode().SelectNode(true);
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
			void CopySymbolName(object sender, RoutedEventArgs args) {
				try {
					Clipboard.SetDataObject(_Symbol.GetOriginalName());
				}
				catch (SystemException) {
					// ignore failure
				}
			}
			void CopyQualifiedSymbolName(object sender, RoutedEventArgs args) {
				try {
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
							t = s.ToDisplayString(args.RoutedEvent == PreviewMouseRightButtonUpEvent ? CodeAnalysisHelper.QualifiedTypeNameFormat : CodeAnalysisHelper.TypeMemberNameFormat);
							break;
					}
					Clipboard.SetDataObject(t);
				}
				catch (SystemException) {
					// ignore failure
				}
			}
			void CopyConstantValue(object sender, RoutedEventArgs args) {
				var f = _Symbol as IFieldSymbol;
				if (f.HasConstantValue == false) {
					return;
				}
				try {
					Clipboard.SetDataObject(f.ConstantValue?.ToString() ?? "null");
				}
				catch (SystemException) {
					// ignore failure
				}
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
			async void FindSpecialGenericReturnTypeMembers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindMembersAsync(_Symbol.GetReturnType().ResolveElementType().ResolveSingleGenericTypeArgument());
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindParameterAssignments(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindParameterAssignmentsAsync(_Symbol as IParameterSymbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindOptionalParameterAssignments(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindParameterAssignmentsAsync(_Symbol as IParameterSymbol, false, UIHelper.IsCtrlDown ? ArgumentAssignmentFilter.ExplicitValue : UIHelper.IsShiftDown ? ArgumentAssignmentFilter.DefaultValue : ArgumentAssignmentFilter.Undefined);
			}

			void FindReferencedSymbols(object sender, RoutedEventArgs e) {
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

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindReferrers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindReferrersAsync(_Symbol, UIHelper.IsCtrlDown);
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindTypeReferrers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindReferrersAsync(_Symbol.Kind == SymbolKind.Method ? _Symbol.ContainingType : _Symbol, UIHelper.IsCtrlDown, s => s.Kind == SymbolKind.NamedType, IsTypeReference);
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindOverrides(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindOverridesAsync(_Symbol);
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindDerivedClasses(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindDerivedClassesAsync(_Symbol, UIHelper.IsCtrlDown, UIHelper.IsShiftDown == false);
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindImplementations(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindImplementationsAsync(_Symbol, UIHelper.IsCtrlDown);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindSubInterfaces(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindSubInterfacesAsync(_Symbol, UIHelper.IsCtrlDown);
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindMethodsBySignature(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindMethodsBySignatureAsync(_Symbol, UIHelper.IsCtrlDown);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindExtensionMethods(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindExtensionMethodsAsync(_Symbol, UIHelper.IsCtrlDown);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindReturnTypeExtensionMethods(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindExtensionMethodsAsync(_Symbol.GetReturnType(), UIHelper.IsCtrlDown);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindSymbolWithName(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindSymbolWithNameAsync(_Symbol, UIHelper.IsCtrlDown);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindConstructorReferrers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindReferrersAsync(_SemanticContext.SemanticModel.GetSymbolOrFirstCandidate(_Node.GetObjectCreationNode()), UIHelper.IsCtrlDown);
			}
			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindObjectInitializers(object sender, RoutedEventArgs e) {
				if (_Symbol is INamedTypeSymbol t && t.GetPrimaryConstructor() != null) {
					await _SemanticContext.FindReferrersAsync(_Symbol, UIHelper.IsCtrlDown, null, n => IsTypeReference(n) == false);
				}
				else {
					await _SemanticContext.FindReferrersAsync(_Symbol, UIHelper.IsCtrlDown, s => s.Kind == SymbolKind.Method);
				}
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindInstanceProducers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindInstanceProducerAsync(_Symbol, UIHelper.IsCtrlDown);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindContainingTypeInstanceProducers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindInstanceProducerAsync(_Symbol.ContainingType, UIHelper.IsCtrlDown);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindInstanceConsumers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindInstanceAsParameterAsync(_Symbol, UIHelper.IsCtrlDown);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void FindContainingTypeInstanceConsumers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindInstanceAsParameterAsync(_Symbol.ContainingType, UIHelper.IsCtrlDown);
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
				search.Items.Add(CreateItem(IconIds.CustomizeWebSearch, R.CMD_Customize, (sender, args) => CodistPackage.Instance.ShowOptionPage(typeof(Options.WebSearchPage)))
					.SetLazyToolTip(() => new CommandToolTip(IconIds.CustomizeWebSearch, R.CMD_Customize + "\n" + R.CMDT_CustomizeSearchEngines))
					.SetTipOptions());
				return search;
			}
			#endregion

			bool IsTypeReference(SyntaxNode node) {
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
	}
}
