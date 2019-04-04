using System;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Codist
{
	sealed class XmlDocRenderer
	{
		internal const string XmlDocNodeName = "member";
		const int LIST_UNDEFINED = -1, LIST_BULLET = -2, LIST_NOT_NUMERIC = -3;
		const int BLOCK_PARA = 0, BLOCK_ITEM = 1, BLOCK_OTHER = 2;

		static readonly Regex _FixWhitespaces = new Regex(@" {2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		readonly Compilation _Compilation;
		readonly SymbolFormatter _SymbolFormatter;
		readonly ISymbol _Symbol;

		public XmlDocRenderer(Compilation compilation, SymbolFormatter symbolFormatter, ISymbol symbol) {
			_Compilation = compilation;
			_SymbolFormatter = symbolFormatter;
			_Symbol = symbol;
		}

		public void Render(XElement content, TextBlock text) {
			if (content == null || content.HasElements == false && content.IsEmpty) {
				return;
			}
			Render(content, text.Inlines);
		}
		public void Render(XElement content, Controls.ThemedTipDocument doc) {
			if (content == null || content.HasElements == false && content.IsEmpty) {
				return;
			}
			var paragraph = new Controls.ThemedTipParagraph(Microsoft.VisualStudio.Imaging.KnownImageIds.Comment);
			doc.Append(paragraph);
			Render(content, paragraph.Content.Inlines);
			if (paragraph.Content.Inlines.FirstInline == null) {
				doc.Children.Remove(paragraph);
			}
		}
		public void Render(XContainer content, InlineCollection inlines) {
			InternalRender(content, inlines, null);
		}

		void InternalRender(XContainer content, InlineCollection inlines, ListContext list) {
			foreach (var item in content.Nodes()) {
				switch (item.NodeType) {
					case XmlNodeType.Element:
						var e = item as XElement;
						switch (e.Name.LocalName) {
							case "list":
								list = list == null
									? new ListContext(e.Attribute("type")?.Value)
									: new ListContext(e.Attribute("type")?.Value, list);
								InternalRender(e, inlines, list);
								list = list.Parent;
								break;
							case "para":
								var isOnlyChildOfItem = list != null && e.Parent.Name == "item" && e.Parent.FirstNode == e.Parent.LastNode && e.Parent.FirstNode == item;
								if (inlines.FirstInline != null && isOnlyChildOfItem == false) {
									inlines.AppendLineWithMargin();
								}
								InternalRender(e, inlines, list);
								if (inlines.FirstInline == null && isOnlyChildOfItem == false) {
									inlines.Add(new LineBreak());
								}
								break;
							case "listheader":
							case "code":
								RenderBlockContent(inlines, list, e, BLOCK_OTHER);
								break;
							case "item":
								RenderBlockContent(inlines, list, e, BLOCK_ITEM);
								break;
							case "see":
								var see = e.Attribute("cref");
								if (see != null) {
									RenderXmlDocSymbol(see.Value, inlines, SymbolKind.Alias);
								}
								else if ((see = e.Attribute("langword")) != null) {
									RenderXmlDocSymbol(see.Value, inlines, SymbolKind.DynamicType);
								}
								break;
							case "paramref":
								var paramName = e.Attribute("name");
								if (paramName != null) {
									RenderXmlDocSymbol(paramName.Value, inlines, SymbolKind.Parameter);
								}
								break;
							case "typeparamref":
								var typeParamName = e.Attribute("name");
								if (typeParamName != null) {
									RenderXmlDocSymbol(typeParamName.Value, inlines, SymbolKind.TypeParameter);
								}
								break;
							case "c":
								StyleInner(e, inlines, new Bold() { Background = ThemeHelper.ToolWindowBackgroundBrush, Foreground = ThemeHelper.ToolWindowTextBrush });
								break;
							case "b":
								StyleInner(e, inlines, new Bold());
								break;
							case "i":
								StyleInner(e, inlines, new Italic());
								break;
							case "u":
								StyleInner(e, inlines, new Underline());
								break;
							//case "list":
							//case "description":
							default:
								InternalRender(e, inlines, list);
								break;
						}
						break;
					case XmlNodeType.Text:
						string t = (item as XText).Value;
						var parentName = item.Parent.Name.LocalName;
						if (parentName != "code") {
							var previous = (item.PreviousNode as XElement)?.Name?.LocalName;
							if (previous == null || IsInlineElementName(previous) == false) {
								t = item.NextNode == null ? t.Trim() : t.TrimStart();
							}
							else if (item.NextNode == null) {
								t = t.TrimEnd();
							}
							t = _FixWhitespaces.Replace(t.Replace('\n', ' '), " ");
						}
						if (t.Length > 0) {
							inlines.Add(new Run(t));
						}
						break;
					case XmlNodeType.CDATA:
						inlines.Add(new Run((item as XText).Value));
						break;
					case XmlNodeType.EntityReference:
					case XmlNodeType.Entity:
						inlines.Add(new Run(item.ToString()));
						break;
				}
			}
		}

		void RenderBlockContent(InlineCollection inlines, ListContext list, XElement e, int blockType) {
			if (inlines.FirstInline != null) {
				inlines.AppendLineWithMargin();
			}
			if (blockType == BLOCK_ITEM) {
				PopulateListNumber(inlines, list);
			}
			InternalRender(e, inlines, list);
			if (inlines.FirstInline == null) {
				inlines.Add(new LineBreak());
			}
		}

		static void PopulateListNumber(InlineCollection text, ListContext list) {
			string indent = list.Indent > 0 ? new string(' ', list.Indent) : String.Empty;
			if (list.ListType > 0) {
				text.Add(new Run(indent + ((int)list.ListType++).ToString() + ". ") { Foreground = ThemeHelper.SystemGrayTextBrush, FontWeight = System.Windows.FontWeights.Bold });
			}
			else if (list.ListType == ListType.Bullet) {
				text.Add(new Run(list.Indent > 0 ? indent + " \u00B7 " : " \u00B7 ") { Foreground = ThemeHelper.SystemGrayTextBrush, FontWeight = System.Windows.FontWeights.Bold });
			}
		}

		internal void RenderXmlDocSymbol(string symbol, InlineCollection inlines, SymbolKind symbolKind) {
			switch (symbolKind) {
				case SymbolKind.Parameter:
					inlines.Add(symbol.Render(false, _SymbolFormatter.Parameter == null, _SymbolFormatter.Parameter));
					return;
				case SymbolKind.TypeParameter:
					inlines.Add(symbol.Render(_SymbolFormatter.TypeParameter == null, false, _SymbolFormatter.TypeParameter));
					return;
				case SymbolKind.DynamicType:
					// highlight keywords
					inlines.Add(symbol.Render(_SymbolFormatter.Keyword));
					return;
			}
			var s = DocumentationCommentId.GetFirstSymbolForDeclarationId(symbol, _Compilation);
			if (s != null) {
				_SymbolFormatter.ToUIText(inlines, s, null);
				return;
			}
			if (symbol.Length > 2 && symbol[1] == ':') {
				switch (symbol[0]) {
					case 'T':
						inlines.Add(symbol.Substring(2).Render(false, true, _SymbolFormatter.Class));
						return;
					case 'M':
						inlines.Add(symbol.Substring(2).Render(false, true, _SymbolFormatter.Method));
						return;
					case 'P':
						inlines.Add(symbol.Substring(2).Render(false, true, _SymbolFormatter.Property));
						return;
					case 'F':
						inlines.Add(symbol.Substring(2).Render(false, true, _SymbolFormatter.Field));
						return;
					case 'E':
						inlines.Add(symbol.Substring(2).Render(false, true, _SymbolFormatter.Delegate));
						return;
					case '!':
						inlines.Add(symbol.Substring(2).Render(true, true, null));
						return;
				}
			}
			inlines.Add(symbol);
		}

		void StyleInner(XElement element, InlineCollection text, Span span) {
			text.Add(span);
			Render(element, span.Inlines);
		}

		static bool IsInlineElementName(string name) {
			switch (name) {
				case "see":
				case "paramref":
				case "typeparamref":
				case "b":
				case "i":
				case "u":
				case "c": return true;
			}
			return false;
		}

		sealed class ListContext
		{
			public readonly int Indent;
			public readonly ListContext Parent;
			public ListType ListType;

			public ListContext(string type) {
				switch (type) {
					case "number": ListType = ListType.Number; break;
					case "bullet": ListType = ListType.Bullet; break;
					default: ListType = ListType.Table; break;
				}
			}
			public ListContext(string type, ListContext parent) : this(type) {
				Indent = parent.Indent + 1;
				Parent = parent;
			}
		}
		enum ListType
		{
			Number = 1,
			Undefined = LIST_UNDEFINED,
			Bullet = LIST_BULLET,
			Table = LIST_NOT_NUMERIC
		}
	}
}
