using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using R = Codist.Properties.Resources;

namespace Codist
{
	sealed class XmlDocRenderer
	{
		const int LIST_UNDEFINED = -1, LIST_BULLET = -2, LIST_NOT_NUMERIC = -3;
		const int BLOCK_PARA = 0, BLOCK_ITEM = 1, BLOCK_OTHER = 2, BLOCK_TITLE = 3;

		static readonly Regex __FixWhitespaces = new Regex(" {2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		readonly Compilation _Compilation;
		readonly SymbolFormatter _SymbolFormatter;

		int _IsCode;
		FontFamily _CodeFont;

		public XmlDocRenderer(Compilation compilation, SymbolFormatter symbolFormatter) {
			_Compilation = compilation;
			_SymbolFormatter = symbolFormatter;
		}

		/// <summary>
		/// Use it to remove paragraphs rendered by VS built-in implementation
		/// </summary>
		public int ParagraphCount { get; set; }

		public ThemedTipDocument RenderXmlDoc(ISymbol symbol, XmlDoc doc) {
			var tip = new ThemedTipDocument();
			var summary = doc.GetDescription(symbol);
			XmlDoc inheritDoc = null;
			bool inheritedXmlDoc = false;
			#region Summary
			if (summary == null
					&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DocumentationFromBaseType)) {
				summary = doc.GetInheritedDescription(symbol, out inheritDoc);
				if (inheritDoc != null && summary != null) {
					inheritedXmlDoc = true;
				}
			}
			if (summary != null
				&& (doc.IsTextOnly == false || Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.TextOnlyDoc))) {
				ParagraphCount = 0;
				if (summary != null && IsEmptyElement(summary) == false) {
					ThemedTipParagraph paragraph;
					if (inheritedXmlDoc) {
						paragraph = new ThemedTipParagraph(IconIds.ReferencedXmlDoc, new ThemedTipText()
							.AddSymbol(inheritDoc.Symbol.ContainingSymbol.OriginalDefinition, false, _SymbolFormatter)
							.Append(".")
							.AddSymbol(inheritDoc.Symbol, true, _SymbolFormatter)
							.Append(": "));
					}
					else {
						paragraph = new ThemedTipParagraph(IconIds.XmlDocComment);
					}
					Render(summary, paragraph.Content.Inlines);
					if (paragraph.Content.Inlines.FirstInline != null) {
						tip.Append(paragraph);
					}
				}
				if (inheritDoc == null) {
					tip.Tag = ParagraphCount;
				}
			}
			if (symbol.HasSource() && Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OrdinaryCommentDoc)) {
				switch (symbol.Kind) {
					case SymbolKind.Event:
					case SymbolKind.Field:
					case SymbolKind.Local:
					case SymbolKind.Method:
					case SymbolKind.NamedType:
					case SymbolKind.Property:
						RenderOrdinaryComment(symbol, tip);
						break;
				}
			}
			#endregion
			#region Type parameter
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.TypeParameters)
				&& symbol.Kind.CeqAny(SymbolKind.Method, SymbolKind.NamedType)) {
				var typeParams = symbol.GetTypeParameters();
				if (typeParams.IsDefaultOrEmpty == false) {
					var para = new ThemedTipParagraph(IconIds.TypeParameters);
					foreach (var param in typeParams) {
						var p = doc.GetTypeParameter(param.Name);
						if (p == null || IsEmptyElement(p)) {
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
					&& (symbol.Kind.CeqAny(SymbolKind.Method, SymbolKind.Property)
					|| symbol.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)symbol).TypeKind == TypeKind.Delegate)) {
				var returns = doc.Returns ?? doc.ExplicitInheritDoc?.Returns ?? doc.InheritedXmlDocs.FirstOrDefault(i => i.Returns != null)?.Returns;
				if (returns != null && IsEmptyElement(returns) == false) {
					tip.Append(new ThemedTipParagraph(IconIds.Return, new ThemedTipText()
						.Append(R.T_Returns, true)
						.Append(returns == doc.Returns ? ": " : (R.T_Inherited + ": "))
						.AddXmlDoc(returns, this))
						);
				}
			}
			#endregion
			#region Value
			if (symbol.Kind == SymbolKind.Property
				|| (symbol.Kind == SymbolKind.Method && symbol.ContainingSymbol.Kind == SymbolKind.Property)) {
				var value = doc.Value ?? doc.ExplicitInheritDoc?.Value ?? doc.InheritedXmlDocs.FirstOrDefault(i => i.Value != null)?.Value;
				if (value != null && IsEmptyElement(value) == false) {
					tip.Append(new ThemedTipParagraph(IconIds.Value, new ThemedTipText()
						.Append(R.T_Value, true)
						.Append(value == doc.Value ? ": " : (R.T_Inherited + ": "))
						.AddXmlDoc(value, this))
						);
				}
			}
			#endregion
			#region Remarks
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.RemarksDoc)
					&& symbol.Kind.CeqAny(SymbolKind.Parameter, SymbolKind.TypeParameter) == false) {
				var remarks = doc.Remarks ?? doc.ExplicitInheritDoc?.Remarks ?? doc.InheritedXmlDocs.FirstOrDefault(i => i.Remarks != null)?.Remarks;
				if (remarks != null && IsEmptyElement(remarks) == false) {
					tip.Append(new ThemedTipParagraph(IconIds.RemarksXmlDoc, new ThemedTipText()
						.Append(R.T_Remarks, true)
						.Append(remarks == doc.Remarks ? ": " : (R.T_Inherited + ": "))
						.AddXmlDoc(remarks, this))
						);
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

		static void RenderOrdinaryComment(ISymbol symbol, ThemedTipDocument tip) {
			var refs = symbol.DeclaringSyntaxReferences;
			if (refs.Length == 0) {
				return;
			}
			var node = refs[0].SyntaxTree.GetCompilationUnitRoot().FindNode(refs[0].Span);
			string t = null;
			SeparatedSyntaxList<VariableDeclaratorSyntax> variables;
			int variableIndex;
			SyntaxToken separatorAfterNode;
			do {
			FIND_COMMENT:
				if (node.HasLeadingTrivia && (t = node.GetLeadingTrivia().GetCommentContent(true)) != null
					|| node.HasTrailingTrivia && (t = node.GetTrailingTrivia().GetCommentContent(false)) != null) {
					break;
				}
				// use comment behind variable separator for multi-variable declarations:
				// int x = 1, // comment for x
				//   y = 2; // comment for y
				if (node.IsKind(SyntaxKind.VariableDeclarator)
					&& node.Parent is VariableDeclarationSyntax v) {
					if ((variables = v.Variables).Count > 1
						&& (variableIndex = variables.IndexOf((VariableDeclaratorSyntax)node)) < variables.SeparatorCount
						&& (separatorAfterNode = variables.GetSeparator(variableIndex)).HasTrailingTrivia
						&& (t = separatorAfterNode.TrailingTrivia.GetCommentContent(false)) != null) {
						break;
					}
					node = v;
					goto FIND_COMMENT;
				}
			} while (node.IsKind(SyntaxKind.VariableDeclaration)
				&& (node = node.Parent) != null);
			if (t != null) {
				tip.Append(new ThemedTipParagraph(IconIds.Comment, new ThemedTipText(t)));
			}
		}

		public void Render(XElement content, TextBlock text) {
			if (content == null || IsEmptyElement(content)) {
				return;
			}
			Render(content, text.Inlines);
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
								++_IsCode;
								var span = RenderBlockContent(inlines, list, e, BLOCK_OTHER);
								span.FontFamily = GetCodeFont();
								span.Background = ThemeHelper.ToolWindowBackgroundBrush;
								span.Foreground = ThemeHelper.ToolWindowTextBrush;
								--_IsCode;
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
								++_IsCode;
								StyleInner(e, inlines, new Span() { FontFamily = GetCodeFont(), Background = ThemeHelper.ToolWindowBackgroundBrush, Foreground = ThemeHelper.ToolWindowTextBrush });
								--_IsCode;
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
							case "hr":
								inlines.AddRange(new Inline[] {
									new LineBreak(),
									new InlineUIContainer(new Border {
											Height = 1,
											Background = ThemeHelper.DocumentTextBrush,
											Margin = WpfHelper.MiddleVerticalMargin,
											Opacity = 0.5
										}.Bind(FrameworkElement.WidthProperty, new Binding {
											RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ThemedTipText), 1),
											Path = new PropertyPath("ActualWidth")
										})),
									new LineBreak() });
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
						if (_IsCode == 0) {
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
							t = __FixWhitespaces.Replace(t.Replace('\n', ' '), " ");
						}
						else {
							t = UnindentTextBlock(t);
						}
						if (t.Length > 0) {
							inlines.Add(new Run(t));
						}
						break;
					case XmlNodeType.CDATA:
						inlines.Add(_IsCode == 0 ? new Run(((XText)item).Value) : new Run(UnindentTextBlock(((XText)item).Value)));
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

		static string UnindentTextBlock(string text) {
			if (text.Length < 100 && text.IndexOf("\n", StringComparison.OrdinalIgnoreCase) < 0) {
				return text;
			}
			var lines = System.Collections.Immutable.ImmutableArray.CreateBuilder<(string t, int sc)>();
			int c = Int32.MaxValue, i, ln = 0, last = 0;
			bool cnt;
			using (var sbr = Microsoft.VisualStudio.Utilities.ReusableStringBuilder.AcquireDefault(100))
			using (var r = new System.IO.StringReader(text)) {
				var sb = sbr.Resource;
				while (r.Peek() >= 0) {
					var l = r.ReadLine().Replace("\t", "    ");
					if (l.Length == 0 && c == Int32.MaxValue) {
						// ignore leading empty lines
						continue;
					}
					i = 0;
					cnt = false;
					foreach (var ch in l) {
						if (ch == ' ') {
							i++;
						}
						else {
							cnt = true;
							break;
						}
					}
					ln++;
					if (cnt) {
						if (i < c) {
							c = i;
						}
						last = ln;
					}
					lines.Add((l, i));
				}
				ln = 0;
				foreach (var (t, sc) in lines) {
					if (t.Length > 0 && sc >= c) {
						sb.Append(t, c, t.Length - c);
					}
					if (++ln == last) {
						// skip tailing empty lines
						break;
					}
					sb.AppendLine();
				}
				return sb.ToString();
			}
		}

		void CreateLink(InlineCollection inlines, XElement e, XAttribute a) {
			var link = new Hyperlink {
				NavigateUri = new Uri(a.Value),
				ToolTip = e.Attribute("title") != null ? $"{e.Attribute("title").Value}{Environment.NewLine}{a.Value}" : a.Value
			}.ClickToNavigate();
			if (e.IsEmpty) {
				link.Inlines.Add(a.Value);
				inlines.Add(link);
			}
			else {
				StyleInner(e, inlines, link);
			}
		}

		void RenderSee(InlineCollection inlines, XElement e) {
			var see = e.Attribute("cref") ?? e.Attribute("href");
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
			if (_IsCode > 0) {
				span.FontFamily = GetCodeFont();
			}
			if (blockType == BLOCK_TITLE) {
				span.FontWeight = FontWeights.Bold;
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
				text.Add(new Run(indent + ((int)list.ListType++).ToString() + ". ") { Foreground = ThemeHelper.SystemGrayTextBrush, FontWeight = FontWeights.Bold });
			}
			else if (list.ListType == ListType.Bullet) {
				text.Add(new Run(list.Indent > 0 ? indent + " \u00B7 " : " \u00B7 ") { Foreground = ThemeHelper.SystemGrayTextBrush, FontWeight = FontWeights.Bold });
			}
		}

		internal void RenderXmlDocSymbol(string symbol, InlineCollection inlines, SymbolKind symbolKind) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.UseCodeFontForXmlDocSymbol)) {
				inlines = UseCodeEditorFont(inlines);
			}

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
			if (s == null) {
				ShowBrokenLink(inlines, symbol, _SymbolFormatter);
				return;
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ContainingType)) {
				ShowContainingType(inlines, s, SymbolFormatter.SemiTransparent);
			}
			_SymbolFormatter.Format(inlines, s, null, false);

			InlineCollection UseCodeEditorFont(InlineCollection ic) {
				var span = new Span { FontFamily = ThemeHelper.CodeTextFont };
				ic.Add(span);
				return span.Inlines;
			}

			void ShowBrokenLink(InlineCollection ic, string link, SymbolFormatter sf) {
				if (link.Length > 2 && link[1] == ':') {
					switch (link[0]) {
						case 'T':
							ic.Add(link.Substring(2).Render(false, true, sf.Class));
							return;
						case 'M':
							ic.Add(link.Substring(2).Render(false, true, sf.Method));
							return;
						case 'P':
							ic.Add(link.Substring(2).Render(false, true, sf.Property));
							return;
						case 'F':
							ic.Add(link.Substring(2).Render(false, true, sf.Field));
							return;
						case 'E':
							ic.Add(link.Substring(2).Render(false, true, sf.Event));
							return;
						case '!':
							ic.Add(link.Substring(2).Render(true, true, null));
							return;
					}
				}
				ic.Add(link);
			}

			void ShowContainingType(InlineCollection ic, ISymbol sym, SymbolFormatter sf) {
				switch (sym.Kind) {
					case SymbolKind.Event:
					case SymbolKind.Field:
					case SymbolKind.Method:
					case SymbolKind.NamedType:
					case SymbolKind.Property:
						if (sym.ContainingType != null) {
							sf.Format(ic, sym.ContainingType, null, false);
							ic.Add(".");
						}
						break;
				}
			}
		}

		void StyleInner(XElement element, InlineCollection text, Span span) {
			text.Add(span);
			Render(element, span.Inlines);
		}

		FontFamily GetCodeFont() {
			if (_CodeFont != null) {
				return _CodeFont;
			}

			ThemeHelper.GetFontSettings(Microsoft.VisualStudio.Shell.Interop.FontsAndColorsCategory.TextEditor, out var fontName, out _);
			return _CodeFont = new FontFamily(fontName);
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

		static bool IsEmptyElement(XElement element) {
			XNode n;
			return element.IsEmpty
				|| (n = element.FirstNode) == null
				|| n.NodeType == XmlNodeType.Text && n.NextNode == null && String.IsNullOrWhiteSpace(((XText)n).Value);
		}

		static XNode GetFirstContent(XElement element) {
			var node = element.FirstNode;
			if (node == null) {
				return null;
			}
			do {
				switch (node.NodeType) {
					case XmlNodeType.Element: return (XElement)node;
					case XmlNodeType.Whitespace:
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
