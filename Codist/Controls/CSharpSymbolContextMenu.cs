using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
		CommandFactory _commandFactory;

		public CSharpSymbolContextMenu(ISymbol symbol, SyntaxNode node, SemanticContext semanticContext) {
			Resources = SharedDictionaryManager.ContextMenu;
			Foreground = ThemeCache.ToolWindowTextBrush;
			this.ReferenceCrispImageBackground(VsColors.CommandBarMenuBackgroundGradientBeginKey);
			_commandFactory = new CommandFactory(semanticContext, symbol, node);
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
			if (_commandFactory.Node != null) {
				AddCommand(CommandId.GoToNode);
				AddCommand(CommandId.SelectNode);
			}
		}
		public void AddSymbolNodeCommands() {
			var symbol = _commandFactory.Symbol;
			if (symbol.HasSource()) {
				AddCommand(CommandId.GoToSymbolDefinition);
				if (symbol.Kind != SymbolKind.Namespace && _commandFactory.Node == null) {
					AddCommand(CommandId.SelectSymbolNode);
				}
			}
			else if (_commandFactory.Node != null) {
				AddCommand(CommandId.SelectNode);
			}
			AddCopyAndSearchSymbolCommands();
		}

		public void AddCopyAndSearchSymbolCommands() {
			var symbol = _commandFactory.Symbol;
			AddCommand(CommandId.CopySymbol);

			if (symbol.HasReferenceableName()) {
				var cmd = CreateWebSearchCommand();
				if (cmd != null) {
					AddCommand(cmd);
				}
			}
		}

		public void AddAnalysisCommands() {
			if (_commandFactory.Context.Document == null) {
				return;
			}
			var symbol = _commandFactory.Symbol;
			switch (symbol.Kind) {
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.Event:
				case SymbolKind.Field:
					CreateCommandForMembers(symbol);
					break;
				case SymbolKind.Parameter:
					CreateCommandForParameter((IParameterSymbol)symbol);
					goto case SymbolKind.Local;
				case SymbolKind.Local:
					CreateCommandsForReturnTypeCommand(symbol.GetReturnType());
					break;
				case SymbolKind.NamedType:
					CreateCommandForNamedType((INamedTypeSymbol)symbol);
					break;
				case SymbolKind.Namespace:
					AddCommand(CommandId.ListSymbolMembers);
					AddCommand(CommandId.ListSymbolLocations);
					break;
				case SymbolKind.ErrorType:
					return;
			}
			if (_commandFactory.Context.SemanticModel != null) {
				if (_commandFactory.Node?.Kind().IsDeclaration() == true
					&& _commandFactory.Node.SyntaxTree == _commandFactory.Context.SemanticModel.SyntaxTree
					&& !symbol.IsAbstract
					&& symbol.Kind != SymbolKind.Namespace) {
					AddCommand(CommandId.ListReferencedSymbols);
				}

				if (String.IsNullOrEmpty(symbol.Name) == false) {
					AddCommand(CommandId.FindSymbolsWithName, symbol.Name);
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
			var symbol = _commandFactory.Symbol;
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
			AddCommand(new CustomMenuItem(_commandFactory.Create(commandId)));
		}
		void AddCommand(CommandId commandId, string substitution) {
			AddCommand(new CustomMenuItem(_commandFactory.Create(commandId, substitution)));
		}
		void AddCommand(CustomMenuItem command) {
			command.AddClickHandler(OnCommandExecuted);
			Items.Add(command);
		}
		void OnCommandExecuted(object sender, RoutedEventArgs args) {
			CommandExecuted?.Invoke(this, args);
		}

		void CreateCommandForMembers(ISymbol symbol) {
			if (symbol.Kind != SymbolKind.Method
				|| IsExternallyCallable(((IMethodSymbol)symbol).MethodKind)) {
				AddCommand(CommandId.FindReferrers);
			}
			if (symbol.MayHaveOverride()) {
				AddCommand(CommandId.FindOverrides);
			}
			var st = symbol.ContainingType;
			if (st?.TypeKind == TypeKind.Interface) {
				AddCommand(CommandId.FindImplementations);
			}
			if (symbol.Kind != SymbolKind.Event) {
				CreateCommandsForReturnTypeCommand(symbol.GetReturnType());
			}
			else {
				CreateCommandForEventArgs();
			}
			if (symbol.Kind == SymbolKind.Method) {
				switch (((IMethodSymbol)symbol).MethodKind) {
					case MethodKind.Constructor:
						if (st.SpecialType == SpecialType.None) {
							AddCommand(CommandId.FindTypeReferrers);
							if (st.BaseType != null || st.Interfaces.Length != 0) {
								AddCommand(CommandId.ListBaseTypes);
							}
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
				var ctor = _commandFactory.Node?.GetObjectCreationNode();
				if (ctor != null) {
					var symbol = _commandFactory.Context.SemanticModel.GetSymbolOrFirstCandidate(ctor);
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
			if (t.IsAnyKind(TypeKind.Class, TypeKind.Struct, TypeKind.Interface)
				&& (t.BaseType != null || t.Interfaces.Length != 0)) {
				AddCommand(CommandId.ListBaseTypes);
			}
			if (t.TypeKind == TypeKind.Delegate) {
				AddCommand(CommandId.FindMethodsBySignature);
			}
			AddCommand(CommandId.FindExtensionMethods);
			if (t.SpecialType == SpecialType.None) {
				CreateInstanceCommandsForType();
			}
		}

		void CreateCommandsForReturnTypeCommand(ITypeSymbol rt) {
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
			var t = ((IEventSymbol)_commandFactory.Symbol).GetEventArgsType();
			if (t?.GetBaseTypes().Any(i => i.MatchTypeName(nameof(EventArgs), nameof(System))) == true) {
				AddCommand(CommandId.ListEventArgsMembers, t.GetOriginalName());
			}
		}

		void CreateCommandForParameter(IParameterSymbol p) {
			if (p.ContainingSymbol is IMethodSymbol m
				&& m.MethodKind.CeqAny(MethodKind.Ordinary, MethodKind.Constructor, MethodKind.LocalFunction, MethodKind.ReducedExtension)) {
				AddCommand(p.HasExplicitDefaultValue ? CommandId.FindOptionalParameterAssignments : CommandId.FindParameterAssignments, p.Name);
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

		CustomMenuItem CreateWebSearchCommand() {
			var symbol = _commandFactory.Symbol;
			var symbolName = symbol.GetOriginalName();
			if (String.IsNullOrEmpty(symbolName)) {
				return null;
			}
			var symbolFullName = symbol.GetQualifiedName();
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
			_commandFactory = null;
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

			public CustomMenuItem(SemanticCommandBase command) {
				Icon = VsImageHelper.GetImage(_ImageId = command.ImageId);
				Header = MakeHeader(command);
				var subCmds = command.GetSubCommands();
				if (subCmds != null) {
					foreach (var item in subCmds) {
						AddSubCommand(item.Description, new AsyncCommandExecutor(item).HandleEvent, item.ImageId);
					}
				}
				var options = command.OptionDescriptors;
				if (options != null) {
					foreach (var item in options) {
						if (item.ApplicationFilter?.Invoke(command.Symbol) == false) {
							continue;
						}
						AddOptionButton(item.Description, item.Options, item.ImageId, ExclusiveButtonConfigurator.Get(item.ExclusiveOptions));
					}
				}
				_ClickHandler = new AsyncCommandExecutor(command).HandleEvent;
				SetToolTip(command);

				static ThemedMenuText MakeHeader(SemanticCommandBase command) {
					var title = command.Title;
					string sub;
					if ((sub = command.TitlePlaceHolderSubstitution) != null) {
						var i = title.IndexOf('<');
						if (i < 0) {
							goto FALLBACK;
						}
						var i2 = title.IndexOf('>', i);
						if (i2 < 0) {
							goto FALLBACK;
						}
						return new ThemedMenuText().Append(title.Substring(0, i))
							.Append(new System.Windows.Documents.Run(String.IsNullOrEmpty(sub) ? "?" : sub) { TextDecorations = { TextDecorations.Underline } })
							.Append(title.Substring(i2 + 1));
					}
				FALLBACK:
					return new ThemedMenuText { Text = title };
				}
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
			void HideMenu(object sender, RoutedEventArgs e) {
				this.GetParent<CSharpSymbolContextMenu>()?.FireCommandExecuted(e);
			}
			void SetCommandOption(object sender, RoutedEventArgs e) {
				var b = sender as ThemedToggleButton;
				_Option = _Option.SetFlags((CommandOptions)b.Tag, b.IsChecked == true);
			}

			void SetToolTip(SemanticCommandBase command) {
				this.SetLazyToolTip(command.CreteToolTip).SetTipOptions();
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

		sealed class AsyncCommandExecutor(SemanticCommandBase command)
		{
			public void HandleEvent(object sender, RoutedEventArgs args) {
				if (command.OptionDescriptors != null) {
					command.Options = (sender as UIElement)?.GetParentOrSelf<CustomMenuItem>()?.Option ?? 0;
				}
				if (args is MouseButtonEventArgs mbe && mbe.ChangedButton == MouseButton.Right) {
					command.Options |= CommandOptions.Alternative;
				}
				command.ExecuteAsync(default).FireAndForget();
			}
		}

		sealed class ExclusiveButtonConfigurator
		{
			readonly CommandOptions _ExclusiveOptions;

			ExclusiveButtonConfigurator(CommandOptions exclusiveOptions) {
				_ExclusiveOptions = exclusiveOptions;
			}

			public static Action<ThemedToggleButton> Get(CommandOptions options) {
				return options switch {
					CommandOptions.Default => null,
					CommandOptions.Explicit | CommandOptions.Implicit => ExplicitImplicit,
					CommandOptions.CurrentFile | CommandOptions.CurrentProject | CommandOptions.RelatedProjects => FileProject,
					CommandOptions.CurrentProject | CommandOptions.RelatedProjects => Projects,
					CommandOptions.SourceCode | CommandOptions.External => SourceCode,
					_ => new ExclusiveButtonConfigurator(options).ConfigureExclusiveAssignmentButton,
				};
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
