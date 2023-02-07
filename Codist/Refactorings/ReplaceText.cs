using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
				ITextSnapshotLine ls = sn.GetLineFromPosition(s.Start.Position), le;
				return ls.Start == s.Start.Position
					&& (le = sn.GetLineFromPosition(s.End.Position - 1)).EndIncludingLineBreak == s.End.Position;
			}

			public override void Refactor(SemanticContext ctx) {
				var sp = ctx.View.Selection.Start.Position;
				ctx.View.Edit(ctx, (v, p, edit) => {
					var s = v.Selection;
					var newLine = v.TextSnapshot.Lines.FirstOrDefault()?.GetLineBreakText()
						?? v.Options.GetOptionValue(Microsoft.VisualStudio.Text.Editor.DefaultOptions.NewLineCharacterOptionId);
					edit.Insert(s.Start.Position, _Start + newLine);
					edit.Insert(s.End.Position, _End + newLine);
				});
				ctx.View.SelectSpan(sp + _SelectStart, _SelectLength, 1);
			}
		}
	}
}