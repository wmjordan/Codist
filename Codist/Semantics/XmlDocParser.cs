using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using CLR;
using Microsoft.CodeAnalysis;

namespace Codist
{
	sealed class XmlDoc
	{
		readonly ISymbol _Symbol;
		readonly Compilation _Compilation;
		readonly bool _HasDoc;
		XElement _Summary, _Remarks, _Returns, _Value;
		bool _IsTextOnly;
		bool _Preliminary;
		List<XElement> _Parameters, _Exceptions, _TypeParameters, _SeeAlsos, _Sees, _Examples;
		int _InheritedLevel;
		List<XmlDoc> _InheritedXmlDocs;
		XmlDoc _ExplicitInheritDoc;

		public XmlDoc(ISymbol symbol, Compilation compilation) : this(symbol, compilation, 0) {
		}
		public XmlDoc(ISymbol symbol, Compilation compilation, int level) {
			_InheritedLevel = level;
			if (symbol == null) {
				return;
			}
			_Symbol = symbol.GetAliasTarget();
			_Compilation = compilation;
			switch (_Symbol.Kind) {
				case SymbolKind.Event:
				case SymbolKind.Field:
				case SymbolKind.Method:
				case SymbolKind.NamedType:
				case SymbolKind.Property:
					_HasDoc = Parse(_Symbol);
					break;
				case SymbolKind.Parameter:
				case SymbolKind.TypeParameter:
					_HasDoc = Parse(_Symbol.ContainingSymbol);
					break;
				case SymbolKind.Namespace:
					_HasDoc = Parse(((INamespaceSymbol)_Symbol).GetTypeMembers("NamespaceDoc").FirstOrDefault());
					break;
			}
		}
		public bool HasDoc => _HasDoc;
		public ISymbol Symbol => _Symbol;
		public XElement Summary => _Summary;
		public XElement Remarks => _Remarks;
		public XElement Returns => _Returns;
		public XElement Value => _Value;
		public IEnumerable<XElement> Examples => _Examples;
		public IEnumerable<XElement> Exceptions => _Exceptions;
		public IEnumerable<XElement> Sees => _Sees;
		public IEnumerable<XElement> SeeAlsos => _SeeAlsos;
		public XmlDoc ExplicitInheritDoc => _ExplicitInheritDoc;
		public bool IsTextOnly => _IsTextOnly;
		public IEnumerable<XmlDoc> InheritedXmlDocs {
			get {
				if (_InheritedXmlDocs == null) {
					_InheritedXmlDocs = new List<XmlDoc>();
					InheritDocumentation(_Symbol, _Symbol);
				}
				return _InheritedXmlDocs;
			}
		}
		public bool IsPreliminary => _Preliminary;

		public XElement GetDescription(ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Parameter: return GetParameter(symbol.Name);
				case SymbolKind.TypeParameter: return GetTypeParameter(symbol.Name);
				default: return Summary;
			}
		}
		public XElement GetInheritedDescription(ISymbol symbol, out XmlDoc inheritDoc) {
			XElement summary = null;
			inheritDoc = ExplicitInheritDoc;
			if (inheritDoc == null || (summary = inheritDoc.GetDescription(symbol)) == null) {
				foreach (var item in InheritedXmlDocs) {
					if ((summary = item.GetDescription(symbol)) != null) {
						inheritDoc = item;
						break;
					}
					if ((summary = item.GetInheritedDescription(symbol, out inheritDoc)) != null) {
						return summary;
					}
				}
			}
			return summary;
		}

		public XElement GetParameter(string name) {
			return GetNamedItem(_Parameters, name) ?? _ExplicitInheritDoc?.GetParameter(name);
		}
		public XElement GetTypeParameter(string name) {
			return GetNamedItem(_TypeParameters, name) ?? _ExplicitInheritDoc?.GetTypeParameter(name);
		}

		static XElement GetNamedItem(List<XElement> elements, string name) {
			if (elements == null) {
				return null;
			}
			foreach (var item in elements) {
				if (item.Attribute("name")?.Value == name) {
					return item;
				}
			}
			return null;
		}

		bool Parse(ISymbol symbol) {
			if (symbol == null) {
				return false;
			}
			if ((symbol.ContainingSymbol as ITypeSymbol)?.TypeKind == TypeKind.Delegate) {
				symbol = symbol.ContainingSymbol;
			}
			string c = symbol.GetUnderlyingSymbol()?.GetDocumentationCommentXml(null, true);
			if (String.IsNullOrEmpty(c)) {
				return false;
			}
			XElement d;
			try {
				d = XElement.Parse(c, LoadOptions.PreserveWhitespace);
			}
			catch (XmlException) {
				// ignore
				return false;
			}
			bool r = false;
			if (d.FirstNode != null && d.HasElements) {
				foreach (var item in d.Elements()) {
					if (ParseDocSection(item)) {
						r = true;
					}
				}
			}
			// use the member element if it begins or ends with a text node
			// support: text only XML Doc
			if (r == false && (d.FirstNode.NodeType == XmlNodeType.Text || d.LastNode.NodeType == XmlNodeType.Text)) {
				_Summary = d;
				_IsTextOnly = true;
				r = true;
			}
			return r;
		}

