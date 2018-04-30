using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist
{
	sealed class SymbolFormatter
	{
		Brush _NamespaceBrush, _InterfaceBrush, _ClassBrush, _StructBrush, _TextBrush, _NumberBrush, _EnumBrush, _KeywordBrush, _MethodBrush, _DelegateBrush, _ParameterBrush, _TypeParameterBrush, _PropertyBrush, _FieldBrush;

		public Brush Namespace { get => _NamespaceBrush; }
		public Brush Interface { get => _InterfaceBrush; }
		public Brush Class { get => _ClassBrush; }
		public Brush Struct { get => _StructBrush; }
		public Brush Text { get => _TextBrush; }
		public Brush Number { get => _NumberBrush; }
		public Brush Enum { get => _EnumBrush; }
		public Brush Keyword { get => _KeywordBrush; }
		public Brush Method { get => _MethodBrush; }
		public Brush Delegate { get => _DelegateBrush; }
		public Brush Parameter { get => _ParameterBrush; }
		public Brush TypeParameter { get => _TypeParameterBrush; }
		public Brush Property { get => _PropertyBrush; }
		public Brush Field { get => _FieldBrush; }

		internal void UpdateSyntaxHighlights(IEditorFormatMap formatMap) {
			System.Diagnostics.Trace.Assert(formatMap != null, "format map is null");
			_InterfaceBrush = formatMap.GetBrush(Constants.CodeInterfaceName);
			_ClassBrush = formatMap.GetBrush(Constants.CodeClassName);
			_TextBrush = formatMap.GetBrush(Constants.CodeString);
			_EnumBrush = formatMap.GetBrush(Constants.CodeEnumName);
			_DelegateBrush = formatMap.GetBrush(Constants.CodeDelegateName);
			_NumberBrush = formatMap.GetBrush(Constants.CodeNumber);
			_StructBrush = formatMap.GetBrush(Constants.CodeStructName);
			_KeywordBrush = formatMap.GetBrush(Constants.CodeKeyword);
			_NamespaceBrush = formatMap.GetBrush(Constants.CSharpNamespaceName);
			_MethodBrush = formatMap.GetBrush(Constants.CSharpMethodName);
			_ParameterBrush = formatMap.GetBrush(Constants.CSharpParameterName);
			_TypeParameterBrush = formatMap.GetBrush(Constants.CSharpTypeParameterName);
			_PropertyBrush = formatMap.GetBrush(Constants.CSharpPropertyName);
			_FieldBrush = formatMap.GetBrush(Constants.CSharpFieldName);
		}

		internal void ToUIText(System.Windows.Documents.InlineCollection text, ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Event: text.Add(symbol.Render(_DelegateBrush)); return;
				case SymbolKind.Field: text.Add(symbol.Render(_FieldBrush)); return;
				case SymbolKind.Method:
					text.Add(symbol.Render(_MethodBrush));
					var method = symbol as IMethodSymbol;
					if (method.IsGenericMethod) {
						var arguments = method.TypeParameters;
						AddTypeArguments(text, arguments);
					}
					return;
				case SymbolKind.NamedType:
					var type = symbol as INamedTypeSymbol;
					switch (type.TypeKind) {
						case TypeKind.Class:
							text.Add(symbol.Render(_ClassBrush)); break;
						case TypeKind.Delegate:
							text.Add(symbol.Render(_DelegateBrush)); return;
						case TypeKind.Dynamic:
							text.Add(symbol.Name.Render(_KeywordBrush)); return;
						case TypeKind.Enum:
							text.Add(symbol.Render(_EnumBrush)); return;
						case TypeKind.Interface:
							text.Add(symbol.Render(_InterfaceBrush)); break;
						case TypeKind.Struct:
							text.Add(symbol.Render(_StructBrush)); break;
						case TypeKind.TypeParameter:
							text.Add(symbol.Name.Render(_TypeParameterBrush)); return;
						default:
							text.Add(symbol.MetadataName.Render(_ClassBrush)); return;
					}
					if (type.IsGenericType) {
						var arguments = type.TypeParameters;
						AddTypeArguments(text, arguments);
					}
					return;
				case SymbolKind.Namespace: text.Add(symbol.Name.Render(_NamespaceBrush)); return;
				case SymbolKind.Parameter: text.Add(symbol.Name.Render(_ParameterBrush)); return;
				case SymbolKind.Property: text.Add(symbol.Render(_PropertyBrush)); return;
				case SymbolKind.TypeParameter: text.Add(symbol.Name.Render(_TypeParameterBrush)); return;
				default: text.Add(symbol.Name); return;
			}
		}

		void AddTypeArguments(System.Windows.Documents.InlineCollection text, System.Collections.Immutable.ImmutableArray<ITypeParameterSymbol> arguments) {
			text.Add("<");
			for (int i = 0; i < arguments.Length; i++) {
				if (i > 0) {
					text.Add(", ");
				}
				text.Add(arguments[i].Name.Render(_TypeParameterBrush));
			}
			text.Add(">");
		}
	}
}
