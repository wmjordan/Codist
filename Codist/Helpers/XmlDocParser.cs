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
				case SymbolKind.Alias:
				case SymbolKind.Event:
				case SymbolKind.Field:
				case SymbolKind.Method:
				case SymbolKind.NamedType:
				case SymbolKind.Property:
					return symbol.GetXmlDoc().GetSummary();
				case SymbolKind.Parameter:
					var p = (symbol as IParameterSymbol);
					if (p.IsThis) {
						return null;
					}
					var m = p.ContainingSymbol as IMethodSymbol;
					return (m.MethodKind == MethodKind.DelegateInvoke ? m.ContainingSymbol : m).GetXmlDoc().GetNamedDocItem("param", symbol.Name);
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
					return null;
			}
		}
		public static XElement GetSummary(this XElement doc) {
			if (doc == null) {
				return null;
			}
			return doc.Element("summary")
				?? (doc.FirstNode != null && doc.FirstNode.NodeType == XmlNodeType.Text && doc.LastNode.NodeType == XmlNodeType.Text ? doc : null) // text only XML doc
				;
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
	}

	sealed class XmlDoc
	{
		internal readonly XElement Summary;
		internal readonly XElement Returns;
		internal readonly List<XElement> Exceptions;
		internal readonly bool IsPreliminary;

		public XmlDoc(ISymbol symbol) {
			XElement doc;
			switch (symbol.Kind) {
				case SymbolKind.Event:
				case SymbolKind.Field:
				case SymbolKind.NamedType:
					doc = symbol.GetXmlDoc();
					if (doc == null) {
						return;
					}
					foreach (var item in doc.Elements()) {
						switch (item.Name.LocalName) {
							case "summary": Summary = item; break;
							case "preliminary": IsPreliminary = true; break;
						}
					}
					if (Summary == null) {
						Summary = AsTextOnlySummary(doc);
					}
					return;
				case SymbolKind.Method:
				case SymbolKind.Property:
					doc = symbol.GetXmlDoc();
					if (doc == null) {
						return;
					}
					foreach (var item in doc.Elements()) {
						switch (item.Name.LocalName) {
							case "summary": Summary = item; break;
							case "returns": Returns = item; break;
							case "exception": (Exceptions ?? (Exceptions = new List<XElement>())).Add(item); break;
							case "preliminary": IsPreliminary = true; break;
						}
					}
					if (Summary == null) {
						Summary = AsTextOnlySummary(doc);
					}
					break;
				case SymbolKind.Parameter:
					var p = (symbol as IParameterSymbol);
					if (p.IsThis) {
						return;
					}
					var m = p.ContainingSymbol as IMethodSymbol;
					Summary = (m.MethodKind == MethodKind.DelegateInvoke ? m.ContainingSymbol : m).GetXmlDoc().GetNamedDocItem("param", symbol.Name);
					break;
				case SymbolKind.TypeParameter:
					var tps = symbol as ITypeParameterSymbol;
					switch (tps.TypeParameterKind) {
						case TypeParameterKind.Type:
							Summary = symbol.ContainingType.GetXmlDoc().GetNamedDocItem("typeparam", symbol.Name);
							break;
						case TypeParameterKind.Method:
							Summary = tps.DeclaringMethod.GetXmlDoc().GetNamedDocItem("typeparam", symbol.Name);
							break;
					}
					break;
			}
		}

		static XElement AsTextOnlySummary(XElement doc) {
			return (doc.FirstNode != null && doc.FirstNode.NodeType == XmlNodeType.Text && doc.LastNode.NodeType == XmlNodeType.Text ? doc : null);
		}
	}
}
