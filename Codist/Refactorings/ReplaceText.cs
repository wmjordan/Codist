using System;
using System.Linq;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;

namespace Codist.Refactorings
{
	abstract class ReplaceText : IRefactoring
	{
		public static readonly ReplaceText WrapInRegionDirective = new WrapInTextRefactoring(R.CMD_SurroundWithRegion, "#region RegionName", "#endregion", 8/*lengthof("#region ")*/, 10/*lengthof(RegionName)*/);
		public static readonly ReplaceText WrapInIfDirective = new WrapInTextRefactoring(R.CMD_SurroundWithIf, "#if DEBUG", "#endif", 4/*lengthof("#if ")*/, 5/*lengthof(DEBUG)*/);
		public static readonly ReplaceText CommentToRegion = new CommentToRegionRefactoring();
		public static readonly ReplaceText SealType = new SealTypeRefactoring();
		public static readonly ReplaceText MakeStatic = new StaticRefactoring();
		public static readonly ReplaceText MakeReadonly = new ReadonlyRefactoring();
		public static readonly ReplaceText MakePublic = new ChangeAccessibilityRefactoring(SyntaxKind.PublicKeyword);
		public static readonly ReplaceText MakeProtected = new ChangeAccessibilityRefactoring(SyntaxKind.ProtectedKeyword);
		public static readonly ReplaceText MakeInternal = new ChangeAccessibilityRefactoring(SyntaxKind.InternalKeyword);
		public static readonly ReplaceText MakePrivate = new ChangeAccessibilityRefactoring(SyntaxKind.PrivateKeyword);
		public static readonly ReplaceText UseVarType = new UseVarTypeRefactoring();
		public static readonly ReplaceText SplitDeclaration = new SplitDeclarationAssignmentRefactoring();

		public abstract int IconId { get; }
		public abstract string Title { get; }

		public abstract bool Accept(RefactoringContext context);

		public abstract void Refactor(SemanticContext context);

		static string GetLineBreakText(ITextSnapshotLine line, ITextView view) {
			return line.GetLineBreakText()
				?? view.Options.GetOptionValue(DefaultOptions.NewLineCharacterOptionId);
		}

		sealed class WrapInTextRefactoring : ReplaceText
		{
			readonly string _Title;
			readonly string _Start, _End;
			readonly int _SelectStart, _SelectLength;

			public WrapInTextRefactoring(string title, string start, string end, int selectStart, int selectLength) {
				_Title = title;
				_Start = start;
				_End = end;
				_SelectStart = selectStart;
				_SelectLength = selectLength;
			}
			public override int IconId => IconIds.SurroundWith;
			public override string Title => _Title;

			public override bool Accept(RefactoringContext ctx) {
				var v = ctx.SemanticContext.View;
				var s = v.Selection;
				if (s.IsEmpty || s.Mode != TextSelectionMode.Stream) {
					return false;
				}
				var sn = v.TextSnapshot;
				int ss = s.Start.Position.Position, se;
				ITextSnapshotLine ls = sn.GetLineFromPosition(ss), le;
				var p1 = ss - ls.Start.Position;
				var w = ls.CountLinePrecedingWhitespace();
				if (p1 >= 0 && p1 <= w) {
					se = s.End.Position.Position;
					le = sn.GetLineFromPosition(se > 0 ? se - 1 : se);
					if (le.EndIncludingLineBreak.Position == se || le.End.Position == se) {
						return IsWhitespaceTrivia(ctx.SemanticContext.Compilation.FindTrivia(ss)) && IsWhitespaceTrivia(ctx.SemanticContext.Compilation.FindTrivia(se));
					}
				}
				return false;
			}

			public override void Refactor(SemanticContext ctx) {
				var sl = ctx.View.TextSnapshot.GetLineFromPosition(ctx.View.Selection.Start.Position);
				var sp = sl.Start.Position;
				var indent = sl.GetLinePrecedingWhitespace();
				ctx.View.Edit(ctx, (v, p, edit) => {
					var s = v.Selection;
					int se = s.End.Position.Position;
					var le = v.TextSnapshot.GetLineFromPosition(se - 1);
					var newLine = GetLineBreakText(sl, v);
					edit.Insert(sl.Start.Position, indent + _Start + newLine);
					edit.Insert(le.EndIncludingLineBreak.Position, indent + _End + newLine);
				});
				ctx.View.SelectSpan(sp + indent.Length + _SelectStart, _SelectLength, 1);
			}

