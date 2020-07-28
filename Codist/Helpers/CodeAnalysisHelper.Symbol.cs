using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;

namespace Codist
{
	/// <summary>
	/// Denotes where an assembly is imported.
	/// </summary>
	public enum AssemblySource
	{
		/// <summary>
		/// The assembly is an external one.
		/// </summary>
		Metadata,
		/// <summary>
		/// The assembly comes from source code.
		/// </summary>
		SourceCode,
		/// <summary>
		/// The assembly comes from other projects.
		/// </summary>
		Retarget
	}

	static partial class CodeAnalysisHelper
	{
		#region Symbol getter
		public static ISymbol GetSymbol(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken = default) {
			return semanticModel.GetSymbolInfo(node, cancellationToken).Symbol
				?? semanticModel.GetDeclaredSymbol(node, cancellationToken)
				?? semanticModel.GetTypeInfo(node, cancellationToken).Type
				?? (node.IsKind(SyntaxKind.FieldDeclaration) ? semanticModel.GetDeclaredSymbol((node as FieldDeclarationSyntax).Declaration.Variables.First(), cancellationToken)
					: node.IsKind(SyntaxKind.EventFieldDeclaration) ? semanticModel.GetDeclaredSymbol((node as EventFieldDeclarationSyntax).Declaration.Variables.First(), cancellationToken)
					: null)
				;
		}

		public static ISymbol GetSymbolExt(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken = default) {
			return (node is AttributeArgumentSyntax
						? semanticModel.GetSymbolInfo(((AttributeArgumentSyntax)node).Expression, cancellationToken).Symbol
						: null)
					?? (node is SimpleBaseTypeSyntax || node is TypeConstraintSyntax
						? semanticModel.GetSymbolInfo(node.FindNode(node.Span, false, true), cancellationToken).Symbol
						: null)
					?? (node is ArgumentListSyntax
						? semanticModel.GetSymbolInfo(node.Parent, cancellationToken).Symbol
						: null)
					?? (node.Parent is MemberAccessExpressionSyntax
						? semanticModel.GetSymbolInfo(node.Parent, cancellationToken).CandidateSymbols.FirstOrDefault()
						: null)
					?? (node.Parent is ArgumentSyntax
						? semanticModel.GetSymbolInfo(((ArgumentSyntax)node.Parent).Expression, cancellationToken).CandidateSymbols.FirstOrDefault()
						: null)
					?? (node is AccessorDeclarationSyntax
						? semanticModel.GetDeclaredSymbol(node.Parent.Parent, cancellationToken)
						: null)
					?? (node is TypeParameterSyntax || node is ParameterSyntax ? semanticModel.GetDeclaredSymbol(node, cancellationToken) : null);
		}

		public static ISymbol GetSymbolOrFirstCandidate(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken = default) {
			var info = semanticModel.GetSymbolInfo(node, cancellationToken);
			return info.Symbol
				?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);
		}
		#endregion

