using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Codist.QuickInfo
{
	sealed partial class CSharpQuickInfo
	{
		static bool? IsParameterAssignedAfterDeclaration(Context ctx, IParameterSymbol parameter) {
			if (ctx.node.Parent.IsKind(SyntaxKind.NameColon)) {
				return null;
			}

			SyntaxNode declaration;
			BlockSyntax body = null;
			CSharpSyntaxNode expression = null;
			DataFlowAnalysis analysis = null;
			foreach (var item in parameter.ContainingSymbol.DeclaringSyntaxReferences) {
				if (item.SyntaxTree != ctx.CompilationUnit.SyntaxTree) {
					continue;
				}
				declaration = ctx.CompilationUnit.FindNode(item.Span);
				if (declaration is BaseMethodDeclarationSyntax m) {
					body = m.Body;
					expression = m.ExpressionBody?.Expression;
				}
				else if (declaration is AccessorDeclarationSyntax a) {
					body = a.Body;
					expression = a.ExpressionBody?.Expression;
				}
				else if (declaration is AnonymousFunctionExpressionSyntax af) {
					expression = af.Body;
				}
				else if (declaration is LocalFunctionStatementSyntax lf) {
					body = lf.Body;
					expression = lf.ExpressionBody?.Expression;
				}
				if (body != null) {
					analysis = ctx.semanticModel.AnalyzeDataFlow(body);
					break;
				}
				if (expression != null) {
					analysis = ctx.semanticModel.AnalyzeDataFlow(expression);
					break;
				}
			}
			return analysis?.WrittenInside.Contains(parameter);
		}

		static bool IsVariableAssignedAfterDeclaration(ILocalSymbol localSymbol, SyntaxNode declarationNode, SemanticModel semanticModel) {
			var scopeNode = GetVariableScope(declarationNode);
			if (scopeNode == null) return false;

			var walker = new AssignmentWalker(localSymbol, semanticModel, declarationNode);
			walker.Visit(scopeNode);
			return walker.HasAdditionalAssignments;
		}

		static SyntaxNode GetVariableScope(SyntaxNode declarationNode) {
			var current = declarationNode;
			while (current != null) {
				if (current is BlockSyntax
					|| current is BaseMethodDeclarationSyntax
					|| current is AnonymousFunctionExpressionSyntax
					|| current is SwitchSectionSyntax) {
					return current;
				}
				current = current.Parent;
			}
			return declarationNode.SyntaxTree.GetRoot();
		}

		sealed class AssignmentWalker : CSharpSyntaxWalker
		{
			readonly ILocalSymbol _targetSymbol;
			readonly SemanticModel _semanticModel;
			readonly SyntaxNode _declarationNode;
			readonly TextSpan _nodeSpan;
			readonly int _spanStart;
			readonly Predicate<TextSpan> _isInDeclarationInitializer;
			public bool HasAdditionalAssignments { get; private set; }

			public AssignmentWalker(ILocalSymbol targetSymbol, SemanticModel semanticModel, SyntaxNode declarationNode) {
				_targetSymbol = targetSymbol;
				_semanticModel = semanticModel;
				_declarationNode = declarationNode;
				_nodeSpan = declarationNode.FullSpan;
				_spanStart = declarationNode.SpanStart;
				_isInDeclarationInitializer = CreateDeclarationInitializerPredicate(declarationNode);
			}

			public override void VisitAssignmentExpression(AssignmentExpressionSyntax node) {
				if (HasAdditionalAssignments) return;
				base.VisitAssignmentExpression(node);
				foreach (var target in FlattenAssignmentTarget(node.Left)) {
					CheckForAssignment(target, node);
				}
			}

			static IEnumerable<ExpressionSyntax> FlattenAssignmentTarget(ExpressionSyntax expression) {
				switch (expression) {
					case ParenthesizedExpressionSyntax parenExpr:
						foreach (var expr in FlattenAssignmentTarget(parenExpr.Expression)) {
							yield return expr;
						}
						break;
					case TupleExpressionSyntax tupleExpr:
						foreach (var argument in tupleExpr.Arguments) {
							foreach (var expr in FlattenAssignmentTarget(argument.Expression)) {
								yield return expr;
							}
						}
						break;
					default:
						yield return expression;
						break;
				}
			}

			public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node) {
				if (HasAdditionalAssignments) return;
				base.VisitPostfixUnaryExpression(node);
				if (node.IsAnyKind(SyntaxKind.PostIncrementExpression, SyntaxKind.PostDecrementExpression)) {
					CheckForAssignment(node.Operand, node);
				}
			}

			public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node) {
				if (HasAdditionalAssignments) return;
				base.VisitPrefixUnaryExpression(node);
				if (node.IsAnyKind(SyntaxKind.PreIncrementExpression, SyntaxKind.PreDecrementExpression)) {
					CheckForAssignment(node.Operand, node);
				}
			}

			public override void VisitArgument(ArgumentSyntax node) {
				if (HasAdditionalAssignments) return;
				base.VisitArgument(node);
				if (node.RefOrOutKeyword.IsAnyKind(SyntaxKind.OutKeyword, SyntaxKind.RefKeyword)) {
					CheckForAssignment(node.Expression, node);
				}
			}

			void CheckForAssignment(ExpressionSyntax expression, SyntaxNode node) {
				if (_targetSymbol.Equals(_semanticModel.GetSymbolInfo(expression).Symbol) == false) {
					return;
				}

				if (IsAfterDeclaration(node) && !IsPartOfDeclarationInitializer(node)) {
					HasAdditionalAssignments = true;
				}
			}

			bool IsAfterDeclaration(SyntaxNode node) {
				return node.SpanStart > _spanStart;
			}

			static Predicate<TextSpan> CreateDeclarationInitializerPredicate(SyntaxNode declarationNode) {
				switch (declarationNode.Kind()) {
					case SyntaxKind.VariableDeclarator:
						var v = (VariableDeclaratorSyntax)declarationNode;
						return span => v.Initializer?.FullSpan.Contains(span) == true;
					case SyntaxKind.DeclarationPattern:
						var d = (DeclarationPatternSyntax)declarationNode;
						return span => d.Parent?.FullSpan.Contains(span) == true;
					case SyntaxKind.ForEachStatement:
						var fe = (ForEachStatementSyntax)declarationNode;
						return span => fe.Expression?.FullSpan.Contains(span) == true;
					case SyntaxKind.ForStatement:
						var f = (ForStatementSyntax)declarationNode;
						return span => f.Declaration?.FullSpan.Contains(span) == true
							|| f.Initializers.FullSpan.Contains(span);
					case SyntaxKind.UsingStatement:
						var u = (UsingStatementSyntax)declarationNode;
						return span => u.Declaration?.FullSpan.Contains(span) == true;
					case SyntaxKind.CatchClause:
						var c = (CatchClauseSyntax)declarationNode;
						return span => c.FullSpan.Contains(span) == true;
					default:
						return span => false;
				}
			}

			bool IsPartOfDeclarationInitializer(SyntaxNode node) {
				var span = node.Span;
				return _nodeSpan.Contains(span) || _isInDeclarationInitializer(span);
			}
		}
	}
}