		bool ParseDocSection(XElement item) {
			switch (item.Name.ToString()) {
				case "summary":
					// in case when there is more than one summary, make the behavior the same as VS
					if (_Summary == null) {
						_Summary = item;
					}
					break;
				case "remarks":
					_Remarks = item; break;
				case "returns":
					_Returns = item; break;
				case "param":
					(_Parameters ?? (_Parameters = new List<XElement>())).Add(item); break;
				case "typeparam":
					(_TypeParameters ?? (_TypeParameters = new List<XElement>())).Add(item); break;
				case "exception":
					(_Exceptions ?? (_Exceptions = new List<XElement>())).Add(item); break;
				case "example":
					(_Examples ?? (_Examples = new List<XElement>())).Add(item); break;
				case "seealso":
					(_SeeAlsos ?? (_SeeAlsos = new List<XElement>())).Add(item); break;
				case "see":
					(_Sees ?? (_Sees = new List<XElement>())).Add(item); break;
				case "preliminary":
					_Preliminary = true; break;
				case "inheritdoc":
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DocumentationFromInheritDoc)) {
						var cref = item.Attribute("cref");
						if (cref != null && String.IsNullOrEmpty(cref.Value) == false) {
							var s = DocumentationCommentId.GetFirstSymbolForDeclarationId(cref.Value, _Compilation);
							if (s != null && ++_InheritedLevel < 255) {
								_ExplicitInheritDoc = new XmlDoc(s, _Compilation, _InheritedLevel);
							}
						}
					}
					break;
				case "value":
					_Value = item; break;
				default:
					return false;
			}
			return true;
		}

		void InheritDocumentation(ISymbol symbol, ISymbol querySymbol) {
			var t = symbol.Kind == SymbolKind.NamedType ? symbol as INamedTypeSymbol : symbol.ContainingType;
			if (t == null) {
				return;
			}
			// inherit from base type
			if (ReferenceEquals(symbol, querySymbol) == false) {
				var kind = querySymbol.Kind;
				ISymbol s;
				if (kind == SymbolKind.Parameter) {
					s = querySymbol.ContainingSymbol;
					kind = s.Kind;
				}
				else {
					s = querySymbol;
				}
				var returnType = s.GetReturnType();
				var parameters = s.GetParameters();
				var typeParams = s.GetTypeParameters();
				var member = t.GetMembers(s.Name)
					.FirstOrDefault(i => i.MatchSignature(kind, returnType, parameters, typeParams))
					?? s.GetExplicitInterfaceImplementations()?.FirstOrDefault(i => i.ContainingType == t);
				if (member != null) {
					if (querySymbol.Kind == SymbolKind.Parameter) {
						member = member.GetParameters()[((IParameterSymbol)querySymbol).Ordinal];
					}
					if (AddInheritedDocFromSymbol(member)) {
						return;
					}
				}
			}
			if (t.BaseType != null && t.IsAnyKind(TypeKind.Class, TypeKind.Struct)) {
				InheritDocumentation(t.BaseType, querySymbol);
			}
			// inherit from implemented interfaces
			foreach (var item in t.Interfaces) {
				InheritDocumentation(item, querySymbol);
			}
			if (symbol.Kind != SymbolKind.NamedType
				&& (t = symbol.ContainingType) != null) {
				switch (symbol.Kind) {
					case SymbolKind.Method:
						foreach (var item in ((IMethodSymbol)symbol).ExplicitInterfaceImplementations) {
							if (AddInheritedDocFromSymbol(item)) {
								return;
							}
						}
						break;
					case SymbolKind.Property:
						foreach (var item in ((IPropertySymbol)symbol).ExplicitInterfaceImplementations) {
							if (AddInheritedDocFromSymbol(item)) {
								return;
							}
						}
						break;
					case SymbolKind.Event:
						foreach (var item in ((IEventSymbol)symbol).ExplicitInterfaceImplementations) {
							if (AddInheritedDocFromSymbol(item)) {
								return;
							}
						}
						break;
				}
				foreach (var item in t.Interfaces) {
					InheritDocumentation(item, querySymbol);
				}
			}
		}

		bool AddInheritedDocFromSymbol(ISymbol symbol) {
			var doc = new XmlDoc(symbol, _Compilation, _InheritedLevel + 1);
			if (doc.HasDoc) {
				_InheritedXmlDocs.Add(doc);
				return true;
			}
			return false;
		}
	}
}
