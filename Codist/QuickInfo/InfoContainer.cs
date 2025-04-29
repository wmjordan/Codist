using System;
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;

namespace Codist.QuickInfo
{
	sealed class InfoContainer
	{
		readonly ImmutableArray<object>.Builder _List = ImmutableArray.CreateBuilder<object>();

		public int ItemCount => _List.Count;

		public void Insert(int index, object item) {
			if (item != null) {
				_List.Insert(index, item);
			}
		}
		public void Add(object item) {
			if (item != null) {
				_List.Add(item);
			}
		}

		public StackPanel ToUI() {
			var s = new StackPanel();
			foreach (var item in _List) {
				if (item is InfoBlock b) {
					s.Children.Add(b.ToUI());
				}
				else if (item is UIElement u) {
					s.Children.Add(u);
				}
				else if (item is string t) {
					s.Children.Add(new ThemedTipText(t));
				}
			}
			return s;
		}
	}

	abstract class InfoBlock
	{
		public abstract UIElement ToUI();
	}

	sealed class GeneralInfoBlock : InfoBlock
	{
		public BlockTitle Title { get; set; }
		public Chain<BlockItem> Items { get; private set; }

		public GeneralInfoBlock() { }

		public GeneralInfoBlock(int iconId, string text) {
			Title = new BlockTitle(iconId, text);
		}

		public GeneralInfoBlock(BlockItem item) {
			Items = new Chain<BlockItem>(item);
		}

		public void AddBlock(BlockItem item) {
			if (Items == null) {
				Items = new Chain<BlockItem>(item);
			}
			else {
				Items.Add(item);
			}
		}

		public override UIElement ToUI() {
			if (Title == null && Items == null) {
				return null;
			}
			var doc = new ThemedTipDocument();
			if (Title != null) {
				doc.AppendTitle(Title.IconId, Title.Text);
			}
			if (Items != null) {
				foreach (var item in Items) {
					doc.AppendParagraph(item.IconId, item.ToUI());
				}
			}
			return doc;
		}
	}

	sealed class BlockTitle
	{
		public BlockTitle(int iconId, string text) {
			IconId = iconId;
			Text = text;
		}

		public int IconId { get; }
		public string Text { get; }
	}

	sealed class BlockItem
	{
		public BlockItem(int iconId, string text) {
			IconId = iconId;
			Segments = new Chain<Segment>(new TextSegment(text));
		}

		public int IconId { get; set; }
		public Chain<Segment> Segments { get; }

		public BlockItem Append(string text) {
			Segments.Add(new TextSegment(text));
			return this;
		}

		public BlockItem Append(string text, bool bold) {
			Segments.Add(new TextSegment(text, bold ? SegmentStyle.Bold : SegmentStyle.Default));
			return this;
		}

		public BlockItem Append(string text, Brush foreground) {
			Segments.Add(new TextSegment(text) { Foreground = foreground });
			return this;
		}

		public BlockItem AddSymbol(ISymbol symbol) {
			Segments.Add(new SymbolSegment(symbol));
			return this;
		}

		public BlockItem AddSymbol(ISymbol symbol, bool bold) {
			Segments.Add(new SymbolSegment(symbol, bold ? SegmentStyle.Bold : SegmentStyle.Default));
			return this;
		}

		public BlockItem AddSymbol(ISymbol symbol, string alias) {
			Segments.Add(new SymbolSegment(symbol) { Text = alias });
			return this;
		}

		public TextBlock ToUI() {
			var text = new ThemedTipText();
			var inlines = text.Inlines;
			foreach (var item in Segments) {
				item.ToUI(inlines);
			}
			return text;
		}
	}

	enum SegmentType
	{
		Text,
		Symbol,
		Icon,
		AttributeData,
		Custom
	}

	[Flags]
	enum SegmentStyle
	{
		Default,
		Bold = 1,
		Italic = 1 << 1,
		Underline = 1 << 2,
	}

	abstract class Segment
	{
		public abstract SegmentType Type { get; }

		public abstract void ToUI(InlineCollection inlines);
	}

	sealed class SymbolSegment : TextSegment
	{
		public SymbolSegment(ISymbol symbol) {
			Symbol = symbol;
		}
		public SymbolSegment(ISymbol symbol, SegmentStyle style) {
			Symbol = symbol;
			Style = style;
		}

		public override SegmentType Type => SegmentType.Symbol;
		public ISymbol Symbol { get; }

		public override void ToUI(InlineCollection inlines) {
			SymbolFormatter.Instance.Format(inlines, Symbol, Text, Style.MatchFlags(SegmentStyle.Bold));
		}
	}

	sealed class AttributeDataSegment : TextSegment
	{
		public AttributeDataSegment(AttributeData data, int dataType) {
			Data = data;
			DataType = dataType;
		}

		public override SegmentType Type => SegmentType.AttributeData;
		public AttributeData Data { get; }
		public int DataType { get; }

		public override void ToUI(InlineCollection inlines) {
			SymbolFormatter.Instance.Format(inlines, Data, DataType);
		}
	}

	sealed class IconSegment : Segment
	{
		public IconSegment(int iconId) {
			IconId = iconId;
		}

		public override SegmentType Type => SegmentType.Icon;
		public int IconId { get; set; }
		public Thickness Margin { get; set; } = WpfHelper.GlyphMargin;

		public override void ToUI(InlineCollection inlines) {
			inlines.Add(new InlineUIContainer(VsImageHelper.GetImage(IconId).WrapMargin(Margin)) { BaselineAlignment = BaselineAlignment.TextTop });
		}
	}

	class TextSegment : Segment
	{
		public TextSegment() {}

		public TextSegment(string text) {
			Text = text;
		}
		public TextSegment(string text, SegmentStyle style) {
			Text = text;
			Style = style;
		}

		public override SegmentType Type => SegmentType.Text;
		public string Text { get; set; }
		public SegmentStyle Style { get; set; }
		public Brush Foreground { get; set; }

		public override void ToUI(InlineCollection inlines) {
			var run = new Run(Text);
			if (Style.MatchFlags(SegmentStyle.Bold)) {
				run.FontWeight = FontWeights.Bold;
			}
			if (Style.MatchFlags(SegmentStyle.Italic)) {
				run.FontStyle = FontStyles.Italic;
			}
			if (Style.MatchFlags(SegmentStyle.Underline)) {
				run.TextDecorations.Add(TextDecorations.Underline);
			}
			run.Foreground = Foreground ?? SymbolFormatter.Instance.PlainText;
			inlines.Add(run);
		}
	}
}
