using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.QuickInfo
{
	internal sealed class CSharpCompletionTooltip : StackPanel
	{
		[Export(typeof(IUIElementProvider<Completion, ICompletionSession>))]
		[Name(nameof(CSharpCompletionTooltip))]
		//Roslyn is the default Tooltip Provider. We must override it if we wish to use custom tooltips
		[Order(Before = "RoslynToolTipProvider")]
		[ContentType("CSharp")]
		internal sealed class CompletionTooltipProvider : IUIElementProvider<Completion, ICompletionSession>
		{
			static readonly SymbolFormatter _SymbolFormatter = new SymbolFormatter();
			static Type _CustomCommitCompletionType;
			static Func<Completion, object> _GetCompletionItem;
			static Func<object, ImmutableArray<string>> _GetTags;
			static Func<object, Document> _GetDocument;

			[Import]
			IEditorFormatMapService _FormatMapService = null;

			IEditorFormatMap _FormatMap;

			public UIElement GetUIElement(Completion itemToRender, ICompletionSession context, UIElementType elementType) {
				if (elementType != UIElementType.Tooltip) {
					return null;
				}
				if (Keyboard.Modifiers.MatchFlags(ModifierKeys.Shift) == false) {
					return null;
				}
				var t = itemToRender.GetType();
				if (_CustomCommitCompletionType == null) {
					InitInternalMethods(t);
				}
				if (t != _CustomCommitCompletionType) {
					return null;
				}
				var ci = _GetCompletionItem(itemToRender);
				var tags = _GetTags(ci);
				var document = _GetDocument(ci);

				var semanticModel = document.GetSemanticModelAsync().Result;
				var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot();
				var triggerPoint = context.GetTriggerPoint(context.TextView.TextSnapshot).Value;
				var token = unitCompilation.FindToken(triggerPoint);
				var node = unitCompilation.FindNode(token.Span, true, true);
				var symbols = semanticModel.LookupSymbols(triggerPoint, null, itemToRender.InsertionText.Replace("<>", String.Empty), true);
				
				// display symbol information
				if (symbols.Length > 0) {
					var formatMap = _FormatMapService.GetEditorFormatMap(context.TextView);
					if (_FormatMap != formatMap) {
						_FormatMap = formatMap;
						_SymbolFormatter.UpdateSyntaxHighlights(formatMap);
					}
					return new CSharpCompletionTooltip(itemToRender, symbols, semanticModel, _SymbolFormatter).LimitSize().Scrollable();
				}
				return null;
			}

			static void InitInternalMethods(Type t) {
				const string CustomCommitCompletion = "Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense.Completion.Presentation.CustomCommitCompletion";
				if (t.FullName != CustomCommitCompletion) {
					return;
				}

				var completionItemField = t.GetField("CompletionItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
				_GetCompletionItem = t.CreateGetFieldMethod<Completion, object>("CompletionItem");
				var ct = completionItemField.FieldType;
				_GetTags = ct.CreateGetPropertyMethod<object, ImmutableArray<string>>("Tags");
				_GetDocument = ct.CreateGetPropertyMethod<object, Document>("Document");
				_CustomCommitCompletionType = t;
			}
		}


		internal CSharpCompletionTooltip(Completion completion, ImmutableArray<ISymbol> symbols, SemanticModel semanticModel, SymbolFormatter formatter) {
			var symbol = symbols[0];
			var sign = SymbolInfoRenderer.ShowSymbolDeclaration(symbol, formatter);
			var stype = symbol.GetReturnType();
			if (stype != null) {
				sign.AddText(" ").AddSymbol(stype, null, formatter).AddText(" ");
			}
			formatter.ToUIText(sign, symbol.ToDisplayParts(), -1);
			if (symbols.Length > 1) {
				sign.AddText("(+" + (symbols.Length - 1).ToString() + " overloads)");
			}
			this.Add(sign.LimitSize());
			var doc = symbol.GetXmlDocForSymbol();
			if (doc != null) {
				this.Add(new XmlDocRenderer(semanticModel.Compilation, formatter).Render(doc));
			}

		}

	}
}
