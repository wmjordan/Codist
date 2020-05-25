using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using AppHelpers;

namespace Codist.Controls
{
	sealed class CSharpSymbolContextMenu : ContextMenu
	{
		SyntaxNode _Node;
		ISymbol _Symbol;
		readonly SemanticContext _SemanticContext;

		public CSharpSymbolContextMenu(SemanticContext semanticContext) {
			Resources = SharedDictionaryManager.ContextMenu;
			Foreground = ThemeHelper.ToolWindowTextBrush;
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			_SemanticContext = semanticContext;
		}

		public SyntaxNode SyntaxNode {
			get => _Node;
			set => _Node = value;
		}

		public ISymbol Symbol {
			get => _Symbol;
			set => _Symbol = value.Kind == SymbolKind.Alias ? ((IAliasSymbol)value).Target : value;
		}

		public event RoutedEventHandler ItemClicked;

		public void AddNodeCommands() {
			if (_Node != null) {
				Items.Add(CreateItem(IconIds.GoToDefinition, "Go to Definition", GoToNode));
				Items.Add(CreateItem(IconIds.SelectCode, "Select Code", SelectNode));
			}
		}
		public void AddSymbolNodeCommands() {
			if (_Symbol.HasSource()) {
				Items.Add(CreateItem(IconIds.GoToDefinition, "Go to Definition", GoToSymbolDefinition));
				if (_Node == null) {
					Items.Add(CreateItem(IconIds.SelectCode, "Select Code", () => _Symbol.GetSyntaxNode().SelectNode(true)));
				}
			}
			else if (_Node != null) {
				Items.Add(CreateItem(IconIds.SelectCode, "Select Code", SelectNode));
			}
			if (_Symbol != null) {
				Items.Add(CreateItem(IconIds.Copy, "Copy Symbol Name", CopySymbolName));
				if (_Symbol.Kind == SymbolKind.NamedType || _Symbol.IsStatic && _Symbol.ContainingType?.IsGenericType == false) {
					Items.Add(CreateItem(IconIds.Copy, "Copy Qualified Symbol Name", CopyQualifiedSymbolName));
				}
			}
		}

		public void AddSymbolCommands() {
			Items.Add(CreateItem(IconIds.Copy, "Copy Symbol Name", CopySymbolName));
			if (_Symbol != null
				&& (_Symbol.Kind == SymbolKind.NamedType ||  _Symbol.IsStatic && _Symbol.ContainingType?.IsGenericType == false)) {
				Items.Add(CreateItem(IconIds.Copy, "Copy Qualified Symbol Name", CopyQualifiedSymbolName));
			}
		}

		public void AddAnalysisCommands() {
			if (_SemanticContext.Document == null) {
				return;
			}
			switch (_Symbol.Kind) {
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
					CreateCommandForNamedType(_Symbol as INamedTypeSymbol);
					break;
				case SymbolKind.Namespace:
					Items.Add(CreateItem(IconIds.ListMembers, "Find Members...", () => FindMembers(_Symbol, _SemanticContext)));
					break;
			}
			if (_SemanticContext.SemanticModel != null) {
				if (_Node != null && _Node.Kind().IsDeclaration()
					&& _Node.SyntaxTree == _SemanticContext.SemanticModel.SyntaxTree
					&& _Symbol.Kind != SymbolKind.Namespace) {
					Items.Add(CreateItem(IconIds.FindReferencingSymbols, "Find Referenced Symbols...", FindReferencedSymbols));
				}
				//Items.Add(CreateCommandMenu("Find references...", KnownImageIds.ReferencedDimension, _Symbol, "No reference found", FindReferences));
				Items.Add(CreateItem(IconIds.FindSymbolsWithName, "Find Symbol with Name " + _Symbol.Name + "...", () => FindSymbolWithName(_Symbol, _SemanticContext)));
			}
		}

