using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Commands
{
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType(Constants.CodeTypes.CSharp)]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	public class AutoParenthesisHandler : IWpfTextViewCreationListener
	{
		static bool __Inited;
		CancellationTokenSource _CancellationTokenSource;
		bool _HookedEvents;
		char _TypedChar;

		public void TextViewCreated(IWpfTextView textView) {
			if (!__Inited) {
				ServicesHelper.Instance.CompletionBroker.CompletionTriggered += CompletionTriggered;
				__Inited = true;
			}
		}

		void CompletionTriggered(object sender, CompletionTriggeredEventArgs e) {
			if (_HookedEvents
				|| !Config.Instance.PunctuationOptions.MatchFlags(PunctuationOptions.MethodParentheses)) {
				return;
			}
			var s = e.CompletionSession;
			if (s.TextView is IWpfTextView v) {
				v.VisualElement.PreviewTextInput += HandleTextInput;
			}
			s.ItemCommitted += CompletionSessionItemCommitted;
			s.Dismissed += CompletionSessionDismissed;
			_HookedEvents = true;
		}

		void HandleTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e) {
			if (e.Text.Length == 1) {
				_TypedChar = e.Text[0];
			}
		}

		void CompletionSessionDismissed(object sender, EventArgs e) {
			_TypedChar = default;
			var s = (IAsyncCompletionSession)sender;
			if (s.TextView is IWpfTextView v) {
				v.VisualElement.PreviewTextInput -= HandleTextInput;
			}
			s.ItemCommitted -= CompletionSessionItemCommitted;
			s.Dismissed -= CompletionSessionDismissed;
			_HookedEvents = false;
		}

		void CompletionSessionItemCommitted(object sender, CompletionItemEventArgs e) {
			if (Char.IsPunctuation(_TypedChar)) {
				return;
			}
			var s = (IAsyncCompletionSession)sender;
			if (s.TextView is IWpfTextView v) {
				var p = v.GetCaretPosition();
				char c;
				if (p == v.TextSnapshot.Length) {
					p = p.Subtract(1);
				}
				if ((c = v.TextSnapshot[p]) == '(') {
					return;
				}
				CompletionSessionItemCommitted((IAsyncCompletionSession)sender, p, c);
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
		async void CompletionSessionItemCommitted(IAsyncCompletionSession session, SnapshotPoint p, char c) {
			if (Char.IsPunctuation(c) && p > 0) {
				p = p.Subtract(1);
			}
			var ct = SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource);
			SemanticContext sc = SemanticContext.GetOrCreateSingletonInstance((IWpfTextView)session.TextView);
			try {
				if (!await sc.UpdateAsync(session.ApplicableToSpan.TextBuffer, p, ct)) {
					$"{typeof(SemanticContext)} not updated".Log();
					return;
				}
				TryAppendParentheses(sc, p, ct);
			}
			catch (OperationCanceledException) { }
			catch (Exception ex) {
				await SyncHelper.SwitchToMainThreadAsync(default);
				MessageWindow.Error(ex, null, null, this);
			}
		}

		static void TryAppendParentheses(SemanticContext sc, SnapshotPoint p, CancellationToken ct) {
			var node = sc.GetNode(p, false, true);
			if (node is ExpressionStatementSyntax es) {
				node = es.Expression;
			}
			SymbolInfo si;
			if (IsTypeReferenceExpression(node)
				|| (si = sc.SemanticModel.GetSymbolInfo(node, ct)).HasSymbol()
					&& (IsMethod(si, node, sc, ct) || IsConstructor(si, node))) {
				InsertParentheses(sc);
			}
		}

		static bool IsTypeReferenceExpression(SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.TypeOfExpression:
					return ((TypeOfExpressionSyntax)node).OpenParenToken.IsMissing;
				case SyntaxKind.SizeOfExpression:
					return ((SizeOfExpressionSyntax)node).OpenParenToken.IsMissing;
				case SyntaxKind.IdentifierName:
					return ((IdentifierNameSyntax)node).Identifier.ValueText == "nameof";
			}
			return false;
		}

		static bool IsMethod(SymbolInfo si, SyntaxNode node, SemanticContext sc, CancellationToken ct) {
			if (!(si.Symbol?.Kind == SymbolKind.Method && ((IMethodSymbol)si.Symbol).IsGenericMethod)
				&& (si.CandidateReason == CandidateReason.None
					|| !si.CandidateSymbols.All(i => i.Kind == SymbolKind.Method && !((IMethodSymbol)i).IsGenericMethod))) {
				// do not append parentheses if:
				//   symbol is not method,
				//   or method is generic,
				//   or not all candidates are all non-generic methods
				return false;
			}

			var pNode = node.GetNodePurpose();
			switch (pNode.Kind()) {
				case SyntaxKind.Attribute:
					// do not append parentheses if attribute constructor does not take parameter
					return si.Symbol != null && ((IMethodSymbol)si.Symbol).Parameters.Length != 0
						|| si.CandidateReason != CandidateReason.None
							&& si.CandidateSymbols.All(i => ((IMethodSymbol)i).Parameters.Length != 0);
				case SyntaxKind.Argument:
					if (IsDelegateTypedArgumentOrName(sc, (ArgumentSyntax)pNode, ct)) {
						// do not append parentheses if method used as delegate or within nameof
						return false;
					}
					break;
				case SyntaxKind.InvocationExpression:
					// already has parentheses
					return false;
				case SyntaxKind.EqualsValueClause:
					// method used as a delegate
					if (sc.SemanticModel.GetTypeInfo(((EqualsValueClauseSyntax)pNode).Value, ct).ConvertedType?.TypeKind == TypeKind.Delegate) {
						return false;
					}
					break;
				case SyntaxKind.AddAssignmentExpression:
				case SyntaxKind.SubtractAssignmentExpression:
					if (sc.SemanticModel.GetTypeInfo(((AssignmentExpressionSyntax)pNode).Right, ct).ConvertedType?.TypeKind == TypeKind.Delegate) {
						return false;
					}
					break;
			}
			return true;
		}

		static bool IsDelegateTypedArgumentOrName(SemanticContext sc, ArgumentSyntax pNode, CancellationToken ct) {
			var pp = pNode.Parent.Parent;
			if (pp is InvocationExpressionSyntax ie) {
				if (ie.Expression is IdentifierNameSyntax n && n.Identifier.Text == "nameof") {
					return true;
				}
			}
			else if (!pp.IsKind(SyntaxKind.ObjectCreationExpression)) {
				return false;
			}
			var si = sc.SemanticModel.GetSymbolInfo(pp, ct);
			if (pNode.NameColon is null) {
				var index = pNode.IndexOfParent();
				if (IsDelegateParam(index, si.Symbol)) {
					return true;
				}
				if (si.CandidateReason != CandidateReason.None) {
					foreach (var item in si.CandidateSymbols) {
						if (IsDelegateParam(index, item)) {
							return true;
						}
					}
				}
			}
			else {
				var name = pNode.NameColon.Name.Identifier.Text;
				if (IsDelegateParam(name, si.Symbol)) {
					return true;
				}
				if (si.CandidateReason != CandidateReason.None) {
					foreach (var item in si.CandidateSymbols) {
						if (IsDelegateParam(name, item)) {
							return true;
						}
					}
				}
			}
			return false;
		}

		static bool IsDelegateParam(int index, ISymbol symbol) {
			var pms = symbol.GetParameters();
			// use math.min to assume the last one can be is params
			if (pms.Length == 0) {
				return false;
			}
			if (index >= pms.Length - 1) {
				var pm = pms[pms.Length - 1];
				return (pm.IsParams ? ((IArrayTypeSymbol)pm.Type).ElementType : pm.Type).TypeKind == TypeKind.Delegate;
			}
			return pms[index].Type.TypeKind == TypeKind.Delegate;
		}
		static bool IsDelegateParam(string name, ISymbol symbol) {
			foreach (var p in symbol.GetParameters()) {
				if (p.Name == name) {
					return p.Type.TypeKind == TypeKind.Delegate;
				}
			}
			return false;
		}

		static bool IsConstructor(SymbolInfo si, SyntaxNode node) {
			if (si.Symbol?.Kind != SymbolKind.NamedType) {
				return false;
			}
			var pNode = node.GetNodePurpose();
			if (pNode.IsKind(SyntaxKind.ObjectCreationExpression) && !(((ObjectCreationExpressionSyntax)pNode).ArgumentList?.Arguments.Count != 0)) {
				return true;
			}
			return false;
		}

		static void InsertParentheses(SemanticContext sc) {
			var v = sc.View;
			var caret = v.GetCaretPosition();
			SnapshotPoint p;
			using (var edit = v.TextBuffer.CreateEdit()) {
				var space = sc.Workspace.Options.GetOption(Microsoft.CodeAnalysis.CSharp.Formatting.CSharpFormattingOptions.SpaceAfterMethodCallName);
				edit.Insert(caret, space ? " ()" : "()");
				p = new SnapshotPoint(edit.Apply(), caret.Position + (space ? 2 : 1));
			}
			v.Caret.MoveTo(p);
			if (Config.Instance.PunctuationOptions.MatchFlags(PunctuationOptions.ShowParameterInfo)) {
				TextEditorHelper.ExecuteEditorCommand("Edit.ParameterInfo");
			}
		}

		void TextView_Closed(object sender, EventArgs e) {
			_CancellationTokenSource.CancelAndDispose();

			if (_HookedEvents) {
				var cb = ServicesHelper.Instance.CompletionBroker;
				var s = cb.GetSession((ITextView)sender);
				if (s.TextView is IWpfTextView v) {
					v.VisualElement.PreviewTextInput -= HandleTextInput;
				}
				s.ItemCommitted -= CompletionSessionItemCommitted;
				s.Dismissed -= CompletionSessionDismissed;
			}
		}
	}
}