			static bool IsWhitespaceTrivia(SyntaxTrivia trivia) {
				return trivia.RawKind.CeqAny(0, (int)SyntaxKind.WhitespaceTrivia, (int)SyntaxKind.EndOfLineTrivia);
			}
		}

		sealed class CommentToRegionRefactoring : ReplaceText
		{
			static readonly char[] __LeadingCommentChars = new[] { '/', ' ', '\t' };
			public override int IconId => IconIds.Region;
			public override string Title => R.CMD_CommentToRegion;

			public override bool Accept(RefactoringContext ctx) {
				var statements = ctx.SelectedStatementInfo.Items;
				return statements?[0].HasLeadingTrivia == true
					&& GetSoloSingleLineComment(statements[0].GetLeadingTrivia()).IsKind(SyntaxKind.SingleLineCommentTrivia);
			}

			public override void Refactor(SemanticContext ctx) {
				const int LENGTH_OF_REGION = 8;
				var comment = GetSoloSingleLineComment(new RefactoringContext(ctx).SelectedStatementInfo.Items[0].GetLeadingTrivia());
				if (comment.FullSpan.Length == 0) {
					return;
				}
				var commentText = comment.ToFullString().TrimStart(__LeadingCommentChars);
				ctx.View.Edit(new { comment, commentText }, (v, p, edit) => {
					var s = v.Selection;
					var sl = v.TextSnapshot.GetLineFromPosition(v.Selection.Start.Position);
					var indent = sl.GetLinePrecedingWhitespace();
					var newLine = GetLineBreakText(sl, v);
					int se = s.End.Position.Position;
					var le = v.TextSnapshot.GetLineFromPosition(se - 1); 
					edit.Replace(p.comment.FullSpan.ToSpan(), "#region " + p.commentText);
					edit.Insert(le.End, newLine + indent + "#endregion" + newLine);
				});
				ctx.View.SelectSpan(comment.SpanStart + LENGTH_OF_REGION, commentText.Length, 1);
			}

			static SyntaxTrivia GetSoloSingleLineComment(SyntaxTriviaList trivias) {
				const int START = 0, COMMENT = 1, EOL = 2;
				var s = START;
				SyntaxTrivia comment = default;
				foreach (var trivia in trivias) {
					var k = trivia.Kind();
					switch (s) {
						case START:
							switch (k) {
								case SyntaxKind.WhitespaceTrivia:
								case SyntaxKind.EndOfLineTrivia:
									continue;
								case SyntaxKind.SingleLineCommentTrivia:
									s = COMMENT;
									comment = trivia;
									continue;
							}
							goto default;
						case COMMENT:
							if (k == SyntaxKind.EndOfLineTrivia) {
								s = EOL;
								continue;
							}
							goto default;
						case EOL:
							if (k.CeqAny(SyntaxKind.EndOfLineTrivia, SyntaxKind.WhitespaceTrivia)) {
								continue;
							}
							goto default;
						default:
							return default;
					}
				}
				return s == EOL ? comment : default;
			}
		}

		abstract class DeclarationModifierRefactoring : ReplaceText
		{
			protected static MemberDeclarationSyntax GetDeclarationNode(SemanticContext ctx) {
				var node = ctx.Node;
				if (node.IsKind(SyntaxKind.VariableDeclarator)) {
					node = node.Parent.Parent;
				}
				return node as MemberDeclarationSyntax;
			}

			protected static int GetModifierInsertionPoint(MemberDeclarationSyntax node) {
				var attrList = node.GetAttributes(out _);
				return attrList.Count != 0
					? attrList[attrList.Count - 1].FullSpan.End
					: node.SpanStart;
			}
		}