		private void CreateCommandForMembers() {
			if (_Symbol.Kind != SymbolKind.Method || IsExternallyCallable(((IMethodSymbol)_Symbol).MethodKind)) {
				Items.Add(CreateItem(IconIds.FindReferrers, "Find Referrers...", () => FindReferrers(_Symbol, _SemanticContext)));
			}
			if (_Symbol.MayHaveOverride()) {
				Items.Add(CreateItem(IconIds.FindOverloads, "Find Overrides...", () => FindOverrides(_Symbol, _SemanticContext)));
			}
			var st = _Symbol.ContainingType;
			if (st != null && st.TypeKind == TypeKind.Interface) {
				Items.Add(CreateItem(IconIds.FindImplementations, "Find Implementations...", () => FindImplementations(_Symbol, _SemanticContext)));
			}
			if (_Symbol.Kind != SymbolKind.Event) {
				CreateCommandsForReturnTypeCommand();
			}
			if (_Symbol.Kind == SymbolKind.Method) {
				switch (((IMethodSymbol)_Symbol).MethodKind) {
					case MethodKind.Constructor:
						if (st.SpecialType == SpecialType.None) {
							CreateInstanceCommandsForType(st);
						}
						break;
					case MethodKind.StaticConstructor:
					case MethodKind.Destructor:
						break;
					default:
						Items.Add(CreateItem(IconIds.FindMethodsMatchingSignature, "Find Methods with Same Signature...", () => FindMethodsBySignature(_Symbol, _SemanticContext)));
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
				var ctor = _SemanticContext.NodeIncludeTrivia.GetObjectCreationNode();
				if (ctor != null) {
					var symbol = _SemanticContext.SemanticModel.GetSymbolOrFirstCandidate(ctor);
					if (symbol != null) {
						Items.Add(CreateItem(IconIds.FindReferrers, "Find Callers...", () => FindReferrers(symbol, _SemanticContext)));
					}
				}
				else if (t.InstanceConstructors.Length > 0) {
					Items.Add(CreateItem(IconIds.FindReferrers, "Find Constructor Callers...", () => FindReferrers(t, _SemanticContext, s => s.Kind == SymbolKind.Method)));
				}
			}
			Items.Add(CreateItem(IconIds.FindTypeReferrers, "Find Type Referrers...", () => FindReferrers(t, _SemanticContext, s => s.Kind == SymbolKind.NamedType, IsTypeReference)));
			Items.Add(CreateItem(IconIds.ListMembers, "Find Members...", () => FindMembers(t, _SemanticContext)));
			if (t.IsStatic) {
				return;
			}
			if (t.IsSealed == false) {
				if (t.TypeKind == TypeKind.Class) {
					Items.Add(CreateItem(IconIds.FindDerivedClasses, "Find Derived Classes...", () => FindDerivedClasses(t, _SemanticContext)));
				}
				else if (t.TypeKind == TypeKind.Interface) {
					Items.Add(CreateItem(IconIds.FindImplementations, "Find Implementations...", () => FindImplementations(t, _SemanticContext)));
				}
			}
			if (t.TypeKind == TypeKind.Delegate) {
				Items.Add(CreateItem(IconIds.FindMethodsMatchingSignature, "Find Methods with Same Signature...", () => FindMethodsBySignature(_Symbol, _SemanticContext)));
			}
			Items.Add(CreateItem(IconIds.ExtensionMethod, "Find Extensions...", () => FindExtensionMethods(t, _SemanticContext)));
			if (t.SpecialType == SpecialType.None) {
				CreateInstanceCommandsForType(t);
			}
		}

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
					return true;
				case SyntaxKind.TypeArgumentList:
					return (p = p.Parent).IsKind(SyntaxKind.GenericName) && ((p = p.Parent.UnqualifyExceptNamespace()).IsKind(SyntaxKind.SimpleMemberAccessExpression) || p.IsKind(SyntaxKind.ObjectCreationExpression));
			}
			return false;
		}

		void CreateCommandsForReturnTypeCommand() {
			var type = _Symbol.GetReturnType();
			if (type != null && type.SpecialType == SpecialType.None && type.IsTupleType == false) {
				var et = type.ResolveElementType();
				Items.Add(CreateItem(IconIds.ListMembers, "Find Members of " + et.Name + et.GetParameterString() + "...", () => FindMembers(et, _SemanticContext)));
				if (type.IsStatic == false) {
					Items.Add(CreateItem(IconIds.ExtensionMethod, "Find Extensions for " + type.Name + type.GetParameterString() + "...", () => FindExtensionMethods(type, _SemanticContext)));
				}
				if (et.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
					Items.Add(CreateItem(IconIds.GoToReturnType, "Go to " + et.Name + et.GetParameterString(), () => et.GoToSource()));
				}
			}
		}

