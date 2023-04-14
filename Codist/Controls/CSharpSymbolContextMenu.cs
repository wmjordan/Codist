using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using R = Codist.Properties.Resources;
using System.Diagnostics.CodeAnalysis;

namespace Codist.Controls
{
	sealed class CSharpSymbolContextMenu : ContextMenu, IDisposable
	{
		static readonly string[] __UnitTestingNamespace = new[] { "UnitTesting", "TestTools", "VisualStudio", "Microsoft" };
		UIHost _Host;

		public CSharpSymbolContextMenu(ISymbol symbol, SyntaxNode node, SemanticContext semanticContext) {
			Resources = SharedDictionaryManager.ContextMenu;
			Foreground = ThemeHelper.ToolWindowTextBrush;
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			_Host = new UIHost(symbol, node, semanticContext);
		}

		public void AddNodeCommands() {
			if (_Host.Node != null) {
				Items.Add(CreateItem(IconIds.GoToDefinition, R.CMD_GoToDefinition, _Host.GoToNode));
				Items.Add(CreateItem(IconIds.SelectCode, R.CMD_SelectCode, _Host.SelectNode));
			}
		}
		public void AddSymbolNodeCommands() {
			var symbol = _Host.Symbol;
			if (symbol.HasSource()) {
				Items.Add(CreateItem(IconIds.GoToDefinition, R.CMD_GoToDefinition, _Host.GoToSymbolDefinition));
				if (symbol.Kind != SymbolKind.Namespace && _Host.Node == null) {
					Items.Add(CreateItem(IconIds.SelectCode, R.CMD_SelectCode, _Host.SelectSymbolNode));
				}
			}
			else if (_Host.Node != null) {
				Items.Add(CreateItem(IconIds.SelectCode, R.CMD_SelectCode, _Host.SelectNode));
			}
			AddCopyAndSearchSymbolCommands();
		}

		public void AddCopyAndSearchSymbolCommands() {
			var symbol = _Host.Symbol;
			Items.Add(CreateItem(IconIds.Copy, R.CMD_CopySymbolName, _Host.CopySymbolName));
			if (symbol.IsQualifiable()) {
				Items.Add(CreateItem(IconIds.Copy, R.CMD_CopyQualifiedSymbolName, _Host.CopyQualifiedSymbolName));
			}
			if (symbol.Kind == SymbolKind.Field && ((IFieldSymbol)symbol).HasConstantValue) {
				Items.Add(CreateItem(IconIds.Constant, R.CMD_CopyConstantValue, _Host.CopyConstantValue));
			}
			if (symbol.CanBeReferencedByName) {
				var search = CreateItem(IconIds.SearchWebSite, R.OT_WebSearch);
				var symbolName = symbol.Name;
				search.Items.AddRange(
					Config.Instance.SearchEngines.ConvertAll(s => CreateItem(
						IconIds.SearchWebSite,
						R.CMD_SearchWith.Replace("<NAME>", s.Name),
						(sender, args) => ExternalCommand.OpenWithWebBrowser(s.Pattern, symbolName))
					)
				);
				search.Items.Add(CreateItem(IconIds.CustomizeWebSearch, R.CMD_Customize, (sender, args) => CodistPackage.Instance.ShowOptionPage(typeof(Options.WebSearchPage))));
				Items.Add(search);
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
				case SymbolKind.Local:
				case SymbolKind.Parameter:
					CreateCommandsForReturnTypeCommand();
					break;
				case SymbolKind.NamedType:
					CreateCommandForNamedType(_Host.Symbol as INamedTypeSymbol);
					break;
				case SymbolKind.Namespace:
					Items.Add(CreateItem(IconIds.ListMembers, R.CMD_FindMembers, _Host.FindSymbolMembers));
					break;
			}
			if (_Host.Context.SemanticModel != null) {
				if (_Host.Node?.Kind().IsDeclaration() == true
					&& _Host.Node.SyntaxTree == _Host.Context.SemanticModel.SyntaxTree
					&& _Host.Symbol.Kind != SymbolKind.Namespace) {
					Items.Add(CreateItem(IconIds.FindReferencingSymbols, R.CMD_FindReferencedSymbols, _Host.FindReferencedSymbols));
				}

				if (String.IsNullOrEmpty(_Host.Symbol.Name) == false) {
					Items.Add(CreateItem(IconIds.FindSymbolsWithName, R.CMD_FindSymbolwithName, _Host.Symbol.Name).HandleEvent(MenuItem.ClickEvent, _Host.FindSymbolWithName));
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
					Items.Add(CreateItem(IconIds.DebugTest, R.CMD_DebugUnitTest)
						.HandleEvent(MenuItem.ClickEvent, _Host.DebugUnitTest));
					Items.Add(CreateItem(IconIds.RunTest, R.CMD_RunUnitTest)
						.HandleEvent(MenuItem.ClickEvent, _Host.RunUnitTest));
				}
			}
		}

