﻿using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using WpfBrush = System.Windows.Media.Brush;

namespace Codist
{
	partial class WpfHelper
	{
		public static TPanel AddReadOnlyTextBox<TPanel>(this TPanel panel, string text, bool alignLeft = false)
		where TPanel : Panel {
			panel.Children.Add(new QuickInfoTextBox {
				Text = text,
				TextAlignment = alignLeft ? TextAlignment.Left : TextAlignment.Right,
				MinWidth = 180,
				MaxWidth = Config.Instance.QuickInfo.MaxWidth > 180 ? Config.Instance.QuickInfo.MaxWidth - 100 : 180,
				TextWrapping = TextWrapping.Wrap,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			});
			return panel;
		}
		public static TextBlock AddImage(this TextBlock block, int imageId) {
			return block.Append(VsImageHelper.GetImage(imageId));
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, string alias, bool bold, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.Format(block.Inlines, symbol, alias, bold);
			}
			return block;
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, bool bold, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.Format(block.Inlines, symbol, null, bold);
			}
			return block;
		}
		public static InlineCollection AddSymbol(this InlineCollection inlines, ISymbol symbol, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.Format(inlines, symbol, null, false);
			}
			return inlines;
		}
		public static InlineCollection AddSymbol(this InlineCollection inlines, ISymbol symbol, bool bold, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.Format(inlines, symbol, null, bold);
			}
			return inlines;
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, string alias, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.Format(block.Inlines, symbol, alias, false);
			}
			else if (String.IsNullOrEmpty(alias) == false) {
				block.Inlines.Add(new Run(alias));
			}
			return block;
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, string alias, WpfBrush brush) {
			if (symbol != null) {
				block.Inlines.Add(symbol.Render(alias, false, brush));
			}
			return block;
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, bool bold, WpfBrush brush) {
			if (symbol != null) {
				block.Inlines.Add(symbol.Render(null, bold, brush));
			}
			return block;
		}
		public static TextBlock AddSymbolDisplayParts(this TextBlock block, ImmutableArray<SymbolDisplayPart> parts, SymbolFormatter formatter) {
			return formatter.Format(block, parts, Int32.MinValue);
		}
		public static TextBlock AddSymbolDisplayParts(this TextBlock block, ImmutableArray<SymbolDisplayPart> parts, SymbolFormatter formatter, int argIndex) {
			return formatter.Format(block, parts, argIndex);
		}
		public static TextBlock AddParameters(this TextBlock block, ImmutableArray<IParameterSymbol> parameters, SymbolFormatter formatter) {
			return formatter.ShowParameters(block, parameters);
		}
		public static TextBlock AddParameters(this TextBlock block, ImmutableArray<IParameterSymbol> parameters, SymbolFormatter formatter, int argIndex) {
			return formatter.ShowParameters(block, parameters, true, false, argIndex);
		}
		public static TextBlock AddXmlDoc(this TextBlock paragraph, XElement content, XmlDocRenderer docRenderer) {
			docRenderer.Render(content, paragraph.Inlines);
			return paragraph;
		}
		public static FrameworkElement AsSymbolLink(this UIElement element, ISymbol symbol) {
			return new SymbolElement(symbol, element);
		}
		public static Inline Render(this ISymbol symbol, string alias, WpfBrush brush) {
			return symbol.Render(alias, brush == null, brush);
		}
		public static Inline Render(this ISymbol symbol, bool bold, WpfBrush brush) {
			return symbol.Render(null, bold, brush);
		}
		public static Inline Render(this ISymbol symbol, string alias, bool bold, WpfBrush brush) {
			var run = new SymbolLink(symbol, alias);
			if (bold) {
				run.FontWeight = FontWeights.Bold;
			}
			if (brush != null) {
				run.Foreground = brush;
			}
			return run;
		}
		public static Inline Render(this SnapshotSpan span, string text) {
			return new SpanLink(span, text);
		}

		public static Inline Render(this SyntaxNodeOrToken syntax, ITextSnapshot textSnapshot, string text) {
			return new SyntaxNodeLink(syntax, textSnapshot, text);
		}

		public static ScrollViewer Scrollable<TElement>(this TElement element)
			where TElement : DependencyObject {
			if (element is TextBlock t && t.TextWrapping == TextWrapping.NoWrap) {
				t.TextWrapping = TextWrapping.Wrap;
			}
			return new ScrollViewer {
				Content = element,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				Padding = ScrollerMargin
			}.ReferenceStyle(VsResourceKeys.GetScrollViewerStyleKey(true));
		}

		public static void SetUITextRenderOptions(DependencyObject element, bool optimize) {
			if (element == null) {
				return;
			}
			//TextOptions.SetTextFormattingMode(element, optimize ? TextFormattingMode.Ideal : TextFormattingMode.Display);
			TextOptions.SetTextHintingMode(element, optimize ? TextHintingMode.Fixed : TextHintingMode.Auto);
			TextOptions.SetTextRenderingMode(element, optimize ? TextRenderingMode.Grayscale : TextRenderingMode.Auto);
		}

		sealed class SymbolElement : Border
		{
			ISymbol _Symbol;

			public SymbolElement(ISymbol symbol, UIElement content) {
				Child = content;
				_Symbol = symbol;
				MouseEnter += InitInteraction;
				Unloaded += SymbolLink_Unloaded;
			}

			void InitInteraction(object sender, MouseEventArgs e) {
				MouseEnter -= InitInteraction;

				Cursor = Cursors.Hand;
				CornerRadius = new CornerRadius(3);
				ToolTip = String.Empty;
				Highlight(sender, e);
				MouseEnter += Highlight;
				MouseLeave += Leave;
				MouseLeftButtonDown += ShowContextMenu;
				MouseRightButtonDown += ShowContextMenu;
			}

			protected override void OnToolTipOpening(ToolTipEventArgs e) {
				base.OnToolTipOpening(e);
				var s = _Symbol;
				if (s != null && ReferenceEquals(ToolTip, String.Empty)) {
					ToolTip = ToolTipHelper.CreateToolTip(s, false, SemanticContext.GetHovered());
					this.SetTipPlacementBottom();
				}
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void ShowContextMenu(object sender, MouseButtonEventArgs e) {
				await SyncHelper.SwitchToMainThreadAsync(default);
				if (ContextMenu != null) {
					goto SHOW_MENU;
				}
				var ctx = SemanticContext.GetHovered();
				if (ctx == null) {
					return;
				}
				await ctx.UpdateAsync(default);
				await SyncHelper.SwitchToMainThreadAsync(default);
				var s = _Symbol.GetUnderlyingSymbol();
				if (s != null) {
					var m = new CSharpSymbolContextMenu(s, s.GetSyntaxNode(), ctx);
					m.AddAnalysisCommands();
					if (m.HasItems) {
						m.Items.Add(new Separator());
					}
					m.AddSymbolNodeCommands();
					m.AddTitleItem(s.GetOriginalName());
					m.PlacementTarget = this;
					m.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
					m.Closed += ReleaseQuickInfo;
					m.CommandExecuted += DismissQuickInfo;
					ContextMenu = m;
				}
			SHOW_MENU:
				HoldQuickInfo(e);
				ContextMenu.IsOpen = true;
				e.Handled = true;
			}

			void HoldQuickInfo(MouseButtonEventArgs e) {
				QuickInfo.QuickInfoOverride.HoldQuickInfo(this, true);
				Highlight(this, e);
			}

			void ReleaseQuickInfo(object sender, RoutedEventArgs e) {
				QuickInfo.QuickInfoOverride.HoldQuickInfo(this, false);
				ClearValue(BackgroundProperty);
			}

			void DismissQuickInfo(object sender, RoutedEventArgs e) {
				QuickInfo.QuickInfoOverride.HoldQuickInfo(this, false);
				QuickInfo.QuickInfoOverride.DismissQuickInfo(this);
			}

			void Highlight(object sender, MouseEventArgs e) {
				Background = (_Symbol.HasSource() ? SystemColors.HighlightBrush : SystemColors.GrayTextBrush).Alpha(DimmedOpacity);
			}

			void Leave(object sender, MouseEventArgs e) {
				ClearValue(BackgroundProperty);
			}

			void SymbolLink_Unloaded(object sender, RoutedEventArgs e) {
				MouseEnter -= InitInteraction;
				MouseLeftButtonDown -= ShowContextMenu;
				MouseRightButtonDown -= ShowContextMenu;
				MouseEnter -= Highlight;
				MouseLeave -= Leave;
				Unloaded -= SymbolLink_Unloaded;
				if (ContextMenu is CSharpSymbolContextMenu m) {
					m.Closed -= ReleaseQuickInfo;
					m.CommandExecuted -= DismissQuickInfo;
					m.Dispose();
					ContextMenu = null;
				}
				_Symbol = null;
			}
		}

		sealed class SymbolLink : InteractiveRun
		{
			readonly ISymbol _Symbol;

			public SymbolLink(ISymbol symbol, string alias) {
				Text = alias ?? symbol.GetOriginalName();
				_Symbol = symbol;
			}

			protected override WpfBrush HighlightBrush => _Symbol.HasSource() ? SystemColors.HighlightBrush : SystemColors.GrayTextBrush;

			protected override void OnInitInteraction() {
				MouseLeftButtonDown += GoToSymbol;
				MouseRightButtonDown += ShowContextMenu;
			}

			protected override void OnUnload() {
				if (ContextMenu is CSharpSymbolContextMenu m) {
					m.Closed -= ReleaseQuickInfo;
					m.CommandExecuted -= DismissQuickInfo;
					m.Dispose();
					ContextMenu = null;
				}
			}

			protected override object CreateToolTip() {
				return _Symbol != null
					? ToolTipHelper.CreateToolTip(_Symbol, false, SemanticContext.GetHovered())
					: base.CreateToolTip();
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void ShowContextMenu(object sender, MouseButtonEventArgs e) {
				await SyncHelper.SwitchToMainThreadAsync(default);
				if (ContextMenu != null) {
					HoldQuickInfo();
					ContextMenu.IsOpen = true;
					return;
				}
				var ctx = SemanticContext.GetHovered();
				if (ctx != null) {
					await ctx.UpdateAsync(default);
					await SyncHelper.SwitchToMainThreadAsync(default);
					var s = _Symbol;
					if (s != null) {
						var m = new CSharpSymbolContextMenu(s, s.GetSyntaxNode(), ctx);
						m.AddAnalysisCommands();
						if (m.HasItems) {
							m.Items.Add(new Separator());
						}
						m.AddSymbolNodeCommands();
						m.AddTitleItem(s.GetOriginalName());
						HoldQuickInfo();
						m.Closed += ReleaseQuickInfo;
						m.CommandExecuted += DismissQuickInfo;
						m.SetProperty(TextBlock.FontFamilyProperty, ThemeCache.ToolTipFont)
							.SetProperty(TextBlock.FontSizeProperty, ThemeCache.ToolTipFontSize);
						ContextMenu = m;
						m.IsOpen = true;
					}
					e.Handled = true;
				}
			}

			void HoldQuickInfo() {
				QuickInfo.QuickInfoOverride.HoldQuickInfo(this, true);
				DoHighlight();
			}

			void ReleaseQuickInfo(object sender, RoutedEventArgs e) {
				QuickInfo.QuickInfoOverride.HoldQuickInfo(this, false);
				ClearValue(BackgroundProperty);
			}

			void DismissQuickInfo(object sender, RoutedEventArgs e) {
				QuickInfo.QuickInfoOverride.HoldQuickInfo(this, false);
				QuickInfo.QuickInfoOverride.DismissQuickInfo(this);
			}

			void GoToSymbol(object sender, RoutedEventArgs e) {
				if (_Symbol.Kind == SymbolKind.Namespace) {
					FindMembersForNamespace(_Symbol);
				}
				else {
					try {
						_Symbol.GoToDefinition();
					}
					catch (Exception ex) {
						MessageWindow.Error(ex, null, null, this);
					}
				}
				QuickInfo.QuickInfoOverride.DismissQuickInfo(this);
				e.Handled = true;

				async void FindMembersForNamespace(ISymbol symbol) {
					await SemanticContext.GetHovered().FindMembersAsync(symbol);
				}
			}
		}

		class SpanLink : InteractiveRun
		{
			SnapshotSpan _Span;

			public SpanLink(SnapshotSpan span, string text) {
				Text = text ?? span.GetText();
				_Span = span;
			}

			protected override object CreateToolTip() {
				string t = CreateTipText();
				return t != Text
					? new ThemedToolTip(Text, t)
					: base.CreateToolTip();
			}

			protected string CreateTipText() {
				var truncated = _Span.Length > 512;
				var t = (truncated ? new SnapshotSpan(_Span.Snapshot, _Span.Start, 512) : _Span).GetText();
				if (t.IndexOf('\n') != -1) {
					var ws = _Span.Snapshot.GetLineFromPosition(_Span.Start).GetLinePrecedingWhitespace();
					var wsLength = ws.Length;
					if (wsLength != 0) {
						t = UnindentText(t, ws, wsLength);
					}
				}
				if (truncated) {
					t += Properties.Resources.T_ExpressionTooLong;
				}
				return t;
			}

			static string UnindentText(string t, string leadingWhitespace, int wsLength) {
				using (var sbr = ReusableStringBuilder.AcquireDefault(t.Length)) {
					var sb = sbr.Resource;
					var sr = new System.IO.StringReader(t);
					string l = sr.ReadLine();
					sb.Append(l);
					while ((l = sr.ReadLine()) != null) {
						sb.AppendLine();
						if (l.StartsWith(leadingWhitespace, StringComparison.Ordinal)) {
							sb.Append(l, wsLength, l.Length - wsLength);
						}
						else {
							sb.Append(l);
						}
					}
					return sb.ToString();
				}
			}

			protected override void OnInitInteraction() {
				MouseLeftButtonDown += GoToSnapshotPoint;
			}

			protected override void OnUnload() {
				_Span = default;
			}

			void GoToSnapshotPoint(object sender, MouseButtonEventArgs e) {
				var view = TextEditorHelper.GetMouseOverDocumentView();
				if (view != null) {
					view.SelectSpan(_Span);
					view.VisualElement.Focus();
				}
			}
		}

		sealed class SyntaxNodeLink : SpanLink
		{
			SyntaxNodeOrToken _NodeOrToken;

			public SyntaxNodeLink(SyntaxNodeOrToken syntax, ITextSnapshot textSnapshot, string text) : base(syntax.Span.CreateSnapshotSpan(textSnapshot), text) {
				_NodeOrToken = syntax;
			}

			protected override object CreateToolTip() {
				var t = CreateTipText();
				var tip = new ThemedToolTip(Text + " = " + _NodeOrToken.RawKind.ToText(), "Span.Length: " + _NodeOrToken.Span.Length);
				tip.AddTextBlock().Text = t;
				return tip;
			}

			protected override void OnUnload() {
				_NodeOrToken = default;
				base.OnUnload();
			}
		}
	}
}
