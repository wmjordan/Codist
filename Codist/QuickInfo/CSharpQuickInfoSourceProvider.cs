using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Codist.QuickInfo
{
	[Export(typeof(IQuickInfoSourceProvider))]
	[Name(Name)]
	[Order(After = "Default Quick Info Presenter")]
	[ContentType(Constants.CodeTypes.CSharp)]
	sealed class CSharpQuickInfoSourceProvider : IQuickInfoSourceProvider
	{
		internal const string Name = nameof(CSharpQuickInfoSourceProvider);

		[Import]
		IEditorFormatMapService _EditorFormatMapService = null;

		[Import]
		internal ITextStructureNavigatorSelectorService _NavigatorService = null;

		[Import]
		IGlyphService _GlyphService = null;

		public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperTooltip)
				? new CSharpQuickInfo(textBuffer, _EditorFormatMapService, _GlyphService, _NavigatorService)
				: null;
		}

		sealed class CSharpQuickInfo : IQuickInfoSource
		{
			static readonly SymbolFormatter _SymbolFormatter = new SymbolFormatter();
			readonly IEditorFormatMapService _FormatMapService;
			readonly ITextStructureNavigatorSelectorService _NavigatorService;
			readonly IGlyphService _GlyphService;
			IEditorFormatMap _FormatMap;
			bool _IsDisposed;
			SemanticModel _SemanticModel;
			ITextBuffer _TextBuffer;

			public CSharpQuickInfo(ITextBuffer subjectBuffer, IEditorFormatMapService formatMapService, IGlyphService glyphService, ITextStructureNavigatorSelectorService navigatorService) {
				_TextBuffer = subjectBuffer;
				_FormatMapService = formatMapService;
				_GlyphService = glyphService;
				_TextBuffer.Changing += TextBuffer_Changing;
				Config.Updated += _ConfigUpdated;
				_NavigatorService = navigatorService;
			}

			public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> qiContent, out ITrackingSpan applicableToSpan) {
				//if (qiContent.Count == 0) {
				//	goto EXIT;
				//}
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.HideOriginalQuickInfo)) {
					qiContent.Clear();
				}
				// Map the trigger point down to our buffer.
				var currentSnapshot = _TextBuffer.CurrentSnapshot;
				var subjectTriggerPoint = session.GetTriggerPoint(currentSnapshot).GetValueOrDefault();
				if (subjectTriggerPoint.Snapshot == null) {
					goto EXIT;
				}

				var workspace = _TextBuffer.GetWorkspace();
				if (workspace == null) {
					goto EXIT;
				}

				var querySpan = new SnapshotSpan(subjectTriggerPoint, 0);
				var semanticModel = _SemanticModel;
				if (semanticModel == null) {
					_SemanticModel = semanticModel = workspace.GetDocument(querySpan).GetSemanticModelAsync().Result;
				}
				var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot();

				//look for occurrences of our QuickInfo words in the span
				var navigator = _NavigatorService.GetTextStructureNavigator(_TextBuffer);
				var node = unitCompilation.FindNode(new TextSpan(querySpan.Start, querySpan.Length), true, true);
				if (node == null || node.Span.Contains(subjectTriggerPoint.Position) == false) {
					goto EXIT;
				}
				var extent = navigator.GetExtentOfWord(querySpan.Start).Span;
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
					ShowParameterInfo(qiContent, node);
				}
				if (node.Kind() == SyntaxKind.Argument) {
					node = (node as ArgumentSyntax).Expression;
				}
				DefaultQuickInfoPanelWrapper w = Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.DocumentationFromBaseType | QuickInfoOptions.OverrideDefaultDocumentation) || Config.Instance.QuickInfoMaxWidth > 0 || Config.Instance.QuickInfoMaxHeight > 0
					? new DefaultQuickInfoPanelWrapper(QuickInfoOverrider.FindDefaultQuickInfoPanel(qiContent))
					: null;
				var symbolInfo = semanticModel.GetSymbolInfo(node);
				ISymbol symbol = symbolInfo.Symbol;
				if (symbol == null) {
					if (symbolInfo.CandidateReason != CandidateReason.None) {
						ShowCandidateInfo(qiContent, symbolInfo, node);
						goto RETURN;
					}
					else {
						symbol = semanticModel.GetSymbolExt(node);
					}
				}
				if (symbol == null) {
					ShowMiscInfo(qiContent, currentSnapshot, node);
					goto RETURN;
				}

				if (node is PredefinedTypeSyntax/* void */) {
					goto EXIT;
				}
				if (Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.DocumentationFromBaseType | QuickInfoOptions.OverrideDefaultDocumentation)) {
					OverrideDocumentation(node, w, symbol);
				}
				var formatMap = _FormatMapService.GetEditorFormatMap(session.TextView);
				if (_FormatMap != formatMap) {
					_FormatMap = formatMap;
					_SymbolFormatter.UpdateSyntaxHighlights(formatMap);
				}

				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
					ShowAttributesInfo(qiContent, node, symbol);
				}
				ShowSymbolInfo(qiContent, node, symbol);
				RETURN:
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ClickAndGo) /*&& node is MemberDeclarationSyntax == false && node.Kind() != SyntaxKind.VariableDeclarator && node.Kind() != SyntaxKind.Parameter*/) {
					w.ApplyClickAndGo(symbol);
				}
				QuickInfoOverrider.LimitQuickInfoItemSize(qiContent, w);
				applicableToSpan = qiContent.Count > 0 && session.TextView.TextSnapshot == currentSnapshot
					? currentSnapshot.CreateTrackingSpan(extent.Start, extent.Length, SpanTrackingMode.EdgeInclusive)
					: null;
				return;
				EXIT:
				applicableToSpan = null;
			}

			void OverrideDocumentation(SyntaxNode node, DefaultQuickInfoPanelWrapper qiWrapper, ISymbol symbol) {
				var doc = symbol.GetXmlDocForSymbol();
				if (doc != null) {
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation)) {
						var desc = doc.ToUIText(RenderXmlDocSymbol);
						if (desc != null) {
							RenderXmlReturnsDoc(symbol, doc.Parent, desc);
							qiWrapper.OverrideDocumentation(desc);
						}
					}
				}
				else if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DocumentationFromBaseType)) {
					ISymbol baseMember;
					var baseDocs = symbol.GetBaseTypeDocumentation(out baseMember);
					if (baseDocs != null) {
						var info = new TextBlock { TextWrapping = TextWrapping.Wrap }.AddText("Documentation from ");
						ToUIText(info, baseMember.ContainingType.ToMinimalDisplayParts(_SemanticModel, node.SpanStart)).AddText(":");
						baseDocs.ToUIText(info.Inlines, RenderXmlDocSymbol);
						RenderXmlReturnsDoc(baseMember, baseDocs.Parent, info);
						qiWrapper.OverrideDocumentation(info);
					}
				}
			}

			void IDisposable.Dispose() {
				if (!_IsDisposed) {
					_TextBuffer.Changing -= TextBuffer_Changing;
					Config.Updated -= _ConfigUpdated; ;
					GC.SuppressFinalize(this);
					_IsDisposed = true;
				}
			}

			void _ConfigUpdated(object sender, EventArgs e) {
				if (_FormatMap != null) {
					_SymbolFormatter.UpdateSyntaxHighlights(_FormatMap);
				}
			}

			void RenderXmlDocSymbol(string symbol, System.Windows.Documents.InlineCollection tb, SymbolKind symbolKind) {
				switch (symbolKind) {
					case SymbolKind.Parameter: tb.Add(symbol.Render(_SymbolFormatter.Parameter)); return;
					case SymbolKind.TypeParameter: tb.Add(symbol.Render(_SymbolFormatter.TypeParameter)); return;
					case SymbolKind.DynamicType:
						// highlight keywords
						tb.Add(symbol.Render(_SymbolFormatter.Keyword));
						return;
				}
				var rs = DocumentationCommentId.GetFirstSymbolForDeclarationId(symbol, _SemanticModel.Compilation);
				if (rs == null) {
					if (symbol.Length > 2 && symbol[1] == ':') {
						switch (symbol[0]) {
							case 'T':
								tb.Add(symbol.Substring(2).Render(false, true, _SymbolFormatter.Class));
								break;
							case 'M':
								tb.Add(symbol.Substring(2).Render(false, true, _SymbolFormatter.Method));
								break;
							case '!':
								tb.Add(symbol.Substring(2).Render(true, true, null));
								break;
						}
					}
					else {
						tb.Add(symbol);
					}
					return;
				}
				_SymbolFormatter.ToUIText(tb, rs);
			}

			void RenderXmlReturnsDoc(ISymbol symbol, XElement doc, TextBlock desc) {
				if (symbol.Kind == SymbolKind.Method) {
					var returns = doc.GetReturns();
					if (returns != null) {
						desc.AddText("\nReturns", true).AddText(": ");
						returns.ToUIText(desc.Inlines, RenderXmlDocSymbol);
					}
				}
			}

			void ShowCandidateInfo(IList<object> qiContent, SymbolInfo symbolInfo, SyntaxNode node) {
				var info = new StackPanel().AddText("Maybe...", true);
				foreach (var item in symbolInfo.CandidateSymbols) {
					info.Add(ToUIText(item, node.SpanStart));
				}
				qiContent.Add(info.Scrollable());
			}

			static bool CanAccess(ISymbol symbol) {
				return symbol.DeclaredAccessibility == Accessibility.Public || symbol.DeclaredAccessibility == Accessibility.NotApplicable || symbol.Locations.Any(l => l.IsInSource);
			}

			void TextBuffer_Changing(object sender, TextContentChangingEventArgs e) {
				_SemanticModel = null;
			}

			void ShowSymbolInfo(IList<object> qiContent, SyntaxNode node, ISymbol symbol) {
				switch (symbol.Kind) {
					case SymbolKind.Event:
						ShowEventInfo(qiContent, node, symbol as IEventSymbol);
						break;
					case SymbolKind.Field:
						ShowFieldInfo(qiContent, node, symbol as IFieldSymbol);
						break;
					case SymbolKind.Local:
						var loc = symbol as ILocalSymbol;
						if (loc.HasConstantValue) {
							ShowConstInfo(qiContent, node, symbol, loc.ConstantValue);
						}
						break;
					case SymbolKind.Method:
						var m = symbol as IMethodSymbol;
						if (m.MethodKind == MethodKind.AnonymousFunction) {
							return;
						}
						ShowMethodInfo(qiContent, node, m);
						if (node.Parent.IsKind(SyntaxKind.Attribute)
							|| node.Parent.Parent.IsKind(SyntaxKind.Attribute) // qualified attribute annotation
							) {
							if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
								ShowAttributesInfo(qiContent, node, symbol.ContainingType);
							}
							ShowTypeInfo(qiContent, node.Parent, symbol.ContainingType as INamedTypeSymbol);
						}
						break;
					case SymbolKind.NamedType:
						ShowTypeInfo(qiContent, node, symbol as INamedTypeSymbol);
						break;
					case SymbolKind.Property:
						ShowPropertyInfo(qiContent, node, symbol as IPropertySymbol);
						break;
				}
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation)) {
					string asmName = symbol.GetAssemblyModuleName();
					if (asmName != null) {
						qiContent.Add(new TextBlock().AddText("Assembly: ", true).AddText(asmName));
					}
				}
			}

			static void ShowMiscInfo(IList<object> qiContent, ITextSnapshot currentSnapshot, SyntaxNode node) {
				StackPanel infoBox = null;
				var nodeKind = node.Kind();
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues) && (nodeKind == SyntaxKind.NumericLiteralExpression || nodeKind == SyntaxKind.CharacterLiteralExpression)) {
					infoBox = ShowNumericForm(node);
				}
				else if (nodeKind == SyntaxKind.SwitchStatement) {
					var s = (node as SwitchStatementSyntax).Sections.Count;
					if (s > 1) {
						qiContent.Add(s + " switch sections");
					}
				}
				else if (nodeKind == SyntaxKind.StringLiteralExpression) {
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.String)) {
						infoBox = ShowStringInfo(node.GetFirstToken().ValueText);
					}
				}
				else if (nodeKind == SyntaxKind.Block) {
					var lines = currentSnapshot.GetLineNumberFromPosition(node.Span.End) - currentSnapshot.GetLineNumberFromPosition(node.SpanStart) + 1;
					if (lines > 100) {
						qiContent.Add(new TextBlock { Text = lines + " lines", FontWeight = FontWeights.Bold });
					}
					else if (lines > 1) {
						qiContent.Add(lines + " lines");
					}
				}
				if (infoBox != null) {
					qiContent.Add(infoBox);
				}
			}

			void ShowAttributesInfo(IList<object> qiContent, SyntaxNode node, ISymbol symbol) {
				// todo: show inherited attributes
				var attrs = symbol.GetAttributes();
				if (attrs.Length > 0) {
					ShowAttributes(qiContent, attrs, node.SpanStart);
				}
			}

			void ShowPropertyInfo(IList<object> qiContent, SyntaxNode node, IPropertySymbol property) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
					&& (property.DeclaredAccessibility != Accessibility.Public || property.IsAbstract || property.IsStatic || property.IsOverride || property.IsVirtual)) {
					ShowDeclarationModifier(qiContent, property, "Property", node.SpanStart);
				}
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
					ShowInterfaceImplementation(qiContent, node, property, property.ExplicitInterfaceImplementations);
				}
			}

			void ShowEventInfo(IList<object> qiContent, SyntaxNode node, IEventSymbol ev) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)) {
					if (ev.DeclaredAccessibility != Accessibility.Public || ev.IsAbstract || ev.IsStatic || ev.IsOverride || ev.IsVirtual) {
						ShowDeclarationModifier(qiContent, ev, "Event", node.SpanStart);
					}
					var invoke = ev.Type.GetMembers("Invoke").FirstOrDefault() as IMethodSymbol;
					if (invoke != null && invoke.Parameters.Length == 2) {
						qiContent.Add(ToUIText(new TextBlock().AddText("Event argument: ", true), invoke.Parameters[1].Type.ToMinimalDisplayParts(_SemanticModel, node.SpanStart)));
					}
				}
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
					ShowInterfaceImplementation(qiContent, node, ev, ev.ExplicitInterfaceImplementations);
				}
			}

			void ShowFieldInfo(IList<object> qiContent, SyntaxNode node, IFieldSymbol field) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
					&& (field.DeclaredAccessibility != Accessibility.Public || field.IsReadOnly || field.IsVolatile || field.IsStatic)
					&& field.ContainingType.TypeKind != TypeKind.Enum) {
					ShowFieldDeclaration(qiContent, field);
				}
				if (field.HasConstantValue) {
					ShowConstInfo(qiContent, node, field, field.ConstantValue);
				}
			}

			void ShowMethodInfo(IList<object> qiContent, SyntaxNode node, IMethodSymbol method) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
					&& (method.DeclaredAccessibility != Accessibility.Public || method.IsAbstract || method.IsStatic || method.IsVirtual || method.IsOverride || method.IsExtern || method.IsSealed)
					&& method.ContainingType.TypeKind != TypeKind.Interface) {
					ShowDeclarationModifier(qiContent, method, "Method", node.SpanStart);
				}
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.TypeParameters) && method.TypeArguments.Length > 0) {
					ShowMethodTypeArguments(qiContent, node, method);
				}
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
					ShowInterfaceImplementation(qiContent, node, method, method.ExplicitInterfaceImplementations);
				}
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation) && method.IsExtensionMethod) {
					ShowExtensionMethod(qiContent, method, node.SpanStart);
				}
				ShowOverloadsInfo(qiContent, node, method);
			}

			void ShowOverloadsInfo(IList<object> qiContent, SyntaxNode node, IMethodSymbol method) {
				var overloads = node.Kind() == SyntaxKind.MethodDeclaration
					? method.ContainingType.GetMembers(method.Name)
					: _SemanticModel.GetMemberGroup(node);
				if (overloads.Length < 2) {
					return;
				}
				var overloadInfo = new StackPanel().AddText("Method overload:", true);
				foreach (var item in overloads) {
					if (item.Equals(method) || item.Kind != SymbolKind.Method) {
						continue;
					}
					overloadInfo.Add(ToUIText(new TextBlock { TextWrapping = TextWrapping.Wrap }.SetGlyph(_GlyphService.GetGlyph(item.GetGlyphGroup(), item.GetGlyphItem())), item.ToMinimalDisplayParts(_SemanticModel, node.SpanStart), -1));
				}
				if (overloadInfo.Children.Count > 1) {
					qiContent.Add(overloadInfo.Scrollable());
				}
			}

			void ShowMethodTypeArguments(IList<object> qiContent, SyntaxNode node, IMethodSymbol method) {
				var info = new StackPanel();
				var l = method.TypeArguments.Length;
				info.AddText("Type argument:", true);
				for (int i = 0; i < l; i++) {
					var argInfo = new TextBlock();
					ShowTypeParameterInfo(method.TypeParameters[i], method.TypeArguments[i], argInfo, node.SpanStart);
					info.Add(argInfo);
				}
				qiContent.Add(info);
			}

			void ShowTypeInfo(IList<object> qiContent, SyntaxNode node, INamedTypeSymbol typeSymbol) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType)) {
					if (typeSymbol.TypeKind == TypeKind.Enum) {
						ShowEnumInfo(qiContent, node, typeSymbol, true);
					}
					else {
						ShowBaseType(qiContent, typeSymbol, node.SpanStart);
					}
				}
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Interfaces)) {
					ShowInterfaces(qiContent, typeSymbol, node.SpanStart);
				}
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
					&& typeSymbol.TypeKind == TypeKind.Class
					&& (typeSymbol.DeclaredAccessibility != Accessibility.Public || typeSymbol.IsAbstract || typeSymbol.IsStatic || typeSymbol.IsSealed)) {
					ShowDeclarationModifier(qiContent, typeSymbol, "Class", node.SpanStart);
				}
				if (node.Parent.Kind() == SyntaxKind.ObjectCreationExpression) {
					var method = _SemanticModel.GetSymbolInfo(node.Parent).Symbol as IMethodSymbol;
					ShowOverloadsInfo(qiContent, node.Parent, method);
				}
			}

			void ShowConstInfo(IList<object> qiContent, SyntaxNode node, ISymbol symbol, object value) {
				var sv = value as string;
				if (sv != null) {
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.String)) {
						qiContent.Add(ShowStringInfo(sv));
					}
				}
				else if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues)) {
					var s = ShowNumericForms(value, NumericForm.None);
					if (s != null) {
						qiContent.Add(s);
						ShowEnumInfo(qiContent, node, symbol.ContainingType, false);
					}
				}
			}

			void ShowInterfaceImplementation<TSymbol>(IList<object> qiContent, SyntaxNode node, TSymbol symbol, IEnumerable<TSymbol> explicitImplementations)
				where TSymbol : class, ISymbol {
				if (symbol.IsStatic || symbol.DeclaredAccessibility != Accessibility.Public && explicitImplementations.FirstOrDefault() == null) {
					return;
				}
				var interfaces = symbol.ContainingType.AllInterfaces;
				if (interfaces.Length == 0) {
					return;
				}
				var explicitIntfs = new List<ITypeSymbol>(3);
				StackPanel info = null;
				var returnType = symbol.GetReturnType();
				var parameters = symbol.GetParameters();
				foreach (var intf in interfaces) {
					foreach (var member in intf.GetMembers()) {
						if (member.Kind == symbol.Kind
							&& member.DeclaredAccessibility == Accessibility.Public
							&& member.Name == symbol.Name
							&& member.MatchSignature(symbol.Kind, returnType, parameters)) {
							explicitIntfs.Add(intf);
						}
					}
				}
				if (explicitIntfs.Count > 0) {
					info = new StackPanel().AddText("Implements:", true);
					foreach (var item in explicitIntfs) {
						info.Add(ToUIText(item, node.SpanStart));
					}
				}
				if (explicitImplementations != null) {
					explicitIntfs.Clear();
					explicitIntfs.AddRange(explicitImplementations.Select(i => i.ContainingType));
					if (explicitIntfs.Count > 0) {
						if (info == null) {
							info = new StackPanel();
						}
						var p = new StackPanel().AddText("Explicit implements:", true);
						foreach (var item in explicitIntfs) {
							p.Add(ToUIText(item, node.SpanStart));
						}
						info.Add(p);
					}
				}
				if (info != null) {
					qiContent.Add(info);
				}
			}
			void ShowExtensionMethod(IList<object> qiContent, IMethodSymbol method, int position) {
				var info = new StackPanel();
				var extType = method.ConstructedFrom.ReceiverType;
				var extTypeParameter = extType as ITypeParameterSymbol;
				if (extTypeParameter != null && (extTypeParameter.HasConstructorConstraint || extTypeParameter.HasReferenceTypeConstraint || extTypeParameter.HasValueTypeConstraint || extTypeParameter.ConstraintTypes.Length > 0)) {
					var ext = new TextBlock().AddText("Extending: ", true).AddSymbol(extType, true, _SymbolFormatter.Class).AddText(" with ");
					ToUIText(ext, method.ReceiverType.ToMinimalDisplayParts(_SemanticModel, position));
					info.Add(ext);
				}
				var def = ToUIText(new TextBlock().AddText("Extended by: ", true), method.ContainingType.ToDisplayParts());
				info.Add(def);
				qiContent.Add(info);
			}

			void ShowTypeParameterInfo(ITypeParameterSymbol typeParameter, ITypeSymbol typeArgument, TextBlock text, int position) {
				text.AddText(typeParameter.Name, _SymbolFormatter.TypeParameter).AddText(" is ");
				ToUIText(text, typeArgument.ToMinimalDisplayParts(_SemanticModel, position));
				if (typeParameter.HasConstructorConstraint == false && typeParameter.HasReferenceTypeConstraint == false && typeParameter.HasValueTypeConstraint == false && typeParameter.ConstraintTypes.Length == 0) {
					return;
				}
				text.AddText(" where ", _SymbolFormatter.Keyword).AddText(typeParameter.Name, _SymbolFormatter.TypeParameter).AddText(" : ");
				var i = 0;
				if (typeParameter.HasReferenceTypeConstraint) {
					text.AddText("class", _SymbolFormatter.Keyword);
					++i;
				}
				if (typeParameter.HasValueTypeConstraint) {
					if (i > 0) {
						text.AddText(", ");
					}
					text.AddText("struct", _SymbolFormatter.Keyword);
					++i;
				}
				if (typeParameter.HasConstructorConstraint) {
					if (i > 0) {
						text.AddText(", ");
					}
					text.AddText("new", _SymbolFormatter.Keyword).AddText("()");
					++i;
				}
				if (typeParameter.ConstraintTypes.Length > 0) {
					foreach (var constraint in typeParameter.ConstraintTypes) {
						if (i > 0) {
							text.AddText(", ");
						}
						ToUIText(text, constraint.ToMinimalDisplayParts(_SemanticModel, position));
						++i;
					}
				}
			}

			static void ShowFieldDeclaration(IList<object> qiContent, IFieldSymbol field) {
				var info = new TextBlock().AddText("Field", true).AddText(" declaration: ");
				ShowAccessibilityInfo(field, info);
				if (field.IsConst) {
					info.AddText("const ", _SymbolFormatter.Keyword);
				}
				else {
					if (field.IsStatic) {
						info.AddText("static ", _SymbolFormatter.Keyword);
					}
					if (field.IsReadOnly) {
						info.AddText("readonly ", _SymbolFormatter.Keyword);
					}
					else if (field.IsVolatile) {
						info.AddText("volatile ", _SymbolFormatter.Keyword);
					}
				}
				qiContent.Add(info);
			}

			static StackPanel ShowNumericForm(SyntaxNode node) {
				return ShowNumericForms(node.GetFirstToken().Value, node.Parent.Kind() == SyntaxKind.UnaryMinusExpression ? NumericForm.Negative : NumericForm.None);
			}

			static StackPanel ShowNumericForms(object value, NumericForm form) {
				if (value is int) {
					var v = (int)value;
					if (form == NumericForm.Negative) {
						v = -v;
					}
					var bytes = new byte[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
					return ShowNumericForms(form == NumericForm.Unsigned ? ((uint)v).ToString() : v.ToString(), bytes);
				}
				else if (value is long) {
					var v = (long)value;
					if (form == NumericForm.Negative) {
						v = -v;
					}
					var bytes = new byte[] { (byte)(v >> 56), (byte)(v >> 48), (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
					return ShowNumericForms(form == NumericForm.Unsigned ? ((ulong)v).ToString() : v.ToString(), bytes);
				}
				else if (value is byte) {
					return ShowNumericForms(((byte)value).ToString(), new byte[] { (byte)value });
				}
				else if (value is short) {
					var v = (short)value;
					if (form == NumericForm.Negative) {
						v = (short)-v;
					}
					var bytes = new byte[] { (byte)(v >> 8), (byte)v };
					return ShowNumericForms(form == NumericForm.Unsigned ? ((ushort)v).ToString() : v.ToString(), bytes);
				}
				else if (value is char) {
					var v = (char)value;
					var bytes = new byte[] { (byte)(v >> 8), (byte)v };
					return ShowNumericForms(((ushort)v).ToString(), bytes);
				}
				else if (value is uint) {
					return ShowNumericForms((int)(uint)value, NumericForm.Unsigned);
				}
				else if (value is ulong) {
					return ShowNumericForms((long)(ulong)value, NumericForm.Unsigned);
				}
				else if (value is ushort) {
					return ShowNumericForms((short)(ushort)value, NumericForm.Unsigned);
				}
				else if (value is sbyte) {
					return ShowNumericForms(((sbyte)value).ToString(), new byte[] { (byte)(sbyte)value });
				}
				return null;
			}

			static StackPanel ShowStringInfo(string sv) {
				return new StackPanel()
					.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(sv.Length.ToString()).AddText("chars", true))
					//.Add(new StackPanel().MakeHorizontal().AddReadOnlyNumericTextBox(System.Text.Encoding.UTF8.GetByteCount(sv).ToString()).AddText("UTF-8 bytes", true))
					//.Add(new StackPanel().MakeHorizontal().AddReadOnlyNumericTextBox(System.Text.Encoding.Default.GetByteCount(sv).ToString()).AddText("System bytes", true))
					.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(sv.GetHashCode().ToString()).AddText("Hash code", true));
			}
			void ShowAttributes(IList<object> qiContent, ImmutableArray<AttributeData> attrs, int position) {
				var info = new StackPanel().AddText("Attribute:", true);
				foreach (var item in attrs) {
					if (CanAccess(item.AttributeClass) == false) {
						continue;
					}
					var a = item.AttributeClass.Name;
					var attrDef = new TextBlock()
						.AddText("[")
						.AddText(a.EndsWith("Attribute", StringComparison.Ordinal) ? a.Substring(0, a.Length - 9) : a, _SymbolFormatter.Class);
					if (item.ConstructorArguments.Length == 0 && item.NamedArguments.Length == 0) {
						attrDef.AddText("]");
						info.Add(attrDef);
						continue;
					}
					attrDef.AddText("(");
					int i = 0;
					foreach (var arg in item.ConstructorArguments) {
						if (++i > 1) {
							attrDef.AddText(", ");
						}
						ToUIText(attrDef, arg, position);
					}
					foreach (var arg in item.NamedArguments) {
						if (++i > 1) {
							attrDef.AddText(", ");
						}
						var attrMember = item.AttributeClass.GetMembers(arg.Key).FirstOrDefault(m => m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property);
						if (attrMember == null) {
							attrDef.AddText(arg.Key, false, true, null);
						}
						else {
							attrDef.AddText(arg.Key, attrMember.Kind == SymbolKind.Property ? _SymbolFormatter.Property : _SymbolFormatter.Field);
						}
						attrDef.AddText("=");
						ToUIText(attrDef, arg.Value, position);
					}
					attrDef.AddText(")]");
					attrDef.TextWrapping = TextWrapping.Wrap;
					info.Children.Add(attrDef);
				}
				if (info.Children.Count > 1) {
					qiContent.Add(info.Scrollable());
				}
			}

			void ShowBaseType(IList<object> qiContent, ITypeSymbol typeSymbol, int position) {
				var baseType = typeSymbol.BaseType;
				if (baseType != null) {
					if (baseType.IsCommonClass() == false) {
						var info = ToUIText(new TextBlock().AddText("Base type: ", true), baseType.ToMinimalDisplayParts(_SemanticModel, position));
						while (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseTypeInheritence) && (baseType = baseType.BaseType) != null) {
							if (CanAccess(baseType) && baseType.IsCommonClass() == false) {
								info.AddText(" - ").AddSymbol(baseType, false, _SymbolFormatter.Class);
							}
						}
						qiContent.Add(info);
					}
				}
			}

			void ShowEnumInfo(IList<object> qiContent, SyntaxNode node, INamedTypeSymbol type, bool fromEnum) {
				if (!Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType)) {
					return;
				}

				var t = type.EnumUnderlyingType;
				if (t == null) {
					return;
				}
				var s = new StackPanel()
					.Add(ToUIText(new TextBlock().AddText("Enum underlying type: ", true), t.ToMinimalDisplayParts(_SemanticModel, node.SpanStart)));
				if (fromEnum == false) {
					qiContent.Add(s);
					return;
				}
				var c = 0;
				object min = null, max = null, bits = null;
				IFieldSymbol minName = null, maxName = null;
				var p = 0L;
				foreach (var m in type.GetMembers()) {
					var f = m as IFieldSymbol;
					if (f == null) {
						continue;
					}
					var v = f.ConstantValue;
					if (v == null) {
						// hack: the value could somehow be null, if the semantic model is not completely loaded
						continue;
					}
					++c;
					if (min == null) {
						min = max = bits = v;
						minName = maxName = f;
						continue;
					}
					if (UnsafeArithmeticHelper.IsGreaterThan(v, max)) {
						max = v;
						maxName = f;
					}
					if (UnsafeArithmeticHelper.IsLessThan(v, min)) {
						min = v;
						minName = f;
					}
					bits = UnsafeArithmeticHelper.Or(v, bits);
				}
				if (min == null) {
					return;
				}
				s.Add(new TextBlock().AddText("Field count: ", true).AddText(c.ToString()))
					.Add(new TextBlock()
						.AddText("Min: ", true)
						.AddText(min.ToString() + "(")
						.AddText(minName.Name, _SymbolFormatter.Enum)
						.AddText(")"))
					.Add(new TextBlock()
						.AddText("Max: ", true)
						.AddText(max.ToString() + "(")
						.AddText(maxName.Name, _SymbolFormatter.Enum)
						.AddText(")"));
				if (type.GetAttributes().FirstOrDefault(a => a.AttributeClass.ToDisplayString() == "System.FlagsAttribute") != null) {
					var d = Convert.ToString(Convert.ToInt64(bits), 2);
					s.Add(new TextBlock()
						.AddText("All flags: ", true)
						.AddText(d)
						.AddText(" (")
						.AddText(d.Length.ToString())
						.AddText(d.Length > 1 ? " bits)" : " bit)"));
				}
				qiContent.Add(s);
			}

			void ShowInterfaces(IList<object> output, ITypeSymbol type, int position) {
				const string Disposable = "IDisposable";
				var showAll = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfacesInheritence);
				var interfaces = type.Interfaces;
				if (interfaces.Length == 0 && showAll == false) {
					return;
				}
				var declaredInterfaces = new List<INamedTypeSymbol>(interfaces.Length);
				var inheritedInterfaces = new List<INamedTypeSymbol>(5);
				INamedTypeSymbol disposable = null;
				foreach (var item in interfaces) {
					if (item.Name == Disposable) {
						disposable = item;
						continue;
					}
					if (item.DeclaredAccessibility == Accessibility.Public || item.Locations.Any(l => l.IsInSource)) {
						declaredInterfaces.Add(item);
					}
				}
				foreach (var item in type.AllInterfaces) {
					if (interfaces.Contains(item)) {
						continue;
					}
					if (item.Name == Disposable) {
						disposable = item;
						continue;
					}
					if (showAll
						&& (item.DeclaredAccessibility == Accessibility.Public || item.Locations.Any(l => l.IsInSource))) {
						inheritedInterfaces.Add(item);
					}
				}
				if (declaredInterfaces.Count == 0 && inheritedInterfaces.Count == 0 && disposable == null) {
					return;
				}
				var stack = new StackPanel().AddText("Interface:", true);
				if (disposable != null) {
					var t = ToUIText(disposable, position);
					if (interfaces.Contains(disposable) == false) {
						t.AddText(" (inherited)");
					}
					stack.Add(t);
				}
				foreach (var item in declaredInterfaces) {
					if (item == disposable) {
						continue;
					}
					stack.Add(ToUIText(item, position));
				}
				foreach (var item in inheritedInterfaces) {
					stack.Add(ToUIText(item, position).AddText(" (inherited)"));
				}
				output.Add(stack.Scrollable());
			}

			void ShowDeclarationModifier(IList<object> qiContent, ISymbol symbol, string type, int position) {
				var info = new TextBlock().AddText(type, true).AddText(" declaration: ");
				ShowAccessibilityInfo(symbol, info);
				if (symbol.IsAbstract) {
					info.AddText("abstract ", _SymbolFormatter.Keyword);
				}
				else if (symbol.IsStatic) {
					info.AddText("static ", _SymbolFormatter.Keyword);
				}
				else if (symbol.IsVirtual) {
					info.AddText("virtual ", _SymbolFormatter.Keyword);
				}
				else if (symbol.IsOverride) {
					info.AddText(symbol.IsSealed ? "sealed override " : "override ", _SymbolFormatter.Keyword);
					INamedTypeSymbol t = null;
					switch (symbol.Kind) {
						case SymbolKind.Method: t = ((IMethodSymbol)symbol).OverriddenMethod?.ContainingType; break;
						case SymbolKind.Property: t = ((IPropertySymbol)symbol).OverriddenProperty?.ContainingType; break;
						case SymbolKind.Event: t = ((IEventSymbol)symbol).OverriddenEvent?.ContainingType; break;
					}
					if (t != null) {
						ToUIText(info, t.ToMinimalDisplayParts(_SemanticModel, position));
					}
				}
				else if (symbol.IsSealed) {
					info.AddText("sealed ", _SymbolFormatter.Keyword);
				}
				if (symbol.IsExtern) {
					info.AddText("extern ", _SymbolFormatter.Keyword);
				}
				qiContent.Add(info);
			}

			static void ShowAccessibilityInfo(ISymbol symbol, TextBlock info) {
				switch (symbol.DeclaredAccessibility) {
					case Accessibility.Public: info.AddText("public ", _SymbolFormatter.Keyword); break;
					case Accessibility.Private: info.AddText("private ", _SymbolFormatter.Keyword); break;
					case Accessibility.ProtectedAndInternal: info.AddText("protected internal ", _SymbolFormatter.Keyword); break;
					case Accessibility.Protected: info.AddText("protected ", _SymbolFormatter.Keyword); break;
					case Accessibility.Internal: info.AddText("internal ", _SymbolFormatter.Keyword); break;
					case Accessibility.ProtectedOrInternal: info.AddText("protected or internal ", _SymbolFormatter.Keyword); break;
				}
			}

			void ShowParameterInfo(IList<object> qiContent, SyntaxNode node) {
				var argument = node;
				if (node.Kind() == SyntaxKind.NullLiteralExpression) {
					argument = node.Parent;
				}
				int depth = 0;
				do {
					var n = argument as ArgumentSyntax;
					if (n != null) {
						ShowParameterInfo(qiContent, node, n);
						return;
					}
				} while ((argument = argument.Parent) != null && ++depth < 4);
			}

			void ShowParameterInfo(IList<object> qiContent, SyntaxNode node, ArgumentSyntax argument) {
				var al = argument.Parent as BaseArgumentListSyntax;
				if (al == null) {
					return;
				}
				var ap = al.Arguments.IndexOf(argument);
				if (ap == -1) {
					return;
				}
				var symbol = _SemanticModel.GetSymbolInfo(al.Parent);
				var argName = argument.NameColon?.Name.ToString();
				if (symbol.Symbol != null) {
					if (argName != null) {
						var mp = (symbol.Symbol as IMethodSymbol).Parameters;
						for (int i = 0; i < mp.Length; i++) {
							if (mp[i].Name == argName) {
								ap = i;
							}
						}
					}
					else if (ap != -1) {
						var mp = (symbol.Symbol as IMethodSymbol).Parameters;
						if (ap < mp.Length) {
							argName = mp[ap].Name;
						}
						else if (mp.Length > 1 && mp[mp.Length-1].IsParams) {
							argName = mp[mp.Length - 1].Name;
						}
					}
					var doc = argName != null ? symbol.Symbol.GetXmlDoc().GetNamedDocItem("param", argName) : null;
					var info = ToUIText(new TextBlock().AddText("Argument of "), symbol.Symbol.ToMinimalDisplayParts(_SemanticModel, node.SpanStart), ap);
					if (doc != null) {
						info.AddText("\n" + argName, true).AddText(": ");
						doc.ToUIText(info.Inlines, RenderXmlDocSymbol);
					}
					qiContent.Add(info);
				}
				else if (symbol.CandidateSymbols.Length > 0) {
					var info = new StackPanel();
					info.Add(new TextBlock().AddText("Maybe", true).AddText(" argument of"));
					foreach (var candidate in symbol.CandidateSymbols) {
						info.Add(ToUIText(new TextBlock(), candidate.ToMinimalDisplayParts(_SemanticModel, node.SpanStart), argName == null ? ap : Int32.MinValue));
					}
					qiContent.Add(info.Scrollable());
				}
				else if (al.Parent.IsKind(SyntaxKind.InvocationExpression)) {
					var methodName = (al.Parent as InvocationExpressionSyntax).Expression.ToString();
					if (methodName == "nameof" && al.Arguments.Count == 1) {
						return;
					}
					qiContent.Add(new TextBlock().AddText("Argument " + ++ap + " of ").AddText(methodName, true));
				}
				else {
					qiContent.Add("Argument " + ++ap);
				}
			}
			static string ToBinString(byte[] bytes) {
				using (var sbr = ReusableStringBuilder.AcquireDefault((bytes.Length << 3) + bytes.Length)) {
					var sb = sbr.Resource;
					for (int i = 0; i < bytes.Length; i++) {
						ref var b = ref bytes[i];
						if (b == 0 && sb.Length == 0) {
							continue;
						}
						if (sb.Length > 0) {
							sb.Append(' ');
						}
						sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
					}
					return sb.Length == 0 ? "00000000" : sb.ToString();
				}
			}

			static string ToHexString(byte[] bytes) {
				switch (bytes.Length) {
					case 1: return bytes[0].ToString("X2");
					case 2: return bytes[0].ToString("X2") + bytes[1].ToString("X2");
					case 4:
						return bytes[0].ToString("X2") + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + bytes[3].ToString("X2");
					case 8:
						return bytes[0].ToString("X2") + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + bytes[3].ToString("X2") + " "
							+ bytes[4].ToString("X2") + bytes[5].ToString("X2") + " " + bytes[6].ToString("X2") + bytes[7].ToString("X2");
					default:
						return string.Empty;
				}
			}

			static StackPanel ShowNumericForms(string dec, byte[] bytes) {
				var s = new StackPanel()
					.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(dec).AddText(" DEC", true))
					.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(ToHexString(bytes)).AddText(" HEX", true))
					.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(ToBinString(bytes)).AddText(" BIN", true));
				return s;
			}

			TextBlock ToUIText(ISymbol symbol, int position) {
				return ToUIText(
					new TextBlock() { TextWrapping = TextWrapping.Wrap }.SetGlyph(_GlyphService.GetGlyph(symbol.GetGlyphGroup(), symbol.GetGlyphItem())),
					symbol.ToMinimalDisplayParts(_SemanticModel, position),
					Int32.MinValue);
			}

			static TextBlock ToUIText(TextBlock block, ImmutableArray<SymbolDisplayPart> parts) {
				return ToUIText(block, parts, Int32.MinValue);
			}

			static TextBlock ToUIText(TextBlock block, ImmutableArray<SymbolDisplayPart> parts, int argIndex) {
				foreach (var part in parts) {
					switch (part.Kind) {
						case SymbolDisplayPartKind.AliasName:
							//todo resolve alias type
							goto default;
						case SymbolDisplayPartKind.ClassName:
							if ((part.Symbol as INamedTypeSymbol).IsAnonymousType) {
								block.AddText("?", _SymbolFormatter.Class);
							}
							else {
								block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, _SymbolFormatter.Class);
							}
							break;
						case SymbolDisplayPartKind.EnumName:
							block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, _SymbolFormatter.Enum);
							break;
						case SymbolDisplayPartKind.InterfaceName:
							block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, _SymbolFormatter.Interface);
							break;
						case SymbolDisplayPartKind.MethodName:
							block.AddSymbol(part.Symbol, argIndex != Int32.MinValue, _SymbolFormatter.Method);
							break;
						case SymbolDisplayPartKind.ParameterName:
							var p = part.Symbol as IParameterSymbol;
							if (p.Ordinal == argIndex || p.IsParams && argIndex > p.Ordinal) {
								block.AddText(p.Name, true, true, _SymbolFormatter.Parameter);
							}
							else {
								block.AddText(p.Name, false, false, _SymbolFormatter.Parameter);
							}
							break;
						case SymbolDisplayPartKind.StructName:
							block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, _SymbolFormatter.Struct);
							break;
						case SymbolDisplayPartKind.DelegateName:
							block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, _SymbolFormatter.Delegate);
							break;
						case SymbolDisplayPartKind.StringLiteral:
							block.AddText(part.ToString(), false, false, _SymbolFormatter.Text);
							break;
						case SymbolDisplayPartKind.Keyword:
							block.AddText(part.ToString(), false, false, _SymbolFormatter.Keyword);
							break;
						case SymbolDisplayPartKind.NamespaceName:
							block.AddText(part.Symbol.Name, _SymbolFormatter.Namespace);
							break;
						case SymbolDisplayPartKind.TypeParameterName:
							block.AddText(part.Symbol.Name, argIndex == Int32.MinValue, false, _SymbolFormatter.TypeParameter);
							break;
						case SymbolDisplayPartKind.FieldName:
							block.AddText(part.Symbol.Name, _SymbolFormatter.Field);
							break;
						case SymbolDisplayPartKind.PropertyName:
							block.AddText(part.Symbol.Name, _SymbolFormatter.Property);
							break;
						case SymbolDisplayPartKind.EventName:
							block.AddText(part.Symbol.Name, _SymbolFormatter.Delegate);
							break;
						default:
							block.AddText(part.ToString());
							break;
					}
				}
				return block;
			}
			void ToUIText(TextBlock block, TypedConstant v, int position) {
				switch (v.Kind) {
					case TypedConstantKind.Primitive:
						if (v.Value is bool) {
							block.AddText((bool)v.Value ? "true" : "false", _SymbolFormatter.Keyword);
						}
						else if (v.Value is string) {
							block.AddText(v.ToCSharpString(), _SymbolFormatter.Text);
						}
						else {
							block.AddText(v.ToCSharpString(), _SymbolFormatter.Number);
						}
						break;
					case TypedConstantKind.Enum:
						var en = v.ToCSharpString();
						if (en.IndexOf('|') != -1) {
							var items = v.Type.GetMembers().Where(i => {
								var field = i as IFieldSymbol;
								return field != null
									&& field.HasConstantValue != false
									&& UnsafeArithmeticHelper.Equals(UnsafeArithmeticHelper.And(v.Value, field.ConstantValue), field.ConstantValue)
									&& UnsafeArithmeticHelper.IsZero(field.ConstantValue) == false;
							});
							var flags = items.ToArray();
							for (int i = 0; i < flags.Length; i++) {
								if (i > 0) {
									block.AddText(" | ");
								}
								block.AddText(v.Type.Name + "." + flags[i].Name, _SymbolFormatter.Enum);
							}
						}
						else {
							block.AddText(v.Type.Name + en.Substring(en.LastIndexOf('.')), _SymbolFormatter.Enum);
						}
						break;
					case TypedConstantKind.Type:
						block.AddText("typeof", _SymbolFormatter.Keyword).AddText("(");
						ToUIText(block, (v.Value as ITypeSymbol).ToMinimalDisplayParts(_SemanticModel, position));
						block.AddText(")");
						break;
					case TypedConstantKind.Array:
						block.AddText("{");
						bool c = false;
						foreach (var item in v.Values) {
							if (c == false) {
								c = true;
							}
							else {
								block.AddText(", ");
							}
							ToUIText(block, item, position);
						}
						block.AddText("}");
						break;
					default:
						block.AddText(v.ToCSharpString());
						break;
				}
			}
		}

		enum NumericForm
		{
			None,
			Negative,
			Unsigned
		}
	}
}
