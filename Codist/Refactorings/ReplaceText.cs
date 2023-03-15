using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;

namespace Codist.Refactorings
{
	abstract class ReplaceText : IRefactoring
	{
		public static readonly ReplaceText WrapInRegion = new WrapInTextRefactoring(R.CMD_SurroundWithRegion, "#region RegionName", "#endregion", 8/*lengthof("#region ")*/, 10/*lengthof(RegionName)*/);
		public static readonly ReplaceText WrapInIf = new WrapInTextRefactoring(R.CMD_SurroundWithIf, "#if DEBUG", "#endif", 4/*lengthof("#if ")*/, 5/*lengthof(DEBUG)*/);

		public abstract int IconId { get; }
		public abstract string Title { get; }

		public abstract bool Accept(RefactoringContext context);

		public abstract void Refactor(SemanticContext context);

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

			public override bool Accept(RefactoringContext context) {
				var v = context.SemanticContext.View;
				var s = v.Selection;
				if (s.IsEmpty || s.Mode != TextSelectionMode.Stream) {
					return false;
				}
				var sn = v.TextSnapshot;
				int ss = s.Start.Position.Position, se;
				ITextSnapshotLine ls = sn.GetLineFromPosition(ss), le;
				var p1 = ss - ls.Start.Position;
				var w = ls.CountLinePrecedingWhitespace();
				if ((p1 >= 0 && p1 <= w)) {
					se = s.End.Position.Position;
					le = sn.GetLineFromPosition(se - 1);
					if (le.EndIncludingLineBreak.Position == se || le.End.Position == se) {
						return IsWhitespaceTrivia(context.SemanticContext.Compilation.FindTrivia(ss)) && IsWhitespaceTrivia(context.SemanticContext.Compilation.FindTrivia(se));
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
					var newLine = sl.GetLineBreakText()
						?? v.Options.GetOptionValue(Microsoft.VisualStudio.Text.Editor.DefaultOptions.NewLineCharacterOptionId);
					edit.Insert(sl.Start.Position, indent + _Start + newLine);
					edit.Insert(le.EndIncludingLineBreak.Position, indent + _End + newLine);
				});
				ctx.View.SelectSpan(sp + indent.Length + _SelectStart, _SelectLength, 1);
			}

			static bool IsWhitespaceTrivia(SyntaxTrivia trivia) {
				var k = trivia.RawKind;
				return k == 0
					|| k == (int)SyntaxKind.WhitespaceTrivia
					|| k == (int)SyntaxKind.EndOfLineTrivia;
			}
		}
	}
}