using System;
using System.Linq;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Text;
using R = Codist.Properties.Resources;

namespace Codist.Refactorings
{
	abstract class ReplaceToken : IRefactoring
	{
		public static readonly ReplaceToken InvertOperator = new InvertOperatorRefactoring();
		public static readonly ReplaceToken UseExplicitType = new UseExplicitTypeRefactoring();
		public static readonly ReplaceToken UseStaticDefault = new UseStaticDefaultRefactoring();

		public abstract int IconId { get; }
		public abstract string Title { get; }

		public abstract bool Accept(RefactoringContext ctx);
		protected abstract string GetReplacement(SemanticContext ctx, SyntaxToken token);

		public void Refactor(SemanticContext ctx) {
			var view = ctx.View;
			var token = ctx.Token;
			var rep = GetReplacement(ctx, token);
			view.Edit(
				(rep, sel: token.Span.ToSpan()),
				(v, p, edit) => edit.Replace(p.sel, p.rep)
			);
			view.MoveCaret(token.SpanStart);
			view.Selection.Select(new SnapshotSpan(view.TextSnapshot, token.SpanStart, rep.Length), false);
		}

		sealed class UseExplicitTypeRefactoring : ReplaceToken
		{
			public override int IconId => IconIds.Class;
			public override string Title => R.CMD_UseExplicitType;

			public override bool Accept(RefactoringContext ctx) {
				var token = ctx.Token;
				return token.IsKind(SyntaxKind.IdentifierToken)
					&& token.Text == "var"
					&& ctx.SemanticContext.SemanticModel.GetSymbol(ctx.Node) != null;
			}

			protected override string GetReplacement(SemanticContext ctx, SyntaxToken token) {
				return ctx.SemanticModel.GetSymbol(ctx.Node)
					?.ToMinimalDisplayString(ctx.SemanticModel, ctx.Node.SpanStart)
					?? "var";
			}
		}

		sealed class UseStaticDefaultRefactoring : ReplaceToken
		{
			string _Title;
			public override int IconId => IconIds.UseStaticField;
			public override string Title => _Title;

			public override bool Accept(RefactoringContext ctx) {
				var token = ctx.Token;
				if (token.IsAnyKind(SyntaxKind.NullKeyword, SyntaxKind.DefaultKeyword)
					&& ctx.Node.FirstAncestorOrSelf<SyntaxNode>(n => n.IsKind(SyntaxKind.ParameterList)) == null
					&& ctx.SemanticContext.SemanticModel.GetTypeInfo(ctx.Node).ConvertedType is ITypeSymbol type) {
					if (type.TypeKind.CeqAny(TypeKind.Class, TypeKind.Struct)) {
						switch (type.SpecialType) {
							case SpecialType.System_Boolean:
								_Title = R.CMD_UseDefault.Replace("default", "false");
								return true;
							case SpecialType.System_Byte:
							case SpecialType.System_UInt16:
							case SpecialType.System_UInt32:
							case SpecialType.System_UInt64:
							case SpecialType.System_DateTime:
								_Title = R.CMD_UseDefault.Replace("default", type.Name + ".MinValue");
								return true;
							case SpecialType.System_String:
								_Title = R.CMD_UseDefault.Replace("default", "String.Empty");
								return true;
							case SpecialType.System_IntPtr:
							case SpecialType.System_UIntPtr:
								_Title = R.CMD_UseDefault.Replace("default", type.Name + ".Zero");
								return true;
							default:
								if (type.MatchTypeName(nameof(TimeSpan), "System")) {
									goto case SpecialType.System_DateTime;
								}
								break;
						}
						var m = type.GetMembers().FirstOrDefault(i => MayBeDefaultMemberName(i.Name)
							&& i.IsStatic
							&& (i is IFieldSymbol f && (f.IsConst || f.IsReadOnly)
								|| i is IPropertySymbol p && p.IsReadOnly)
							);
						if (m != null) {
							_Title = R.CMD_UseDefault.Replace("default", type.Name+"."+m.Name);
							return true;
						}

						if (token.IsKind(SyntaxKind.NullKeyword)) {
							_Title = R.CMD_UseDefault;
							return true;
						}
					}

					if (type.Kind == SymbolKind.ArrayType && type.BaseType.GetMembers("Empty").Length > 0) {
						_Title = R.CMD_UseDefault.Replace("default", "Array.Empty");
						return true;
					}
				}
				return false;
			}

