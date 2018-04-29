using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Codist
{
	static class XmlDocParser
	{
		static readonly Regex _FixWhitespaces = new Regex(@" {2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

		public static XElement GetXmlDoc(this ISymbol symbol) {
			if (symbol == null) {
				return null;
			}
			var s = symbol.GetDocumentationCommentXml(null, true);
			try {
				return String.IsNullOrEmpty(s) == false ? XElement.Parse(s, LoadOptions.None) : null;
			}
			catch (XmlException) {
				// ignore
				return null;
			}
		}
		public static XElement GetXmlDocForSymbol(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Parameter:
					return (symbol as IParameterSymbol).ContainingSymbol.GetXmlDoc().GetNamedDocItem("param", symbol.Name);
				case SymbolKind.TypeParameter:
					var tps = symbol as ITypeParameterSymbol;
					switch (tps.TypeParameterKind) {
						case TypeParameterKind.Type:
							return symbol.ContainingType.GetXmlDoc().GetNamedDocItem("typeparam", symbol.Name);
						case TypeParameterKind.Method:
							return tps.DeclaringMethod.GetXmlDoc().GetNamedDocItem("typeparam", symbol.Name);
					}
					return null;
				default:
					return symbol.GetXmlDoc().GetSummary();
			}
		}
		public static XElement GetSummary(this XElement doc) {
			return doc?.Element("summary");
		}
		public static XElement GetReturns(this XElement doc) {
			return doc?.Element("returns");
		}
		public static XElement GetNamedDocItem(this XElement doc, string element, string name) {
			return doc?.Elements(element)?.FirstOrDefault(i => i.Attribute("name")?.Value == name);
		}
		public static IEnumerable<XElement> GetExceptions(this XElement doc) {
			return doc?.Elements("exception");
		}
		public static TextBlock ToUIText(this XElement content, Action<string, TextBlock, SymbolKind> symbolRenderer) {
			if (content == null || content.HasElements == false && content.IsEmpty) {
				return null;
			}
			var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
			ToUIText(content, text, symbolRenderer);
			return text.Inlines.FirstInline != null ? text : null;
		}

		public static void ToUIText(this XContainer content, TextBlock text, Action<string, TextBlock, SymbolKind> symbolRenderer) {
			foreach (var item in content.Nodes()) {
				switch (item.NodeType) {
					case XmlNodeType.Element:
						var e = item as XElement;
						switch (e.Name.LocalName) {
							case "para":
							case "listheader":
							case "item":
								if (e.PreviousNode != null && (e.PreviousNode as XElement)?.Name != "para") {
									text.Add(new LineBreak());
								}
								ToUIText(e, text, symbolRenderer);
								if (e.NextNode != null) {
									text.Add(new LineBreak());
								}
								break;
							case "see":
								var see = e.Attribute("cref");
								if (see != null) {
									symbolRenderer(see.Value, text, SymbolKind.Alias);
								}
								else if ((see = e.Attribute("langword")) !=null) {
									symbolRenderer(see.Value, text, SymbolKind.DynamicType);
								}
								break;
							case "paramref":
								var paramName = e.Attribute("name");
								if (paramName != null) {
									symbolRenderer(paramName.Value, text, SymbolKind.Parameter);
								}
								break;
							case "typeparamref":
								var typeParamName = e.Attribute("name");
								if (typeParamName != null) {
									symbolRenderer(typeParamName.Value, text, SymbolKind.TypeParameter);
								}
								break;
							case "list":
							case "description":
							case "c":
								ToUIText(e, text, symbolRenderer);
								break;
						}
						break;
					case XmlNodeType.Text:
						string t = (item as XText).Value;
						if (item.Parent.Name != "code") {
							var previous = (item.PreviousNode as XElement)?.Name;
							if (previous == null || previous != "see" && previous != "paramref" && previous != "typeparamref" && previous != "c") {
								t = item.NextNode == null ? t.Trim() : t.TrimStart();
							}
							else if (item.NextNode == null) {
								t = t.TrimEnd();
							}
							t = _FixWhitespaces.Replace(t.Replace('\n', ' '), " ");
						}
						text.AddText(t);
						break;
					case XmlNodeType.CDATA:
						text.AddText((item as XText).Value);
						break;
					case XmlNodeType.EntityReference:
					case XmlNodeType.Entity:
						text.AddText(item.ToString());
						break;
				}
			}
		}
	}
}
