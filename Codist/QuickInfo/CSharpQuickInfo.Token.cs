using System;
using System.Collections.Generic;
using System.Linq;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	partial class CSharpQuickInfo
	{
		static readonly Dictionary<SyntaxKind, Action<Context>> __TokenProcessors = new Dictionary<SyntaxKind, Action<Context>> {
			{ SyntaxKind.WhitespaceTrivia, TokenUnavailable },
			{ SyntaxKind.SingleLineCommentTrivia, TokenUnavailable },
			{ SyntaxKind.MultiLineCommentTrivia, TokenUnavailable },
			{ SyntaxKind.OpenBraceToken, ProcessOpenBraceToken },
			{ SyntaxKind.CloseBraceToken, ProcessCloseBraceToken },
			{ SyntaxKind.TrueKeyword, ProcessAsBoolean },
			{ SyntaxKind.FalseKeyword, ProcessAsBoolean },
			{ SyntaxKind.IsKeyword, ProcessAsBoolean },
			{ SyntaxKind.AmpersandAmpersandToken, ProcessAsBoolean },
			{ SyntaxKind.BarBarToken, ProcessAsBoolean },
			{ SyntaxKind.ThisKeyword, ProcessTypeToken },
			{ SyntaxKind.BaseKeyword, ProcessTypeToken },
			{ SyntaxKind.OverrideKeyword, ProcessTypeToken },
			{ SyntaxKind.EqualsGreaterThanToken, ProcessEqualsGreaterThanToken },
			{ SyntaxKind.ExclamationEqualsToken, ProcessAsConvertedType },
			{ SyntaxKind.EqualsEqualsToken, ProcessAsConvertedType },
			{ SyntaxKind.EqualsToken, ProcessAsConvertedType },
			{ SyntaxKind.CheckedKeyword, ProcessAsConvertedType },
			{ SyntaxKind.UncheckedKeyword, ProcessAsConvertedType },
			{ SyntaxKind.SwitchKeyword, ProcessSwitchToken },
			{ SyntaxKind.IfKeyword, ProcessIfToken },
			{ SyntaxKind.DoKeyword, ProcessStatementKeyword },
			{ SyntaxKind.WhileKeyword, ProcessWhileToken },
			{ SyntaxKind.ForKeyword, ProcessStatementKeyword },
			{ SyntaxKind.LockKeyword, ProcessStatementKeyword },
			{ SyntaxKind.TryKeyword, ProcessStatementKeyword },
			{ SyntaxKind.UnsafeKeyword, ProcessUnsafeKeyword },
			{ SyntaxKind.ForEachKeyword, ProcessForEachToken },
			{ SyntaxKind.ForEachVariableStatement, ProcessStatementKeyword },
			{ SyntaxKind.InKeyword, ProcessInToken },
			{ SyntaxKind.UsingKeyword, ProcessUsingToken },
			{ SyntaxKind.NullKeyword, ProcessValueToken },
			{ SyntaxKind.QuestionToken, ProcessValueToken },
			{ SyntaxKind.ColonToken, ProcessValueToken },
			{ SyntaxKind.QuestionQuestionToken, ProcessValueToken },
			{ CodeAnalysisHelper.QuestionQuestionEqualsToken, ProcessValueToken },
			{ SyntaxKind.UnderscoreToken, ProcessValueToken },
			{ SyntaxKind.WhereKeyword, ProcessValueToken },
			{ SyntaxKind.OrderByKeyword, ProcessValueToken },
			{ CodeAnalysisHelper.WithKeyword, ProcessValueToken },
			{ SyntaxKind.AsKeyword, ProcessAsType },
			{ SyntaxKind.CaseKeyword, ProcessCaseToken },
			{ SyntaxKind.DefaultKeyword, ProcessDefaultToken },
			{ SyntaxKind.ReturnKeyword, ProcessReturnToken },
			{ SyntaxKind.AwaitKeyword, ProcessAwaitToken },
			{ SyntaxKind.DotToken, ProcessDotToken },
			{ SyntaxKind.OpenParenToken, ProcessParenToken },
			{ SyntaxKind.CloseParenToken, ProcessParenToken },
			{ SyntaxKind.CommaToken, UsePreviousToken },
			{ SyntaxKind.SemicolonToken, UsePreviousToken },
			{ SyntaxKind.OpenBracketToken, ProcessBracketToken },
			{ SyntaxKind.CloseBracketToken, ProcessBracketToken },
			{ SyntaxKind.LessThanToken, ProcessCompareToken },
			{ SyntaxKind.LessThanEqualsToken, ProcessCompareToken },
			{ SyntaxKind.GreaterThanToken, ProcessCompareToken },
			{ SyntaxKind.GreaterThanEqualsToken, ProcessCompareToken },
			{ SyntaxKind.EndRegionKeyword, ProcessEndRegion },
			{ SyntaxKind.EndIfKeyword, ProcessEndIf },
			{ SyntaxKind.HashToken, ProcessHashToken },
			{ SyntaxKind.VoidKeyword, TokenUnavailable },
			{ SyntaxKind.TypeOfKeyword, c => c.symbol = c.semanticModel.GetSystemTypeSymbol(nameof(Type)) },
			{ SyntaxKind.ThrowKeyword, ProcessThrowKeyword },
			{ SyntaxKind.StackAllocKeyword, ProcessNewToken },
			{ CodeAnalysisHelper.DotDotToken, ProcessDotDotToken },
			{ CodeAnalysisHelper.ExtensionKeyword, ProcessExtension },
			{ SyntaxKind.StringLiteralToken, ProcessStringToken },
			{ SyntaxKind.InterpolatedStringStartToken, ProcessStringToken },
			{ SyntaxKind.InterpolatedStringEndToken, ProcessStringToken },
			{ SyntaxKind.InterpolatedVerbatimStringStartToken, ProcessStringToken },
			{ SyntaxKind.InterpolatedStringToken, ProcessStringToken },
			{ SyntaxKind.InterpolatedStringTextToken, ProcessStringToken },
			{ SyntaxKind.NameOfKeyword, ProcessStringToken },
			{ CodeAnalysisHelper.Utf8StringLiteralToken, ProcessNewToken },
			{ CodeAnalysisHelper.SingleLineRawStringLiteralToken, ProcessStringToken },
			{ CodeAnalysisHelper.MultiLineRawStringLiteralToken, ProcessStringToken },
			{ SyntaxKind.CharacterLiteralToken, ProcessCharToken },
			{ SyntaxKind.NumericLiteralToken, ProcessNumericToken },
			{ SyntaxKind.XmlComment, TokenUnavailable },
			{ SyntaxKind.XmlCommentStartToken, TokenUnavailable },
			{ SyntaxKind.XmlCommentEndToken, TokenUnavailable },
			{ SyntaxKind.XmlCDataSection, TokenUnavailable },
			{ SyntaxKind.XmlCDataStartToken, TokenUnavailable },
			{ SyntaxKind.XmlCDataEndToken, TokenUnavailable },
			{ SyntaxKind.XmlCrefAttribute, TokenUnavailable },
			{ SyntaxKind.XmlElement, TokenUnavailable },
			{ SyntaxKind.XmlElementStartTag, TokenUnavailable },
			{ SyntaxKind.XmlElementEndTag, TokenUnavailable },
			{ SyntaxKind.XmlEmptyElement, TokenUnavailable },
			{ SyntaxKind.XmlEntityLiteralToken, TokenUnavailable },
			{ SyntaxKind.XmlName, TokenUnavailable },
			{ SyntaxKind.XmlNameAttribute, TokenUnavailable },
			{ SyntaxKind.XmlPrefix, TokenUnavailable },
			{ SyntaxKind.XmlProcessingInstruction, TokenUnavailable },
			{ SyntaxKind.XmlProcessingInstructionStartToken, TokenUnavailable },
			{ SyntaxKind.XmlProcessingInstructionEndToken, TokenUnavailable },
			{ SyntaxKind.XmlText, TokenUnavailable },
			{ SyntaxKind.XmlTextAttribute, TokenUnavailable },
			{ SyntaxKind.XmlTextLiteralNewLineToken, TokenUnavailable },
			{ SyntaxKind.XmlTextLiteralToken, TokenUnavailable },
			{ SyntaxKind.ArgListKeyword, ProcessAsConvertedType },
			{ SyntaxKind.MakeRefKeyword, ProcessAsConvertedType },
			{ SyntaxKind.RefTypeKeyword, ProcessAsConvertedType },
			{ SyntaxKind.RefValueKeyword, ProcessAsConvertedType },
		};

		static void ProcessToken(Context ctx) {
			if (ctx.token.Kind().IsPredefinedSystemType()) {
				ctx.symbol = ctx.semanticModel.GetSystemTypeSymbol(ctx.token.Kind());
				return;
			}
			if (ctx.token.Span.Contains(ctx.TriggerPoint, true) == false
				|| ctx.token.IsReservedKeyword()) {
				if (ctx.node is StatementSyntax) {
					ShowBlockInfo(ctx);
				}

				if (ctx.Container.ItemCount > 0) {
					ctx.Result = ctx.CreateQuickInfoItem(ctx.Container);
				}
				else {
					ctx.State = State.Undefined;
				}
				return;
			}
			ctx.State = State.Undefined;
		}

		static void ProcessCharToken(Context ctx) {
			ctx.UseTokenNode();
			ctx.symbol = ctx.semanticModel.Compilation.GetSpecialType(SpecialType.System_Char);
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues)) {
				if (ctx.token.Span.Length >= 8) { // tooltip for '\u0000' presentation
					ctx.Container.Add(new BigTextInfoBlock(ctx.token.ValueText));
				}
				var node = ctx.node;
				ctx.Container.Add(new NumericInfoBlock(node.GetFirstToken().Value, node.Parent.IsKind(SyntaxKind.UnaryMinusExpression)));
			}
			ctx.isConvertedType = true;
			ctx.State = State.Process;
		}

		static void ProcessNumericToken(Context ctx) {
			ctx.UseTokenNode();
			ctx.symbol = ctx.semanticModel.GetSystemTypeSymbol(ctx.token.Value.GetType().Name);
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues)) {
				var node = ctx.node;
				ctx.Container.Add(new NumericInfoBlock(node.GetFirstToken().Value, node.Parent.IsKind(SyntaxKind.UnaryMinusExpression)));
			}
			ctx.isConvertedType = true;
			ctx.State = State.Process;
		}

		static void ProcessStringToken(Context ctx) {
			ctx.UseTokenNode();
			ctx.symbol = ctx.semanticModel.Compilation.GetSpecialType(SpecialType.System_String);
			ctx.isConvertedType = true;
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.String)
				&& ctx.node.IsAnyKind(SyntaxKind.StringLiteralExpression, SyntaxKind.InterpolatedStringText)) {
				ctx.Container.Add(new StringInfoBlock(ctx.node.GetFirstToken().ValueText, false));
			}
			ctx.State = State.PredefinedSymbol;
		}

		static void ProcessExtension(Context ctx) {
			ctx.symbol = ctx.semanticModel.GetDeclaredSymbol(ctx.node = ctx.token.Parent, ctx.cancellationToken);
			ctx.State = State.Process;
		}

		static void ProcessDotDotToken(Context ctx) {
			ctx.symbol = ctx.semanticModel.Compilation.GetSpecialType(SpecialType.System_Int32);
			ctx.isConvertedType = true;
			ctx.State = State.PredefinedSymbol;
		}

		static void ProcessNewToken(Context ctx) {
			ctx.SetSymbol(ctx.semanticModel.GetTypeInfo(ctx.node = ctx.token.Parent, ctx.cancellationToken));
			if (ctx.symbol != null) {
				ctx.State = State.Process;
			}
		}

		static void ProcessThrowKeyword(Context ctx) {
			ExpressionSyntax throwExpr;
			if ((ctx.node = ctx.token.Parent) is ThrowStatementSyntax t) {
				// do not use SetSymbol which prefers ConvertedType,
				// which will be Exception instead of concrete exception
				throwExpr = t.Expression ?? t.FirstAncestorOrSelf<CatchClauseSyntax>()?.Declaration?.Type;
			}
			else if (ctx.node is ThrowExpressionSyntax te) {
				if ((throwExpr = te.Expression)?.IsMissing != false) {
					throwExpr = te.FirstAncestorOrSelf<CatchClauseSyntax>()?.Declaration?.Type;
				}
			}
			else {
				return;
			}
			if (throwExpr != null
				&& (ctx.symbol = ctx.semanticModel.GetTypeInfo(throwExpr, ctx.cancellationToken).Type) != null) {
				ctx.State = State.Return;
			}
		}

		static void ProcessHashToken(Context ctx) {
			ctx.token = ctx.token.GetNextToken();
			if (ctx.token.IsKind(SyntaxKind.EndRegionKeyword)) {
				ProcessEndRegion(ctx);
				return;
			}
			if (ctx.token.IsKind(SyntaxKind.EndIfKeyword)) {
				ProcessEndIf(ctx);
				return;
			}
			ctx.State = State.Unavailable;
		}

		static void ProcessEndIf(Context ctx) {
			ctx.Container.Add(new BlockItem(IconIds.Region, R.T_EndOfIf)
				.Append((ctx.token.Parent as EndIfDirectiveTriviaSyntax).GetIf()?.GetDeclarationSignature(), true)
				);
			ctx.Result = ctx.CreateQuickInfoItem(ctx.Container);
		}

		static void ProcessEndRegion(Context ctx) {
			ctx.Container.Add(new BlockItem(IconIds.Region, R.T_EndOfRegion)
				.Append((ctx.token.Parent as EndRegionDirectiveTriviaSyntax).GetRegion()?.GetDeclarationSignature(), true)
				);
			ctx.Result = ctx.CreateQuickInfoItem(ctx.Container);
		}

		static void ProcessCompareToken(Context ctx) {
			var node = ctx.node = ctx.token.Parent;
			if (node is BinaryExpressionSyntax) {
				ctx.State = State.Process;
			}
			else if (node.IsKind(SyntaxKind.TypeArgumentList)) {
				ctx.SetSymbol(ctx.semanticModel.GetSymbolInfo(ctx.node = node.Parent, ctx.cancellationToken));
				ctx.State = State.Process;
			}
			else if (node.IsKind(CodeAnalysisHelper.RelationPattern)) {
				ctx.SetSymbol(ctx.semanticModel.GetTypeInfo(ctx.node, ctx.cancellationToken));
				ctx.State = State.Return;
			}
			else {
				ProcessParenToken(ctx);
			}
		}

		static void ProcessInToken(Context ctx) {
			ForEachStatementInfo info;
			if ((ctx.node = ctx.token.Parent).IsKind(SyntaxKind.ForEachStatement)
						&& (ctx.symbol = (info = ctx.semanticModel.GetForEachStatementInfo((CommonForEachStatementSyntax)ctx.node)).GetEnumeratorMethod) != null) {
				if (info.ElementConversion.Exists && info.ElementConversion.IsIdentity == false) {
					ctx.Container.Add(new BlockItem(IconIds.ExplicitConversion).AddSymbol(info.ElementType, false, __SymbolFormatter));
				}
				ctx.State = State.Return;
			}
		}

		static void ProcessUsingToken(Context ctx) {
			ctx.node = ctx.token.Parent;
			ctx.symbol = ctx.semanticModel.GetDisposeMethodForUsingStatement(ctx.node, ctx.cancellationToken);
			ShowBlockInfo(ctx);
			ShowControlFlowInfo(ctx);
			ctx.State = State.Return;
		}

		static void ProcessBracketToken(Context ctx) {
			SyntaxNode node = ctx.node = ctx.token.Parent;
			if (node.IsKind(SyntaxKind.BracketedArgumentList)
						&& node.Parent.IsKind(SyntaxKind.ElementAccessExpression)) {
				ctx.SetSymbol(ctx.semanticModel.GetSymbolInfo((ElementAccessExpressionSyntax)node.Parent, ctx.cancellationToken));
			}
			else if (node.IsAnyKind(CodeAnalysisHelper.CollectionExpression, CodeAnalysisHelper.ListPatternExpression)) {
				if (node.IsKind(CodeAnalysisHelper.CollectionExpression)) {
					ctx.Container.Add(new BlockItem(IconIds.InstanceMember, R.T_ElementCount + ((ExpressionSyntax)node).GetCollectionExpressionElementsCount().ToText()));
				}
				else {
					ctx.Container.Add(new BlockItem(IconIds.InstanceMember, R.T_PatternCount + ((PatternSyntax)node).GetListPatternsCount().ToText()));
				}
				ctx.SetSymbol(ctx.semanticModel.GetTypeInfo(node, ctx.cancellationToken));
				if (ctx.symbol != null) {
					ctx.State = State.Process;
					return;
				}
			}
			if (ctx.symbol == null) {
				ProcessParenToken(ctx);
			}
			else {
				ctx.isConvertedType = true;
			}
		}

		static void ProcessParenToken(Context ctx) {
			var node = ctx.node = ctx.token.Parent;
			if (node.IsKind(SyntaxKind.ArgumentList)) {
				ctx.node = node.Parent;
				ctx.State = State.Process;
				return;
			}
			else if (node.IsKind(SyntaxKind.TupleExpression)) {
				ctx.State = State.Process;
				return;
			}
			UsePreviousToken(ctx);
		}

		static void ProcessDotToken(Context ctx) {
			ctx.token = ctx.token.GetNextToken();
			ctx.skipTriggerPointCheck = true;
		}

		static void ProcessAwaitToken(Context ctx) {
			var node = ctx.node = ctx.token.Parent as AwaitExpressionSyntax;
			if (node != null) {
				ctx.SetSymbol(ctx.semanticModel.GetTypeInfo(node, ctx.cancellationToken));
			}
			ctx.State = State.Process;
		}

		static void ProcessReturnToken(Context ctx) {
			var tb = ShowReturnInfo(ctx.token.Parent as ReturnStatementSyntax, ctx.semanticModel, ctx.cancellationToken);
			if (tb == null) {
				ctx.State = State.Unavailable;
				return;
			}
			ctx.Result = ctx.CreateQuickInfoItem(tb);
		}

		static void ProcessForEachToken(Context ctx) {
			var node = (CommonForEachStatementSyntax)ctx.token.Parent;
			var info = ctx.semanticModel.GetForEachStatementInfo(node);
			ctx.node = node;
			ctx.symbol = info.GetEnumeratorMethod;
			var collectionType = ctx.semanticModel.GetTypeInfo(node.Expression, ctx.cancellationToken).Type;
			if (collectionType != null && collectionType.TypeKind != TypeKind.Error) {
				var tip = new GeneralInfoBlock();
				tip.Add(new BlockItem(IconIds.ForEach)
						.Append("foreach", __SymbolFormatter.Keyword)
						.Append(" ")
						.AddSymbol(info.ElementType, false, __SymbolFormatter)
						.Append(" in ", __SymbolFormatter.PlainText)
						.AddSymbol(collectionType, false, __SymbolFormatter)
				);
				if (collectionType.TypeKind == TypeKind.Array) {
					tip.Add(new BlockItem(IconIds.None, R.T_ForEachOnArrayWillBeOptimized));
				}
				else {
					tip.Add(new BlockItem(IconIds.None)
						.AppendIcon(IconIds.Method)
						.AddSymbol(info.MoveNextMethod, false, __SymbolFormatter));
					tip.Add(new BlockItem(IconIds.None)
						.AppendIcon(IconIds.ReadonlyProperty)
						.AddSymbol(info.CurrentProperty, false, __SymbolFormatter));
					if (info.DisposeMethod != null) {
						tip.Add(new BlockItem(IconIds.None)
							.AppendIcon(IconIds.Delete)
							.AddSymbol(info.DisposeMethod, false, __SymbolFormatter));
					}
				}
				ctx.Container.Add(tip);
			}
			ShowControlFlowInfo(ctx);
			ctx.State = State.Return;
		}

		static void ProcessCaseToken(Context ctx) {
			if ((ctx.node = ctx.token.Parent).Parent is SwitchSectionSyntax section) {
				ctx.node = section;
				if (Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.CodeFlow)) {
					ShowControlFlowInfo(ctx);
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DataFlow)) {
						var statements = section.Statements;
						var df = ctx.semanticModel.AnalyzeDataFlow(statements.First(), statements.Last());
						if (df.Succeeded) {
							ShowDataFlowAnalysis(ctx, df);
						}
					}
				}
				ctx.State = State.Return;
			}
		}

		static void ProcessDefaultToken(Context ctx) {
			if (ctx.token.Parent.IsKind(SyntaxKind.DefaultSwitchLabel)) {
				ProcessCaseToken(ctx);
			}
			else {
				ProcessValueToken(ctx);
			}
		}

		static void ProcessTypeToken(Context ctx) {
			ctx.State = State.AsType;
		}

		static void ProcessValueToken(Context ctx) {
			ctx.SetSymbol(ctx.semanticModel.GetTypeInfo(ctx.node = ctx.token.Parent, ctx.cancellationToken));
			if (ctx.symbol == null) {
				if (!Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
					ctx.State = State.Unavailable;
				}
				return;
			}
			ctx.isConvertedType = true;
			ctx.State = State.Process;
		}

		static void ProcessStatementKeyword(Context ctx) {
			ctx.UseTokenNode();
			ShowBlockInfo(ctx);
			ShowControlFlowInfo(ctx);
			ctx.State = State.Return;
		}

		static void ProcessIfToken(Context ctx) {
			ctx.UseTokenNode();
			ProcessStatementKeyword(ctx);
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ControlFlow)) {
				var ifStatement = ctx.node as IfStatementSyntax;
				var conditions = 1;
				StatementSyntax elseStatement;
				while ((elseStatement = ifStatement.Else?.Statement) != null) {
					conditions++;
					if ((ifStatement = elseStatement as IfStatementSyntax) == null) {
						break;
					}
				}
				if (conditions > 1) {
					ctx.Container.Add(new GeneralInfoBlock(new BlockItem(IconIds.If, R.T_ConditionalBranch).Append(conditions.ToText(), true, __SymbolFormatter.Number)));
				}
			}
		}

		static void ProcessSwitchToken(Context ctx) {
			var node = ctx.node = ctx.token.Parent;
			if (node.IsKind(CodeAnalysisHelper.SwitchExpression)) {
				ProcessSwitchExpression(ctx, node);
				if (ctx.symbol == null) {
					ctx.State = State.Unavailable;
					return;
				}
				ctx.isConvertedType = true;
			}
			else if (node.IsKind(SyntaxKind.SwitchStatement)) {
				ProcessSwitchStatementNode(ctx);
				ctx.State = State.Return;
				return;
			}
			ctx.State = State.AsType;
		}

		static void ProcessSwitchExpression(Context ctx, SyntaxNode node) {
			ctx.SetSymbol(ctx.semanticModel.GetTypeInfo(node.ChildNodes().First(), ctx.cancellationToken));
			if (ctx.symbol != null) {
				ctx.Container.Add(new BlockItem(IconIds.Input).AddSymbol(ctx.symbol, false, __SymbolFormatter));
			}
			var c = ((ExpressionSyntax)node).GetSwitchExpressionArmsCount();
			if (c > 1) {
				ctx.Container.Add(new BlockItem(IconIds.Switch, R.T_SwitchCases.Replace("<C>", c.ToText())));
			}
			ctx.SetSymbol(ctx.semanticModel.GetTypeInfo(node, ctx.cancellationToken));
		}

		static void ProcessWhileToken(Context ctx) {
			if (ctx.token.Parent.IsKind(SyntaxKind.WhileStatement)) {
				ProcessStatementKeyword(ctx);
			}
		}

		static void ProcessUnsafeKeyword(Context ctx) {
			if (ctx.token.Parent.IsKind(SyntaxKind.UnsafeStatement)) {
				ProcessStatementKeyword(ctx);
			}
		}

		static void ProcessAsConvertedType(Context ctx) {
			ctx.SetSymbol(ctx.semanticModel.GetTypeInfo(ctx.token.Parent, ctx.cancellationToken));
			MarkConvertedTypeIfSymbolNotNull(ctx);
		}

		static void MarkConvertedTypeIfSymbolNotNull(Context ctx) {
			ctx.isConvertedType = ctx.symbol != null;
			ctx.State = State.AsType;
		}

		static void ProcessEqualsGreaterThanToken(Context ctx) {
			SyntaxNode node;
			ctx.node = node = ctx.token.Parent;
			if (node.IsKind(CodeAnalysisHelper.SwitchExpressionArm) && node.Parent.IsKind(CodeAnalysisHelper.SwitchExpression)) {
				ctx.SetSymbol(ctx.semanticModel.GetTypeInfo(node.Parent, ctx.cancellationToken));
				var patternType = ctx.semanticModel.GetTypeInfo(node.GetSwitchExpressionArmPattern(), ctx.cancellationToken);
				if (patternType.ConvertedType != null) {
					var typeInfo = new BlockItem(IconIds.Input).AddSymbol(patternType.ConvertedType, null, __SymbolFormatter);
					if (patternType.ConvertedType.Equals(patternType.Type) == false) {
						typeInfo.Append(" (", __SymbolFormatter.PlainText)
							.AddSymbol(patternType.Type, null, __SymbolFormatter)
							.Append(")", __SymbolFormatter.PlainText);
					}
					ctx.Container.Add(typeInfo);
				}
				MarkConvertedTypeIfSymbolNotNull(ctx);
			}
		}

		static void ProcessAsBoolean(Context ctx) {
			ctx.symbol = ctx.semanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean);
			MarkConvertedTypeIfSymbolNotNull(ctx);
		}

		static void ProcessAsType(Context ctx) {
			var asType = (ctx.token.Parent as BinaryExpressionSyntax)?.GetLastIdentifier();
			if (asType != null) {
				ctx.token = asType.Identifier;
				ctx.skipTriggerPointCheck = true;
			}
			ctx.State = State.AsType;
		}

		static void ProcessCloseBraceToken(Context ctx) {
			ctx.keepBuiltInXmlDoc = true;
			if ((ctx.node = ctx.token.Parent).IsKind(SyntaxKind.Interpolation)) {
				UsePreviousToken(ctx);
				return;
			}
			ShowBlockInfo(ctx);
			ctx.State = State.Return;
		}

		static void UsePreviousToken(Context ctx) {
			ctx.token = ctx.token.GetPreviousToken();
			ctx.skipTriggerPointCheck = true;
			ctx.State = State.ReparseToken;
		}

		static void ProcessOpenBraceToken(Context ctx) {
			switch ((ctx.node = ctx.token.Parent).Kind()) {
				case SyntaxKind.Interpolation:
					ctx.symbol = ctx.semanticModel.Compilation.GetSpecialType(SpecialType.System_String);
					ctx.isConvertedType = ctx.symbol != null;
					ctx.State = State.Process;
					return;
				case CodeAnalysisHelper.RecursivePattern:
					ctx.SetSymbol(ctx.semanticModel.GetTypeInfo(ctx.node, ctx.cancellationToken));
					ctx.State = State.Process;
					return;
				case SyntaxKind.CollectionInitializerExpression:
				case SyntaxKind.ComplexElementInitializerExpression:
					ctx.SetSymbol(ctx.semanticModel.GetCollectionInitializerSymbolInfo((ExpressionSyntax)ctx.node, ctx.cancellationToken));
					ctx.State = State.Process;
					break;
				default:
					ctx.State = State.Return;
					break;
			}
			ctx.keepBuiltInXmlDoc = true;
			ShowBlockInfo(ctx);
		}

		static void TokenUnavailable(Context context) {
			context.State = State.Unavailable;
		}
	}
}