		sealed class SealTypeRefactoring : DeclarationModifierRefactoring
		{
			string _Title;
			public override int IconId => IconIds.SealedClass;
			public override string Title => _Title;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.Node;
				if (node.IsAnyKind(SyntaxKind.ClassDeclaration, CodeAnalysisHelper.RecordDeclaration)
					&& CanBeSealed(((TypeDeclarationSyntax)node).Modifiers)) {
					_Title = node.IsKind(CodeAnalysisHelper.RecordDeclaration)
						? R.CMD_SealRecord
						: R.CMD_SealClass;
					return true;
				}
				return false;
			}

			static bool CanBeSealed(SyntaxTokenList modifiers) {
				foreach (var item in modifiers) {
					switch (item.Kind()) {
						case SyntaxKind.SealedKeyword:
						case SyntaxKind.AbstractKeyword:
						case SyntaxKind.StaticKeyword:
						case SyntaxKind.VirtualKeyword:
							return false;
					}
				}
				return true;
			}

			public override void Refactor(SemanticContext ctx) {
				const int LENGTH_OF_SEALED = 6;
				var d = ctx.Node as TypeDeclarationSyntax;
				var m = d.Modifiers;
				var insertAt = m.FullSpan.Length == 0 ? d.SpanStart
					: m[0].IsAnyKind(SyntaxKind.PublicKeyword, SyntaxKind.InternalKeyword, SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword) ? m[0].FullSpan.End
					: GetModifierInsertionPoint(d);
				ctx.View.Edit(insertAt, (view, param, edit) => edit.Insert(param, "sealed "));
				ctx.View.SelectSpan(insertAt, LENGTH_OF_SEALED, 1);
			}
		}


		sealed class ReadonlyRefactoring : DeclarationModifierRefactoring
		{
			public override int IconId => IconIds.ReadonlyField;
			public override string Title => R.CMD_MakeReadonly;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.Node;
				if (!node.IsKind(SyntaxKind.VariableDeclarator)) {
					return node.IsKind(SyntaxKind.StructDeclaration) && CanBeReadonly((StructDeclarationSyntax)node);
				}
				node = node.Parent.Parent;
				return node.IsAnyKind(SyntaxKind.FieldDeclaration, SyntaxKind.EventFieldDeclaration)
					&& CanBeReadonly(((BaseFieldDeclarationSyntax)node).Modifiers);
			}

			static bool CanBeReadonly(SyntaxTokenList modifiers) {
				foreach (var item in modifiers) {
					switch (item.Kind()) {
						case SyntaxKind.ReadOnlyKeyword:
						case SyntaxKind.ConstKeyword:
						case SyntaxKind.VolatileKeyword:
							return false;
					}
				}
				return true;
			}

			static bool CanBeReadonly(StructDeclarationSyntax node) {
				foreach (var member in node.Members) {
					switch (member.Kind()) {
						case SyntaxKind.FieldDeclaration:
						case SyntaxKind.EventFieldDeclaration:
							if (IsWritableInstance((BaseFieldDeclarationSyntax)member)) {
								return false;
							}
							break;
						case SyntaxKind.PropertyDeclaration:
						case SyntaxKind.EventDeclaration:
							if (IsWritableInstance((BasePropertyDeclarationSyntax)member)) {
								return false;
							}
							break;
					}
				}
				return true;
			}