			protected override string GetReplacement(SemanticContext ctx, SyntaxToken token) {
				if (!(ctx.SemanticModel.GetTypeInfo(ctx.Node).ConvertedType is ITypeSymbol type)) {
					return String.Empty;
				}
				if (type.TypeKind.CeqAny(TypeKind.Class, TypeKind.Struct)) {
					switch (type.SpecialType) {
						case SpecialType.System_Boolean:
							return "false";
						case SpecialType.System_Byte:
						case SpecialType.System_UInt16:
						case SpecialType.System_UInt32:
						case SpecialType.System_UInt64:
						case SpecialType.System_DateTime:
							return type.ToMinimalDisplayString(ctx.SemanticModel, token.SpanStart) + ".MinValue";
						case SpecialType.System_String:
							return type.ToMinimalDisplayString(ctx.SemanticModel, token.SpanStart) + ".Empty";
						case SpecialType.System_IntPtr:
						case SpecialType.System_UIntPtr:
							return type.ToMinimalDisplayString(ctx.SemanticModel, token.SpanStart) + ".Zero";
						default:
							if (type.MatchTypeName(nameof(TimeSpan), "System")) {
								goto case SpecialType.System_DateTime;
							}
							break;
					}

					var m = type.GetMembers().FirstOrDefault(i => MayBeDefaultMemberName(i.Name)
						&& i.IsStatic
						&& (i is IFieldSymbol f && (f.IsConst || f.IsReadOnly)
							|| i is IPropertySymbol p && p.IsReadOnly)
						);
					if (m != null) {
						return type.ToMinimalDisplayString(ctx.SemanticModel, token.SpanStart) + "." + m.Name;
					}

					if (token.IsKind(SyntaxKind.NullKeyword)) {
						return "default";
					}
				}

				if (type.Kind == SymbolKind.ArrayType) {
					return $"{type.BaseType.ToMinimalDisplayString(ctx.SemanticModel, token.SpanStart)}.Empty<{((IArrayTypeSymbol)type).ElementType.ToMinimalDisplayString(ctx.SemanticModel, token.SpanStart)}>";
				}
				return String.Empty;
			}

			static bool MayBeDefaultMemberName(string name) {
				return name == "Empty" || name == "Default" || name == "Zero" || name == "Null";
			}
		}

		sealed class InvertOperatorRefactoring : ReplaceToken
		{
			public override int IconId => IconIds.InvertOperator;
			public override string Title => R.CMD_InvertOperator;

			public override bool Accept(RefactoringContext ctx) {
				switch (ctx.Token.Kind()) {
					case SyntaxKind.EqualsEqualsToken:
					case SyntaxKind.ExclamationEqualsToken:
					case SyntaxKind.AmpersandAmpersandToken:
					case SyntaxKind.BarBarToken:
					case SyntaxKind.MinusMinusToken:
					case SyntaxKind.PlusPlusToken:
					case SyntaxKind.LessThanToken:
					case SyntaxKind.GreaterThanToken:
					case SyntaxKind.LessThanEqualsToken:
					case SyntaxKind.GreaterThanEqualsToken:
					case SyntaxKind.PlusToken:
					case SyntaxKind.MinusToken:
					case SyntaxKind.AsteriskToken:
					case SyntaxKind.SlashToken:
					case SyntaxKind.AmpersandToken:
					case SyntaxKind.BarToken:
					case SyntaxKind.LessThanLessThanToken:
					case SyntaxKind.GreaterThanGreaterThanToken:
					case SyntaxKind.PlusEqualsToken:
					case SyntaxKind.MinusEqualsToken:
					case SyntaxKind.AsteriskEqualsToken:
					case SyntaxKind.SlashEqualsToken:
					case SyntaxKind.LessThanLessThanEqualsToken:
					case SyntaxKind.GreaterThanGreaterThanEqualsToken:
					case SyntaxKind.AmpersandEqualsToken:
					case SyntaxKind.BarEqualsToken:
						return true;
				}
				return false;
			}

			protected override string GetReplacement(SemanticContext ctx, SyntaxToken token) {
				switch (token.Kind()) {
					case SyntaxKind.EqualsEqualsToken: return "!=";
					case SyntaxKind.ExclamationEqualsToken: return "==";
					case SyntaxKind.AmpersandAmpersandToken: return "||";
					case SyntaxKind.BarBarToken: return "&&";
					case SyntaxKind.MinusMinusToken: return "++";
					case SyntaxKind.PlusPlusToken: return "--";
					case SyntaxKind.LessThanToken: return ">=";
					case SyntaxKind.GreaterThanToken: return "<=";
					case SyntaxKind.LessThanEqualsToken: return ">";
					case SyntaxKind.GreaterThanEqualsToken: return "<";
					case SyntaxKind.PlusToken: return "-";
					case SyntaxKind.MinusToken: return "+";
					case SyntaxKind.AsteriskToken: return "/";
					case SyntaxKind.SlashToken: return "*";
					case SyntaxKind.AmpersandToken: return "|";
					case SyntaxKind.BarToken: return "&";
					case SyntaxKind.LessThanLessThanToken: return ">>";
					case SyntaxKind.GreaterThanGreaterThanToken: return "<<";
					case SyntaxKind.PlusEqualsToken: return "-=";
					case SyntaxKind.MinusEqualsToken: return "+=";
					case SyntaxKind.AsteriskEqualsToken: return "/=";
					case SyntaxKind.SlashEqualsToken: return "*=";
					case SyntaxKind.LessThanLessThanEqualsToken: return ">>=";
					case SyntaxKind.GreaterThanGreaterThanEqualsToken: return "<<=";
					case SyntaxKind.AmpersandEqualsToken: return "|=";
					case SyntaxKind.BarEqualsToken: return "&=";
				}
				return String.Empty;
			}
		}
	}
}
