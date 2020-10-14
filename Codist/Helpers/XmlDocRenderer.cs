using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Imaging;
using R = Codist.Properties.Resources;

namespace Codist
{
	sealed class XmlDocRenderer
	{
		internal const string XmlDocNodeName = "member";
		const int LIST_UNDEFINED = -1, LIST_BULLET = -2, LIST_NOT_NUMERIC = -3;
		const int BLOCK_PARA = 0, BLOCK_ITEM = 1, BLOCK_OTHER = 2, BLOCK_TITLE = 3;

		static readonly Regex _FixWhitespaces = new Regex(" {2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		readonly Compilation _Compilation;
		readonly SymbolFormatter _SymbolFormatter;

		int _isCode;
		FontFamily _codeFont;

		public XmlDocRenderer(Compilation compilation, SymbolFormatter symbolFormatter) {
			_Compilation = compilation;
			_SymbolFormatter = symbolFormatter;
		}

		/// <summary>
		/// Use it to remove paragraphs rendered by VS builtin implementation
		/// </summary>
		public int ParagraphCount { get; set; }

		public ThemedTipDocument RenderXmlDoc(ISymbol symbol, XmlDoc doc) {
			var tip = new ThemedTipDocument();
			var summary = doc.GetDescription(symbol);
			XmlDoc inheritDoc = null;
			bool showSummaryIcon = true;
			#region Summary
			if (summary == null
					&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DocumentationFromBaseType)) {
				summary = doc.GetInheritedDescription(symbol, out inheritDoc);
				if (inheritDoc != null && summary != null) {
					tip.Append(new ThemedTipParagraph(IconIds.ReferencedXmlDoc, new ThemedTipText()
							.Append(R.T_DocumentationFrom)
							.AddSymbol(inheritDoc.Symbol.ContainingSymbol, false, _SymbolFormatter)
							.Append(".")
							.AddSymbol(inheritDoc.Symbol, true, _SymbolFormatter)
							.Append(":"))
					);
					showSummaryIcon = false;
				}
			}
			if (summary != null
				&& (summary.Name.LocalName != XmlDocNodeName || Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.TextOnlyDoc))) {
				ParagraphCount = 0;
				Render(summary, tip, showSummaryIcon);
				if (inheritDoc == null) {
					tip.Tag = ParagraphCount;
				}
			}
			#endregion
			#region Type parameter
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.TypeParameters)
				&& (symbol.Kind == SymbolKind.Method || symbol.Kind == SymbolKind.NamedType)) {
				var typeParams = symbol.GetTypeParameters();
				if (typeParams.IsDefaultOrEmpty == false) {
					var para = new ThemedTipParagraph(IconIds.TypeParameters);
					foreach (var param in typeParams) {
						var p = doc.GetTypeParameter(param.Name);
						if (p == null) {
							continue;
						}
						if (para.Content.Inlines.FirstInline != null) {
							para.Content.AppendLine();
						}
						para.Content
							.Append(param.Name, _SymbolFormatter.TypeParameter)
							.Append(": ")
							.AddXmlDoc(p, this);
					}
					if (para.Content.Inlines.FirstInline != null) {
						tip.Append(para);
					}
				}
			}
			#endregion
			#region Returns
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ReturnsDoc)
					&& (symbol.Kind == SymbolKind.Method
					|| symbol.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)symbol).TypeKind == TypeKind.Delegate)) {
				var returns = doc.Returns ?? doc.ExplicitInheritDoc?.Returns ?? doc.InheritedXmlDocs.FirstOrDefault(i => i.Returns != null)?.Returns;
				if (returns != null && returns.FirstNode != null) {
					tip.Append(new ThemedTipParagraph(IconIds.Return, new ThemedTipText()
						.Append(R.T_Returns, true)
						.Append(returns == doc.Returns ? ": " : (R.T_Inherited + ": "))
						.AddXmlDoc(returns, this))
						);
				}
			}
			#endregion
			#region Remarks
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.RemarksDoc)
					&& symbol.Kind != SymbolKind.Parameter
					&& symbol.Kind != SymbolKind.TypeParameter) {
				var remarks = doc.Remarks ?? doc.ExplicitInheritDoc?.Remarks ?? doc.InheritedXmlDocs.FirstOrDefault(i => i.Remarks != null)?.Remarks;
				if (remarks != null && remarks.FirstNode != null) {
					tip.Append(new ThemedTipParagraph(IconIds.RemarksXmlDoc, new ThemedTipText()
						.Append(R.T_Remarks, true)
						.Append(remarks == doc.Remarks ? ": " : (R.T_Inherited + ": "))
						))
						.Append(new ThemedTipParagraph(new ThemedTipText().AddXmlDoc(remarks, this)));
				}
			}
			#endregion
			#region SeeAlso
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SeeAlsoDoc)) {
				var seeAlsos = doc.SeeAlsos ?? doc.ExplicitInheritDoc?.SeeAlsos ?? doc.InheritedXmlDocs.FirstOrDefault(i => i.SeeAlsos != null)?.SeeAlsos;
				ThemedTipText seeAlso = null;
				bool hasItem = false;
				if (seeAlsos != null) {
					seeAlso = new ThemedTipText()
						.Append(R.T_SeeAlso, true)
						.Append(seeAlsos == doc.SeeAlsos ? ": " : (R.T_Inherited + ": "));
					foreach (var item in seeAlsos) {
						if (hasItem) {
							seeAlso.Append(", ");
						}
						RenderSee(seeAlso.Inlines, item);
						hasItem = true;
					}
				}
				var sees = doc.Sees ?? doc.ExplicitInheritDoc?.Sees ?? doc.InheritedXmlDocs.FirstOrDefault(i => i.Sees != null)?.Sees;
				if (sees != null) {
					if (seeAlso == null) {
						seeAlso = new ThemedTipText()
						   .Append(R.T_SeeAlso, true)
						   .Append(sees == doc.Sees ? ": " : (R.T_Inherited + ": "));
					}
					foreach (var item in sees) {
						if (hasItem) {
							seeAlso.Append(", ");
						}
						RenderSee(seeAlso.Inlines, item);
						hasItem = true;
					}
				}
				if (seeAlso != null) {
					tip.Append(new ThemedTipParagraph(IconIds.SeeAlsoXmlDoc, seeAlso));
				}
			}
			#endregion
			#region Example
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ExampleDoc)) {
				var examples = doc.Examples ?? doc.ExplicitInheritDoc?.Examples ?? doc.InheritedXmlDocs.FirstOrDefault(i => i.Examples != null)?.Examples;
				if (examples != null) {
					tip.Append(new ThemedTipParagraph(IconIds.ExampleXmlDoc, new ThemedTipText()
						.Append(R.T_Example, true)
						.Append(examples == doc.Examples ? ": " : (R.T_Inherited + ": "))));
					foreach (var item in examples) {
						tip.Append(new ThemedTipParagraph(new ThemedTipText().AddXmlDoc(item, this)));
					}
				}
			}
			#endregion
			return tip;
		}

		public void Render(XElement content, TextBlock text) {
			if (content == null || content.HasElements == false && content.IsEmpty) {
				return;
			}
			Render(content, text.Inlines);
		}
		public void Render(XElement content, ThemedTipDocument doc, bool showSummaryIcon) {
			if (content == null || content.HasElements == false && content.IsEmpty) {
				return;
			}
			var paragraph = new ThemedTipParagraph(showSummaryIcon ? IconIds.XmlDocComment : 0);
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
								if (isOnlyChildOfItem) {
									ParagraphCount++;
								}
								else if (inlines.LastInline != null && inlines.LastInline is LineBreak == false) {
									inlines.AppendLineWithMargin();
									ParagraphCount++;
								}
								InternalRender(e, inlines, list);
								if (inlines.FirstInline == null && isOnlyChildOfItem == false) {
									inlines.Add(new LineBreak());
								}
								break;
							case "listheader":
								RenderBlockContent(inlines, list, e, BLOCK_OTHER);
								break;
							case "h1":
								RenderBlockContent(inlines, list, e, BLOCK_TITLE).FontSize = ThemeHelper.ToolTipFontSize + 5;
								break;
							case "h2":
								RenderBlockContent(inlines, list, e, BLOCK_TITLE).FontSize = ThemeHelper.ToolTipFontSize + 3;
								break;
							case "h3":
								RenderBlockContent(inlines, list, e, BLOCK_TITLE).FontSize = ThemeHelper.ToolTipFontSize + 2;
								break;
							case "h4":
								RenderBlockContent(inlines, list, e, BLOCK_TITLE).FontSize = ThemeHelper.ToolTipFontSize + 1;
								break;
							case "h5":
								RenderBlockContent(inlines, list, e, BLOCK_TITLE).FontSize = ThemeHelper.ToolTipFontSize + 0.5;
								break;
							case "h6":
								RenderBlockContent(inlines, list, e, BLOCK_TITLE);
								break;
							case "code":
								++_isCode;
								var span = RenderBlockContent(inlines, list, e, BLOCK_OTHER);
								span.FontFamily = GetCodeFont();
								span.Background = ThemeHelper.ToolWindowBackgroundBrush;
								span.Foreground = ThemeHelper.ToolWindowTextBrush;
								--_isCode;
								break;
							case "item":
								RenderBlockContent(inlines, list, e, BLOCK_ITEM);
								break;
							case "see":
							case "seealso":
								RenderSee(inlines, e);
								break;
							case "paramref":
								RenderParamRef(inlines, e);
								break;
							case "typeparamref":
								RenderTypeParamRef(inlines, e);
								break;
							case "c":
							case "tt":
								++_isCode;
								StyleInner(e, inlines, new Span() { FontFamily = GetCodeFont(), Background = ThemeHelper.ToolWindowBackgroundBrush, Foreground = ThemeHelper.ToolWindowTextBrush });
								--_isCode;
								break;
							case "b":
							case "strong":
							case "term":
								StyleInner(e, inlines, new Bold());
								break;
							case "i":
							case "em":
								StyleInner(e, inlines, new Italic());
								break;
							case "u":
								StyleInner(e, inlines, new Underline());
								break;
							case "a":
								var a = e.Attribute("href");
								if (a != null && IsUrl(a)) {
									CreateLink(inlines, e, a);
								}
								else {
									goto case "u";
								}
								break;
							case "br":
								inlines.Add(new LineBreak());
								break;
							//case "list":
							//case "description":
							default:
								InternalRender(e, inlines, list);
								break;
						}
						break;
					case XmlNodeType.Text:
						string t = ((XText)item).Value;
						if (_isCode == 0) {
							var previous = (item.PreviousNode as XElement)?.Name?.LocalName;
							if (previous == null || IsInlineElementName(previous) == false) {
								t = item.NextNode == null ? t.Trim() : t.TrimStart();
							}
							else if (item.NextNode == null) {
								t = t.TrimEnd();
							}
							if (t.Length == 0) {
								break;
							}
							t = _FixWhitespaces.Replace(t.Replace('\n', ' '), " ");
						}
						else {
							t = t.Replace("\n    ", "\n");
						}
						if (t.Length > 0) {
							inlines.Add(new Run(t));
						}
						break;
					case XmlNodeType.CDATA:
						inlines.Add(_isCode == 0 ? new Run(((XText)item).Value) : new Run(((XText)item).Value.Replace("\n    ", "\n").TrimEnd()));
						break;
					case XmlNodeType.EntityReference:
					case XmlNodeType.Entity:
						inlines.Add(new Run(item.ToString()));
						break;
				}
			}
			var lastNode = content.LastNode;
			if (lastNode != null
				&& (lastNode.NodeType != XmlNodeType.Element || ((XElement)lastNode).Name != "para")
				&& IsInlineElementName((lastNode.PreviousNode as XElement)?.Name.LocalName) == false) {
				ParagraphCount++;
			}
		}

		static bool IsUrl(XAttribute a) {
			return a.Value.StartsWith("http://", StringComparison.Ordinal) || a.Value.StartsWith("https://", StringComparison.Ordinal);
		}

		void CreateLink(InlineCollection inlines, XElement e, XAttribute a) {
			var link = new Hyperlink {
				NavigateUri = new Uri(a.Value),
				ToolTip = String.Join(Environment.NewLine, e.Attribute("title"), a.Value)
			}.ClickToNavigate();
			if (e.IsEmpty) {
				link.Inlines.Add(a.Value);
				inlines.Add(link);
			}
			else {
				StyleInner(e, inlines, link);
			}
		}

		XAttribute RenderSee(InlineCollection inlines, XElement e) {
			var see = e.Attribute("cref");
			if (see != null) {
				if (IsUrl(see)) {
					CreateLink(inlines, e, see);
				}
				else {
					RenderXmlDocSymbol(see.Value, inlines, SymbolKind.Alias);
				}
			}
			else if ((see = e.Attribute("langword")) != null) {
				RenderXmlDocSymbol(see.Value, inlines, SymbolKind.DynamicType);
			}

			return see;
		}

		XAttribute RenderParamRef(InlineCollection inlines, XElement e) {
			var r = e.Attribute("name");
			if (r != null) {
				RenderXmlDocSymbol(r.Value, inlines, SymbolKind.Parameter);
			}

			return r;
		}

		XAttribute RenderTypeParamRef(InlineCollection inlines, XElement e) {
			var r = e.Attribute("name");
			if (r != null) {
				RenderXmlDocSymbol(r.Value, inlines, SymbolKind.TypeParameter);
			}

			return r;
		}

		Span RenderBlockContent(InlineCollection inlines, ListContext list, XElement e, int blockType) {
			if (inlines.LastInline != null && inlines.LastInline is LineBreak == false) {
				inlines.AppendLineWithMargin();
			}
			if (blockType == BLOCK_ITEM) {
				PopulateListNumber(inlines, list);
			}
			else {
				ParagraphCount++;
			}
			var span = new Span();
			if (_isCode > 0) {
				span.FontFamily = GetCodeFont();
			}
			if (blockType == BLOCK_TITLE) {
				span.FontWeight = System.Windows.FontWeights.Bold;
			}
			inlines.Add(span);
			InternalRender(e, span.Inlines, list);
			if (blockType != BLOCK_ITEM && e.NextNode != null
				&& IsInlineElementName((e.NextNode as XElement)?.Name.LocalName) == false) {
				inlines.Add(new LineBreak());
			}
			return span;
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
				_SymbolFormatter.Format(inlines, s, null, false);
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
						inlines.Add(symbol.Substring(2).Render(false, true, _SymbolFormatter.Event));
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

		FontFamily GetCodeFont() {
			if (_codeFont != null) {
				return _codeFont;
			}
			ThemeHelper.GetFontSettings(Microsoft.VisualStudio.Shell.Interop.FontsAndColorsCategory.TextEditor, out var fontName, out var fontSize);
			return _codeFont = new FontFamily(fontName);
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

		static XNode GetFirstContent(XElement element) {
			var node = element.FirstNode;
			if (node == null) {
				return null;
			}
			do {
				switch (node.NodeType) {
					case XmlNodeType.Element: return (XElement)node;
					case XmlNodeType.Whitespace: continue;
					case XmlNodeType.SignificantWhitespace: continue;
					case XmlNodeType.Text:
						if (((XText)node).Value.Trim().Length == 0) {
							continue;
						}
						return node;
				}
				return node;
			} while ((node = node.NextNode) != null);
			return null;
		}

		static XElement GetPrevElement(XElement element) {
			XNode node = element;
			while ((node = node.PreviousNode) != null) {
				switch (node.NodeType) {
					case XmlNodeType.Element: return (XElement)node;
					case XmlNodeType.Whitespace: continue;
					case XmlNodeType.SignificantWhitespace: continue;
					case XmlNodeType.Text:
						if (((XText)node).Value.Trim().Length == 0) {
							continue;
						}
						return null;
				}
				return null;
			}
			return null;
		}

		static XElement GetNextElement(XElement element) {
			XNode node = element;
			while ((node = node.NextNode) != null) {
				switch (node.NodeType) {
					case XmlNodeType.Element: return (XElement)node;
					case XmlNodeType.Whitespace: continue;
					case XmlNodeType.SignificantWhitespace: continue;
					case XmlNodeType.Text:
						if (((XText)node).Value.Trim().Length == 0) {
							continue;
						}
						return null;
				}
				return null;
			}
			return null;
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