		#region Assembly and namespace
		public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol namespaceSymbol, CancellationToken cancellationToken = default) {
			var stack = new Stack<INamespaceOrTypeSymbol>();
			stack.Push(namespaceSymbol);
			while (stack.Count > 0) {
				cancellationToken.ThrowIfCancellationRequested();
				var namespaceOrTypeSymbol = stack.Pop();
				var namespaceSymbol2 = namespaceOrTypeSymbol as INamespaceSymbol;
				if (namespaceSymbol2 != null) {
					foreach (var ns in namespaceSymbol2.GetMembers()) {
						stack.Push(ns);
					}
				}
				else {
					var namedTypeSymbol = (INamedTypeSymbol)namespaceOrTypeSymbol;
					foreach (var item in namedTypeSymbol.GetTypeMembers()) {
						stack.Push(item);
					}
					yield return namedTypeSymbol;
				}
			}
		}

		public static string GetAssemblyModuleName(this ISymbol symbol) {
			return symbol.ContainingAssembly?.Modules?.FirstOrDefault()?.Name
					?? symbol.ContainingAssembly?.Name;
		}

		public static string GetOriginalName(this ISymbol symbol) {
			if (symbol.Kind == SymbolKind.Method) {
				var m = (IMethodSymbol)symbol;
				if (m.MethodKind == MethodKind.ExplicitInterfaceImplementation) {
					return m.ExplicitInterfaceImplementations[0].Name;
				}
			}
			else if (symbol.Kind == SymbolKind.Property) {
				var p = ((IPropertySymbol)symbol).ExplicitInterfaceImplementations;
				if (p.Length > 0) {
					return p[0].Name;
				}
			}
			return symbol.Name;
		}
		#endregion

		#region Symbol information
		public static string GetAbstractionModifier(this ISymbol symbol) {
			if (symbol.IsAbstract && (symbol.Kind != SymbolKind.NamedType || (symbol as INamedTypeSymbol).TypeKind != TypeKind.Interface)) {
				return "abstract ";
			}
			else if (symbol.IsStatic) {
				return "static ";
			}
			else if (symbol.IsVirtual) {
				return "virtual ";
			}
			else if (symbol.IsOverride) {
				return symbol.IsSealed ? "sealed override " : "override ";
			}
			else if (symbol.IsSealed && (symbol.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)symbol).TypeKind == TypeKind.Class || symbol.Kind == SymbolKind.Method)) {
				return "sealed ";
			}
			return String.Empty;
		}

		public static string GetAccessibility(this ISymbol symbol) {
			switch (symbol.DeclaredAccessibility) {
				case Accessibility.Public: return "public ";
				case Accessibility.Private: return "private ";
				case Accessibility.ProtectedAndInternal: return "internal protected ";
				case Accessibility.Protected: return "protected ";
				case Accessibility.Internal: return "internal ";
				case Accessibility.ProtectedOrInternal: return "protected internal ";
				default: return String.Empty;
			}
		}

		public static ISymbol GetAliasTarget(this ISymbol symbol) {
			return symbol.Kind == SymbolKind.Alias ? (symbol as IAliasSymbol).Target : symbol;
		}

		public static ITypeSymbol ResolveElementType(this ITypeSymbol t) {
			while (t.Kind == SymbolKind.ArrayType) {
				t = ((IArrayTypeSymbol)t).ElementType;
			}
			if (t.Kind == SymbolKind.PointerType) {
				t = ((IPointerTypeSymbol)t).PointedAtType;
			}
			return t;
		}

		public static int GetImageId(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Assembly: return KnownImageIds.Assembly;
				case SymbolKind.DynamicType: return KnownImageIds.Dynamic;
				case SymbolKind.Event:
					var ev = symbol as IEventSymbol;
					switch (ev.DeclaredAccessibility) {
						case Accessibility.Public: return KnownImageIds.EventPublic;
						case Accessibility.Protected:
						case Accessibility.ProtectedOrInternal:
							return KnownImageIds.EventProtected;
						case Accessibility.Private: return KnownImageIds.EventPrivate;
						case Accessibility.ProtectedAndInternal:
						case Accessibility.Internal: return KnownImageIds.EventInternal;
						default: return IconIds.Event;
					}
				case SymbolKind.Field:
					var f = symbol as IFieldSymbol;
					if (f.IsConst) {
						switch (f.DeclaredAccessibility) {
							case Accessibility.Public: return KnownImageIds.ConstantPublic;
							case Accessibility.Protected:
							case Accessibility.ProtectedOrInternal:
								return KnownImageIds.ConstantProtected;
							case Accessibility.Private: return KnownImageIds.ConstantPrivate;
							case Accessibility.ProtectedAndInternal:
							case Accessibility.Internal: return KnownImageIds.ConstantInternal;
							default: return KnownImageIds.Constant;
						}
					}
					switch (f.DeclaredAccessibility) {
						case Accessibility.Public: return KnownImageIds.FieldPublic;
						case Accessibility.Protected:
						case Accessibility.ProtectedOrInternal:
							return KnownImageIds.FieldProtected;
						case Accessibility.Private: return KnownImageIds.FieldPrivate;
						case Accessibility.ProtectedAndInternal:
						case Accessibility.Internal: return KnownImageIds.FieldInternal;
						default: return IconIds.Field;
					}
				case SymbolKind.Label: return KnownImageIds.Label;
				case SymbolKind.Local: return IconIds.LocalVariable;
				case SymbolKind.Method:
					var m = symbol as IMethodSymbol;
					//if (m.IsExtensionMethod) {
					//	return KnownImageIds.ExtensionMethod;
					//}
					if (m.MethodKind == MethodKind.Constructor) {
						switch (m.DeclaredAccessibility) {
							case Accessibility.Public: return IconIds.PublicConstructor;
							case Accessibility.Protected:
							case Accessibility.ProtectedOrInternal:
								return IconIds.ProtectedConstructor;
							case Accessibility.Private: return IconIds.PrivateConstructor;
							case Accessibility.ProtectedAndInternal:
							case Accessibility.Internal: return IconIds.InternalConstructor;
							default: return IconIds.Constructor;
						}
					}
					switch (m.DeclaredAccessibility) {
						case Accessibility.Public: return KnownImageIds.MethodPublic;
						case Accessibility.Protected:
						case Accessibility.ProtectedOrInternal:
							return KnownImageIds.MethodProtected;
						case Accessibility.Private: return KnownImageIds.MethodPrivate;
						case Accessibility.ProtectedAndInternal:
						case Accessibility.Internal: return KnownImageIds.MethodInternal;
						default: return IconIds.Method;
					}
				case SymbolKind.NamedType:
					var t = symbol as INamedTypeSymbol;
					switch (t.TypeKind) {
						case TypeKind.Class:
							switch (t.DeclaredAccessibility) {
								case Accessibility.Public: return KnownImageIds.ClassPublic;
								case Accessibility.Protected:
								case Accessibility.ProtectedOrInternal:
									return KnownImageIds.ClassProtected;
								case Accessibility.Private: return KnownImageIds.ClassPrivate;
								case Accessibility.ProtectedAndInternal:
								case Accessibility.Internal: return KnownImageIds.ClassInternal;
								default: return IconIds.Class;
							}
						case TypeKind.Delegate:
							switch (t.DeclaredAccessibility) {
								case Accessibility.Public: return KnownImageIds.DelegatePublic;
								case Accessibility.Protected:
								case Accessibility.ProtectedOrInternal:
									return KnownImageIds.DelegateProtected;
								case Accessibility.Private: return KnownImageIds.DelegatePrivate;
								case Accessibility.ProtectedAndInternal:
								case Accessibility.Internal: return KnownImageIds.DelegateInternal;
								default: return IconIds.Delegate;
							}
						case TypeKind.Enum:
							switch (t.DeclaredAccessibility) {
								case Accessibility.Public: return KnownImageIds.EnumerationPublic;
								case Accessibility.Protected:
								case Accessibility.ProtectedOrInternal:
									return KnownImageIds.EnumerationProtected;
								case Accessibility.Private: return KnownImageIds.EnumerationPrivate;
								case Accessibility.ProtectedAndInternal:
								case Accessibility.Internal: return KnownImageIds.EnumerationInternal;
								default: return IconIds.Enum;
							}
						case TypeKind.Interface:
							switch (t.DeclaredAccessibility) {
								case Accessibility.Public: return KnownImageIds.InterfacePublic;
								case Accessibility.Protected:
								case Accessibility.ProtectedOrInternal:
									return KnownImageIds.InterfaceProtected;
								case Accessibility.Private: return KnownImageIds.InterfacePrivate;
								case Accessibility.ProtectedAndInternal:
								case Accessibility.Internal: return KnownImageIds.InterfaceInternal;
								default: return IconIds.Interface;
							}
						case TypeKind.Struct:
							switch (t.DeclaredAccessibility) {
								case Accessibility.Public: return KnownImageIds.StructurePublic;
								case Accessibility.Protected:
								case Accessibility.ProtectedOrInternal:
									return KnownImageIds.StructureProtected;
								case Accessibility.Private: return KnownImageIds.StructurePrivate;
								case Accessibility.ProtectedAndInternal:
								case Accessibility.Internal: return KnownImageIds.StructureInternal;
								default: return IconIds.Structure;
							}
						case TypeKind.TypeParameter:
						default: return KnownImageIds.Type;
					}
				case SymbolKind.Namespace: return IconIds.Namespace;
				case SymbolKind.Parameter: return IconIds.Argument;
				case SymbolKind.Property:
					switch ((symbol as IPropertySymbol).DeclaredAccessibility) {
						case Accessibility.Public: return KnownImageIds.PropertyPublic;
						case Accessibility.Protected:
						case Accessibility.ProtectedOrInternal:
							return KnownImageIds.PropertyProtected;
						case Accessibility.Private: return KnownImageIds.PropertyPrivate;
						case Accessibility.ProtectedAndInternal:
						case Accessibility.Internal: return KnownImageIds.PropertyInternal;
						default: return KnownImageIds.Property;
					}
				default: return KnownImageIds.Item;
			}
		}

		public static ImmutableArray<ITypeParameterSymbol> GetTypeParameters(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Method: return (symbol as IMethodSymbol).TypeParameters;
				case SymbolKind.NamedType: return (symbol as INamedTypeSymbol).TypeParameters;
				default: return ImmutableArray<ITypeParameterSymbol>.Empty;
			}
		}

		public static bool HasConstraint(this ITypeParameterSymbol symbol) {
			return symbol.HasReferenceTypeConstraint
				|| symbol.HasValueTypeConstraint
				|| symbol.HasConstructorConstraint
				|| symbol.HasUnmanagedTypeConstraint
				|| symbol.ConstraintTypes.Length > 0;
		}

		public static ImmutableArray<IParameterSymbol> GetParameters(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Method: return ((IMethodSymbol)symbol).Parameters;
				case SymbolKind.Event: return ((IEventSymbol)symbol).AddMethod.Parameters;
				case SymbolKind.Property: return ((IPropertySymbol)symbol).Parameters;
				case SymbolKind.NamedType:
					return (symbol = symbol.AsMethod()) != null ? ((IMethodSymbol)symbol).Parameters
						: ImmutableArray<IParameterSymbol>.Empty;
			}
			return ImmutableArray<IParameterSymbol>.Empty;
		}

		public static ITypeSymbol GetReturnType(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Field: return ((IFieldSymbol)symbol).Type;
				case SymbolKind.Local: return ((ILocalSymbol)symbol).Type;
				case SymbolKind.Method:
					var m = (IMethodSymbol)symbol;
					return m.MethodKind == MethodKind.Constructor ? m.ContainingType : m.ReturnType;
				case SymbolKind.Parameter: return ((IParameterSymbol)symbol).Type;
				case SymbolKind.Property: return ((IPropertySymbol)symbol).Type;
				case SymbolKind.Alias: return GetReturnType(((IAliasSymbol)symbol).Target);
				case SymbolKind.NamedType:
					return (symbol = symbol.AsMethod()) != null
						? ((IMethodSymbol)symbol).ReturnType
						: null;
			}
			return null;
		}

		public static string GetParameterString(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Property: return GetPropertyAccessors((IPropertySymbol)symbol);
				case SymbolKind.Method: return GetMethodParameters((IMethodSymbol)symbol);
				case SymbolKind.NamedType: return GetTypeParameters((INamedTypeSymbol)symbol);
				case SymbolKind.Event: return GetMethodParameters(((IEventSymbol)symbol).Type.GetMembers("Invoke").FirstOrDefault() as IMethodSymbol);
				default: return String.Empty;
			}

			string GetPropertyAccessors(IPropertySymbol p) {
				using (var sbr = ReusableStringBuilder.AcquireDefault(30)) {
					var sb = sbr.Resource;
					sb.Append(" {");
					var m = p.GetMethod;
					if (m != null) {
						if (m.DeclaredAccessibility != Accessibility.Public) {
							sb.Append(m.GetAccessibility());
						}
						sb.Append("get;");
					}
					m = p.SetMethod;
					if (m != null) {
						if (m.DeclaredAccessibility != Accessibility.Public) {
							sb.Append(m.GetAccessibility());
						}
						sb.Append("set;");
					}
					return sb.Append('}').ToString();
				}
			}
			string GetMethodParameters(IMethodSymbol m) {
				using (var sbr = ReusableStringBuilder.AcquireDefault(100)) {
					var sb = sbr.Resource;
					if (m.IsGenericMethod) {
						BuildTypeParametersString(sb, m.TypeParameters);
					}
					BuildParametersString(sb, m.Parameters);
					return sb.ToString();
				}
			}
			void BuildTypeParametersString(StringBuilder sb, ImmutableArray<ITypeParameterSymbol> paramList) {
				sb.Append('<');
				var s = false;
				foreach (var item in paramList) {
					if (s) {
						sb.Append(", ");
					}
					else {
						s = true;
					}
					sb.Append(item.Name);
				}
				sb.Append('>');
			}
			void BuildParametersString(StringBuilder sb, ImmutableArray<IParameterSymbol> paramList) {
				sb.Append('(');
				var p = false;
				foreach (var item in paramList) {
					if (p) {
						sb.Append(", ");
					}
					else {
						p = true;
					}
					if (item.IsOptional) {
						sb.Append('[');
					}
					switch (item.RefKind) {
						case RefKind.Ref: sb.Append("ref "); break;
						case RefKind.Out: sb.Append("out "); break;
						case RefKind.In: sb.Append("in "); break;
					}
					GetTypeName(item.Type, sb);
					if (item.IsOptional) {
						sb.Append(']');
					}
				}
				sb.Append(')');
			}
			string GetTypeParameters(INamedTypeSymbol t) {
				if (t.TypeKind == TypeKind.Delegate) {
					using (var sbr = ReusableStringBuilder.AcquireDefault(100)) {
						var sb = sbr.Resource;
						if (t.IsGenericType) {
							BuildTypeParametersString(sb, t.TypeParameters);
						}
						BuildParametersString(sb, t.DelegateInvokeMethod.Parameters);
						return sb.ToString();
					}
				}
				return t.Arity == 0 ? String.Empty : "<" + new string(',', t.Arity - 1) + ">";
			}
			void GetTypeName(ITypeSymbol type, StringBuilder output) {
				switch (type.TypeKind) {
					case TypeKind.Array:
						GetTypeName(((IArrayTypeSymbol)type).ElementType, output);
						output.Append("[]");
						return;

					case TypeKind.Dynamic:
					case TypeKind.Error:
						output.Append('?'); return;
					case TypeKind.Module:
					case TypeKind.TypeParameter:
					case TypeKind.Enum:
						output.Append(type.Name); return;
					case TypeKind.Pointer:
						GetTypeName(((IPointerTypeSymbol)type).PointedAtType, output);
						output.Append('*');
						return;
				}
				output.Append(type.GetSpecialTypeAlias() ?? type.Name);
				var nt = type as INamedTypeSymbol;
				if (nt == null || nt.IsGenericType == false) {
					return;
				}
				var s = false;
				output.Append('<');
				foreach (var item in nt.TypeArguments) {
					if (s) {
						output.Append(", ");
					}
					else {
						s = true;
					}
					GetTypeName(item, output);
				}
				output.Append('>');
			}
		}

		public static string GetSpecialTypeAlias(this ITypeSymbol type) {
			switch (type.SpecialType) {
				case SpecialType.System_Object: return "object";
				case SpecialType.System_Void: return "void";
				case SpecialType.System_Boolean: return "bool";
				case SpecialType.System_Char: return "char";
				case SpecialType.System_SByte: return "sbyte";
				case SpecialType.System_Byte: return "byte";
				case SpecialType.System_Int16: return "short";
				case SpecialType.System_UInt16: return "ushort";
				case SpecialType.System_Int32: return "int";
				case SpecialType.System_UInt32: return "uint";
				case SpecialType.System_Int64: return "long";
				case SpecialType.System_UInt64: return "ulong";
				case SpecialType.System_Decimal: return "decimal";
				case SpecialType.System_Single: return "float";
				case SpecialType.System_Double: return "double";
				case SpecialType.System_String: return "string";
			}
			return null;
		}

		public static string GetSymbolKindName(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Event: return "event";
				case SymbolKind.Field: return ((IFieldSymbol)symbol).IsConst ? "const" : "field";
				case SymbolKind.Label: return "label";
				case SymbolKind.Local: return ((ILocalSymbol)symbol).IsConst ? "local const" : "local";
				case SymbolKind.Method:
					return ((IMethodSymbol)symbol).IsExtensionMethod ? "extension" : "method";
				case SymbolKind.NamedType:
					switch (((INamedTypeSymbol)symbol).TypeKind) {
						case TypeKind.Array: return "array";
						case TypeKind.Dynamic: return "dynamic";
						case TypeKind.Class: return "class";
						case TypeKind.Delegate: return "delegate";
						case TypeKind.Enum: return "enum";
						case TypeKind.Interface: return "interface";
						case TypeKind.Struct: return "struct";
						case TypeKind.TypeParameter: return "type parameter";
					}
					return "type";
				case SymbolKind.Namespace: return "namespace";
				case SymbolKind.Parameter: return "parameter";
				case SymbolKind.Property: return "property";
				case SymbolKind.TypeParameter: return "type parameter";
				default: return symbol.Kind.ToString();
			}
		}

		public static string GetSpecialMethodModifier(this IMethodSymbol method) {
			if (method == null) {
				return null;
			}
			string t = null;
			if (method.IsAsync) {
				t = "async ";
			}
			else if (method.IsExtern) {
				t = "extern ";
			}
			if (method.ReturnsByRef) {
				t += "ref ";
			}
			else if (method.ReturnsByRefReadonly) {
				t += "ref readonly ";
			}
			return t;
		}

		public static bool IsCommonClass(this ISymbol symbol) {
			var type = symbol as ITypeSymbol;
			if (type == null) {
				return false;
			}
			switch (type.SpecialType) {
				case SpecialType.System_Object:
				case SpecialType.System_ValueType:
				case SpecialType.System_Enum:
				case SpecialType.System_MulticastDelegate:
				case SpecialType.System_Delegate:
					return true;
			}
			return false;
		}

		public static bool IsAwaitable(this ITypeSymbol t) {
			if (t is null || t.ContainingNamespace == null || t.ContainingNamespace.MetadataName != "System.Runtime.CompilerServices") {
				return false;
			}

			foreach (var item in t.GetMembers(WellKnownMemberNames.GetAwaiter)) {
				if (item.Kind != SymbolKind.Method) {
					continue;
				}
				var m = (IMethodSymbol)item;
				if (m.Parameters.Length == 0 && m.ReturnType.Name == "TaskAwaiter") {
					return true;
				}
			}
			return false;
		}

		public static bool IsQualifiable(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.ArrayType:
				case SymbolKind.Event:
				case SymbolKind.Field:
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.NamedType:
				case SymbolKind.Namespace:
				case SymbolKind.PointerType:
					return true;
			}
			return false;
		}

		public static bool IsMemberOrType(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Event:
				case SymbolKind.Field:
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.NamedType:
					return true;
			}
			return false;
		}

		public static IReadOnlyList<ISymbol> GetExplicitInterfaceImplementations(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Method:
					if (((IMethodSymbol)symbol).MethodKind == MethodKind.ExplicitInterfaceImplementation) {
						return ((IMethodSymbol)symbol).ExplicitInterfaceImplementations;
					}
					break;
				case SymbolKind.Property:
					return ((IPropertySymbol)symbol).ExplicitInterfaceImplementations;
				case SymbolKind.Event:
					return ((IEventSymbol)symbol).ExplicitInterfaceImplementations;
			}
			return null;
		}

		public static IMethodSymbol AsMethod(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Method: return symbol as IMethodSymbol;
				case SymbolKind.Event: return (symbol as IEventSymbol).RaiseMethod;
				case SymbolKind.NamedType:
					var t = symbol as INamedTypeSymbol;
					return t.TypeKind == TypeKind.Delegate ? t.DelegateInvokeMethod as IMethodSymbol : null;
				default: return null;
			}
		}
		#endregion

		#region Source
		public static bool HasSource(this ISymbol symbol) {
			return AssemblySourceReflector.GetSourceType(symbol.ContainingAssembly) != AssemblySource.Metadata;
		}

		public static AssemblySource GetSourceType(this IAssemblySymbol assembly) {
			return AssemblySourceReflector.GetSourceType(assembly);
		}

		public static SyntaxNode GetSyntaxNode(this ISymbol symbol, CancellationToken cancellationToken = default) {
			var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault()
				?? (symbol.IsImplicitlyDeclared && symbol.ContainingSymbol != null
					? symbol.ContainingSymbol?.DeclaringSyntaxReferences.FirstOrDefault()
					: null);
			return syntaxReference?.GetSyntax(cancellationToken);
		}

		public static ImmutableArray<SyntaxReference> GetSourceReferences(this ISymbol symbol) {
			if (symbol is IMethodSymbol m) {
				if (m.PartialDefinitionPart != null) {
					return symbol.DeclaringSyntaxReferences.AddRange(m.PartialDefinitionPart.DeclaringSyntaxReferences);
				}
				if (m.PartialImplementationPart != null) {
					return symbol.DeclaringSyntaxReferences.AddRange(m.PartialImplementationPart.DeclaringSyntaxReferences);
				}
			}
			return symbol.DeclaringSyntaxReferences;
		}

		public static Location ToLocation(this SyntaxReference syntaxReference) {
			return Location.Create(syntaxReference.SyntaxTree, syntaxReference.Span);
		}

		public static void GoToSource(this ISymbol symbol) {
			symbol.DeclaringSyntaxReferences.FirstOrDefault().GoToSource();
		}

		public static void GoToSource(this Location loc) {
			if (loc != null) {
				var pos = loc.GetLineSpan().StartLinePosition;
				CodistPackage.DTE.OpenFile(loc.SourceTree.FilePath, pos.Line + 1, pos.Character + 1);
			}
		}

		public static void GoToSource(this SyntaxReference loc) {
			if (loc != null) {
				var pos = loc.SyntaxTree.GetLineSpan(loc.Span).StartLinePosition;
				CodistPackage.DTE.OpenFile(loc.SyntaxTree.FilePath, pos.Line + 1, pos.Character + 1);
			}
		}

		public static bool IsAccessible(this ISymbol symbol, bool checkContainingType) {
			return symbol != null
				&& (symbol.DeclaredAccessibility == Accessibility.Public
					|| symbol.DeclaredAccessibility == Accessibility.Protected
					|| symbol.DeclaredAccessibility == Accessibility.ProtectedOrInternal
					|| symbol.ContainingAssembly.GetSourceType() != AssemblySource.Metadata)
				&& (checkContainingType == false || symbol.ContainingType == null || symbol.ContainingType.IsAccessible(true));
		}
		#endregion

		#region Symbol relationship
		/// <summary>
		/// Returns whether a given type <paramref name="from"/> can access symbol <paramref name="target"/>.
		/// </summary>
		public static bool CanAccess(this ITypeSymbol from, ISymbol target, IAssemblySymbol assembly) {
			if (target == null) {
				return false;
			}
			switch (target.DeclaredAccessibility) {
				case Accessibility.Public:
					return target.ContainingType == null || from.CanAccess(target.ContainingType, assembly);
				case Accessibility.Private:
					return target.ContainingType.Equals(from) || target.ContainingAssembly.GetSourceType() != AssemblySource.Metadata;
				case Accessibility.Internal:
					return target.ContainingAssembly.GivesAccessTo(assembly) &&
						(target.ContainingType == null || from.CanAccess(target.ContainingType, assembly));
				case Accessibility.ProtectedOrInternal:
					if (target.ContainingAssembly.GivesAccessTo(assembly)) {
						return true;
					}
					goto case Accessibility.Protected;
				case Accessibility.Protected:
					target = target.ContainingType;
					if (target.ContainingType != null && from.CanAccess(target.ContainingType, assembly) == false) {
						return false;
					}
					do {
						if (from.Equals(target)) {
							return true;
						}
					} while ((from = from.BaseType) != null);
					return false;
				case Accessibility.ProtectedAndInternal:
					if (target.ContainingAssembly.GivesAccessTo(assembly)) {
						target = target.ContainingType;
						if (target.ContainingType != null && from.CanAccess(target.ContainingType, null) == false) {
							return false;
						}
						do {
							if (from.Equals(target)) {
								return true;
							}
						} while ((from = from.BaseType) != null);
						return false;
					}
					return false;
			}
			return false;
		}

		public static bool CanConvertTo(this ITypeSymbol symbol, ITypeSymbol target) {
			if (symbol.Equals(target)) {
				return true;
			}
			if (target.TypeKind == TypeKind.TypeParameter) {
				var param = target as ITypeParameterSymbol;
				foreach (var item in param.ConstraintTypes) {
					if (item.CanConvertTo(symbol)) {
						return true;
					}
				}
				return false;
			}
			if (symbol.TypeKind == TypeKind.TypeParameter) {
				var param = symbol as ITypeParameterSymbol;
				foreach (var item in param.ConstraintTypes) {
					if (item.CanConvertTo(target)) {
						return true;
					}
				}
				return false;
			}
			foreach (var item in symbol.Interfaces) {
				if (item.CanConvertTo(target)) {
					return true;
				}
			}
			while ((symbol = symbol.BaseType) != null) {
				if (symbol.CanConvertTo(target)) {
					return true;
				}
			}
			return false;
		}

		public static int CompareSymbol<TSymbol>(TSymbol a, TSymbol b) where TSymbol : ISymbol {
			var s = b.ContainingAssembly.GetSourceType().CompareTo(a.ContainingAssembly.GetSourceType());
			if (s != 0) {
				return s;
			}
			INamedTypeSymbol ta = a.ContainingType, tb = b.ContainingType;
			var ct = ta != null && tb != null;
			return ct && (s = tb.DeclaredAccessibility.CompareTo(ta.DeclaredAccessibility)) != 0 ? s
				: (s = b.DeclaredAccessibility.CompareTo(a.DeclaredAccessibility)) != 0 ? s
				: ct && (s = ta.Name.CompareTo(tb.Name)) != 0 ? s
				: ct && (s = ta.GetHashCode().CompareTo(tb.GetHashCode())) != 0 ? s
				: (s = a.Name.CompareTo(b.Name)) != 0 ? s
				: 0;
		}

		public static bool AreEqual(ITypeSymbol a, ITypeSymbol b, bool ignoreTypeConstraint) {
			if (ReferenceEquals(a, b) || a.Equals(b)) {
				return true;
			}
			switch (a.TypeKind) {
				case TypeKind.Class:
				case TypeKind.Struct:
				case TypeKind.Interface:
				case TypeKind.Delegate:
					return AreEqual(a as INamedTypeSymbol, b as INamedTypeSymbol, ignoreTypeConstraint)
						|| ignoreTypeConstraint && b.TypeKind == TypeKind.TypeParameter;
				case TypeKind.TypeParameter:
					return ignoreTypeConstraint
						|| b.TypeKind == TypeKind.TypeParameter && AreEqual(a as ITypeParameterSymbol, b as ITypeParameterSymbol);
			}
			return false;
		}

		static bool AreEqual(INamedTypeSymbol ta, INamedTypeSymbol tb, bool ignoreTypeConstraint) {
			if (ta != null && tb != null
				&& ta.IsGenericType == tb.IsGenericType
				&& ReferenceEquals(ta.OriginalDefinition, tb.OriginalDefinition)) {
				var pa = ta.TypeArguments;
				var pb = tb.TypeArguments;
				if (pa.Length == pb.Length) {
					for (int i = pa.Length - 1; i >= 0; i--) {
						if (AreEqual(pa[i], pb[i], ignoreTypeConstraint) == false) {
							return false;
						}
					}
				}
				return true;
			}
			return false;
		}

		static bool AreEqual(ITypeParameterSymbol a, ITypeParameterSymbol b) {
			if (ReferenceEquals(a, b)) {
				return true;
			}
			if (a == null || b == null
				|| a.HasReferenceTypeConstraint != b.HasReferenceTypeConstraint
				|| a.HasValueTypeConstraint != b.HasValueTypeConstraint
				|| a.HasConstructorConstraint != b.HasConstructorConstraint
				|| a.HasUnmanagedTypeConstraint != b.HasUnmanagedTypeConstraint) {
				return false;
			}
			var ac = a.ConstraintTypes;
			var bc = b.ConstraintTypes;
			if (ac.Length != bc.Length) {
				return false;
			}
			for (int i = ac.Length - 1; i >= 0; i--) {
				if (ac[i].Equals(bc[i]) == false) {
					return false;
				}
			}
			return true;
		}

		public static int CompareByAccessibilityKindName(ISymbol a, ISymbol b) {
			int s;
			if ((s = b.DeclaredAccessibility - a.DeclaredAccessibility) != 0 // sort by visibility first
				|| (s = a.Kind - b.Kind) != 0 // then by member kind
				|| (s = a.Name.CompareTo(b.Name)) != 0) { // then by name
				return s;
			}
			switch (a.Kind) {
				case SymbolKind.NamedType: return ((INamedTypeSymbol)a).Arity.CompareTo(((INamedTypeSymbol)b).Arity);
				case SymbolKind.Method: return ((IMethodSymbol)a).Arity.CompareTo(((IMethodSymbol)b).Arity);
				default: return 0;
			}
		}

		public static int CompareByFieldIntegerConst(ISymbol a, ISymbol b) {
			IFieldSymbol fa = a as IFieldSymbol, fb = b as IFieldSymbol;
			return fa == null ? -1 : fb == null ? 1 : Convert.ToInt64(fa.ConstantValue).CompareTo(Convert.ToInt64(fb.ConstantValue));
		}

		public static bool ContainsTypeArgument(this INamedTypeSymbol generic, ITypeSymbol target) {
			if (generic == null || generic.IsGenericType == false || generic.IsUnboundGenericType) {
				return false;
			}
			foreach (var item in generic.TypeArguments) {
				if (item.CanConvertTo(target)) {
					return true;
				}
			}
			return false;
		}

		/// <summary>Checks whether the given symbol has the given <paramref name="kind"/>, <paramref name="returnType"/>, <paramref name="parameters"/> and <paramref name="typeParameters"/>.</summary>
		/// <param name="symbol">The symbol to be checked.</param>
		/// <param name="kind">The <see cref="SymbolKind"/> the symbol should have.</param>
		/// <param name="returnType">The type that the symbol should return.</param>
		/// <param name="parameters">The parameters the symbol should take.</param>
		/// <param name="typeParameters">The type parameters the symbol should take.</param>
		/// <remarks>Details of <paramref name="typeParameters"/> are not yet compared.</remarks>
		public static bool MatchSignature(this ISymbol symbol, SymbolKind kind, ITypeSymbol returnType, ImmutableArray<IParameterSymbol> parameters, ImmutableArray<ITypeParameterSymbol> typeParameters) {
			if (symbol.Kind != kind) {
				return false;
			}
			var retType = symbol.GetReturnType();
			if (returnType == null && retType != null
				|| returnType != null && returnType.CanConvertTo(retType) == false) {
				if (AreEqual(returnType, retType, false) == false) {
					return false;
				}
			}
			var method = symbol.AsMethod();
			if (method == null) {
				return true;
			}
			if (parameters.IsDefault == false) {
				var memberParameters = method.Parameters;
				if (memberParameters.Length != parameters.Length) {
					return false;
				}
				for (var i = parameters.Length - 1; i >= 0; i--) {
					var pi = parameters[i];
					var mi = memberParameters[i];
					if (AreEqual(pi.Type, mi.Type, false) == false
						|| pi.RefKind != mi.RefKind) {
						return false;
					}
				}
			}
			if (typeParameters.IsDefault == false) {
				var typeParams = method.TypeParameters;
				if (typeParams.Length != typeParameters.Length) {
					return false;
				}
			}
			return true;
		}

		/// <summary>Returns whether a symbol could have an override.</summary>
		public static bool MayHaveOverride(this ISymbol symbol) {
			return symbol?.ContainingType?.TypeKind == TypeKind.Class &&
				   (symbol.IsVirtual || symbol.IsAbstract || symbol.IsOverride) &&
				   symbol.IsSealed == false;
		}
		#endregion

		sealed class SymbolCallerInfoComparer : IEqualityComparer<SymbolCallerInfo>
		{
			internal static readonly SymbolCallerInfoComparer Instance = new SymbolCallerInfoComparer();

			public bool Equals(SymbolCallerInfo x, SymbolCallerInfo y) {
				return x.CallingSymbol == y.CallingSymbol;
			}

			public int GetHashCode(SymbolCallerInfo obj) {
				return obj.CallingSymbol.GetHashCode();
			}
		}

		static class AssemblySourceReflector
		{
			static readonly Func<IAssemblySymbol, int> __getAssemblyType = CreateAssemblySourceTypeFunc();
			public static AssemblySource GetSourceType(IAssemblySymbol assembly) {
				return assembly is null ? AssemblySource.Metadata : (AssemblySource)__getAssemblyType(assembly);
			}

			static Func<IAssemblySymbol, int> CreateAssemblySourceTypeFunc() {
				var m = new DynamicMethod("GetAssemblySourceType", typeof(int), new Type[] { typeof(IAssemblySymbol) }, true);
				var il = m.GetILGenerator();
				var isSource = il.DefineLabel();
				var isRetargetSource = il.DefineLabel();
				var a = System.Reflection.Assembly.GetAssembly(typeof(Microsoft.CodeAnalysis.CSharp.CSharpExtensions));
				const string NS = "Microsoft.CodeAnalysis.CSharp.Symbols.";
				var s = a.GetType(NS + "PublicModel.AssemblySymbol"); // from VS16.5
				Type ts, tr = a.GetType(NS + "Retargeting.RetargetingAssemblySymbol");
				System.Reflection.PropertyInfo ua = null;
				if (s != null) { // from VS16.5
					ts = a.GetType(NS + "PublicModel.SourceAssemblySymbol");
					ua = s.GetProperty("UnderlyingAssemblySymbol", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				}
				else {
					ts = a.GetType(NS + "SourceAssemblySymbol");
				}
				if (ts != null) {
					il.Emit(OpCodes.Ldarg_0);
					il.Emit(OpCodes.Isinst, ts);
					il.Emit(OpCodes.Brtrue_S, isSource);
				}
				if (ua != null) { // VS16.5
					// (asm as AssemblySymbol).UnderlyingAssemblySymbol is RetargetingAssemblySymbol
					il.Emit(OpCodes.Ldarg_0);
					il.Emit(OpCodes.Isinst, s);
					il.Emit(OpCodes.Callvirt, ua.GetGetMethod(true));
					il.Emit(OpCodes.Isinst, tr);
					il.Emit(OpCodes.Brtrue_S, isRetargetSource);
				}
				else if (tr != null) { // prior to VS16.5
					il.Emit(OpCodes.Ldarg_0);
					il.Emit(OpCodes.Isinst, tr);
					il.Emit(OpCodes.Brtrue_S, isRetargetSource);
				}
				il.Emit(OpCodes.Ldc_I4_0);
				il.Emit(OpCodes.Ret);
				if (ts != null) {
					il.MarkLabel(isSource);
					il.Emit(OpCodes.Ldc_I4_1);
					il.Emit(OpCodes.Ret);
				}
				if (tr != null) {
					il.MarkLabel(isRetargetSource);
					il.Emit(OpCodes.Ldc_I4_2);
					il.Emit(OpCodes.Ret);
				}
				return m.CreateDelegate(typeof(Func<IAssemblySymbol, int>)) as Func<IAssemblySymbol, int>;
			}
		}
	}
}
