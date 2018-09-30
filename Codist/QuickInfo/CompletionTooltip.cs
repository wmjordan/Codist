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
			static Type _CustomCommitCompletionType;
			static Func<Completion, object> _GetCompletionItem;
			static Func<object, ImmutableArray<string>> _GetTags;
			static Func<object, Document> _GetDocument;

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

					return new CSharpCompletionTooltip(itemToRender, symbols, semanticModel).LimitSize().Scrollable();
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


		internal CSharpCompletionTooltip(Completion completion, ImmutableArray<ISymbol> symbols, SemanticModel semanticModel) {
			var symbol = symbols[0];
			var sign = SymbolFormatter.Instance.ShowSymbolDeclaration(symbol, new Controls.ToolTipText(), true, false);
			var stype = symbol.GetReturnType();
			if (stype != null) {
				sign.Append(" ").AddSymbol(stype, null, SymbolFormatter.Instance).Append(" ");
			}
			sign.AddSymbolDisplayParts(symbol.ToDisplayParts(), SymbolFormatter.Instance);
			if (symbols.Length > 1) {
				sign.Append("(+" + (symbols.Length - 1).ToString() + " overloads)");
			}
			this.Add(sign.LimitSize());
			var doc = symbol.GetXmlDocSummaryForSymbol();
			if (doc != null) {
				var t = new Controls.ToolTipText();
				new XmlDocRenderer(semanticModel.Compilation, SymbolFormatter.Instance, symbol).Render(doc, t);
				this.Add(t);
			}

		}

	}
}