		public void AddTitleItem(string name) {
			Items.Add(new MenuItem {
				Header = name,
				IsEnabled = false,
				Icon = null,
				HorizontalContentAlignment = HorizontalAlignment.Right
			});
		}

		public void DisposeOnClose() {
			Closed += CSharpSymbolContextMenu_Closed;
		}

		void CreateCommandForMembers() {
			if (_Host.Symbol.Kind != SymbolKind.Method || IsExternallyCallable(((IMethodSymbol)_Host.Symbol).MethodKind)) {
				Items.Add(CreateItem(IconIds.FindReferrers, R.CMD_FindReferrers, _Host.FindReferrers));
			}
			if (_Host.Symbol.MayHaveOverride()) {
				Items.Add(CreateItem(IconIds.FindOverloads, R.CMD_FindOverrides, _Host.FindOverrides));
			}
			var st = _Host.Symbol.ContainingType;
			if (st?.TypeKind == TypeKind.Interface) {
				Items.Add(CreateItem(IconIds.FindImplementations, R.CMD_FindImplementations, _Host.FindImplementations));
			}
			if (_Host.Symbol.Kind != SymbolKind.Event) {
				CreateCommandsForReturnTypeCommand();
			}
			if (_Host.Symbol.Kind == SymbolKind.Method) {
				switch (((IMethodSymbol)_Host.Symbol).MethodKind) {
					case MethodKind.Constructor:
						if (st.SpecialType == SpecialType.None) {
							CreateInstanceCommandsForContainingType();
						}
						break;
					case MethodKind.StaticConstructor:
					case MethodKind.Destructor:
						break;
					default:
						Items.Add(CreateItem(IconIds.FindMethodsMatchingSignature, R.CMD_FindMethodsSameSignature, _Host.FindMethodsBySignature));
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
			if (t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.Struct) {
				var ctor = _Host.Node?.GetObjectCreationNode();
				if (ctor != null) {
					var symbol = _Host.Context.SemanticModel.GetSymbolOrFirstCandidate(ctor);
					if (symbol != null) {
						Items.Add(CreateItem(IconIds.FindReferrers, R.CMD_FindCallers, _Host.FindConstructorReferrers));
					}
				}
				else if (t.InstanceConstructors.Length > 0) {
					Items.Add(CreateItem(IconIds.FindReferrers, R.CMD_FindConstructorCallers, _Host.FindObjectInitializers));
				}
			}
			Items.Add(CreateItem(IconIds.FindTypeReferrers, R.CMD_FindTypeReferrers, _Host.FindTypeReferrers));
			Items.Add(CreateItem(IconIds.ListMembers, R.CMD_FindMembers, _Host.FindSymbolMembers));
			if (t.IsStatic) {
				return;
			}
			if (t.IsSealed == false) {
				if (t.TypeKind == TypeKind.Class) {
					Items.Add(CreateItem(IconIds.FindDerivedTypes, R.CMD_FindDerivedClasses, _Host.FindDerivedClasses));
				}
				else if (t.TypeKind == TypeKind.Interface) {
					Items.Add(CreateItem(IconIds.FindImplementations, R.CMD_FindImplementations, _Host.FindImplementations));
					Items.Add(CreateItem(IconIds.FindDerivedTypes, R.CMD_FindInheritedInterfaces, _Host.FindSubInterfaces));
				}
			}
			if (t.TypeKind == TypeKind.Delegate) {
				Items.Add(CreateItem(IconIds.FindMethodsMatchingSignature, R.CMD_FindMethodsSameSignature, _Host.FindMethodsBySignature));
			}
			Items.Add(CreateItem(IconIds.ExtensionMethod, R.CMD_FindExtensions, _Host.FindExtensionMethods));
			if (t.SpecialType == SpecialType.None) {
				CreateInstanceCommandsForType();
			}
		}

		void CreateCommandsForReturnTypeCommand() {
			var rt = _Host.Symbol.GetReturnType();
			if (rt.SpecialType == SpecialType.System_Void
				|| rt.SpecialType == SpecialType.System_Object
				|| rt.TypeKind == TypeKind.TypeParameter
				|| rt.TypeKind == TypeKind.Error
				|| rt.TypeKind == TypeKind.Dynamic
				|| rt.IsTupleType) {
				return;
			}
			var et = rt.ResolveElementType();
			var ga = et.ResolveSingleGenericTypeArgument();
			string typeName = et.Name + et.GetParameterString();
			Items.Add(CreateItem(IconIds.ListMembers, R.CMD_FindMembersOf, typeName).HandleEvent(MenuItem.ClickEvent, _Host.FindReturnTypeMembers));
			if (rt.IsStatic == false) {
				Items.Add(CreateItem(IconIds.ExtensionMethod, R.CMD_FindExtensionsFor, typeName).HandleEvent(MenuItem.ClickEvent, _Host.FindReturnTypeExtensionMethods));
			}
			if (et.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
				Items.Add(CreateItem(IconIds.GoToReturnType, R.CMD_GoTo, typeName).HandleEvent(MenuItem.ClickEvent, _Host.GoToSymbolReturnType));
			}
			if (ReferenceEquals(ga, et) == false) {
				typeName = ga.Name + ga.GetParameterString();
				Items.Add(CreateItem(IconIds.ListMembers, R.CMD_FindMembersOf, typeName).HandleEvent(MenuItem.ClickEvent, _Host.FindSpecialGenericReturnTypeMembers));
				if (ga.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
					Items.Add(CreateItem(IconIds.GoToReturnType, R.CMD_GoTo, typeName).HandleEvent(MenuItem.ClickEvent, _Host.GoToSpecialGenericSymbolReturnType));
				}
			}
		}

		void CreateInstanceCommandsForType() {
			Items.Add(CreateItem(IconIds.InstanceProducer, R.CMD_FindInstanceProducer, _Host.FindInstanceProducers));
			Items.Add(CreateItem(IconIds.Argument, R.CMD_FindInstanceAsParameter, _Host.FindInstanceConsumers));
		}

		void CreateInstanceCommandsForContainingType() {
			Items.Add(CreateItem(IconIds.InstanceProducer, R.CMD_FindInstanceProducer, _Host.FindContainingTypeInstanceProducers));
			Items.Add(CreateItem(IconIds.Argument, R.CMD_FindInstanceAsParameter, _Host.FindContainingTypeInstanceConsumers));
		}

		static MenuItem CreateItem(int imageId, string title) {
			return new MenuItem {
				Icon = ThemeHelper.GetImage(imageId),
				Header = new ThemedMenuText { Text = title }
			};
		}

		static MenuItem CreateItem(int imageId, string title, string substitutions) {
			var i = title.IndexOf('<');
			if (i < -1) {
				goto FALLBACK;
			}
			var i2 = title.IndexOf('>', i);
			if (i2 < 0) {
				goto FALLBACK;
			}
			return new MenuItem {
				Icon = ThemeHelper.GetImage(imageId),
				Header = new ThemedMenuText().Append(title.Substring(0, i))
					.Append(new System.Windows.Documents.Run(substitutions) { TextDecorations = { TextDecorations.Underline } })
					.Append(title.Substring(i2 + 1))
			};
			FALLBACK:
			return CreateItem(imageId, title);
		}

		static MenuItem CreateItem(int imageId, string title, RoutedEventHandler clickHandler) {
			var item = CreateItem(imageId, title);
			item.Click += clickHandler;
			return item;
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
			public void GoToNode(object sender, RoutedEventArgs args) {
				_Node.GetReference().GoToSource();
			}
			public void SelectNode(object sender, RoutedEventArgs args) {
				_Node.SelectNode(true);
			}
			public void SelectSymbolNode(object sender, RoutedEventArgs args) {
				_Symbol.GetSyntaxNode().SelectNode(true);
			}
			public void RunUnitTest(object sender, RoutedEventArgs args) {
				if (_SemanticContext.Node.FirstAncestorOrSelf<SyntaxNode>(n => n.Kind().IsAny(SyntaxKind.ClassDeclaration, SyntaxKind.MethodDeclaration)) != _Node) {
					_SemanticContext.View.MoveCaret(_Node.SpanStart);
				}
				TextEditorHelper.ExecuteEditorCommand("TestExplorer.RunAllTestsInContext");
			}
			public void DebugUnitTest(object sender, RoutedEventArgs args) {
				if (_SemanticContext.Node.FirstAncestorOrSelf<SyntaxNode>(n => n.Kind().IsAny(SyntaxKind.ClassDeclaration, SyntaxKind.MethodDeclaration)) != _Node) {
					_SemanticContext.View.MoveCaret(_Node.SpanStart);
				}
				TextEditorHelper.ExecuteEditorCommand("TestExplorer.DebugAllTestsInContext");
			}
			public void GoToSymbolDefinition(object sender, RoutedEventArgs args) {
				var locs = _Symbol.GetSourceReferences();
				if (locs.Length == 1) {
					locs[0].GoToSource();
				}
				else {
					_SemanticContext.ShowLocations(_Symbol, locs, (sender as UIElement).GetParent<ListBoxItem>());
				}
			}
			public void GoToSymbolReturnType(object sender, RoutedEventArgs args) {
				_Symbol.GetReturnType().ResolveElementType().GoToSource();
			}
			public void GoToSpecialGenericSymbolReturnType(object sender, RoutedEventArgs args) {
				_Symbol.GetReturnType().ResolveElementType().ResolveSingleGenericTypeArgument().GoToSource();
			}
			public void CopySymbolName(object sender, RoutedEventArgs args) {
				try {
					Clipboard.SetDataObject(_Symbol.GetOriginalName());
				}
				catch (SystemException) {
					// ignore failure
				}
			}
			public void CopyQualifiedSymbolName(object sender, RoutedEventArgs args) {
				try {
					var s = _Symbol.OriginalDefinition;
					string t;
					switch (s.Kind) {
						case SymbolKind.Namespace:
						case SymbolKind.NamedType: t = _Symbol.ToDisplayString(CodeAnalysisHelper.QualifiedTypeNameFormat); break;
						case SymbolKind.Method:
							var m = s as IMethodSymbol;
							if (m.ReducedFrom != null) {
								s = m.ReducedFrom;
							}
							if (m.MethodKind == MethodKind.Constructor) {
								s = m.ContainingType;
								goto case SymbolKind.NamedType;
							}
							goto default;
						default:
							t = s.ToDisplayString(CodeAnalysisHelper.TypeMemberNameFormat);
							break;
					}
					Clipboard.SetDataObject(t);
				}
				catch (SystemException) {
					// ignore failure
				}
			}
			public void CopyConstantValue(object sender, RoutedEventArgs args) {
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
			public async void FindSymbolMembers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindMembersAsync(_Symbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindReturnTypeMembers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindMembersAsync(_Symbol.GetReturnType().ResolveElementType());
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindSpecialGenericReturnTypeMembers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindMembersAsync(_Symbol.GetReturnType().ResolveElementType().ResolveSingleGenericTypeArgument());
			}

			public void FindReferencedSymbols(object sender, RoutedEventArgs e) {
				var m = new SymbolMenu(_SemanticContext);
				var c = 0;
				var containerType = _Symbol.ContainingType ?? _Symbol;
				var loc = _Node.SyntaxTree.FilePath;
				foreach (var s in _Node.FindReferencingSymbols(_SemanticContext.SemanticModel, true)
						.OrderBy(i => i.Key.ContainingType == containerType ? null : (i.Key.ContainingType ?? i.Key).Name)
						.ThenBy(i => i.Key.Name)
						.Select(i => i.Key)) {
					var sl = s.DeclaringSyntaxReferences[0];
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
					++c;
				}
				var symbol = _Symbol;
				m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
					.AddSymbol(symbol, null, true, SymbolFormatter.Instance)
					.Append(R.T_ReferencedSymbols)
					.Append(c.ToString());
				m.Show();
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindReferrers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindReferrersAsync(_Symbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindTypeReferrers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindReferrersAsync(_Symbol, s => s.Kind == SymbolKind.NamedType, IsTypeReference);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindOverrides(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindOverridesAsync(_Symbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindDerivedClasses(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindDerivedClassesAsync(_Symbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindImplementations(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindImplementationsAsync(_Symbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindSubInterfaces(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindSubInterfacesAsync(_Symbol);
			}

			public void FindMethodsBySignature(object sender, RoutedEventArgs e) {
				_SemanticContext.FindMethodsBySignature(_Symbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindExtensionMethods(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindExtensionMethodsAsync(_Symbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindReturnTypeExtensionMethods(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindExtensionMethodsAsync(_Symbol.GetReturnType());
			}

			public void FindSymbolWithName(object sender, RoutedEventArgs e) {
				_SemanticContext.FindSymbolWithName(_Symbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindConstructorReferrers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindReferrersAsync(_SemanticContext.SemanticModel.GetSymbolOrFirstCandidate(_Node.GetObjectCreationNode()));
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindObjectInitializers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindReferrersAsync(_Symbol, s => s.Kind == SymbolKind.Method);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindInstanceProducers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindInstanceProducerAsync(_Symbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindContainingTypeInstanceProducers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindInstanceProducerAsync(_Symbol.ContainingType);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindInstanceConsumers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindInstanceAsParameterAsync(_Symbol);
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			public async void FindContainingTypeInstanceConsumers(object sender, RoutedEventArgs e) {
				await _SemanticContext.FindInstanceAsParameterAsync(_Symbol.ContainingType);
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
						return p.Parent.IsKind(SyntaxKind.IsPatternExpression) || p.Parent.IsKind(SyntaxKind.CasePatternSwitchLabel);
				}
				return false;
			}
		}
	}
}
