using System;
using System.Collections.Generic;
using System.Linq;
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
			string s = symbol.GetDocumentationCommentXml(null, true);
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
					IParameterSymbol p = symbol as IParameterSymbol;
					if (p.IsThis) {
						return null;
					}
					IMethodSymbol m = p.ContainingSymbol as IMethodSymbol;
					return (m.MethodKind == MethodKind.DelegateInvoke ? m.ContainingSymbol : m).GetXmlDoc().GetNamedDocItem("param", symbol.Name);

				case SymbolKind.TypeParameter:
					ITypeParameterSymbol tps = symbol as ITypeParameterSymbol;
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

		public static XElement InheritDocumentation(this ISymbol symbol, out ISymbol baseMember) {
			return InheritDocumentation(symbol, symbol, out baseMember);
		}

		static XElement InheritDocumentation(ISymbol symbol, ISymbol querySymbol, out ISymbol baseMember) {
			INamedTypeSymbol t = symbol.Kind == SymbolKind.NamedType ? symbol as INamedTypeSymbol : symbol.ContainingType;
			if (t == null
				// go to the base type if not querying interface
				|| t.TypeKind != TypeKind.Interface && (t = t.BaseType) == null
				) {
				baseMember = null;
				return null;
			}
			XElement doc;
			ISymbol member = t.GetMembers(querySymbol.Name).FirstOrDefault(i => i.MatchSignature(querySymbol.Kind, querySymbol.GetReturnType(), querySymbol.GetParameters()));
			if (member != null && (doc = member.GetXmlDoc().GetSummary()) != null) {
				baseMember = member;
				return doc;
			}
			if (t.TypeKind != TypeKind.Interface && (doc = InheritDocumentation(t, querySymbol, out baseMember)) != null) {
				return doc;
			}
			else if (symbol == querySymbol
				&& symbol.Kind != SymbolKind.NamedType
				&& (t = symbol.ContainingType) != null) {
				foreach (INamedTypeSymbol item in t.Interfaces) {
					if ((doc = InheritDocumentation(item, querySymbol, out baseMember)) != null) {
						return doc;
					}
				}
				switch (symbol.Kind) {
					case SymbolKind.Method:
						foreach (IMethodSymbol item in (symbol as IMethodSymbol).ExplicitInterfaceImplementations) {
							if ((doc = item.GetXmlDoc().GetSummary()) != null) {
								baseMember = item;
								return doc;
							}
						}
						break;

					case SymbolKind.Property:
						foreach (IPropertySymbol item in (symbol as IPropertySymbol).ExplicitInterfaceImplementations) {
							if ((doc = item.GetXmlDoc().GetSummary()) != null) {
								baseMember = item;
								return doc;
							}
						}
						break;

					case SymbolKind.Event:
						foreach (IEventSymbol item in (symbol as IEventSymbol).ExplicitInterfaceImplementations) {
							if ((doc = item.GetXmlDoc().GetSummary()) != null) {
								baseMember = item;
								return doc;
							}
						}
						break;
				}
			}
			baseMember = null;
			return null;
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
					foreach (XElement item in doc.Elements()) {
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
					foreach (XElement item in doc.Elements()) {
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
					IParameterSymbol p = symbol as IParameterSymbol;
					if (p.IsThis) {
						return;
					}
					IMethodSymbol m = p.ContainingSymbol as IMethodSymbol;
					Summary = (m.MethodKind == MethodKind.DelegateInvoke ? m.ContainingSymbol : m).GetXmlDoc().GetNamedDocItem("param", symbol.Name);
					break;

				case SymbolKind.TypeParameter:
					ITypeParameterSymbol tps = symbol as ITypeParameterSymbol;
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
			return doc.FirstNode != null && doc.FirstNode.NodeType == XmlNodeType.Text && doc.LastNode.NodeType == XmlNodeType.Text ? doc : null;
		}
	}
}