			static bool IsWritableInstance(BaseFieldDeclarationSyntax field) {
				foreach (var modifier in field.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.StaticKeyword:
						case SyntaxKind.ConstKeyword:
						case SyntaxKind.ReadOnlyKeyword:
							return false;
					}
				}
				return true;
			}

			static bool IsWritableInstance(BasePropertyDeclarationSyntax property) {
				foreach (var modifier in property.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.StaticKeyword:
						case SyntaxKind.ReadOnlyKeyword:
							return false;
					}
				}
				var al = property.AccessorList;
				if (al is null) {
					return false;
				}
				foreach (var accessor in al.Accessors) {
					switch (accessor.Keyword.Kind()) {
						case SyntaxKind.SetKeyword:
							if (accessor.Body is null && accessor.ExpressionBody is null) {
								return true;
							}
							continue;
						case SyntaxKind.AddKeyword:
						case SyntaxKind.RemoveKeyword:
						case CodeAnalysisHelper.InitKeyword:
							return false;
					}
				}
				return false;
			}

			public override void Refactor(SemanticContext ctx) {
				const int LENGTH_OF_READONLY = 8;
				var node = ctx.Node;
				SyntaxTokenList m;
				MemberDeclarationSyntax md;
				int ip; // default insertion point
				if (node.IsKind(SyntaxKind.VariableDeclarator)) {
					if (node.Parent.Parent is BaseFieldDeclarationSyntax d) {
						m = d.Modifiers;
						md = d;
						ip = md.SpanStart;
					}
					else {
						return;
					}
				}
				else if (node.IsKind(SyntaxKind.StructDeclaration)) {
					var d = (StructDeclarationSyntax)node;
					m = d.Modifiers;
					md = d;
					ip = d.Keyword.SpanStart;
				}
				else {
					return;
				}
				var insertAt = m.FullSpan.Length == 0 ? ip
					: m[0].IsAnyKind(SyntaxKind.PublicKeyword, SyntaxKind.InternalKeyword, SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword, SyntaxKind.StaticKeyword) ? m[0].FullSpan.End
					: GetModifierInsertionPoint(md);
				ctx.View.Edit(insertAt, (view, param, edit) => edit.Insert(param, "readonly "));
				ctx.View.SelectSpan(insertAt, LENGTH_OF_READONLY, 1);
			}
		}

		sealed class StaticRefactoring : DeclarationModifierRefactoring
		{
			public override int IconId => IconIds.StaticMember;
			public override string Title => R.CMD_MakeStatic;

			public override bool Accept(RefactoringContext ctx) {
				if (GetDeclarationNode(ctx.SemanticContext) is MemberDeclarationSyntax d) {
					var m = d.GetModifiers(out var canHaveModifier);
					TypeDeclarationSyntax t;
					return canHaveModifier
						&& !d.IsAnyKind(SyntaxKind.ConstructorDeclaration, SyntaxKind.DestructorDeclaration, CodeAnalysisHelper.RecordDeclaration, CodeAnalysisHelper.RecordStructDeclaration)
						&& ((t = d as TypeDeclarationSyntax) == null
							|| t.GetParameterList() == null) // exclude primary constructor
						&& CanBeStatic(m);
				}
				return false;
			}

			static bool CanBeStatic(SyntaxTokenList modifiers) {
				foreach (var item in modifiers) {
					switch (item.Kind()) {
						case SyntaxKind.StaticKeyword:
						case SyntaxKind.OverrideKeyword:
						case SyntaxKind.SealedKeyword:
						case SyntaxKind.VirtualKeyword:
						case SyntaxKind.AbstractKeyword:
							return false;
					}
				}
				return true;
			}

			public override void Refactor(SemanticContext ctx) {
				const int LENGTH_OF_STATIC = 6;
				var d = GetDeclarationNode(ctx);
				if (d == null) {
					return;
				}
				var m = d.GetModifiers(out var canHaveModifier);
				var insertAt = m.FullSpan.Length == 0 ? d.SpanStart
					: m[0].IsAnyKind(SyntaxKind.PublicKeyword, SyntaxKind.InternalKeyword, SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword) ? m[0].FullSpan.End
					: GetModifierInsertionPoint(d);
				ctx.View.Edit(insertAt, (view, param, edit) => edit.Insert(param, "static "));
				ctx.View.SelectSpan(insertAt, LENGTH_OF_STATIC, 1);
			}
		}

		sealed class ChangeAccessibilityRefactoring : DeclarationModifierRefactoring
		{
			readonly SyntaxKind _KeywordKind;
			readonly int _IconId;
			readonly string _Title;

			public override int IconId => _IconId;
			public override string Title => _Title;

			public ChangeAccessibilityRefactoring(SyntaxKind accessibility) {
				switch (_KeywordKind = accessibility) {
					case SyntaxKind.PublicKeyword:
						_IconId = IconIds.PublicSymbols;
						_Title = R.CMD_MakePublic;
						break;
					case SyntaxKind.ProtectedKeyword:
						_IconId = IconIds.ProtectedSymbols;
						_Title = R.CMD_MakeProtected;
						break;
					case SyntaxKind.InternalKeyword:
						_IconId = IconIds.InternalSymbols;
						_Title = R.CMD_MakeInternal;
						break;
					case SyntaxKind.PrivateKeyword:
						_IconId = IconIds.PrivateSymbols;
						_Title = R.CMD_MakePrivate;
						break;
				}
			}

			public override bool Accept(RefactoringContext ctx) {
				var node = GetDeclarationNode(ctx.SemanticContext);
				return node != null && CanChangeAccessibility(node);
			}

			bool CanChangeAccessibility(MemberDeclarationSyntax d) {
				if (d.IsAnyKind(SyntaxKind.EnumMemberDeclaration, CodeAnalysisHelper.ExtensionDeclaration)) {
					return false;
				}
				var m = d.GetModifiers(out var canHaveModifier);
				if (!canHaveModifier
					|| m.Any(_KeywordKind)
					|| m.Any(SyntaxKind.OverrideKeyword)) {
					return false;
				}
				switch (_KeywordKind) {
					case SyntaxKind.PublicKeyword:
					case SyntaxKind.InternalKeyword:
						return true;
					case SyntaxKind.ProtectedKeyword:
						return !m.Any(SyntaxKind.SealedKeyword)
							&& d.Parent is ClassDeclarationSyntax c
							&& !c.Modifiers.Any(SyntaxKind.SealedKeyword);
					case SyntaxKind.PrivateKeyword:
						if (d is BaseTypeDeclarationSyntax t
							&& !t.IsKind(SyntaxKind.InterfaceDeclaration)) {
							return d.Parent is BaseTypeDeclarationSyntax;
						}
						return true;
				}
				return true;
			}

			public override void Refactor(SemanticContext ctx) {
				var d = GetDeclarationNode(ctx);
				var modifiers = d.GetModifiers(out var canHaveModifier);
				if (!canHaveModifier) {
					return;
				}

				var modifier = GetModifier(_KeywordKind);
				if (modifiers.Count != 0 && ctx.View.Edit((modifiers, modifier), (view, param, edit) => {
					var replaced = false;
					Span span;
					foreach (var item in param.modifiers) {
						switch (item.Kind()) {
							case SyntaxKind.PublicKeyword:
							case SyntaxKind.ProtectedKeyword:
							case SyntaxKind.InternalKeyword:
							case SyntaxKind.PrivateKeyword:
								if (!replaced) {
									replaced = edit.Replace(span = item.Span.ToSpan(), param.modifier);
									view.SelectSpan(span);
								}
								else {
									var firstTrailing = item.TrailingTrivia.FirstOrDefault();
									if (firstTrailing.IsKind(SyntaxKind.WhitespaceTrivia)) {
										var s = item.Span;
										span = new Span(s.Start, s.Length + firstTrailing.FullSpan.Length);
									}
									else {
										span = item.Span.ToSpan();
									}
									edit.Replace(span, String.Empty);
								}
								break;
						}
					}
				}) != null) {
					ctx.View.SelectSpan(ctx.View.GetCaretPosition().Position - modifier.Length, modifier.Length, 0);
					return;
				}

				var tp = modifiers.Count != 0
					? modifiers.Span.Start
					: GetModifierInsertionPoint(d);
				ctx.View.Edit((tp, modifier), (view, param, edit) => edit.Insert(param.tp, param.modifier + " "));
				ctx.View.SelectSpan(tp, modifier.Length, 1);
			}

			static string GetModifier(SyntaxKind kind) {
				switch (kind) {
					case SyntaxKind.PublicKeyword: return "public";
					case SyntaxKind.InternalKeyword: return "internal";
					case SyntaxKind.ProtectedKeyword: return "protected";
					case SyntaxKind.PrivateKeyword: return "private";
				}
				return String.Empty;
			}
		}

		abstract class VarDeclarationRefactoring : DeclarationModifierRefactoring
		{
			protected static SyntaxNode GetVariableDeclarationNode(SyntaxNode node) {
				return node is VariableDeclaratorSyntax
					? node.Parent
					: node is IdentifierNameSyntax name
					? name.Parent.UnqualifyExceptNamespace()
					: node.Parent;
			}
		}

		sealed class UseVarTypeRefactoring : VarDeclarationRefactoring
		{
			public override int IconId => IconIds.Class;
			public override string Title => R.CMD_UseVarType;

			public override bool Accept(RefactoringContext ctx) {
				return GetVariableDeclarationNode(ctx.Node) is VariableDeclarationSyntax dec
					&& !(dec.Type is IdentifierNameSyntax n && n.IsVar)
					&& dec.Parent is LocalDeclarationStatementSyntax loc
					&& !loc.IsConst
					&& dec.Variables.All(i => i.Initializer != null);
			}

			public override void Refactor(SemanticContext ctx) {
				if (GetVariableDeclarationNode(ctx.Node) is VariableDeclarationSyntax dec) {
					switch (dec.Variables.Count) {
						case 0: return;
						case 1:
							var span = dec.Type.Span;
							ctx.View.Edit(span, (view, param, edit) => edit.Replace(param.ToSpan(), "var"));
							ctx.View.SelectSpan(span.Start, 3, 1);
							return;
					}

					ctx.View.Edit((ctx, dec), (view, param, edit) => {
						var (indent, newLine) = param.ctx.GetIndentAndNewLine(param.dec.SpanStart, 0);
						using (var sbr = Microsoft.VisualStudio.Utilities.ReusableStringBuilder.AcquireDefault(128)) {
							var sb = sbr.Resource;
							sb.Append(param.dec.GetLeadingTrivia().ToString());
							bool isFirst = true;
							foreach (var variable in param.dec.Variables) {
								if (isFirst) {
									isFirst = false;
								}
								else {
									sb.Append(newLine).Append(indent);
								}
								sb.Append("var ")
									.Append(variable.ToFullString())
									.Append(';');
							}
							sb.Append(param.dec.Parent.GetTrailingTrivia().ToString());
							edit.Replace(param.dec.Parent.FullSpan.ToSpan(), sb.ToString());
						}
					});
				}
			}
		}

		sealed class SplitDeclarationAssignmentRefactoring : VarDeclarationRefactoring
		{
			public override int IconId => IconIds.SplitCondition;
			public override string Title => R.CMD_SplitDeclarationAssignment;

			public override bool Accept(RefactoringContext ctx) {
				SeparatedSyntaxList<VariableDeclaratorSyntax> vars;
				return GetVariableDeclarationNode(ctx.Node) is VariableDeclarationSyntax dec
					&& !(dec.Type is IdentifierNameSyntax n && n.IsVar)
					&& dec.Parent is LocalDeclarationStatementSyntax loc
					&& !loc.IsConst
					&& dec.Variables.All(i => i.Initializer != null);
			}

			public override void Refactor(SemanticContext ctx) {
				if (GetVariableDeclarationNode(ctx.Node) is VariableDeclarationSyntax dec) {
					SeparatedSyntaxList<VariableDeclaratorSyntax> vars = dec.Variables;
					var v = vars[0];
					ctx.View.Edit((ctx, dec, v, vars), (view, param, edit) => {
						var (indent, newLine) = param.ctx.GetIndentAndNewLine(param.dec.SpanStart, 0);
						var start = param.v.Identifier.Span.Start;
						var end = param.dec.Parent.FullSpan.End;
						using (var sbr = Microsoft.VisualStudio.Utilities.ReusableStringBuilder.AcquireDefault(128)) {
							var sb = sbr.Resource;
							bool isFirst = true;
							foreach (var variable in param.dec.Variables) {
								if (isFirst) {
									isFirst = false;
								}
								else {
									sb.Append(", ");
								}
								sb.Append(variable.Identifier.Text);
							}
							sb.Append(';');
							foreach (var variable in param.dec.Variables) {
								sb.Append(newLine)
									.Append(indent)
									.Append(variable.Identifier.ToFullString())
									.Append(variable.Initializer.ToFullString())
									.Append(';');
							}
							sb.Append(param.dec.Parent.GetTrailingTrivia().ToString());
							edit.Replace(Span.FromBounds(start, end), sb.ToString());
						}
					});
					ctx.View.SelectSpan(v.Identifier.Span.ToSpan());
				}
			}
		}
	}
}