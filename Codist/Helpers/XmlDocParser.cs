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
		/// <summary>
		/// Gets the XML Doc for an <see cref="ISymbol"/>. For constructors which do not have XML Doc, the XML Doc of its containing type is used.
		/// </summary>
		public static XElement GetXmlDoc(this ISymbol symbol) {
			if (symbol == null) {
				return null;
			}
			symbol = symbol.GetAliasTarget();
			switch (symbol.Kind) {
				case SymbolKind.Event:
				case SymbolKind.Field:
				case SymbolKind.Method:
				case SymbolKind.NamedType:
				case SymbolKind.Property:
					break;
				default:
					return null;
			}
			string s = symbol.GetDocumentationCommentXml(null, true);
			if (String.IsNullOrEmpty(s) && symbol.Kind == SymbolKind.Method) {
				var m = symbol as IMethodSymbol;
				if (m.MethodKind == MethodKind.Constructor) {
					s = m.ContainingType.GetDocumentationCommentXml(null, true);
				}
			}
			try {
				return String.IsNullOrEmpty(s) == false ? XElement.Parse(s, LoadOptions.None) : null;
			}
			catch (XmlException) {
				// ignore
				return null;
			}
		}

		public static XElement GetXmlDocSummaryForSymbol(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Alias:
					return (symbol as IAliasSymbol).Target.GetXmlDocSummaryForSymbol();
				case SymbolKind.Event:
				case SymbolKind.Field:
				case SymbolKind.Method:
				case SymbolKind.NamedType:
				case SymbolKind.Property:
					return symbol.GetXmlDoc().GetSummary();

				case SymbolKind.Parameter:
					var p = symbol as IParameterSymbol;
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

		static XElement GetSummary(this XElement doc) {
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

		public static XElement GetRemarks(this XElement doc) {
			return doc?.Element("remarks");
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
			var t = symbol.Kind == SymbolKind.NamedType ? symbol as INamedTypeSymbol : symbol.ContainingType;
			if (t == null
				// go to the base type if not querying interface
				|| t.TypeKind != TypeKind.Interface && (t = t.BaseType) == null
				) {
				baseMember = null;
				return null;
			}
			XElement doc;
			var kind = querySymbol.Kind;
			var returnType = querySymbol.GetReturnType();
			var parameters = querySymbol.GetParameters();
			var member = t.GetMembers(querySymbol.Name)
				.FirstOrDefault(i => i.MatchSignature(kind, returnType, parameters));
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
				foreach (var item in t.Interfaces) {
					if ((doc = InheritDocumentation(item, querySymbol, out baseMember)) != null) {
						return doc;
					}
				}
				switch (symbol.Kind) {
					case SymbolKind.Method:
						foreach (var item in (symbol as IMethodSymbol).ExplicitInterfaceImplementations) {
							if ((doc = item.GetXmlDoc().GetSummary()) != null) {
								baseMember = item;
								return doc;
							}
						}
						break;

					case SymbolKind.Property:
						foreach (var item in (symbol as IPropertySymbol).ExplicitInterfaceImplementations) {
							if ((doc = item.GetXmlDoc().GetSummary()) != null) {
								baseMember = item;
								return doc;
							}
						}
						break;

					case SymbolKind.Event:
						foreach (var item in (symbol as IEventSymbol).ExplicitInterfaceImplementations) {
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
}
