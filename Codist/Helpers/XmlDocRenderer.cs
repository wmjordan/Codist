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
		static readonly Regex _FixTextOnlyDoc = new Regex(@"([^\n])[ \t\r\n]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		readonly Compilation _Compilation;
		readonly SymbolFormatter _SymbolFormatter;

		public XmlDocRenderer(Compilation compilation, SymbolFormatter symbolFormatter) {
			_Compilation = compilation;
			_SymbolFormatter = symbolFormatter;
		}

		internal void RenderXmlDocSymbol(string symbol, InlineCollection inlines, SymbolKind symbolKind) {
			switch (symbolKind) {
				case SymbolKind.Parameter: inlines.Add(symbol.Render(false, _SymbolFormatter.Parameter == null, _SymbolFormatter.Parameter)); return;
				case SymbolKind.TypeParameter: inlines.Add(symbol.Render(_SymbolFormatter.TypeParameter == null, false, _SymbolFormatter.TypeParameter)); return;
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
			foreach (var item in content.Nodes()) {
				switch (item.NodeType) {
					case XmlNodeType.Element:
						var e = item as XElement;
						switch (e.Name.LocalName) {
							case "para":
							case "listheader":
							case "item":
							case "code":
								if (e.PreviousNode != null && (e.PreviousNode as XElement)?.Name != "para") {
									text.Add(new LineBreak());
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
								StyleInner(e, text, new Bold() { Background = Brushes.LightGray });
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
							if (parentName != XmlDocNodeName) {
								var previous = (item.PreviousNode as XElement)?.Name?.LocalName;
								if (previous == null || previous != "see" && previous != "paramref" && previous != "typeparamref" && previous != "c" && previous != "b" && previous != "i" && previous != "u") {
									t = item.NextNode == null ? t.Trim() : t.TrimStart();
								}
								else if (item.NextNode == null) {
									t = t.TrimEnd();
								}
								t = _FixWhitespaces.Replace(t.Replace('\n', ' '), " ");
							}
							else {
								// fix whitespace for text only XML doc
								t = _FixTextOnlyDoc.Replace(t, "$1");
							}
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
	}
}