		public void AddFindAllReferencesCommand() {
			Items.Add(CreateItem(IconIds.FindReference, "Find All References", FindAllReferences));
		}

		public void AddGoToAnyCommands() {
			Items.Add(CreateItem(IconIds.GoToMember, "Go to Member", GoToMember));
			Items.Add(CreateItem(IconIds.GoToType, "Go to Type", GoToType));
			Items.Add(CreateItem(IconIds.GoToSymbol, "Go to Symbol", GoToSymbol));
		}

		public void AddTitleItem(string name) {
			Items.Add(new MenuItem {
				Header = name,
				IsEnabled = false,
				Icon = null,
				HorizontalContentAlignment = HorizontalAlignment.Right
			});
		}

		static MenuItem CreateItem(int imageId, string title) {
			return new MenuItem {
				Icon = ThemeHelper.GetImage(imageId),
				Header = new ThemedMenuText { Text = title }
			};
		}

		public MenuItem CreateItem(int imageId, string title, RoutedEventHandler clickHandler) {
			var item = CreateItem(imageId, title);
			item.Click += Item_Click;
			item.Click += clickHandler;
			return item;
		}

		public MenuItem CreateItem(int imageId, string title, Action clickHandler) {
			var item = CreateItem(imageId, title);
			item.Click += Item_Click;
			if (clickHandler != null) {
				item.Click += (s, args) => clickHandler();
			}
			return item;
		}

		public MenuItem CreateItem(int imageId, Action<MenuItem> itemConfigurator, Action clickHandler) {
			var item = new MenuItem { Icon = ThemeHelper.GetImage(imageId) };
			itemConfigurator(item);
			item.Click += Item_Click;
			if (clickHandler != null) {
				item.Click += (s, args) => clickHandler();
			}
			return item;
		}

		void Item_Click(object sender, RoutedEventArgs e) {
			ItemClicked?.Invoke(sender, e);
		}

		void CreateInstanceCommandsForType(INamedTypeSymbol t) {
			Items.Add(CreateItem(IconIds.InstanceProducer, "Find Instance Producer...", () => FindInstanceProducer(t, _SemanticContext)));
			Items.Add(CreateItem(IconIds.Argument, "Find Instance as Parameter...", () => FindInstanceAsParameter(t, _SemanticContext)));
		}

		static void FindReferrers(ISymbol symbol, SemanticContext context, Predicate<ISymbol> definitionFilter = null, Predicate<SyntaxNode> nodeFilter = null) {
			var referrers = SyncHelper.RunSync(() => symbol.FindReferrersAsync(context.Document.Project, definitionFilter, nodeFilter));
			if (referrers == null) {
				return;
			}
			var m = new SymbolMenu(context, SymbolListType.SymbolReferrers);
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(" referrers");
			var containerType = symbol.ContainingType;
			foreach (var (referrer, occurance) in referrers) {
				var s = referrer;
				var i = m.Menu.Add(s, false);
				i.Location = occurance.FirstOrDefault().Item2.Location;
				foreach (var item in occurance) {
					i.Usage |= item.Item1;
				}
				if (s.ContainingType != containerType) {
					i.Hint = (s.ContainingType ?? s).ToDisplayString(CodeAnalysisHelper.MemberNameFormat);
				}
			}
			m.Menu.ExtIconProvider = ExtIconProvider.Default.GetExtIconsWithUsage;
			m.Show();
		}

		static void FindDerivedClasses(ISymbol symbol, SemanticContext context) {
			var classes = SyncHelper.RunSync(() => SymbolFinder.FindDerivedClassesAsync(symbol as INamedTypeSymbol, context.Document.Project.Solution, null, default)).ToList();
			ShowSymbolMenuForResult(symbol, context, classes, " derived classes", false);
		}

