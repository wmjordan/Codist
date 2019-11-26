using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist
{
	sealed class SymbolFormatter
	{
		private SymbolFormatter(IEditorFormatMap formatMap, Func<Brush, Brush> brushConfigurator) {
			_brushConfigurator = brushConfigurator;
			if (formatMap != null) {
				foreach (var setter in _BrushSetter) {
					setter.Value(this, formatMap);
				}
				formatMap.FormatMappingChanged += FormatMap_FormatMappingChanged;
			}
		}

		static readonly Dictionary<string, Action<SymbolFormatter, IEditorFormatMap>> _BrushSetter = CreatePropertySetter();
		internal static readonly SymbolFormatter Instance = new SymbolFormatter(ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap("text"), b => { b?.Freeze(); return b; });
		internal static readonly SymbolFormatter SemiTransparent = new SymbolFormatter(ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap("text"), b => b?.Alpha(0.6));
		internal static readonly SymbolFormatter Empty = new SymbolFormatter(null, null);
		readonly Func<Brush, Brush> _brushConfigurator;

		[ClassificationType(ClassificationTypeNames = Constants.CodeClassName)]
		public Brush Class { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpConstFieldName + ";" + Constants.CodeConstantName)]
		public Brush Const { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeDelegateName)]
		public Brush Delegate { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeEnumName)]
		public Brush Enum { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeEnumMemberName)]
		public Brush EnumField { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpFieldName + ";" + Constants.CodeFieldName)]
		public Brush Field { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeInterfaceName)]
		public Brush Interface { get; private set; }
		[ClassificationType(ClassificationTypeNames =Constants.CSharpLocalVariableName + ";" +  Constants.CodeLocalName)]
		public Brush Local { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeKeyword)]
		public Brush Keyword { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpMethodName + ";" + Constants.CodeMethodName)]
		public Brush Method { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpNamespaceName + ";" + Constants.CodeNamespaceName)]
		public Brush Namespace { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeNumber)]
		public Brush Number { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpParameterName + ";" + Constants.CodeParameterName)]
		public Brush Parameter { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpPropertyName + ";" + Constants.CodePropertyName)]
		public Brush Property { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeStructName)]
		public Brush Struct { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeString)]
		public Brush Text { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpTypeParameterName + ";" + Constants.CodeTypeParameterName)]
		public Brush TypeParameter { get; private set; }

		public TextBlock ShowSymbolDeclaration(ISymbol symbol, TextBlock info, bool defaultPublic, bool hideTypeKind) {
			if (defaultPublic == false || symbol.DeclaredAccessibility != Accessibility.Public) {
				info.Append(symbol.GetAccessibility(), Keyword);
			}
			if (symbol.Kind == SymbolKind.Field) {
				ShowFieldDeclaration(symbol as IFieldSymbol, info);
			}
			else if (symbol.Kind == SymbolKind.Local) {
				ShowLocalDeclaration(symbol as ILocalSymbol, info);
			}
			else {
				ShowSymbolDeclaration(symbol, info);
			}
			if (hideTypeKind == false) {
				info.Append(symbol.GetSymbolKindName(), symbol.Kind == SymbolKind.NamedType ? Keyword : null).Append(" ");
			}
			return info;
		}

		public void ShowTypeConstaints(ITypeParameterSymbol typeParameter, TextBlock text) {
			bool hasConstraint = false;
			if (typeParameter.HasReferenceTypeConstraint) {
				text.Append(hasConstraint ? ", " : String.Empty).Append("class", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.HasValueTypeConstraint) {
				text.Append(hasConstraint ? ", " : String.Empty).Append("struct", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.HasUnmanagedTypeConstraint) {
				text.Append(hasConstraint ? ", " : String.Empty).Append("unmanaged", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.HasConstructorConstraint) {
				text.Append(hasConstraint ? ", " : String.Empty).Append("new", Keyword).Append("()");
				hasConstraint = true;
			}
			foreach (var constraint in typeParameter.ConstraintTypes) {
				text.Append(hasConstraint ? ", " : String.Empty).AddSymbol(constraint, false, this);
				hasConstraint = true;
			}
		}

		internal void Format(InlineCollection text, ISymbol symbol, string alias, bool bold) {
			switch (symbol.Kind) {
				case SymbolKind.ArrayType:
					Format(text, ((IArrayTypeSymbol)symbol).ElementType, alias, bold);
					if (alias == null) {
						text.Add("[]");
					}
					return;
				case SymbolKind.Event: text.Add(symbol.Render(alias, bold, Delegate)); return;
				case SymbolKind.Field:
					text.Add(symbol.Render(alias, bold, ((IFieldSymbol)symbol).IsConst ? Const : Field));
					return;
				case SymbolKind.Method:
					var method = (IMethodSymbol)symbol;
					text.Add(method.MethodKind != MethodKind.Constructor
						? symbol.Render(alias, bold, Method)
						: symbol.Render(alias ?? method.ContainingType.Name, bold, GetBrushForMethod(method)));
					if (method.IsGenericMethod) {
						AddTypeArguments(text, method.TypeArguments);
					}
					return;
				case SymbolKind.NamedType:
					var type = (INamedTypeSymbol)symbol;
					var specialType = type.GetSpecialTypeAlias();
					if (specialType != null) {
						text.Add((alias ?? specialType).Render(Keyword)); return;
					}
					switch (type.TypeKind) {
						case TypeKind.Class:
							text.Add(symbol.Render(alias ?? (type.IsAnonymousType ? "?" : null), bold, Class)); break;
						case TypeKind.Delegate:
							text.Add(symbol.Render(alias, bold, Delegate)); break;
						case TypeKind.Dynamic:
							text.Add(symbol.Render(alias ?? symbol.Name, bold, Keyword)); return;
						case TypeKind.Enum:
							text.Add(symbol.Render(alias, bold, Enum)); return;
						case TypeKind.Interface:
							text.Add(symbol.Render(alias, bold, Interface)); break;
						case TypeKind.Struct:
							if (type.IsTupleType) {
								text.Add("(");
								for (int i = 0; i < type.TupleElements.Length; i++) {
									if (i > 0) {
										text.Add(", ");
									}
									Format(text, type.TupleElements[i].Type, null, false);
									text.Add(" ");
									text.Add(type.TupleElements[i].Render(null, Field));
								}
								text.Add(")");
							}
							else {
								text.Add(symbol.Render(alias, bold, Struct));
							}
							break;
						case TypeKind.TypeParameter:
							text.Add(symbol.Render(alias ?? symbol.Name, bold, TypeParameter)); return;
						default:
							text.Add(symbol.MetadataName.Render(bold, false, Class)); return;
					}
					if (type.IsGenericType) {
						AddTypeArguments(text, type.TypeArguments);
					}
					return;
				case SymbolKind.Namespace: text.Add(symbol.Name.Render(Namespace)); return;
				case SymbolKind.Parameter: text.Add(symbol.Render(null, bold, Parameter)); return;
				case SymbolKind.Property: text.Add(symbol.Render(alias, bold, Property)); return;
				case SymbolKind.Local: text.Add(symbol.Render(null, bold, Local)); return;
				case SymbolKind.TypeParameter: text.Add(symbol.Render(null, bold, TypeParameter)); return;
				case SymbolKind.PointerType:
					Format(text, ((IPointerTypeSymbol)symbol).PointedAtType, alias, bold);
					if (alias == null) {
						text.Add("*");
					}
					return;
				case SymbolKind.ErrorType:
					text.Add("?");
					return;
				default: text.Add(symbol.Name); return;
			}

			Brush GetBrushForMethod(IMethodSymbol m) {
				switch (m.ContainingType.TypeKind) {
					case TypeKind.Class: return Class;
					case TypeKind.Struct: return Struct;
				}
				return Method;
			}
		}

		internal TextBlock Format(TextBlock block, ImmutableArray<SymbolDisplayPart> parts, int argIndex) {
			const SymbolDisplayPartKind ExtensionName = (SymbolDisplayPartKind)29;

			foreach (var part in parts) {
				switch (part.Kind) {
					case SymbolDisplayPartKind.AliasName:
						//todo resolve alias type
						goto default;
					case SymbolDisplayPartKind.ClassName:
						if (part.Symbol.Kind == SymbolKind.Method) {
							block.AddSymbol(part.Symbol, true, Method);
						}
						else if ((part.Symbol as INamedTypeSymbol).IsAnonymousType) {
							block.Append("?", Class);
						}
						else {
							block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Class);
						}
						break;
					case SymbolDisplayPartKind.EnumName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Enum);
						break;
					case SymbolDisplayPartKind.InterfaceName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Interface);
						break;
					case SymbolDisplayPartKind.MethodName:
						block.AddSymbol(part.Symbol, argIndex != Int32.MinValue, Method);
						break;
					case SymbolDisplayPartKind.ParameterName:
						var p = part.Symbol as IParameterSymbol;
						if (p.Ordinal == argIndex || p.IsParams && argIndex > p.Ordinal) {
							block.Append(p.Name, true, true, Parameter);
						}
						else {
							block.Append(p.Name, false, false, Parameter);
						}
						break;
					case SymbolDisplayPartKind.StructName:
						if (part.Symbol.Kind == SymbolKind.Method) {
							block.AddSymbol(part.Symbol, true, Method);
						}
						else {
							block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Struct);
						}
						break;
					case SymbolDisplayPartKind.DelegateName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Delegate);
						break;
					case SymbolDisplayPartKind.StringLiteral:
						block.Append(part.ToString(), false, false, Text);
						break;
					case SymbolDisplayPartKind.Keyword:
						block.Append(part.ToString(), false, false, Keyword);
						break;
					case SymbolDisplayPartKind.NamespaceName:
						block.Append(part.Symbol.Name, Namespace);
						break;
					case SymbolDisplayPartKind.TypeParameterName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, TypeParameter);
						break;
					case SymbolDisplayPartKind.FieldName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Field);
						break;
					case SymbolDisplayPartKind.PropertyName:
						block.Append(part.Symbol.Name, Property);
						break;
					case SymbolDisplayPartKind.EventName:
						block.Append(part.Symbol.Name, Delegate);
						break;
					case ExtensionName:
						block.AddSymbol(part.Symbol, true, Method);
						break;
					default:
						block.Append(part.ToString());
						break;
				}
			}
			return block;
		}

		internal void Format(InlineCollection block, AttributeData item, bool isReturn) {
			var a = item.AttributeClass.Name;
			block.Add("[");
			if (isReturn) {
				block.Add("return".Render(Keyword));
				block.Add(": ");
			}
			block.Add(WpfHelper.Render(item.AttributeConstructor ?? (ISymbol)item.AttributeClass, a.EndsWith("Attribute", StringComparison.Ordinal) ? a.Substring(0, a.Length - 9) : a, Class));
			if (item.ConstructorArguments.Length == 0 && item.NamedArguments.Length == 0) {
				var node = item.ApplicationSyntaxReference?.GetSyntax() as Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax;
				if (node?.ArgumentList?.Arguments.Count > 0) {
					block.Add(node.ArgumentList.ToString().Render(ThemeHelper.SystemGrayTextBrush));
				}
				block.Add("]");
				return;
			}
			block.Add("(");
			int i = 0;
			foreach (var arg in item.ConstructorArguments) {
				if (++i > 1) {
					block.Add(", ");
				}
				Format(block, arg);
			}
			foreach (var arg in item.NamedArguments) {
				if (++i > 1) {
					block.Add(", ");
				}
				var attrMember = item.AttributeClass.GetMembers(arg.Key).FirstOrDefault(m => m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property);
				if (attrMember != null) {
					block.Add(arg.Key.Render(attrMember.Kind == SymbolKind.Property ? Property : Field));
				}
				else {
					block.Add(arg.Key.Render(false, true, null));
				}
				block.Add("=");
				Format(block, arg.Value);
			}
			block.Add(")]");
		}

		static Dictionary<string, Action<SymbolFormatter, IEditorFormatMap>> CreatePropertySetter() {
			var r = new Dictionary<string, Action<SymbolFormatter, IEditorFormatMap>>(19, StringComparer.OrdinalIgnoreCase);
			foreach (var item in typeof(SymbolFormatter).GetProperties()) {
				var ctn = item.GetCustomAttribute<ClassificationTypeAttribute>().ClassificationTypeNames;
				var a = ReflectionHelper.CreateSetPropertyMethod<SymbolFormatter, Brush>(item.Name);
				r.Add(item.Name, (f, m) => {
					var brush = m.GetBrush(ctn.Split(';'));
					a(f, f._brushConfigurator != null ? f._brushConfigurator(brush) : brush);
				});
			}
			return r;
		}

		void FormatMap_FormatMappingChanged(object sender, FormatItemsEventArgs e) {
			var m = sender as IEditorFormatMap;
			foreach (var item in e.ChangedItems) {
				if (_BrushSetter.TryGetValue(item, out var a)) {
					a(this, m);
				}
			}
		}

		void AddTypeArguments(InlineCollection text, ImmutableArray<ITypeSymbol> arguments) {
			text.Add("<");
			for (int i = 0; i < arguments.Length; i++) {
				if (i > 0) {
					text.Add(", ");
				}
				Format(text, arguments[i], null, false);
			}
			text.Add(">");
		}

		void Format(InlineCollection block, TypedConstant constant) {
			if (constant.IsNull) {
				block.Add("null".Render(Keyword));
				return;
			}
			switch (constant.Kind) {
				case TypedConstantKind.Primitive:
					if (constant.Value is bool) {
						block.Add(WpfHelper.Render((bool)constant.Value ? "true" : "false", Keyword));
					}
					else if (constant.Value is string) {
						block.Add(constant.ToCSharpString().Render(Text));
					}
					else {
						block.Add(constant.ToCSharpString().Render(Number));
					}
					break;
				case TypedConstantKind.Enum:
					var en = constant.ToCSharpString();
					int d;
					if (en.IndexOf('|') != -1) {
						var items = constant.Type.GetMembers().Where(i => {
							var field = i as IFieldSymbol;
							return field != null
								&& field.HasConstantValue
								&& UnsafeArithmeticHelper.Equals(UnsafeArithmeticHelper.And(constant.Value, field.ConstantValue), field.ConstantValue)
								&& UnsafeArithmeticHelper.IsZero(field.ConstantValue) == false;
						});
						var flags = items.ToArray();
						for (int i = 0; i < flags.Length; i++) {
							if (i > 0) {
								block.Add(" | ");
							}
							block.Add((constant.Type.Name + "." + flags[i].Name).Render(Enum));
						}
					}
					else if ((d = en.LastIndexOf('.')) != -1)  {
						block.Add((constant.Type.Name + en.Substring(d)).Render(Enum));
					}
					else {
						block.Add(en.Render(Enum));
					}
					break;
				case TypedConstantKind.Type:
					block.Add("typeof".Render(Keyword));
					block.Add("(");
					Format(block, constant.Value as ISymbol, null, false);
					block.Add(")");
					break;
				case TypedConstantKind.Array:
					block.Add("new".Render(Keyword));
					block.Add("[] { ");
					bool c = false;
					foreach (var item in constant.Values) {
						if (c) {
							block.Add(", ");
						}
						else {
							c = true;
						}
						Format(block, item);
					}
					block.Add(" }");
					break;
				default:
					block.Add(constant.ToCSharpString());
					break;
			}
		}

		void ShowFieldDeclaration(IFieldSymbol field, TextBlock info) {
			if (field.IsConst) {
				info.Append("const ", Keyword);
			}
			else {
				if (field.IsStatic) {
					info.Append("static ", Keyword);
				}
				if (field.IsReadOnly) {
					info.Append("readonly ", Keyword);
				}
				else if (field.IsVolatile) {
					info.Append("volatile ", Keyword);
				}
			}
		}

		void ShowLocalDeclaration(ILocalSymbol local, TextBlock info) {
			if (local.IsConst) {
				info.Append("const ", Keyword);
			}
			else {
				if (local.IsStatic) {
					info.Append("static ", Keyword);
				}
				if (local.IsRef) {
					info.Append(local.RefKind == RefKind.RefReadOnly ? "ref readonly " : "ref", Keyword);
				}
				if (local.IsFixed) {
					info.Append("fixed ", Keyword);
				}
			}
		}

		void ShowSymbolDeclaration(ISymbol symbol, TextBlock info) {
			if (symbol.IsAbstract) {
				info.Append("abstract ", Keyword);
			}
			else if (symbol.IsStatic) {
				info.Append("static ", Keyword);
			}
			else if (symbol.IsVirtual) {
				info.Append("virtual ", Keyword);
			}
			else if (symbol.IsOverride) {
				info.Append(symbol.IsSealed ? "sealed override " : "override ", Keyword);
				ISymbol o = null;
				switch (symbol.Kind) {
					case SymbolKind.Method: o = ((IMethodSymbol)symbol).OverriddenMethod; break;
					case SymbolKind.Property: o = ((IPropertySymbol)symbol).OverriddenProperty; break;
					case SymbolKind.Event: o = ((IEventSymbol)symbol).OverriddenEvent; break;
				}
				if (o != null) {
					var t = o.ContainingType;
					if (t != null && t.IsCommonClass() == false) {
						info.AddSymbol(t, null, this).Append(".").AddSymbol(o, null, this).Append(" ");
					}
				}
			}
			else if (symbol.IsSealed && (symbol.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)symbol).TypeKind == TypeKind.Class || symbol.Kind == SymbolKind.Method)) {
				info.Append("sealed ", Keyword);
			}
			if (symbol.Kind == SymbolKind.Method) {
				var m = (symbol as IMethodSymbol).GetSpecialMethodModifier();
				if (m != null) {
					info.Append(m, Keyword);
				}
			}
		}
	}
}
