﻿using System;
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
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
		internal static readonly SymbolFormatter Instance = new SymbolFormatter(ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(Constants.CodeText), b => { b?.Freeze(); return b; });
		internal static readonly SymbolFormatter SemiTransparent = new SymbolFormatter(ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(Constants.CodeText), b => {
			if (b != null) {
				b = b.Alpha(0.6); b.Freeze();
			}
			return b; });
		readonly Func<Brush, Brush> _brushConfigurator;

		[ClassificationType(ClassificationTypeNames = Constants.CodeClassName)]
		public Brush Class { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpConstFieldName + ";" + Constants.CodeConstantName)]
		public Brush Const { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeDelegateName)]
		public Brush Delegate { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeEnumName)]
		public Brush Enum { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpEnumFieldName + ";" + Constants.CodeEnumMemberName)]
		public Brush EnumField { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpEventName + ";" + Constants.CodeEventName)]
		public Brush Event { get; private set; }
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
		[ClassificationType(ClassificationTypeNames = Constants.CodePlainText)]
		public Brush PlainText { get; private set; }
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
			else if (symbol.Kind == SymbolKind.Parameter) {
				ShowParameterDeclaration(symbol as IParameterSymbol, info);
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
				text.Append("class", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.HasValueTypeConstraint) {
				AppendSeparatorIfHasContraint(text, hasConstraint).Append("struct", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.HasUnmanagedTypeConstraint) {
				AppendSeparatorIfHasContraint(text, hasConstraint).Append("unmanaged", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.HasConstructorConstraint) {
				AppendSeparatorIfHasContraint(text, hasConstraint).Append("new", Keyword).Append("()", PlainText);
				hasConstraint = true;
			}
			foreach (var constraint in typeParameter.ConstraintTypes) {
				AppendSeparatorIfHasContraint(text, hasConstraint).AddSymbol(constraint, false, this);
				hasConstraint = true;
			}
		}

		TextBlock AppendSeparatorIfHasContraint(TextBlock text, bool c) {
			return c ? text.Append(", ".Render(PlainText)) : text;
		}

		internal void Format(InlineCollection text, ISymbol symbol, string alias, bool bold) {
			switch (symbol.Kind) {
				case SymbolKind.ArrayType:
					Format(text, ((IArrayTypeSymbol)symbol).ElementType, alias, bold);
					if (alias == null) {
						text.Add("[]".Render(PlainText));
					}
					return;
				case SymbolKind.Event: text.Add(symbol.Render(alias, bold, Event)); return;
				case SymbolKind.Field:
					text.Add(symbol.Render(alias, bold, ((IFieldSymbol)symbol).IsConst ? Const : Field));
					return;
				case SymbolKind.Method:
					var method = (IMethodSymbol)symbol;
					text.Add(method.MethodKind == MethodKind.Constructor
						? symbol.Render(alias ?? method.ContainingType.Name, bold, GetBrushForMethod(method))
						: method.MethodKind == CodeAnalysisHelper.FunctionPointerMethod
						? symbol.Render("delegate*", true, GetBrushForMethod(method))
				: method.MethodKind == MethodKind.LambdaMethod
				? symbol.Render("lambda", true, Method)
						: symbol.Render(alias, bold, Method));
					if (method.IsGenericMethod) {
						AddTypeArguments(text, method.TypeArguments);
					}
					return;
				case SymbolKind.NamedType:
					var type = (INamedTypeSymbol)symbol;
					var specialType = type.GetSpecialTypeAlias();
					if (specialType != null) {
				text.Add((alias ?? specialType).Render(Keyword));
				if (type.GetNullableAnnotation() == 2) {
					text.Add("?".Render(PlainText));
				}
				return;
					}
					switch (type.TypeKind) {
						case TypeKind.Class:
					text.Add(symbol.Render(alias ?? (type.IsAnonymousType ? "{anonymous}" : null), bold, Class)); break;
						case TypeKind.Delegate:
							text.Add(symbol.Render(alias, bold, Delegate)); break;
						case TypeKind.Dynamic:
							text.Add(symbol.Render(alias ?? symbol.Name, bold, Keyword)); return;
						case TypeKind.Enum:
							text.Add(symbol.Render(alias, bold, Enum)); return;
						case TypeKind.Interface:
							text.Add(symbol.Render(alias, bold, Interface)); break;
						case TypeKind.Struct:
					ITypeSymbol nullable;
							if (type.IsTupleType) {
								text.Add("(".Render(PlainText));
								for (int i = 0; i < type.TupleElements.Length; i++) {
									if (i > 0) {
										text.Add(", ".Render(PlainText));
									}
									Format(text, type.TupleElements[i].Type, null, false);
									text.Add(" ");
									text.Add(type.TupleElements[i].Render(null, Field));
								}
								text.Add(")".Render(PlainText));
							}
					else if ((nullable = type.GetNullableValueType()) != null) {
						Format(text, nullable, null, false);
						text.Add("?".Render(PlainText));
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
			if (type.GetNullableAnnotation() == 2) {
				text.Add("?".Render(PlainText));
			}
			if (type.IsGenericType && type.IsTupleType == false) {
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
						text.Add("*".Render(PlainText));
					}
					return;
				case SymbolKind.ErrorType:
					text.Add("?".Render(PlainText));
					return;
				default: text.Add(symbol.Name); return;
			}
		}

		Brush GetBrushForMethod(IMethodSymbol m) {
			switch (m.ContainingType?.TypeKind) {
				case TypeKind.Class: return Class;
				case TypeKind.Struct: return Struct;
			}
			return Method;
		}

		internal Brush GetBrush(ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.ArrayType:
					return GetBrush((IArrayTypeSymbol)symbol);
				case SymbolKind.Event: return Event;
				case SymbolKind.Field: return ((IFieldSymbol)symbol).IsConst ? Const : Field;
				case SymbolKind.Method:
					var method = (IMethodSymbol)symbol;
					return method.MethodKind != MethodKind.Constructor
						? Method
						: GetBrushForMethod(method);
				case SymbolKind.NamedType:
					var type = (INamedTypeSymbol)symbol;
					var specialType = type.GetSpecialTypeAlias();
					if (specialType != null) {
						return Keyword;
					}
					switch (type.TypeKind) {
						case TypeKind.Class: return Class;
						case TypeKind.Delegate: return Delegate;
						case TypeKind.Dynamic: return Keyword;
						case TypeKind.Enum: return Enum;
						case TypeKind.Interface: return Interface;
						case TypeKind.Struct: return Struct;
						case TypeKind.TypeParameter: return TypeParameter;
						default: return Class;
					}
				case SymbolKind.Namespace: return Namespace;
				case SymbolKind.Parameter: return Parameter;
				case SymbolKind.Property: return Property;
				case SymbolKind.Local: return Local;
				case SymbolKind.TypeParameter: return TypeParameter;
				case SymbolKind.PointerType: return GetBrush(((IPointerTypeSymbol)symbol).PointedAtType);
				default: return null;
			}
		}

		internal Brush GetBrush(SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.PropertyDeclaration:
				case SyntaxKind.IndexerDeclaration: return Property;
				case SyntaxKind.FieldDeclaration: return ((BaseFieldDeclarationSyntax)node).Modifiers.Any(SyntaxKind.ConstKeyword) ? Const : Field;
				case SyntaxKind.ConstructorDeclaration: return GetBrush(node.Parent);
				case SyntaxKind.MethodDeclaration: return Method;
				case SyntaxKind.ClassDeclaration:
				case CodeAnalysisHelper.RecordDeclaration:
					return Class;
				case SyntaxKind.StructDeclaration:
				case CodeAnalysisHelper.RecordStructDesclaration:
					return Struct;
				case SyntaxKind.InterfaceDeclaration: return Interface;
				case SyntaxKind.EventDeclaration:
				case SyntaxKind.EventFieldDeclaration: return Event;
				case SyntaxKind.DelegateDeclaration: return Delegate;
				case SyntaxKind.EnumDeclaration: return Enum;
				case SyntaxKind.EnumMemberDeclaration: return EnumField;
				case SyntaxKind.NamespaceDeclaration: return Namespace;
				case SyntaxKind.VariableDeclarator: return GetBrush(node.Parent.Parent);
				default: return null;
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
						block.AddSymbol(p, p.Ordinal == argIndex || p.IsParams && argIndex > p.Ordinal, Parameter);
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
						block.Append(part.Symbol.Name, Event);
						break;
					case ExtensionName:
						block.AddSymbol(part.Symbol, true, Method);
						break;
					default:
						block.Append(part.ToString(), PlainText);
						break;
				}
			}
			return block;
		}

		internal void Format(InlineCollection block, AttributeData item, bool isReturn) {
			var a = item.AttributeClass.Name;
			block.Add("[".Render(PlainText));
			if (isReturn) {
				block.Add("return".Render(Keyword));
				block.Add(": ".Render(PlainText));
			}
			block.Add(WpfHelper.Render(item.AttributeConstructor ?? (ISymbol)item.AttributeClass, a.EndsWith("Attribute", StringComparison.Ordinal) ? a.Substring(0, a.Length - 9) : a, Class));
			if (item.ConstructorArguments.Length == 0 && item.NamedArguments.Length == 0) {
				var node = item.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
				if (node?.ArgumentList?.Arguments.Count > 0) {
					block.Add(node.ArgumentList.ToString().Render(ThemeHelper.SystemGrayTextBrush));
				}
				block.Add("]".Render(PlainText));
				return;
			}
			block.Add("(".Render(PlainText));
			int i = 0;
			foreach (var arg in item.ConstructorArguments) {
				if (++i > 1) {
					block.Add(", ".Render(PlainText));
				}
				Format(block, arg);
			}
			foreach (var arg in item.NamedArguments) {
				if (++i > 1) {
					block.Add(", ".Render(PlainText));
				}
				var attrMember = item.AttributeClass.GetMembers(arg.Key).FirstOrDefault(m => m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property);
				if (attrMember != null) {
					block.Add(arg.Key.Render(attrMember.Kind == SymbolKind.Property ? Property : Field));
				}
				else {
					block.Add(arg.Key.Render(false, true, null));
				}
				block.Add("=".Render(PlainText));
				Format(block, arg.Value);
			}
			block.Add(")]".Render(PlainText));
		}

		static Dictionary<string, Action<SymbolFormatter, IEditorFormatMap>> CreatePropertySetter() {
			var r = new Dictionary<string, Action<SymbolFormatter, IEditorFormatMap>>(19, StringComparer.OrdinalIgnoreCase);
			foreach (var item in typeof(SymbolFormatter).GetProperties()) {
				var ctn = item.GetCustomAttribute<ClassificationTypeAttribute>().ClassificationTypeNames.Split(';');
				var setFormatBrush = ReflectionHelper.CreateSetPropertyMethod<SymbolFormatter, Brush>(item.Name);
				foreach (var ct in ctn) {
					r.Add(ct, (f, m) => {
						var brush = m.GetBrush(ctn);
						setFormatBrush(f, f._brushConfigurator != null ? f._brushConfigurator(brush) : brush);
					});
				}
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
			text.Add("<".Render(PlainText));
			for (int i = 0; i < arguments.Length; i++) {
				if (i > 0) {
					text.Add(", ".Render(PlainText));
				}
				Format(text, arguments[i], null, false);
			}
			text.Add(">".Render(PlainText));
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
								block.Add(" | ".Render(PlainText));
							}
							block.Add(constant.Type.Render(null, Enum));
							block.Add(".".Render(PlainText));
							block.Add(flags[i].Render(null, EnumField));
						}
					}
					else if ((d = en.LastIndexOf('.')) != -1)  {
						block.Add(constant.Type.Render(null, Enum));
						block.Add(".".Render(PlainText));
						block.Add(en.Substring(d + 1).Render(EnumField));
					}
					else {
						block.Add(en.Render(Enum));
					}
					break;
				case TypedConstantKind.Type:
					block.Add("typeof".Render(Keyword));
					block.Add("(".Render(PlainText));
					Format(block, constant.Value as ISymbol, null, false);
					block.Add(")".Render(PlainText));
					break;
				case TypedConstantKind.Array:
					block.Add("new".Render(Keyword));
					block.Add("[] { ".Render(PlainText));
					bool c = false;
					foreach (var item in constant.Values) {
						if (c) {
							block.Add(", ".Render(PlainText));
						}
						else {
							c = true;
						}
						Format(block, item);
					}
					block.Add(" }".Render(PlainText));
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

		static void ShowParameterDeclaration(IParameterSymbol parameter, TextBlock info) {
			switch (parameter.RefKind) {
				case RefKind.Ref: info.Append("ref "); break;
				case RefKind.Out: info.Append("out "); break;
				case RefKind.In: info.Append("in "); break;
			}
		}

		void ShowSymbolDeclaration(ISymbol symbol, TextBlock info) {
			if (symbol.IsAbstract && ((symbol is INamedTypeSymbol nt) == false || nt.TypeKind != TypeKind.Interface)) {
				info.Append("abstract ", Keyword);
			}
			else if (symbol.IsStatic && symbol.Kind != SymbolKind.Namespace) {
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
			else if (symbol.IsSealed) {
				switch (symbol.Kind) {
					case SymbolKind.NamedType:
						switch (((INamedTypeSymbol)symbol).TypeKind) {
							case TypeKind.Class:
								info.Append("sealed ", Keyword); break;
							case TypeKind.Struct:
								if (((INamedTypeSymbol)symbol).IsReadOnly()) {
									info.Append("readonly ", Keyword);
								}
								if (((INamedTypeSymbol)symbol).IsRefLike()) {
									info.Append("ref ", Keyword);
								}
								break;
						}
						break;
					case SymbolKind.Method:
						info.Append("sealed ", Keyword); break;
				}
			}
			if (symbol.Kind == SymbolKind.Method) {
				var m = (symbol as IMethodSymbol).GetSpecialMethodModifier();
				if (m != null) {
					info.Append(m, Keyword);
				}
			}
			else if (symbol.Kind == SymbolKind.Property && symbol is IPropertySymbol p) {
				if (p.ReturnsByRefReadonly) {
					info.Append("ref readonly ", Keyword);
				}
				else if (p.ReturnsByRef) {
					info.Append("ref", Keyword);
				}
			}
		}
	}
}