		static void FindOverrides(ISymbol symbol, SemanticContext context) {
			var m = new SymbolMenu(context);
			int c = 0;
			foreach (var ov in SyncHelper.RunSync(() => SymbolFinder.FindOverridesAsync(symbol, context.Document.Project.Solution, null, default))) {
				m.Menu.Add(ov, ov.ContainingType);
				++c;
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(" overrides: ")
				.Append(c.ToString());
			m.Show();
		}

		static void FindImplementations(ISymbol symbol, SemanticContext context) {
			var implementations = new List<ISymbol>(SyncHelper.RunSync(() => SymbolFinder.FindImplementationsAsync(symbol, context.Document.Project.Solution, null, default)));
			implementations.Sort((a, b) => a.Name.CompareTo(b.Name));
			var m = new SymbolMenu(context);
			if (symbol.Kind == SymbolKind.NamedType) {
				foreach (var impl in implementations) {
					m.Menu.Add(impl, false);
				}
			}
			else {
				foreach (var impl in implementations) {
					m.Menu.Add(impl, impl.ContainingSymbol);
				}
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(" implementations: ")
				.Append(implementations.Count.ToString());
			m.Show();
		}

		static void FindMembers(ISymbol symbol, SemanticContext context) {
			var m = new SymbolMenu(context, symbol.Kind == SymbolKind.Namespace ? SymbolListType.TypeList : SymbolListType.None);
			var (count, inherited) = m.Menu.AddSymbolMembers(symbol);
			if (m.Menu.IconProvider == null) {
				if (symbol.Kind == SymbolKind.NamedType) {
					switch (((INamedTypeSymbol)symbol).TypeKind) {
						case TypeKind.Interface:
							m.Menu.ExtIconProvider = ExtIconProvider.InterfaceMembers.GetExtIcons; break;
						case TypeKind.Class:
						case TypeKind.Struct:
							m.Menu.ExtIconProvider = ExtIconProvider.Default.GetExtIcons; break;
					}
				}
				else {
					m.Menu.ExtIconProvider = ExtIconProvider.Default.GetExtIcons;
				}
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append($" members: {count} ({inherited} inherited)");
			m.Show();
		}

		static void FindInstanceAsParameter(ISymbol symbol, SemanticContext context) {
			var members = SyncHelper.RunSync(() => (symbol as ITypeSymbol).FindInstanceAsParameterAsync(context.Document.Project, default));
			ShowSymbolMenuForResult(symbol, context, members, " as parameter", true);
		}

		static void FindInstanceProducer(ISymbol symbol, SemanticContext context) {
			var members = SyncHelper.RunSync(() => (symbol as ITypeSymbol).FindSymbolInstanceProducerAsync(context.Document.Project, default));
			ShowSymbolMenuForResult(symbol, context, members, " producers", true);
		}

		static void FindExtensionMethods(ISymbol symbol, SemanticContext context) {
			var members = SyncHelper.RunSync(() => (symbol as ITypeSymbol).FindExtensionMethodsAsync(context.Document.Project, default));
			ShowSymbolMenuForResult(symbol, context, members, " extensions", true);
		}

		void FindReferencedSymbols() {
			var m = new SymbolMenu(_SemanticContext);
			var c = 0;
			var containerType = _Symbol.ContainingType ?? _Symbol;
			var loc = _Node.SyntaxTree.FilePath;
			foreach (var s in _Node.FindReferencingSymbols(_SemanticContext.SemanticModel, true)
					.OrderBy(i => i.Key.ContainingType == containerType ? null : (i.Key.ContainingType ?? i.Key).Name)
					.ThenBy(i => i.Key.Name)
					.Select(i => i.Key)) {
				var sl = s.DeclaringSyntaxReferences.First();
				SymbolItem i;
				if (sl.SyntaxTree.FilePath != loc) {
					i = m.Menu.Add(sl.ToLocation());
					i.Content.FontWeight = FontWeights.Bold;
					i.Content.HorizontalAlignment = HorizontalAlignment.Center;
					loc = sl.SyntaxTree.FilePath;
				}
				i = m.Menu.Add(s, false);
				if (s.ContainingType.Equals(containerType) == false) {
					i.Hint = (s.ContainingType ?? s).ToDisplayString(CodeAnalysisHelper.MemberNameFormat);
				}
				++c;
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(_Symbol.GetImageId()))
				.Append(_Symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(" referenced symbols: ")
				.Append(c.ToString());
			m.Show();
		}

		static void FindSymbolWithName(ISymbol symbol, SemanticContext context) {
			var result = context.SemanticModel.Compilation.FindDeclarationMatchName(symbol.Name, Keyboard.Modifiers == ModifierKeys.Control, true, default);
			ShowSymbolMenuForResult(symbol, context, new List<ISymbol>(result), " name alike", true);
		}

		static void FindMethodsBySignature(ISymbol symbol, SemanticContext context) {
			var result = context.SemanticModel.Compilation.FindMethodBySignature(symbol, Keyboard.Modifiers == ModifierKeys.Control, default);
			ShowSymbolMenuForResult(symbol, context, new List<ISymbol>(result), " signature match", true);
		}

		internal static void ShowLocations(ISymbol symbol, ICollection<SyntaxReference> locations, SemanticContext context) {
			var m = new SymbolMenu(context, SymbolListType.Locations);
			var locs = new SortedList<(string, string, int), Location>();
			foreach (var item in locations) {
				locs.Add((System.IO.Path.GetDirectoryName(item.SyntaxTree.FilePath), System.IO.Path.GetFileName(item.SyntaxTree.FilePath), item.Span.Start), item.ToLocation());
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(" source locations: ")
				.Append(locs.Count);
			foreach (var loc in locs) {
				m.Menu.Add(loc.Value);
			}
			foreach (var loc in symbol.Locations.RemoveAll(l => l.IsInMetadata == false).Sort((x, y) => String.CompareOrdinal(x.MetadataModule.Name, y.MetadataModule.Name))) {
				m.Menu.Add(loc);
			}
			m.Show();
		}

		static void ShowSymbolMenuForResult<TSymbol>(ISymbol source, SemanticContext context, List<TSymbol> members, string suffix, bool groupByType) where TSymbol : ISymbol {
			members.Sort(CodeAnalysisHelper.CompareSymbol);
			var m = new SymbolMenu(context);
			m.Title.SetGlyph(ThemeHelper.GetImage(source.GetImageId()))
				.Append(source.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(suffix);
			INamedTypeSymbol containingType = null;
			foreach (var item in members) {
				if (groupByType && item.ContainingType != containingType) {
					m.Menu.Add((ISymbol)(containingType = item.ContainingType) ?? item.ContainingNamespace, false)
						.Usage = SymbolUsageKind.Container;
					if (containingType?.TypeKind == TypeKind.Delegate) {
						continue; // skip Invoke method in Delegates, for results from FindMethodBySignature
					}
				}
				m.Menu.Add(item, false);
			}
			m.Menu.ExtIconProvider = ExtIconProvider.Default.GetExtIcons;
			m.Show();
		}

		#region Menu event handlers
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
				Clipboard.SetDataObject(_Symbol.ToDisplayString(_Symbol.Kind == SymbolKind.NamedType ? CodeAnalysisHelper.QualifiedTypeNameFormat : CodeAnalysisHelper.TypeMemberNameFormat));
			}
			catch (SystemException) {
				// ignore failure
			}
		}
		static void FindAllReferences(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.FindAllReferences");
		}
		static void GoToMember(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.GoToMember");
		}
		static void GoToType(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.GoToType");
		}
		static void GoToSymbol(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.GoToSymbol");
		}
		void GoToNode(object sender, RoutedEventArgs args) {
			_Node.GetReference().GoToSource();
		}
		void SelectNode(object sender, RoutedEventArgs args) {
			_Node.SelectNode(true);
		}
		void GoToSymbolDefinition(object sender, RoutedEventArgs args) {
			var locs = _Symbol.GetSourceReferences();
			if (locs.Length == 1) {
				locs[0].GoToSource();
			}
			else {
				ShowLocations(_Symbol, locs, _SemanticContext);
			}
		}
		#endregion

		abstract class CommandItem : MenuItem
		{
			protected CommandItem(int imageId, string title) {
				Icon = ThemeHelper.GetImage(imageId);
				Header = new ThemedMenuText { Text = title };
			}

			public ISymbol Symbol { get; set; }

			public abstract bool QueryStatus();
			public abstract void Execute();
		}

		sealed class ExtIconProvider
		{
			public ExtIconProvider(bool containerIsInterface) {
				_ContainerIsInterface = containerIsInterface;
			}
			public static readonly ExtIconProvider Default = new ExtIconProvider(false);
			public static readonly ExtIconProvider InterfaceMembers = new ExtIconProvider(true);
			readonly bool _ContainerIsInterface;

			public StackPanel GetExtIcons(SymbolItem symbolItem) {
				return GetSpecialSymbolIcon(symbolItem.Symbol);
			}

			public StackPanel GetExtIconsWithUsage(SymbolItem symbolItem) {
				var icons = GetSpecialSymbolIcon(symbolItem.Symbol);
				if (symbolItem.Usage != SymbolUsageKind.Normal) {
					AddSymbolUsageIcons(ref icons, symbolItem.Usage);
				}
				return icons;
			}

			StackPanel GetSpecialSymbolIcon(ISymbol symbol) {
				StackPanel icons = null;
				switch (symbol.Kind) {
					case SymbolKind.Method:
						var ms = symbol as IMethodSymbol;
						if (ms.IsAsync || ms.ReturnType.IsAwaitable()) {
							AddIcon(ref icons, IconIds.AsyncMember);
						}
						if (ms.IsGenericMethod) {
							AddIcon(ref icons, IconIds.Generic);
						}
						if (_ContainerIsInterface == false) {
							if (ms.IsAbstract) {
								AddIcon(ref icons, IconIds.AbstractMember);
							}
							else if (ms.IsSealed) {
								AddIcon(ref icons, IconIds.SealedMethod);
							}
							else if (ms.IsExtensionMethod) {
								AddIcon(ref icons, IconIds.ExtensionMethod);
							}
						}
						break;
					case SymbolKind.NamedType:
						var type = symbol as INamedTypeSymbol;
						if (type.IsGenericType) {
							AddIcon(ref icons, IconIds.Generic);
						}
						if (type.TypeKind == TypeKind.Class) {
							if (type.IsSealed && type.IsStatic == false) {
								AddIcon(ref icons, IconIds.SealedClass);
							}
							else if (type.IsAbstract) {
								AddIcon(ref icons, IconIds.AbstractClass);
							}
						}
						break;
					case SymbolKind.Field:
						var f = symbol as IFieldSymbol;
						if (f.IsConst) {
							return null;
						}
						if (f.IsReadOnly) {
							AddIcon(ref icons, IconIds.ReadonlyField);
						}
						else if (f.IsVolatile) {
							AddIcon(ref icons, IconIds.VolatileField);
						}
						break;
					case SymbolKind.Event:
						if (_ContainerIsInterface == false) {
							if (symbol.IsAbstract) {
								AddIcon(ref icons, IconIds.AbstractMember);
							}
							else if (symbol.IsSealed) {
								AddIcon(ref icons, IconIds.SealedEvent);
							}
						}
						break;
					case SymbolKind.Property:
						if (_ContainerIsInterface == false) {
							if (symbol.IsAbstract) {
								AddIcon(ref icons, IconIds.AbstractMember);
							}
							else if (symbol.IsSealed) {
								AddIcon(ref icons, IconIds.SealedProperty);
							}
							if (((IPropertySymbol)symbol).SetMethod == null) {
								AddIcon(ref icons, IconIds.ReadonlyProperty);
							}
						}
						break;
					case SymbolKind.Namespace:
						return null;
				}
				if (symbol.IsStatic) {
					AddIcon(ref icons, IconIds.StaticMember);
				}
				return icons;
			}

			static void AddSymbolUsageIcons(ref StackPanel icons, SymbolUsageKind usage) {
				if (usage.MatchFlags(SymbolUsageKind.Write)) {
					AddIcon(ref icons, IconIds.UseToWrite);
				}
				else if (usage.MatchFlags(SymbolUsageKind.Catch)) {
					AddIcon(ref icons, IconIds.UseToCatch);
				}
				else if (usage.HasAnyFlag(SymbolUsageKind.Attach | SymbolUsageKind.Detach)) {
					if (usage.MatchFlags(SymbolUsageKind.Attach)) {
						AddIcon(ref icons, IconIds.AttachEvent);
					}
					if (usage.MatchFlags(SymbolUsageKind.Detach)) {
						AddIcon(ref icons, IconIds.DetachEvent);
					}
				}
				else if (usage.MatchFlags(SymbolUsageKind.TypeCast)) {
					AddIcon(ref icons, IconIds.UseToCast);
				}
				else if (usage.MatchFlags(SymbolUsageKind.TypeParameter)) {
					AddIcon(ref icons, IconIds.UseAsTypeParameter);
				}
				else if (usage.MatchFlags(SymbolUsageKind.Delegate)) {
					AddIcon(ref icons, IconIds.UseAsDelegate);
				}
			}

			static void AddIcon(ref StackPanel container, int imageId) {
				if (container == null) {
					container = new StackPanel { Orientation = Orientation.Horizontal };
				}
				container.Children.Add(ThemeHelper.GetImage(imageId));
			}
		}
	}

	sealed class SymbolMenu
	{
		readonly ExternalAdornment _Container;
		readonly StackPanel _HeaderPanel;

		public SymbolList Menu { get; }
		public ThemedMenuText Title { get; }
		public SymbolFilterBox FilterBox { get; }

		public SymbolMenu(SemanticContext semanticContext) : this(semanticContext, SymbolListType.None) { }
		public SymbolMenu(SemanticContext semanticContext, SymbolListType listType) {
			_Container = semanticContext.View.Properties.GetOrCreateSingletonProperty(() => new ExternalAdornment(semanticContext.View));
			Menu = new SymbolList(semanticContext) {
				Container = _Container,
				ContainerType = listType
			};
			Menu.Header = _HeaderPanel = new StackPanel {
				Margin = WpfHelper.MenuItemMargin,
				Children = {
						(Title = new ThemedMenuText {
							TextAlignment = TextAlignment.Left,
							Padding = WpfHelper.SmallVerticalMargin
						}),
						(FilterBox = new SymbolFilterBox(Menu) {
							Margin = WpfHelper.NoMargin
						}),
						new Separator()
					}
			};
			Menu.HeaderButtons = new StackPanel {
				Orientation = Orientation.Horizontal,
				Children = {
					new ThemedButton(ThemeHelper.GetImage(IconIds.TogglePinning), "Pin", TogglePinButton),
					new ThemedButton(IconIds.Close, "Close", () => {
						_Container.Children.Remove(Menu);
						_Container.FocusOnTextView();
					})
				}
			};
			Menu.MouseLeftButtonUp += MenuItemSelect;
			_Container.MakeDraggable(Menu);
		}

		void TogglePinButton(object sender, RoutedEventArgs e) {
			((ThemedButton)e.Source).Content = ThemeHelper.GetImage((Menu.IsPinned = !Menu.IsPinned) ? IconIds.Pin : IconIds.Unpin);
		}

		public void Show() {
			ShowMenu();
			UpdateNumbers();
			FilterBox.FocusFilterBox();
		}

		void ShowMenu() {
			//if (_Bar._SymbolList != menu) {
			//	_Bar._SymbolListContainer.Children.Clear();
			_Container.Children.Add(Menu);
			//	_Bar._SymbolList = menu;
			//}
			Menu.ItemsControlMaxHeight = _Container.ActualHeight / 2;
			Menu.RefreshItemsSource();
			Menu.ScrollToSelectedItem();
			Menu.PreviewKeyUp -= OnMenuKeyUp;
			Menu.PreviewKeyUp += OnMenuKeyUp;
			if (Menu.Symbols.Count > 100) {
				Menu.EnableVirtualMode = true;
			}

			var p = Mouse.GetPosition(_Container);
			Canvas.SetLeft(Menu, p.X);
			Canvas.SetTop(Menu, p.Y);
			//var point = visual.TransformToVisual(View.VisualElement).Transform(new Point());
			//Canvas.SetLeft(Menu, point.X + visual.RenderSize.Width);
			//Canvas.SetTop(Menu, point.Y);
		}
		void UpdateNumbers() {
			FilterBox.UpdateNumbers(Menu.Symbols);
		}

		void MenuItemSelect(object sender, MouseButtonEventArgs e) {
			var menu = sender as VirtualList;
			if (e.OccursOn<ListBoxItem>()) {
				_Container.FocusOnTextView();
				(menu.SelectedItem as SymbolItem)?.GoToSource();
			}
		}

		void OnMenuKeyUp(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				_Container.Children.Remove(Menu);
				e.Handled = true;
			}
		}
	}
}
