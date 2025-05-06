using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.QuickInfo
{
	abstract class InfoBlock
	{
		public abstract UIElement ToUI();
	}

	[Export(typeof(IViewElementFactory))]
	[Name("InfoBlock Quick Info Factory")]
	[TypeConversion(from: typeof(InfoBlock), to: typeof(UIElement))]
	[Order(Before = "Default object converter")]
	public class InfoBlockQuickInfoFactory : IViewElementFactory
	{
		public TView CreateViewElement<TView>(ITextView textView, object model)
			where TView : class {
			return model is InfoBlock data
				? data.ToUI().Tag() as TView
				: null;
		}
	}

	sealed class GeneralInfoBlock : InfoBlock
	{
		Chain<BlockItem> _Items;

		public BlockTitle Title { get; set; }

		public GeneralInfoBlock() { }

		public GeneralInfoBlock(int iconId, string text) {
			Title = new BlockTitle(iconId, text);
		}

		public GeneralInfoBlock(BlockItem item) {
			_Items = new Chain<BlockItem>(item);
		}

		public bool HasItem => _Items?.IsEmpty == false;
		public Chain<BlockItem> Items => _Items ?? (_Items = new Chain<BlockItem>());

		public void Add(BlockItem item) {
			if (_Items == null) {
				_Items = new Chain<BlockItem>(item);
			}
			else {
				_Items.Add(item);
			}
		}

		public override UIElement ToUI() {
			if (Title == null && _Items == null) {
				return null;
			}
			var doc = new ThemedTipDocument();
			if (Title != null) {
				doc.AppendTitle(Title.IconId, Title.Text);
			}
			if (_Items != null) {
				foreach (var item in _Items) {
					doc.AppendParagraph(item.IconId, item.ToTextBlock());
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

	sealed class BlockItem : InfoBlock
	{
		public BlockItem() {
			Segments = new Chain<Segment>();
		}
		public BlockItem(int iconId, string text) {
			IconId = iconId;
			Segments = new Chain<Segment>(new TextSegment(text));
		}
		public BlockItem(int iconId, string text, bool bold) {
			IconId = iconId;
			Segments = new Chain<Segment>(new TextSegment(text, bold ? SegmentStyle.Bold : SegmentStyle.Default));
		}

		public int IconId { get; set; }
		public Chain<Segment> Segments { get; }

		public BlockItem SetGlyph(int iconId) {
			IconId = iconId;
			return this;
		}

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

		public BlockItem Append(string text, bool bold, Brush foreground) {
			Segments.Add(new TextSegment(text, bold ? SegmentStyle.Bold : SegmentStyle.Default) { Foreground = foreground });
			return this;
		}

		public BlockItem AppendLine() {
			Segments.Add(new LineBreakSegment());
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

		public BlockItem Append(SnapshotSpan snapshotSpan, string text) {
			Segments.Add(new SnapshotSpanSegment(snapshotSpan, text));
			return this;
		}

		public GeneralInfoBlock MakeBlock() {
			return new GeneralInfoBlock(this);
		}

		public TextBlock ToTextBlock() {
			var text = new ThemedTipText();
			var inlines = text.Inlines;
			foreach (var item in Segments) {
				item.ToUI(inlines);
			}
			return text;
		}

		public override UIElement ToUI() {
			return ToTextBlock();
		}
	}

	enum SegmentType
	{
		Text,
		Symbol,
		Icon,
		LineBreak,
		AttributeData,
		SnapshotSpan,
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

	sealed class SnapshotSpanSegment : TextSegment
	{
		public SnapshotSpanSegment(SnapshotSpan span, string text) {
			Span = span;
			Text = text;
		}

		public SnapshotSpan Span { get; }

		public override SegmentType Type => SegmentType.SnapshotSpan;

		public override void ToUI(InlineCollection inlines) {
			var inline = Span.Render(Text);
			ApplySegmentStyle(inline);
			inlines.Add(inline);
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

	sealed class LineBreakSegment : Segment
	{
		public override SegmentType Type => SegmentType.LineBreak;
		public override void ToUI(InlineCollection inlines) {
			inlines.Add(new LineBreak());
		}
	}

	class TextSegment : Segment
	{
		public TextSegment() { }

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
			ApplySegmentStyle(run);
			inlines.Add(run);
		}

		protected void ApplySegmentStyle(Inline inline) {
			if (Style.MatchFlags(SegmentStyle.Bold)) {
				inline.FontWeight = FontWeights.Bold;
			}
			if (Style.MatchFlags(SegmentStyle.Italic)) {
				inline.FontStyle = FontStyles.Italic;
			}
			if (Style.MatchFlags(SegmentStyle.Underline)) {
				inline.TextDecorations.Add(TextDecorations.Underline);
			}
			inline.Foreground = Foreground ?? SymbolFormatter.Instance.PlainText;
		}
	}
}
