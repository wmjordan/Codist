using System;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Codist
{
	sealed class XmlDocRenderer
	{
		internal const string XmlDocNodeName = "member";

		static readonly Regex _FixWhitespaces = new Regex(@" {2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		readonly Compilation _Compilation;
		readonly SymbolFormatter _SymbolFormatter;
		readonly ISymbol _Symbol;

		public XmlDocRenderer(Compilation compilation, SymbolFormatter symbolFormatter, ISymbol symbol) {
			_Compilation = compilation;
			_SymbolFormatter = symbolFormatter;
			_Symbol = symbol;
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

		public void Render(XElement content, TextBlock text) {
			if (content == null || content.HasElements == false && content.IsEmpty) {
				return;
			}
			Render(content, text.Inlines);
		}

		public void Render(XContainer content, InlineCollection text) {
			const int LIST_UNDEFINED = -1, LIST_BULLET = -2, LIST_NOT_NUMERIC = -3;
			var listNum = LIST_UNDEFINED;
			foreach (var item in content.Nodes()) {
				switch (item.NodeType) {
					case XmlNodeType.Element:
						var e = item as XElement;
						var name = e.Name.LocalName;
						switch (name) {
							case "para":
							case "listheader":
							case "item":
							case "code":
								if (e.PreviousNode != null && IsBlockElementName((e.PreviousNode as XElement)?.Name.ToString()) == false) {
									text.Add(new LineBreak());
								}
								if (name == "item") {
									if (listNum == LIST_UNDEFINED) {
										switch ((e.Parent as XElement).Attribute("type")?.Value) {
											case "number": listNum = 0; break;
											case "bullet": listNum = LIST_BULLET; break;
											default: listNum = LIST_NOT_NUMERIC; break;
										}
									}
									if (listNum >= 0) {
										text.Add(new Run((++listNum).ToString() + ". ") { Foreground = ThemeHelper.SystemGrayTextBrush, FontWeight = System.Windows.FontWeights.Bold });
									}
									else if (listNum == LIST_BULLET) {
										text.Add(new Run(" \u00B7 ") { Foreground = ThemeHelper.SystemGrayTextBrush, FontWeight = System.Windows.FontWeights.Bold });
									}
								}
								Render(e, text);
								if (e.NextNode != null) {
									text.Add(new LineBreak());
								}
								break;
							case "see":
								var see = e.Attribute("cref");
								if (see != null) {
									RenderXmlDocSymbol(see.Value, text, SymbolKind.Alias);
								}
								else if ((see = e.Attribute("langword")) != null) {
									RenderXmlDocSymbol(see.Value, text, SymbolKind.DynamicType);
								}
								break;
							case "paramref":
								var paramName = e.Attribute("name");
								if (paramName != null) {
									RenderXmlDocSymbol(paramName.Value, text, SymbolKind.Parameter);
								}
								break;
							case "typeparamref":
								var typeParamName = e.Attribute("name");
								if (typeParamName != null) {
									RenderXmlDocSymbol(typeParamName.Value, text, SymbolKind.TypeParameter);
								}
								break;
							case "c":
								StyleInner(e, text, new Bold() { Background = ThemeHelper.ToolWindowBackgroundBrush, Foreground = ThemeHelper.ToolWindowTextBrush });
								break;
							case "b":
								StyleInner(e, text, new Bold());
								break;
							case "i":
								StyleInner(e, text, new Italic());
								break;
							case "u":
								StyleInner(e, text, new Underline());
								break;
							//case "list":
							//case "description":
							default:
								Render(e, text);
								break;
						}
						break;
					case XmlNodeType.Text:
						string t = (item as XText).Value;
						var parentName = item.Parent.Name.LocalName;
						if (parentName != "code") {
							var previous = (item.PreviousNode as XElement)?.Name?.LocalName;
							if (previous == null || previous != "see" && previous != "paramref" && previous != "typeparamref" && previous != "c" && previous != "b" && previous != "i" && previous != "u") {
								t = item.NextNode == null ? t.Trim() : t.TrimStart();
							}
							else if (item.NextNode == null) {
								t = t.TrimEnd();
							}
							t = _FixWhitespaces.Replace(t.Replace('\n', ' '), " ");
						}
						text.Add(new Run(t));
						break;
					case XmlNodeType.CDATA:
						text.Add(new Run((item as XText).Value));
						break;
					case XmlNodeType.EntityReference:
					case XmlNodeType.Entity:
						text.Add(new Run(item.ToString()));
						break;
				}
			}
		}

		void StyleInner(XElement element, InlineCollection text, Span span) {
			text.Add(span);
			Render(element, span.Inlines);
		}

		static bool IsBlockElementName(string name) {
			switch (name) {
				case "para":
				case "listheader":
				case "item":
				case "code": return true;
			}
			return false;
		}
	}
}
