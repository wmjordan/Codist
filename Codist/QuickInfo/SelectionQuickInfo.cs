using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	sealed class SelectionQuickInfo : SingletonQuickInfoSource
	{
		const string Name = "SelectionInfo";

		protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			return Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Selection) == false
				|| session.Mark(nameof(SelectionQuickInfo)) == false
				? Task.FromResult<QuickInfoItem>(null)
				: InternalGetQuickInfoItemAsync(session, cancellationToken);
		}

		static Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			var textSnapshot = session.TextView.TextSnapshot;
			var triggerPoint = session.GetTriggerPoint(textSnapshot).GetValueOrDefault();
			try {
				return Task.FromResult(ShowSelectionInfo(session, triggerPoint));
			}
			catch (ArgumentException /*triggerPoint has a differ TextBuffer from textSnapshot*/) {
				return Task.FromResult<QuickInfoItem>(null);
			}
		}

		/// <summary>Displays numbers about selected characters and lines in quick info.</summary>
		static QuickInfoItem ShowSelectionInfo(IAsyncQuickInfoSession session, SnapshotPoint point) {
			var selection = session.TextView.Selection;
			if (selection.IsEmpty) {
				return null;
			}
			var p1 = selection.Start.Position.Position;
			if (point.Position.IsOutside(p1, selection.End.Position.Position)) {
				return null;
			}
			var c = 0;
			var lines = selection.SelectedSpans.Count;
			SnapshotSpan activeSpan = default;
			foreach (var item in selection.SelectedSpans) {
				c += item.Length;
				if (item.Contains(point)) {
					activeSpan = item;
				}
			}
			if (activeSpan.IsEmpty) {
				activeSpan = selection.SelectedSpans[0];
			}
			if (c == 1) {
				return ShowCharacterInfo(activeSpan, point.Snapshot.GetText(p1, 1));
			}
			if (c == 2 && Char.IsHighSurrogate(point.Snapshot[p1])) {
				return ShowCharacterInfo(activeSpan, point.Snapshot.GetText(p1, 2));
			}
			var block = new GeneralInfoBlock() { Name = Name };
			var info = new BlockItem(IconIds.SelectCode, R.T_Selection, true)
				.Append($": {c} {R.T_Characters}");
			if (lines > 1) {
				info.Append($", {lines.ToText()} {R.T_Spans}");
			}
			else {
				lines = selection.StreamSelectionSpan.SnapshotSpan.GetLineSpan().Length;
				if (lines > 0) {
					info.Append($", {(lines + 1).ToText()}{R.T_Lines}");
				}
			}
			block.Add(info);
			return new QuickInfoItem(activeSpan.ToTrackingSpan(), block);
		}

		static QuickInfoItem ShowCharacterInfo(SnapshotSpan activeSpan, string ch) {
			return new QuickInfoItem(activeSpan.ToTrackingSpan(), new CharInfoBlock(ch));
		}

		unsafe static string InternalToHexBinString(byte[] source) {
			int length;
			if (source == null || (length = source.Length) == 0) {
				return String.Empty;
			}
			var result = new string(' ', length << 1);
			fixed (char* p = result)
			fixed (byte* bp = &source[0]) {
				byte* b = bp;
				byte* end = bp + length;
				var mapper = HexBinByteValues.GetHexBinMapper();
				int* h = (int*)p;
				while (b < end) {
					*(h++) = mapper[*(b++)];
				}
				return result;
			}
		}

		sealed class CharInfoBlock : InfoBlock
		{
			public CharInfoBlock(string character) {
				Character = character;
			}

			public string Character { get; }

			public override UIElement ToUI() {
				var ch = Character;
				var unicode = Char.ConvertToUtf32(ch, 0);
				var codes = new StackPanel {
					Margin = WpfHelper.SmallMargin,
					Children = {
					new ThemedTipText($"Unicode: {unicode} / 0x{unicode:X4}")
				}
				};
				if (unicode > 127) {
					codes.Add(new ThemedTipText($"UTF-8: 0x{InternalToHexBinString(Encoding.UTF8.GetBytes(ch))}"));
					codes.Add(new ThemedTipText($"UTF-16: 0x{InternalToHexBinString(Encoding.Unicode.GetBytes(ch))}"));
					codes.Add(new ThemedTipText($"UTF-16BE: 0x{InternalToHexBinString(Encoding.BigEndianUnicode.GetBytes(ch))}"));
					var gb18030 = Encoding.GetEncoding("GB18030");
					if (gb18030 != null) {
						codes.Add(new ThemedTipText($"GB18030: 0x{InternalToHexBinString(gb18030.GetBytes(ch))}"));
					}
				}
				return new StackPanel {
					Name = Name,
					Children = {
					new ThemedTipText(IconIds.SelectCode, R.T_SelectedCharacter),
					new StackPanel {
						Orientation = Orientation.Horizontal,
						Children = {
							new ThemedTipText (ch) {
								FontSize = ThemeCache.ToolTipFontSize * 3,
								Margin = WpfHelper.SmallMargin
							},
							codes
						}
					}
				}
				};
			}
		}

		static class HexBinByteValues
		{
			static readonly int[] __HexBins = InitHexBin();
			internal static readonly ulong QuadrupleZero = ((ulong)__HexBins[0]) << 32 | (uint)__HexBins[0];

			unsafe static int[] InitHexBin() {
				var v = new int[Byte.MaxValue + 1];
				var a = new char[2];
				const int A = 0x41 - 10;
				for (int i = 0; i <= Byte.MaxValue; i++) {
					var t = (byte)(i >> 4);
					a[0] = (char)(t > 9 ? t + A : t + 0x30);
					t = (byte)(i & 0x0F);
					a[1] = (char)(t > 9 ? t + A : t + 0x30);
					fixed (char* p = new string(a)) {
						v[i] = *(int*)p;
					}
				}
				return v;
			}
			public static int[] GetHexBinMapper() {
				return __HexBins;
			}
		}
	}
}
