using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	/// <summary>
	/// Completion tooltip provider for VS 2017.
	/// </summary>
	[Export(typeof(IUIElementProvider<Completion, ICompletionSession>))]
	[Name(nameof(CSharpCompletionTooltip))]
	//Roslyn is the default Tooltip Provider. We must override it if we wish to use custom tooltips
	[Order(Before = "RoslynToolTipProvider")]
	[ContentType("CSharp")]
	internal sealed class CSharpCompletionTooltipProvider : IUIElementProvider<Completion, ICompletionSession>
	{
		public UIElement GetUIElement(Completion itemToRender, ICompletionSession context, UIElementType elementType) {
			SemanticContext sc;
			Completion3 completion;
			if (elementType != UIElementType.Tooltip
				|| (completion = itemToRender as Completion3) == null
				|| Keyboard.Modifiers.MatchFlags(ModifierKeys.Control)
				|| (sc = SemanticContext.GetOrCreateSingletonInstance(context.TextView as IWpfTextView)) == null) {
				return null;
			}
			switch (GetId(completion)) {
				case KnownImageIds.Snippet:
				case KnownImageIds.IntellisenseKeyword:
					return null;
			}
			return new CSharpCompletionTooltip(sc, context, completion.DisplayText);
		}

		static int GetId(Completion3 completion) {
			return completion.IconMoniker.Id;
		}

		sealed class CSharpCompletionTooltip : ContentControl
		{
			readonly SemanticContext _Context;
			readonly ICompletionSession _Session;
			readonly string _DisplayText;
			readonly Task _UpdateContentTask;
			CancellationTokenSource _CTS = new CancellationTokenSource();

			public CSharpCompletionTooltip(SemanticContext sc, ICompletionSession session, string displayText) {
				_Context = sc;
				_Session = session;
				Content = _DisplayText = displayText;

				// Kick off the task to produce the new content.  When it completes, call back on 
				// the UI thread to update the display.
				var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
				_UpdateContentTask = _Context.UpdateAsync(_CTS.Token)
					.ContinueWith(ProcessDescription, _CTS.Token, TaskContinuationOptions.OnlyOnRanToCompletion, scheduler);

				// If we get unloaded (i.e. the user scrolls down in the completion list and VS 
				// dismisses the existing tooltip), then cancel the work we're doing
				Unloaded += AfterUnload;
			}

			void ProcessDescription(Task<bool> task) {
				var triggerPoint = _Session.GetTriggerPoint(_Session.TextView.TextSnapshot).Value;
				var semanticModel = _Context.SemanticModel;
				//var enclosing = semanticModel.GetEnclosingSymbol(triggerPoint)?.ContainingType as INamespaceOrTypeSymbol;
				var text = _DisplayText;
				if (text.EndsWith("<>", StringComparison.Ordinal) || text.EndsWith(" =", StringComparison.Ordinal)) {
					text = text.Substring(0, text.Length - 2);
				}
				if (triggerPoint > 0 && (triggerPoint - 1).GetChar() == '.') {
					triggerPoint -= 1;
				}
				var node = semanticModel.SyntaxTree.GetRoot().FindNode(new TextSpan(triggerPoint, 0), true, true);
				INamespaceOrTypeSymbol nsOrType = null;
				ImmutableArray<ISymbol> symbols;
				if (node is QualifiedNameSyntax qn) {
					nsOrType = semanticModel.GetSymbolOrFirstCandidate(qn.Left) as INamespaceOrTypeSymbol;
				}
				else if (node is IdentifierNameSyntax id) {
					nsOrType = semanticModel.GetSymbolOrFirstCandidate(id) as INamespaceOrTypeSymbol;
				}

				if (node.IsAnyKind(SyntaxKind.Attribute, SyntaxKind.AttributeList) || node.Parent.IsKind(SyntaxKind.Attribute)) {
					symbols = semanticModel.LookupNamespacesAndTypes(triggerPoint, nsOrType, text + "Attribute");
					if (symbols.Length == 0) {
						symbols = semanticModel.LookupNamespacesAndTypes(triggerPoint, nsOrType, text);
					}
				}
				else if (node.Parent.IsKind(SyntaxKind.AttributeArgument)) {
					nsOrType = semanticModel.GetTypeInfo(node.FirstAncestorOrSelf<AttributeSyntax>()).Type;
					symbols = semanticModel.LookupSymbols(triggerPoint, nsOrType, text);
				}
				else {
					symbols = semanticModel.LookupSymbols(triggerPoint, null, text, true);
				}
				if (symbols.Length == 0) {
					if (node is MemberAccessExpressionSyntax ma) {
						while (ma.Expression is MemberAccessExpressionSyntax m2) {
							ma = m2;
							nsOrType = semanticModel.GetTypeInfo(ma).Type;
							if (nsOrType != null) {
								goto FINAL_ATTEMPT;
							}
						}
					}
					else if (node.IsKind(SyntaxKind.IdentifierName)
						&& (ma = node.Parent as MemberAccessExpressionSyntax) != null) {
					}
					else if (node.IsKind(SyntaxKind.IdentifierName)
						&& node.Parent.IsKind(SyntaxKind.QualifiedName)) {
						ma = null;
						var p = semanticModel.GetSymbolOrFirstCandidate(((QualifiedNameSyntax)node.Parent).Left);
						if (p?.Kind == SymbolKind.Parameter) {
							nsOrType = ((IParameterSymbol)p).Type;
							if (nsOrType != null) {
								goto FINAL_ATTEMPT;
							}
						}
					}
					else if (node is ExpressionStatementSyntax es
						&& (ma = es.Expression as MemberAccessExpressionSyntax) != null) {
					}
					else {
						if (symbols.Length == 0) {
							symbols = semanticModel.LookupNamespacesAndTypes(triggerPoint, nsOrType, text);
						}
						goto EXIT;
					}
					if (ma == null || ma.Expression == null) {
						return;
					}
					nsOrType = semanticModel.GetTypeInfo(ma.Expression).Type;
					if (nsOrType == null) {
						goto EXIT;
					}
					FINAL_ATTEMPT:
					symbols = semanticModel.LookupSymbols(triggerPoint, nsOrType, text, true);
				}
			EXIT:
				// display symbol information
				if (symbols.Length != 0) {
					Content = Render(symbols, semanticModel, new StackPanel());
				}
			}

			void AfterUnload(object sender, EventArgs e) {
				SyncHelper.CancelAndDispose(ref _CTS, false);
			}

			static StackPanel Render(ImmutableArray<ISymbol> symbols, SemanticModel semanticModel, StackPanel content) {
				var symbol = symbols[0];
				content.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
				content.Add(SymbolFormatter.Instance.ShowSignature(symbol));
				var doc = new XmlDoc(symbol, semanticModel.Compilation);
				if (doc?.Summary?.FirstNode != null) {
					var docRenderer = new XmlDocRenderer(semanticModel.Compilation, SymbolFormatter.Instance);
					content.Add(docRenderer.RenderXmlDoc(symbol, doc).WrapMargin(WpfHelper.MiddleMargin));
				}
				if (symbols.Length > 1) {
					var op = new StackPanel { Margin = WpfHelper.MiddleMargin };
					op.Add(new TextBlock().SetGlyph(IconIds.MethodOverloads).Append(R.T_MethodOverload, true));
					for (int i = 1; i < symbols.Length; i++) {
						ISymbol s = symbols[i];
						if (s is IMethodSymbol m) {
							op.Add(SymbolFormatter.Instance.ShowParameters(new TextBlock().SetGlyph(m.GetImageId()).AddSymbol(m, false, SymbolFormatter.SemiTransparent), m.Parameters, true, true));
						}
					}
					content.Add(op);
				}
				return content;
			}
		}
	}
}
