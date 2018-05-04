using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

		internal TextBlock ToUIText(TextBlock block, ImmutableArray<SymbolDisplayPart> parts, int argIndex) {
			foreach (var part in parts) {
				switch (part.Kind) {
					case SymbolDisplayPartKind.AliasName:
						//todo resolve alias type
						goto default;
					case SymbolDisplayPartKind.ClassName:
						if ((part.Symbol as INamedTypeSymbol).IsAnonymousType) {
							block.AddText("?", _ClassBrush);
						}
						else {
							block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, _ClassBrush);
						}
						break;
					case SymbolDisplayPartKind.EnumName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, _EnumBrush);
						break;
					case SymbolDisplayPartKind.InterfaceName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, _InterfaceBrush);
						break;
					case SymbolDisplayPartKind.MethodName:
						block.AddSymbol(part.Symbol, argIndex != Int32.MinValue, _MethodBrush);
						break;
					case SymbolDisplayPartKind.ParameterName:
						var p = part.Symbol as IParameterSymbol;
						if (p.Ordinal == argIndex || p.IsParams && argIndex > p.Ordinal) {
							block.AddText(p.Name, true, true, _ParameterBrush);
						}
						else {
							block.AddText(p.Name, false, false, _ParameterBrush);
						}
						break;
					case SymbolDisplayPartKind.StructName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, _StructBrush);
						break;
					case SymbolDisplayPartKind.DelegateName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, _DelegateBrush);
						break;
					case SymbolDisplayPartKind.StringLiteral:
						block.AddText(part.ToString(), false, false, _TextBrush);
						break;
					case SymbolDisplayPartKind.Keyword:
						block.AddText(part.ToString(), false, false, _KeywordBrush);
						break;
					case SymbolDisplayPartKind.NamespaceName:
						block.AddText(part.Symbol.Name, _NamespaceBrush);
						break;
					case SymbolDisplayPartKind.TypeParameterName:
						block.AddText(part.Symbol.Name, argIndex == Int32.MinValue, false, _TypeParameterBrush);
						break;
					case SymbolDisplayPartKind.FieldName:
						block.AddText(part.Symbol.Name, _FieldBrush);
						break;
					case SymbolDisplayPartKind.PropertyName:
						block.AddText(part.Symbol.Name, _PropertyBrush);
						break;
					case SymbolDisplayPartKind.EventName:
						block.AddText(part.Symbol.Name, _DelegateBrush);
						break;
					default:
						block.AddText(part.ToString());
						break;
				}
			}
			return block;
		}

		internal void ToUIText(TextBlock block, TypedConstant constant) {
			switch (constant.Kind) {
				case TypedConstantKind.Primitive:
					if (constant.Value is bool) {
						block.AddText((bool)constant.Value ? "true" : "false", _KeywordBrush);
					}
					else if (constant.Value is string) {
						block.AddText(constant.ToCSharpString(), _TextBrush);
					}
					else {
						block.AddText(constant.ToCSharpString(), _NumberBrush);
					}
					break;
				case TypedConstantKind.Enum:
					var en = constant.ToCSharpString();
					if (en.IndexOf('|') != -1) {
						var items = constant.Type.GetMembers().Where(i => {
							var field = i as IFieldSymbol;
							return field != null
								&& field.HasConstantValue != false
								&& UnsafeArithmeticHelper.Equals(UnsafeArithmeticHelper.And(constant.Value, field.ConstantValue), field.ConstantValue)
								&& UnsafeArithmeticHelper.IsZero(field.ConstantValue) == false;
						});
						var flags = items.ToArray();
						for (int i = 0; i < flags.Length; i++) {
							if (i > 0) {
								block.AddText(" | ");
							}
							block.AddText(constant.Type.Name + "." + flags[i].Name, _EnumBrush);
						}
					}
					else {
						block.AddText(constant.Type.Name + en.Substring(en.LastIndexOf('.')), _EnumBrush);
					}
					break;
				case TypedConstantKind.Type:
					block.AddText("typeof", _KeywordBrush).AddText("(")
						.AddSymbol((constant.Value as ITypeSymbol), this)
						.AddText(")");
					break;
				case TypedConstantKind.Array:
					block.AddText("{");
					bool c = false;
					foreach (var item in constant.Values) {
						if (c == false) {
							c = true;
						}
						else {
							block.AddText(", ");
						}
						ToUIText(block, item);
					}
					block.AddText("}");
					break;
				default:
					block.AddText(constant.ToCSharpString());
					break;
			}
		}

		void AddTypeArguments(System.Windows.Documents.InlineCollection text, ImmutableArray<ITypeParameterSymbol> arguments) {
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
