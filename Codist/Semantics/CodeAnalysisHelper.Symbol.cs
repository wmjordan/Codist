using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Utilities;

namespace Codist
{
	static partial class CodeAnalysisHelper
	{
		#region Symbol getter
		public static ISymbol GetSymbol(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken = default) {
			return semanticModel.GetSymbolInfo(node, cancellationToken).Symbol
				?? semanticModel.GetDeclaredSymbol(node, cancellationToken)
				?? semanticModel.GetTypeInfo(node, cancellationToken).Type
				?? (node.IsKind(SyntaxKind.FieldDeclaration) ? semanticModel.GetDeclaredSymbol(((FieldDeclarationSyntax)node).Declaration.Variables.First(), cancellationToken)
					: node.IsKind(SyntaxKind.EventFieldDeclaration) ? semanticModel.GetDeclaredSymbol(((EventFieldDeclarationSyntax)node).Declaration.Variables.First(), cancellationToken)
					: node.IsKind(RecordDeclaration) || node.IsKind(RecordStructDeclaration) ? semanticModel.GetDeclaredSymbol(node, cancellationToken)
					: null)
				;
		}

		public static ISymbol GetSymbolExt(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken = default) {
			SyntaxKind k;
			ISymbol s = null;
			if ((k = node.Kind()) == SyntaxKind.AttributeArgument) {
				s = semanticModel.GetSymbolInfo(((AttributeArgumentSyntax)node).Expression, cancellationToken).Symbol;
			}
			else if (node is SimpleBaseTypeSyntax
				|| k == SyntaxKind.TypeConstraint) {
				s = semanticModel.GetSymbolInfo(node.FindNode(node.Span, false, true), cancellationToken).Symbol;
			}
			else if (k == SyntaxKind.ArgumentList) {
				s = semanticModel.GetSymbolInfo(node.Parent, cancellationToken).Symbol;
			}
			else if (node is AccessorDeclarationSyntax
				|| k.CeqAny(SyntaxKind.TypeParameter, SyntaxKind.Parameter, RecordDeclaration, RecordStructDeclaration)) {
				s = semanticModel.GetDeclaredSymbol(node, cancellationToken);
			}
			if (s != null) {
				return s;
			}
			if ((node = node.Parent) is null) {
				return null;
			}
			switch (node.Kind()) {
				case SyntaxKind.SimpleMemberAccessExpression:
				case SyntaxKind.PointerMemberAccessExpression:
					return semanticModel.GetSymbolInfo(node, cancellationToken).CandidateSymbols.FirstOrDefault();
				case SyntaxKind.Argument:
					return semanticModel.GetSymbolInfo(((ArgumentSyntax)node).Expression, cancellationToken).CandidateSymbols.FirstOrDefault();
				case SyntaxKind.ElementAccessExpression:
					return semanticModel.GetSymbolInfo((ElementAccessExpressionSyntax)node, cancellationToken).Symbol;
			}
			return s;
		}

		public static ISymbol GetSymbolOrFirstCandidate(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken = default) {
			ImmutableArray<ISymbol> candidates;
			var info = semanticModel.GetSymbolInfo(node, cancellationToken);
			return info.Symbol
				?? ((candidates = info.CandidateSymbols).Length != 0 ? candidates[0] : null);
		}

		public static INamedTypeSymbol GetSystemTypeSymbol(this SemanticModel semanticModel, SyntaxKind kind) {
			SpecialType type;
			switch (kind) {
				case SyntaxKind.BoolKeyword: type = SpecialType.System_Boolean; break;
				case SyntaxKind.ByteKeyword: type = SpecialType.System_Byte; break;
				case SyntaxKind.SByteKeyword: type = SpecialType.System_SByte; break;
				case SyntaxKind.ShortKeyword: type = SpecialType.System_Int16; break;
				case SyntaxKind.UShortKeyword: type = SpecialType.System_UInt16; break;
				case SyntaxKind.IntKeyword: type = SpecialType.System_Int32; break;
				case SyntaxKind.UIntKeyword: type = SpecialType.System_UInt32; break;
				case SyntaxKind.LongKeyword: type = SpecialType.System_Int64; break;
				case SyntaxKind.ULongKeyword: type = SpecialType.System_UInt64; break;
				case SyntaxKind.FloatKeyword: type = SpecialType.System_Single; break;
				case SyntaxKind.DoubleKeyword: type = SpecialType.System_Double; break;
				case SyntaxKind.DecimalKeyword: type = SpecialType.System_Decimal; break;
				case SyntaxKind.StringKeyword: type = SpecialType.System_String; break;
				case SyntaxKind.CharKeyword: type = SpecialType.System_Char; break;
				case SyntaxKind.ObjectKeyword: type = SpecialType.System_Object; break;
				case SyntaxKind.VoidKeyword: type = SpecialType.System_Void; break;
				default: return null;
			}
			return semanticModel.Compilation.GetSpecialType(type);
		}
		static readonly string[] __SystemNamespace = new[] { "System" };
		public static INamedTypeSymbol GetSystemTypeSymbol(this SemanticModel semanticModel, string typeName) {
			return semanticModel.GetTypeSymbol(typeName, __SystemNamespace);
		}
		public static INamedTypeSymbol GetTypeSymbol(this SemanticModel semanticModel, string name, params string[] namespaces) {
			return GetNamespaceSymbol(semanticModel, namespaces)?.GetTypeMembers(name).FirstOrDefault();
		}
		public static INamespaceSymbol GetNamespaceSymbol(this SemanticModel semanticModel, params string[] namespaces) {
			var n = semanticModel.Compilation.GlobalNamespace;
			foreach (var item in namespaces) {
				foreach (var m in n.GetNamespaceMembers()) {
					if (m.Name == item) {
						n = m;
						goto NEXT;
					}
				}
				return null;
			NEXT:;
			}
			return n;
		}

		public static IMethodSymbol GetDisposeMethodForUsingStatement(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken = default) {
			ISymbol symbol = null;
			if (node.IsKind(SyntaxKind.UsingStatement)) {
				var us = (UsingStatementSyntax)node;
				if (us.Declaration != null) {
					symbol = semanticModel.GetSymbol(us.Declaration.Type, cancellationToken);
				}
				else if (us.Expression != null) {
					symbol = semanticModel.GetTypeInfo(us.Expression, cancellationToken).Type;
				}
			}
			else if (node.IsKind(SyntaxKind.LocalDeclarationStatement)) {
				var ld = (LocalDeclarationStatementSyntax)node;
				if (ld.Declaration != null) {
					symbol = semanticModel.GetSymbol(ld.Declaration.Type, cancellationToken);
				}
			}

			if (symbol is INamedTypeSymbol usingType) {
				symbol = semanticModel.GetSystemTypeSymbol(nameof(IDisposable)).GetMembers(nameof(IDisposable.Dispose))[0];
				if (usingType.TypeKind != TypeKind.Interface) {
					return usingType.FindImplementationForInterfaceMember(symbol) as IMethodSymbol;
				}
			}
			return symbol as IMethodSymbol;
		}

		public static ImmutableArray<ISymbol> GetCapturedVariables(this SemanticModel semanticModel, SyntaxNode range) {
			var f = semanticModel.AnalyzeDataFlow(range);
			var c = f.CapturedInside;
			return c.Length != 0 ? c.RemoveRange(f.VariablesDeclared) : c;
		}
		#endregion

		#region Assembly and namespace
		public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol namespaceSymbol, CancellationToken cancellationToken = default) {
			var stack = new Stack<INamespaceOrTypeSymbol>();
			stack.Push(namespaceSymbol);
			while (stack.Count > 0) {
				cancellationToken.ThrowIfCancellationRequested();
				var namespaceOrTypeSymbol = stack.Pop();
				if (namespaceOrTypeSymbol is INamespaceSymbol namespaceSymbol2) {
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

		/// <summary>Gets the folder and file of the referenced assembly path.</summary>
		/// <returns>If <paramref name="asm"/> is from source code, folder is <see cref="String.Empty"/> and file is the assembly name.</returns>
		public static (string folder, string file) GetReferencedAssemblyPath(this Compilation compilation, IAssemblySymbol asm) {
			if (asm is null) {
				return default;
			}
			MetadataReference mr;
			return asm.GetSourceType() == AssemblySource.Metadata
					&& (mr = compilation.GetMetadataReference(asm)) != null
				? FileHelper.DeconstructPath(mr.Display)
				: (String.Empty, asm.Modules?.FirstOrDefault()?.Name ?? asm.Name);
		}

		public static string GetAssemblyModuleName(this ISymbol symbol) {
			return symbol.ContainingAssembly?.Modules?.FirstOrDefault()?.Name
					?? symbol.ContainingAssembly?.Name;
		}

		public static bool IsExplicitNamespace(this INamespaceSymbol ns) {
			return ns?.IsGlobalNamespace == false;
		}
		#endregion

		#region Symbol information
		public static string GetDeclarationId(this ISymbol symbol) {
			return DocumentationCommentId.CreateDeclarationId(symbol is IMethodSymbol m
				&& m.MethodKind == MethodKind.ReducedExtension
					? m.OriginalDefinition.ReducedFrom
					: symbol.OriginalDefinition);
		}

		public static string GetQualifiedName(this ISymbol symbol) {
			if (symbol.Name.Contains('.')) {
				// explicit interface implementation name
				return symbol.Name;
			}
			var chain = new Chain<string>(symbol.Name);
			if (symbol is IMethodSymbol m) {
				switch (m.MethodKind) {
					case MethodKind.Constructor:
					case MethodKind.StaticConstructor:
					case MethodKind.Destructor:
					case MethodKind.AnonymousFunction:
						chain.Clear();
						break;
					case MethodKind.ReducedExtension:
						symbol = m.ReducedFrom;
						break;
				}
			}
			while ((symbol = symbol.ContainingSymbol) != null) {
				if (String.IsNullOrEmpty(symbol.Name)) {
					break;
				}
				chain.Insert(symbol.Name);
			}
			return String.Join(".", chain);
		}

		public static string GetOriginalName(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Method:
					return GetOriginalName((IMethodSymbol)symbol);
				case SymbolKind.Property:
					return GetOriginalName((IPropertySymbol)symbol);
				case SymbolKind.Event:
					var e = ((IEventSymbol)symbol).ExplicitInterfaceImplementations;
					if (e.Length != 0) {
						return e[0].Name;
					}
					break;
			}
			return symbol.Name;
		}

		static string GetOriginalName(this IMethodSymbol m) {
			switch (m.MethodKind) {
				case MethodKind.ExplicitInterfaceImplementation:
					var mi = m.ExplicitInterfaceImplementations;
					if (mi.Length != 0) {
						return mi[0].Name;
					}
					break;
				case MethodKind.Constructor:
				case MethodKind.StaticConstructor:
					return m.ContainingType.Name;
				case MethodKind.Destructor:
					return "~" + m.ContainingType.Name;
			}
			return m.Name;
		}

		public static string GetOriginalName(this IPropertySymbol ps) {
			var p = ps.ExplicitInterfaceImplementations;
			if (p.Length != 0) {
				ps = p[0];
			}
			return ps.IsIndexer
				? ps.Name.Replace("[]", String.Empty)
				: ps.Name;
		}

		public static string GetAbstractionModifier(this ISymbol symbol) {
			if (symbol.IsAbstract) {
				if (symbol.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)symbol).TypeKind == TypeKind.Interface) {
					return String.Empty;
				}
				else if (symbol.IsStatic && symbol.ContainingType?.TypeKind == TypeKind.Interface) {
					return "static abstract ";
				}
				return "abstract ";
			}
			if (symbol.IsStatic && symbol.Kind != SymbolKind.Namespace) {
				return "static ";
			}
			if (symbol.IsVirtual) {
				return "virtual ";
			}
			if (symbol.IsOverride) {
				return symbol.IsSealed ? "sealed override " : "override ";
			}
			if (symbol.IsSealed
				&& (symbol.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)symbol).TypeKind == TypeKind.Class
					|| symbol.Kind == SymbolKind.Method)) {
				return "sealed ";
			}
			return String.Empty;
		}

		public static string GetAccessibility(this ISymbol symbol) {
			switch (symbol.DeclaredAccessibility) {
				case Accessibility.Public: return symbol.Kind != SymbolKind.Namespace ? "public " : String.Empty;
				case Accessibility.Private:
					return symbol.GetExplicitInterfaceImplementations().Count != 0 ? String.Empty : "private ";
				case Accessibility.ProtectedAndInternal: return "private protected ";
				case Accessibility.Protected: return "protected ";
				case Accessibility.Internal: return "internal ";
				case Accessibility.ProtectedOrInternal: return "internal protected ";
				default: return String.Empty;
			}
		}

		public static string GetValueAccessModifier(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.NamedType: return ShowTypeValueAccessModifier((ITypeSymbol)symbol);
				case SymbolKind.Method: return ((IMethodSymbol)symbol).GetSpecialMethodModifier();
				case SymbolKind.Property: return ShowPropertyValueAccessModifier((IPropertySymbol)symbol);
				case SymbolKind.Field: return ShowFieldValueAccessModifier((IFieldSymbol)symbol);
				case SymbolKind.Parameter: return ShowParameterAccessModifier((IParameterSymbol)symbol);
				default: return String.Empty;
			}

			string ShowTypeValueAccessModifier(ITypeSymbol t) {
				if (t.IsReadOnly()) {
					return t.IsRefLike() ? "ref readonly " : "readonly ";
				}
				return t.IsRefLike() ? "ref " : String.Empty;
			}

			string ShowPropertyValueAccessModifier(IPropertySymbol p) {
				return (p.IsRequired() ? "required " : String.Empty)
					+ (p.ReturnsByRefReadonly ? "ref readonly "
						: p.ReturnsByRef ? "ref "
						: String.Empty);
			}

			string ShowFieldValueAccessModifier(IFieldSymbol f) {
				return f.IsReadOnly ? "readonly "
					: f.IsVolatile ? "volatile "
					: String.Empty;
			}

			string ShowParameterAccessModifier(IParameterSymbol p) {
				return p.GetScopedKind() != 0 ? "scoped " : String.Empty;
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
			else if (method.IsReadOnly()) {
				t += "readonly ";
			}
			return t;
		}

		public static ISymbol GetUnderlyingSymbol(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.ArrayType:
					return ((IArrayTypeSymbol)symbol).ElementType.GetUnderlyingSymbol();
				case SymbolKind.PointerType:
					return ((IPointerTypeSymbol)symbol).PointedAtType.GetUnderlyingSymbol();
				case SymbolKind.Alias:
					return ((IAliasSymbol)symbol).Target.GetUnderlyingSymbol();
				case SymbolKind.Discard:
					return ((IDiscardSymbol)symbol).Type.GetUnderlyingSymbol();
				case SymbolKind.Method:
					return ((IMethodSymbol)symbol).MethodKind == MethodKind.BuiltinOperator && symbol.ContainingType != null
						? (symbol.ContainingType.GetMembers(symbol.Name).FirstOrDefault() ?? symbol)
						: symbol;
			}
			return symbol;
		}

		public static ISymbol GetAliasTarget(this ISymbol symbol) {
			return symbol.Kind == SymbolKind.Alias ? ((IAliasSymbol)symbol).Target : symbol;
		}

		public static IMethodSymbol AsMethod(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Method: return (IMethodSymbol)symbol;
				case SymbolKind.Event: return ((IEventSymbol)symbol).RaiseMethod;
				case SymbolKind.NamedType:
					var t = (INamedTypeSymbol)symbol;
					return t.TypeKind == TypeKind.Delegate ? t.DelegateInvokeMethod : null;
				default: return null;
			}
		}

		public static bool IsPrimaryConstructor(this IMethodSymbol method) {
			if (method is null || method.MethodKind != MethodKind.Constructor || method.ContainingType is null) {
				return false;
			}
			var rm = method.DeclaringSyntaxReferences;
			if (rm.Length == 0) {
				return false;
			}
			var rt = method.ContainingType.DeclaringSyntaxReferences;
			foreach (var m in rm) {
				foreach (var t in rt) {
					if (m.SyntaxTree == t.SyntaxTree && m.Span == t.Span) {
						return true;
					}
				}
			}
			return false;
		}

		public static IMethodSymbol GetPrimaryConstructor(this INamedTypeSymbol type) {
			if (type is null || type.IsAnyKind(TypeKind.Class, TypeKind.Struct) == false) {
				return null;
			}
			var rt = type.DeclaringSyntaxReferences;
			foreach (var ctor in type.InstanceConstructors) {
				foreach (var m in ctor.DeclaringSyntaxReferences) {
					foreach (var t in rt) {
						if (m.SyntaxTree == t.SyntaxTree && m.Span == t.Span) {
							return ctor;
						}
					}
				}
			}
			return null;
		}

		public static bool IsDefinedInGenericType(this ISymbol symbol) {
			INamedTypeSymbol type;
			while ((type = symbol.ContainingType) != null) {
				if (type.IsUnboundGenericType) {
					return true;
				}
				symbol = type;
			}
			return false;
		}

		public static ImmutableArray<INamedTypeSymbol> GetImplementedInterfaces(this ISymbol symbol) {
			if (symbol.ContainingType.TypeKind == TypeKind.Interface) {
				return ImmutableArray<INamedTypeSymbol>.Empty;
			}
			var interfaces = symbol.ContainingType.AllInterfaces;
			if (interfaces.Length == 0) {
				return ImmutableArray<INamedTypeSymbol>.Empty;
			}
			var directIfs = symbol.ContainingType.GetImplementedInterfaces();
			var implementedIntfs = ImmutableArray.CreateBuilder<INamedTypeSymbol>(3);
			var refKind = symbol.GetRefKind();
			var returnType = symbol.GetReturnType();
			var parameters = symbol.GetParameters();
			var typeParams = symbol.GetTypeParameters();
			foreach (var intf in interfaces) {
				foreach (var member in intf.GetMembers(symbol.Name)) {
					if (member.Kind == symbol.Kind
						&& member.DeclaredAccessibility == Accessibility.Public
						&& member.GetRefKind() == refKind
						&& member.MatchSignature(symbol.Kind, returnType, parameters, typeParams)
						&& (symbol.IsOverride || directIfs.Contains(intf))) {
						implementedIntfs.Add(intf);
					}
				}
			}
			return implementedIntfs.ToImmutable();
		}

		static ImmutableHashSet<INamedTypeSymbol> GetImplementedInterfaces(this INamedTypeSymbol type) {
			var intfs = type.Interfaces;
			var d = new HashSet<INamedTypeSymbol>(intfs);
			foreach (var i in intfs) {
				foreach (var item in i.AllInterfaces) {
					d.Add(item);
				}
			}
			return d.ToImmutableHashSet();
		}

		public static bool HasDirectImplementationFor(this INamedTypeSymbol symbol, INamedTypeSymbol interfaceType) {
			Func<INamedTypeSymbol, INamedTypeSymbol, bool> comparer = interfaceType.IsGenericType && interfaceType.ConstructedFrom == interfaceType
				? (t, infSym) => t.ConstructedFrom == infSym
				: (t, infSym) => t.MatchWith(infSym);
			return CompareInterface(symbol, interfaceType, comparer);
		}

		public static bool IsDirectImplementationOf(this ISymbol symbol, ISymbol interfaceMember) {
			if (interfaceMember.Kind == SymbolKind.NamedType) {
				return symbol.Kind == SymbolKind.NamedType
					&& ((INamedTypeSymbol)symbol).HasDirectImplementationFor((INamedTypeSymbol)interfaceMember);
			}

			Func<INamedTypeSymbol, INamedTypeSymbol, bool> comparer = interfaceMember.IsDefinedInGenericType()
				? (t, infSym) => t.ConstructedFrom == infSym
				: (t, infSym) => t.MatchWith(infSym);
			return CompareInterface(symbol.ContainingType, interfaceMember.ContainingType, comparer);
		}

		static bool CompareInterface(INamedTypeSymbol symbol, INamedTypeSymbol interfaceType, Func<INamedTypeSymbol, INamedTypeSymbol, bool> comparer) {
			foreach (var item in symbol.Interfaces) {
				if (comparer(item, interfaceType)) {
					return true;
				}
				foreach (var baseInterfaces in item.AllInterfaces) {
					if (comparer(baseInterfaces, interfaceType)) {
						return true;
					}
				}
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
			return Array.Empty<ISymbol>();
		}

		public static IEnumerable<INamedTypeSymbol> GetContainingTypes(this ISymbol symbol) {
			var t = symbol.ContainingType;
			while (t != null) {
				yield return t;
				t = t.ContainingType;
			}
		}

		public static IEnumerable<INamedTypeSymbol> GetBaseTypes(this INamedTypeSymbol type) {
			while ((type = type.BaseType) != null) {
				yield return type;
			}
		}

		public static IEnumerable<INamedTypeSymbol> GetBaseTypes(this ITypeSymbol type) {
			var baseType = type.BaseType;
			while (baseType != null) {
				yield return baseType;
				baseType = baseType.BaseType;
			}
		}

		public static ITypeSymbol ResolveElementType(this ITypeSymbol type) {
			switch (type.Kind) {
				case SymbolKind.ArrayType: return ResolveElementType(((IArrayTypeSymbol)type).ElementType);
				case SymbolKind.PointerType: return ResolveElementType(((IPointerTypeSymbol)type).PointedAtType);
			}
			return type;
		}

		public static ITypeSymbol ResolveSingleGenericTypeArgument(this ITypeSymbol type) {
			return type != null
					&& type.SpecialType == SpecialType.None
					&& type.TypeKind != TypeKind.TypeParameter
					&& type.IsTupleType == false
					&& type is INamedTypeSymbol t
					&& t.IsGenericType && t.IsUnboundGenericType == false && t.Arity == 1
					&& (t = t.TypeArguments[0] as INamedTypeSymbol) != null
					&& t.TypeKind != TypeKind.Error
				? t
				: type;
		}

		public static ImmutableArray<ITypeParameterSymbol> GetTypeParameters(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Method: return ((IMethodSymbol)symbol).TypeParameters;
				case SymbolKind.NamedType: return ((INamedTypeSymbol)symbol).TypeParameters;
				default: return ImmutableArray<ITypeParameterSymbol>.Empty;
			}
		}

		public static bool HasConstraint(this ITypeParameterSymbol symbol) {
			return symbol.HasReferenceTypeConstraint
				|| symbol.HasValueTypeConstraint
				|| symbol.HasConstructorConstraint
				|| symbol.HasUnmanagedTypeConstraint
				|| symbol.AllowRefLikeType()
				|| symbol.HasNotNullConstraint()
				|| symbol.ConstraintTypes.Length > 0;
		}

		public static ImmutableArray<IParameterSymbol> GetParameters(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Method: return ((IMethodSymbol)symbol).Parameters;
				case SymbolKind.Event: return ((IEventSymbol)symbol).AddMethod.Parameters;
				case SymbolKind.Property: return ((IPropertySymbol)symbol).Parameters;
				case SymbolKind.NamedType:
					return (symbol = symbol.AsMethod()) != null ? ((IMethodSymbol)symbol).Parameters
						: default;
			}
			return default;
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

		public static RefKind GetRefKind(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Method: return ((IMethodSymbol)symbol).RefKind;
				case SymbolKind.Property: return ((IPropertySymbol)symbol).RefKind;
				case SymbolKind.Local: return ((ILocalSymbol)symbol).RefKind;
				case SymbolKind.Parameter: return ((IParameterSymbol)symbol).RefKind;
				default: return RefKind.None;
			}
		}

		public static ITypeSymbol GetNullableValueType(this ITypeSymbol type) {
			if (type.IsValueType
				&& type is INamedTypeSymbol nt
				&& nt.IsGenericType
				&& type.Name == nameof(Nullable)
				&& type.ContainingNamespace?.Name == "System"
				&& type.ContainingNamespace.ContainingNamespace.IsExplicitNamespace()
				&& nt.TypeArguments.Length == 1) {
				return nt.TypeArguments[0];
			}
			return null;
		}

		public static ITypeSymbol GetEventArgsType(this IEventSymbol symbol) {
			ImmutableArray<IParameterSymbol> parameters;
			return symbol.Type.GetMembers("Invoke").FirstOrDefault() is IMethodSymbol invoke
				&& (parameters = invoke.Parameters).Length == 2
				? parameters[1].Type
				: null;
		}

		public static string GetParameterString(this ISymbol symbol, bool withParamName = false) {
			switch (symbol.Kind) {
				case SymbolKind.Property: return GetPropertyAccessors((IPropertySymbol)symbol);
				case SymbolKind.Method: return GetMethodParameters((IMethodSymbol)symbol, withParamName);
				case SymbolKind.NamedType: return GetTypeParameters((INamedTypeSymbol)symbol, withParamName);
				case SymbolKind.Event: return GetMethodParameters(((IEventSymbol)symbol).Type.GetMembers("Invoke").FirstOrDefault() as IMethodSymbol, withParamName);
				default: return String.Empty;
			}

			string GetPropertyAccessors(IPropertySymbol p) {
				using (var sbr = ReusableStringBuilder.AcquireDefault(30)) {
					var sb = sbr.Resource;
					var pp = p.Parameters;
					if (pp.Length > 0) {
						sb.Append('[');
						bool s = false;
						foreach (var item in pp) {
							if (s) {
								sb.Append(',');
							}
							else {
								s = true;
							}
							sb.Append(item.Type.GetTypeName());
						}
						sb.Append(']');
					}
					sb.Append(" {");
					var m = p.GetMethod;
					if (m != null) {
						if (m.DeclaredAccessibility != Accessibility.Public) {
							sb.Append(m.GetAccessibility());
						}
						if (m.IsReadOnly()) {
							sb.Append("readonly ");
						}
						sb.Append("get;");
					}
					m = p.SetMethod;
					if (m != null) {
						if (m.DeclaredAccessibility != Accessibility.Public) {
							sb.Append(m.GetAccessibility());
						}
						sb.Append(m.IsInitOnly() ? "init;" : "set;");
					}
					return sb.Append('}').ToString();
				}
			}
			string GetMethodParameters(IMethodSymbol m, bool pn) {
				if (m == null) {
					return "(?)";
				}
				if (m.IsVararg) {
					return "(__arglist)";
				}
				using (var sbr = ReusableStringBuilder.AcquireDefault(100)) {
					var sb = sbr.Resource;
					if (m.IsGenericMethod) {
						BuildTypeParametersString(sb, m.TypeParameters);
					}
					BuildParametersString(sb, m.Parameters, pn);
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
			void BuildParametersString(StringBuilder sb, ImmutableArray<IParameterSymbol> paramList, bool pn) {
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
					if (item.GetScopedKind() != 0) {
						sb.Append("scoped ");
					}
					switch (item.RefKind) {
						case RefKind.Ref: sb.Append("ref "); break;
						case RefKind.Out: sb.Append("out "); break;
						case RefKind.In: sb.Append("in "); break;
						case RefReadonly: sb.Append("ref readonly "); break;
					}
					GetTypeName(item.Type, sb);
					if (pn) {
						sb.Append(' ').Append(item.Name);
					}
					if (item.IsOptional) {
						sb.Append(']');
					}
				}
				sb.Append(')');
			}
			string GetTypeParameters(INamedTypeSymbol t, bool pn) {
				if (t.TypeKind == TypeKind.Delegate) {
					using (var sbr = ReusableStringBuilder.AcquireDefault(100)) {
						var sb = sbr.Resource;
						if (t.IsGenericType) {
							BuildTypeParametersString(sb, t.TypeParameters);
						}
						BuildParametersString(sb, t.DelegateInvokeMethod.Parameters, pn);
						return sb.ToString();
					}
				}
				else if (t.TypeKind == Extension) {
					using (var sbr = ReusableStringBuilder.AcquireDefault(100)) {
						var sb = sbr.Resource;
						if (t.IsGenericType) {
							BuildTypeParametersString(sb, t.TypeParameters);
						}
						BuildParametersString(sb, ImmutableArray.Create(t.GetExtensionParameter()), pn);
						return sb.ToString();
					}
				}
				return t.Arity > 0 ? $"<{new string (',', t.Arity - 1)}>" : String.Empty;
			}
		}

		public static bool MatchTypeName(this ITypeSymbol typeSymbol, string className, params string[] namespaces) {
			return typeSymbol.Name == className && MatchNamespaces(typeSymbol, namespaces);
		}

		public static bool MatchNamespaces(this ITypeSymbol typeSymbol, params string[] namespaces) {
			var ns = typeSymbol.ContainingNamespace;
			foreach (var item in namespaces) {
				if (ns.IsExplicitNamespace() == false || ns.Name != item) {
					return false;
				}
				ns = ns.ContainingNamespace;
			}
			return ns.IsExplicitNamespace() == false;
		}

		public static INamespaceSymbol GetCompilationNamespace(this INamespaceSymbol namespaceSymbol, SemanticModel semanticModel) {
			return semanticModel.Compilation.GetCompilationNamespace(namespaceSymbol) ?? namespaceSymbol;
		}

		public static string GetTypeName(this ITypeSymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.NamedType:
				case SymbolKind.ArrayType:
				case SymbolKind.ErrorType:
				case SymbolKind.PointerType:
				case SymbolKind.DynamicType:
				case FunctionPointerType:
					using (var sb = ReusableStringBuilder.AcquireDefault(30)) {
						var b = sb.Resource;
						GetTypeName(symbol, b);
						return b.ToString();
					}
				default:
					return symbol.Name;
			}
		}
		static void GetTypeName(ITypeSymbol type, StringBuilder output) {
			switch (type.TypeKind) {
				case TypeKind.Array:
					GetTypeName(((IArrayTypeSymbol)type).ElementType, output);
					output.Append("[]");
					return;

				case TypeKind.Dynamic:
					output.Append('?'); return;
				case TypeKind.Module:
				case TypeKind.TypeParameter:
				case TypeKind.Enum:
					output.Append(type.Name); return;
				case TypeKind.Pointer:
					GetTypeName(((IPointerTypeSymbol)type).PointedAtType, output);
					output.Append('*');
					return;
				case FunctionPointer:
					var sig = type.GetFunctionPointerTypeSignature();
					if (sig != null) {
						output.Append("delegate*");
						switch (sig.GetCallingConvention()) {
							case 0: break;
							case 1: output.Append(" unmanaged[Cdecl]"); break;
							case 2: output.Append(" unmanaged[Stdcall]"); break;
							case 3: output.Append(" unmanaged[Thiscall]"); break;
							case 4: output.Append(" unmanaged[Fastcall]"); break;
							case 5: output.Append(" unmanaged[Varargs]"); break;
							case 9: output.Append(" unmanaged"); break;
							default: break; // not supported
						}
						output.Append('<');
						foreach (var item in sig.Parameters) {
							GetTypeName(item.Type, output);
							output.Append(',');
						}
						GetTypeName(sig.ReturnType, output);
						output.Append('>');
					}
					return;
				case TypeKind.Error:
					output.Append(type.Name.Length != 0 ? type.Name : "!"); return;
			}
			output.Append(type.GetSpecialTypeAlias() ?? type.Name);
			var nt = type as INamedTypeSymbol;
			if (nt == null || nt.IsGenericType == false || nt.Arity == 0) {
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

		public static string GetSpecialMethodName(this IMethodSymbol m) {
			switch (m.MethodKind) {
				case MethodKind.UserDefinedOperator:
					switch (m.Name) {
						case WellKnownMemberNames.EqualityOperatorName: return "==";
						case WellKnownMemberNames.InequalityOperatorName: return "!=";
						case WellKnownMemberNames.AdditionOperatorName: return "+";
						case WellKnownMemberNames.BitwiseAndOperatorName: return "&";
						case WellKnownMemberNames.BitwiseOrOperatorName: return "|";
						case WellKnownMemberNames.DecrementOperatorName: return "--";
						case WellKnownMemberNames.DivisionOperatorName: return "/";
						case WellKnownMemberNames.ExclusiveOrOperatorName: return "^";
						case WellKnownMemberNames.GreaterThanOperatorName: return ">";
						case WellKnownMemberNames.GreaterThanOrEqualOperatorName: return ">=";
						case WellKnownMemberNames.IncrementOperatorName: return "++";
						case WellKnownMemberNames.LeftShiftOperatorName: return "<<";
						case WellKnownMemberNames.LessThanOperatorName: return "<";
						case WellKnownMemberNames.LessThanOrEqualOperatorName: return "<=";
						case WellKnownMemberNames.LogicalAndOperatorName: return "&&";
						case WellKnownMemberNames.LogicalNotOperatorName: return "!";
						case WellKnownMemberNames.LogicalOrOperatorName: return "||";
						case WellKnownMemberNames.ModulusOperatorName: return "%";
						case WellKnownMemberNames.MultiplyOperatorName: return "*";
						case WellKnownMemberNames.RightShiftOperatorName: return ">>";
						case WellKnownMemberNames.SubtractionOperatorName: return "-";
						case WellKnownMemberNames.UnsignedLeftShiftOperatorName: return "<<<";
						case WellKnownMemberNames.UnsignedRightShiftOperatorName: return ">>>";
					}
					break;
				case MethodKind.Conversion:
					switch (m.Name) {
						case WellKnownMemberNames.ImplicitConversionName: return "implicit";
						case WellKnownMemberNames.ExplicitConversionName: return "explicit";
					}
					break;
			}
			return null;
		}

		public static string GetSymbolKindName(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Event: return "event";
				case SymbolKind.Field: return "field";
				case SymbolKind.Label: return "label";
				case SymbolKind.Local: return "local";
				case SymbolKind.Method: return GetMethodKindName((IMethodSymbol)symbol);
				case SymbolKind.NamedType: return GetTypeKindName((ITypeSymbol)symbol);
				case SymbolKind.Namespace: return "namespace";
				case SymbolKind.Parameter: return "parameter";
				case SymbolKind.Property: return "property";
				case SymbolKind.TypeParameter: return "type parameter";
				case FunctionPointerType: return "function pointer";
				default: return symbol.Kind.ToString();
			}
		}

		static string GetMethodKindName(IMethodSymbol method) {
			if (method.IsExtensionMethod) {
				return "extension";
			}
			switch (method.MethodKind) {
				case MethodKind.StaticConstructor:
				case MethodKind.Constructor: return "constructor";
				case MethodKind.Destructor: return "destructor";
				case MethodKind.PropertyGet: return "getter";
				case MethodKind.PropertySet: return "setter";
				case MethodKind.EventAdd: return "event add";
				case MethodKind.EventRemove: return "event remove";
				case MethodKind.LocalFunction: return "local function";
				case MethodKind.LambdaMethod: return "lambda method";
				default: return "method";
			}
		}

		static string GetTypeKindName(ITypeSymbol type) {
			switch (type.TypeKind) {
				case TypeKind.Array: return "array";
				case TypeKind.Dynamic: return "dynamic";
				case TypeKind.Class: return type.IsRecord() ? "record" : "class";
				case TypeKind.Delegate: return "delegate";
				case TypeKind.Enum: return "enum";
				case TypeKind.Interface: return "interface";
				case TypeKind.Struct: return type.IsRecord() ? "record struct" : "struct";
				case TypeKind.TypeParameter: return "type parameter";
				case Extension: return "extension";
			}
			return "type";
		}

		public static bool IsPublicConcreteInstance(this ISymbol symbol) {
			return symbol.DeclaredAccessibility == Accessibility.Public
				&& symbol.IsStatic == false
				&& symbol.IsAbstract == false;
		}

		public static bool IsAccessor(this IMethodSymbol method) {
			switch (method.MethodKind) {
				case MethodKind.EventAdd:
				case MethodKind.EventRemove:
				case MethodKind.PropertyGet:
				case MethodKind.PropertySet:
					return true;
			}
			return false;
		}

		public static bool IsTypeSpecialMethod(this IMethodSymbol method) {
			switch (method.MethodKind) {
				case MethodKind.Constructor:
				case MethodKind.Destructor:
				case MethodKind.StaticConstructor:
					return true;
			}
			return false;
		}

		public static bool IsBoundedGenericMethod(this IMethodSymbol method) {
			return method?.IsGenericMethod == true
				&& method.Equals(method.OriginalDefinition) == false;
		}

		public static bool CanTypeParametersBeInferred(this IMethodSymbol method) {
			ImmutableArray<IParameterSymbol> parameters;
			if (method?.IsGenericMethod != true) {
				return false;
			}
			if (method.MethodKind == MethodKind.ReducedExtension) {
				method = method.ReducedFrom;
			}
			if ((parameters = method.Parameters).IsEmpty) {
				return false;
			}

			foreach (var typeParam in method.TypeParameters) {
				bool isParamInferred = false;
				foreach (var param in parameters) {
					if (TypeContainsTypeParameter(param.Type, typeParam)) {
						isParamInferred = true;
						break;
					}
				}
				if (!isParamInferred) {
					return false;
				}
			}
			return true;
		}

		static bool TypeContainsTypeParameter(ITypeSymbol type, ITypeParameterSymbol typeParam) {
			if (type == null) {
				return false;
			}

			if (type.Equals(typeParam)) {
				return true;
			}

			if (type is IArrayTypeSymbol arrayType) {
				return TypeContainsTypeParameter(arrayType.ElementType, typeParam);
			}

			if (type is INamedTypeSymbol namedType) {
				foreach (var typeArg in namedType.TypeArguments) {
					if (TypeContainsTypeParameter(typeArg, typeParam)) {
						return true;
					}
				}
			}

			return false;
		}

		public static bool IsObjectOrValueType(this INamedTypeSymbol type) {
			return type.SpecialType == SpecialType.System_Object || type.SpecialType == SpecialType.System_ValueType;
		}

		public static bool IsAttributeType(this INamedTypeSymbol type) {
			return type?.TypeKind == TypeKind.Class
				&& type.GetBaseTypes().Any(t => t.MatchTypeName(nameof(Attribute), "System"));
		}

		public static bool IsExceptionType(this INamedTypeSymbol type) {
			return type?.TypeKind == TypeKind.Class
				&& type.GetBaseTypes().Any(t => t.MatchTypeName(nameof(Exception), "System"));
		}

		public static bool IsCommonBaseType(this ISymbol symbol) {
			if (symbol is ITypeSymbol type) {
				switch (type.SpecialType) {
					case SpecialType.System_Object:
					case SpecialType.System_ValueType:
					case SpecialType.System_Enum:
					case SpecialType.System_MulticastDelegate:
					case SpecialType.System_Delegate:
						return true;
				}
			}
			return false;
		}

		public static bool IsAwaitable(this ITypeSymbol type) {
			if (type is null || type.IsStatic) {
				return false;
			}
			foreach (var item in type.GetMembers(WellKnownMemberNames.GetAwaiter)) {
				if (item.Kind != SymbolKind.Method || item.IsStatic) {
					continue;
				}
				var m = (IMethodSymbol)item;
				if (m.Parameters.Length == 0
					&& m.ReturnType.IsAwaiter()) {
					return true;
				}
			}
			return false;
		}

		public static bool IsAwaiter(this ITypeSymbol type) {
			return type.TypeKind != TypeKind.Dynamic
					&& type.MatchNamespaces("CompilerServices", "Runtime", "System")
					&& (type.Name == nameof(System.Runtime.CompilerServices.TaskAwaiter)
						|| type.Name == nameof(System.Runtime.CompilerServices.ValueTaskAwaiter<int>))
				|| IsCustomAwaiter(type);
		}

		static bool IsCustomAwaiter(ITypeSymbol type) {
			int f = 0;
			const int HAS_GET_RESULT = 1, HAS_ON_COMPLETED = 2, HAS_IS_COMPLETED = 4, IS_AWAITER = 7;
			foreach (var item in type.GetMembers()) {
				if (item.IsStatic) {
					continue;
				}
				switch (item.Kind) {
					case SymbolKind.Method:
						var m = (IMethodSymbol)item;
						if (m.IsGenericMethod) {
							continue;
						}
						switch (m.Name) {
							case "GetResult":
								if (m.Parameters.Length == 0) {
									f |= HAS_GET_RESULT;
								}
								continue;
							case "OnCompleted":
								var mp = m.Parameters;
								if (m.ReturnsVoid
									&& mp.Length == 1
									&& mp[0].Type is INamedTypeSymbol pt
									&& pt.IsGenericType == false
									&& pt.MatchTypeName("Action", "System")) {
									f |= HAS_ON_COMPLETED;
								}
								continue;
						}
						continue;
					case SymbolKind.Property:
						if (item.GetReturnType()?.SpecialType == SpecialType.System_Boolean
							&& item.Name == "IsCompleted") {
							f |= HAS_IS_COMPLETED;
						}
						continue;
				}
			}
			return f == IS_AWAITER;
		}

		public static bool IsDisposable(this ISymbol symbol) {
			return symbol.Name == nameof(IDisposable)
				&& (symbol = symbol.ContainingNamespace)?.Name == nameof(System)
				&& symbol.ContainingNamespace.IsExplicitNamespace() == false;
		}

		public static bool IsAsyncDisposable(this ISymbol symbol) {
			return symbol.Name == "IAsyncDisposable"
				&& (symbol = symbol.ContainingNamespace)?.Name == nameof(System)
				&& symbol.ContainingNamespace.IsExplicitNamespace() == false;
		}

		public static bool IsObsolete(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Property:
				case SymbolKind.Method:
				case SymbolKind.Field:
				case SymbolKind.NamedType:
				case SymbolKind.Event:
					return symbol.GetAttributes()
						.Any(a => a.AttributeClass.MatchTypeName(nameof(ObsoleteAttribute), "System"));
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
				case FunctionPointerType:
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

		static readonly string[] __CompilerServicesNamespace = new string[] { "CompilerServices", "Runtime", "System" };
		public static bool IsCompilerGenerated(this ISymbol symbol) {
			string name;
			return symbol.IsImplicitlyDeclared
				|| (!symbol.CanBeReferencedByName
					&& !String.IsNullOrEmpty(name = symbol.Name)
					&& (name[0] == '<'
						|| symbol.GetAttributes().Any(i => i.AttributeClass.MatchTypeName("CompilerGeneratedAttribute", __CompilerServicesNamespace))));
		}

		public static bool MayHaveDocumentation(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Event:
				case SymbolKind.Field:
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.NamedType:
				case SymbolKind.Namespace:
					return true;
			}
			return false;
		}

		public static bool IsEmpty(this SymbolInfo symbolInfo) {
			return symbolInfo.Symbol == null && symbolInfo.CandidateReason == CandidateReason.None;
		}

		public static bool HasSymbol(this SymbolInfo symbolInfo) {
			return symbolInfo.Symbol != null || symbolInfo.CandidateReason != CandidateReason.None;
		}

		public static ImmutableArray<ISymbol> GetSymbolOrCandidates(this SymbolInfo symbolInfo) {
			return symbolInfo.Symbol != null
				? ImmutableArray.Create(symbolInfo.Symbol)
				: symbolInfo.CandidateSymbols;
		}

		#region Protected/Future property accessors
		public static int GetNullableAnnotation(this ILocalSymbol local) {
			return local != null ? NonPublicOrFutureAccessors.GetLocalNullableAnnotation(local) : 0;
		}
		public static int GetScopedKind(this ILocalSymbol local) {
			return local != null ? NonPublicOrFutureAccessors.GetLocalScopedKind(local) : 0;
		}
		public static bool IsForEach(this ILocalSymbol local) {
			return local != null && NonPublicOrFutureAccessors.GetLocalIsForEach(local);
		}
		public static bool IsUsing(this ILocalSymbol local) {
			return local != null && NonPublicOrFutureAccessors.GetLocalIsUsing(local);
		}
		public static bool IsFileLocal(this INamedTypeSymbol type) {
			return type != null && NonPublicOrFutureAccessors.GetNamedTypeIsFileLocal(type);
		}
		public static bool IsNativeIntegerType(this ITypeSymbol type) {
			return type != null && NonPublicOrFutureAccessors.GetTypeIsNativeIntegerType(type);
		}
		public static bool IsReadOnly(this ITypeSymbol type) {
			return type != null && NonPublicOrFutureAccessors.GetNamedTypeIsReadOnly(type);
		}
		public static bool IsRecord(this ITypeSymbol type) {
			return type != null && NonPublicOrFutureAccessors.GetTypeIsRecord(type);
		}
		public static bool IsRefLike(this ITypeSymbol type) {
			return type != null && NonPublicOrFutureAccessors.GetNamedTypeIsRefLikeType(type);
		}
		public static int GetNullableAnnotation(this ITypeSymbol type) {
			return type != null ? NonPublicOrFutureAccessors.GetTypeNullableAnnotation(type) : 0;
		}
		public static IParameterSymbol GetExtensionParameter(this ITypeSymbol type) {
			return type != null ? NonPublicOrFutureAccessors.GetTypeExtensionParameter(type) : null;
		}
		public static bool IsExtensionMember(this ISymbol symbol) {
			return symbol.ContainingType?.GetExtensionParameter() != null;
		}
		public static bool IsInitOnly(this IMethodSymbol method) {
			return method != null && NonPublicOrFutureAccessors.GetMethodIsInitOnly(method);
		}
		public static bool IsReadOnly(this IMethodSymbol method) {
			return method != null && NonPublicOrFutureAccessors.GetMethodIsReadOnly(method);
		}
		public static int GetCallingConvention(this IMethodSymbol method) {
			return method != null ? NonPublicOrFutureAccessors.GetMethodCallingConvention(method) : 0;
		}
		public static int GetScopedKind(this IParameterSymbol parameter) {
			return parameter != null ? NonPublicOrFutureAccessors.GetParameterScopedKind(parameter) : 0;
		}
		public static INamedTypeSymbol GetNativeIntegerUnderlyingType(this INamedTypeSymbol type) {
			return type != null ? NonPublicOrFutureAccessors.GetNamedTypeNativeIntegerUnderlyingType(type) : null;
		}
		public static IMethodSymbol GetFunctionPointerTypeSignature(this ITypeSymbol symbol) {
			return symbol?.TypeKind == FunctionPointer ? NonPublicOrFutureAccessors.GetFunctionPointerTypeSignature(symbol) : null;
		}
		public static IFieldSymbol GetPropertyBackingField(this IPropertySymbol property) {
			return property != null && property.ContainingAssembly.GetSourceType() != AssemblySource.Metadata ? NonPublicOrFutureAccessors.GetPropertyBackingField(property) : null;
		}
		public static IPropertySymbol GetPropertyPartialDefinitionPart(this IPropertySymbol property) {
			return property != null ? NonPublicOrFutureAccessors.GetPropertyPartialDefinitionPart(property) : null;
		}
		public static IPropertySymbol GetPropertyPartialImplementationPart(this IPropertySymbol property) {
			return property != null ? NonPublicOrFutureAccessors.GetPropertyPartialImplementationPart(property) : null;
		}
		public static IEventSymbol GetEventPartialDefinitionPart(this IEventSymbol ev) {
			return ev != null ? NonPublicOrFutureAccessors.GetEventPartialDefinitionPart(ev) : null;
		}
		public static IEventSymbol GetEventPartialImplementationPart(this IEventSymbol ev) {
			return ev != null ? NonPublicOrFutureAccessors.GetEventPartialImplementationPart(ev) : null;
		}
		public static bool IsRequired(this IPropertySymbol property) {
			return property != null && NonPublicOrFutureAccessors.GetPropertyIsRequired(property);
		}
		public static bool AllowRefLikeType(this ITypeParameterSymbol typeParameter) {
			return typeParameter != null && NonPublicOrFutureAccessors.GetTypeParameterAllowRefLikeType(typeParameter);
		}
		public static bool HasNotNullConstraint(this ITypeParameterSymbol typeParameter) {
			return typeParameter != null && NonPublicOrFutureAccessors.GetTypeParameterHasNotNullConstraint(typeParameter);
		}
		public static AssemblySource GetSourceType(this IAssemblySymbol assembly) {
			return assembly is null
				? AssemblySource.Metadata
				: (AssemblySource)NonPublicOrFutureAccessors.GetAssemblySourceType(assembly);
		}
		public static int GetSwitchExpressionArmsCount(this ExpressionSyntax switchExpression) {
			return switchExpression.IsKind(SwitchExpression) ? NonPublicOrFutureAccessors.GetSwitchExpressionArmsCount(switchExpression) : 0;
		}
		public static int GetPropertyPatternSubPatternsCount(this CSharpSyntaxNode propertyPatternClause) {
			return propertyPatternClause.IsKind(PropertyPatternClause) ? NonPublicOrFutureAccessors.GetPropertyPatternSubPatternsCount(propertyPatternClause) : 0;
		}
		public static int GetCollectionExpressionElementsCount(this ExpressionSyntax collectionExpression) {
			return collectionExpression.IsKind(CollectionExpression) ? NonPublicOrFutureAccessors.GetCollectionExpressionElementsCount(collectionExpression) : 0;
		}
		public static int GetListPatternsCount(this PatternSyntax listPattern) {
			return listPattern.IsKind(ListPatternExpression) ? NonPublicOrFutureAccessors.GetListPatternsCount(listPattern) : 0;
		}
		#endregion

		public static Task<Project> GetProjectAsync(this ISymbol symbol, Solution solution, CancellationToken cancellationToken = default) {
			var asm = symbol.ContainingAssembly;
			return asm == null
				? Task.FromResult<Project>(null)
				: GetProjectAsync(asm, solution, cancellationToken);

			async Task<Project> GetProjectAsync(IAssemblySymbol a, Solution s, CancellationToken ct) {
				foreach (var item in s.Projects) {
					if (item.SupportsCompilation
						&& (await item.GetCompilationAsync(ct).ConfigureAwait(false)).Assembly.OriginallyEquals(a)) {
						return item;
					}
				}
				return null;
			}
		}
		#endregion

		#region Enum
		public static IEnumerable<IFieldSymbol> GetFlaggedEnumFields(this INamedTypeSymbol enumType, object value) {
			switch (enumType?.EnumUnderlyingType.SpecialType) {
				case SpecialType.System_SByte: return new EnumFieldMatcher<sbyte>(value).GetMatchedFieldSymbols(enumType);
				case SpecialType.System_Byte: return new EnumFieldMatcher<byte>(value).GetMatchedFieldSymbols(enumType);
				case SpecialType.System_Int16: return new EnumFieldMatcher<short>(value).GetMatchedFieldSymbols(enumType);
				case SpecialType.System_UInt16: return new EnumFieldMatcher<ushort>(value).GetMatchedFieldSymbols(enumType);
				case SpecialType.System_Int32: return new EnumFieldMatcher<int>(value).GetMatchedFieldSymbols(enumType);
				case SpecialType.System_UInt32: return new EnumFieldMatcher<uint>(value).GetMatchedFieldSymbols(enumType);
				case SpecialType.System_Int64: return new EnumFieldMatcher<long>(value).GetMatchedFieldSymbols(enumType);
				case SpecialType.System_UInt64: return new EnumFieldMatcher<ulong>(value).GetMatchedFieldSymbols(enumType);
				default: return Enumerable.Empty<IFieldSymbol>();
			}
		}

		sealed class EnumFieldMatcher<T>
			where T : struct, IComparable, IConvertible
		{
			readonly T _Value;

			public EnumFieldMatcher(object value) {
				_Value = Op.Unbox<T>(value);
			}

			public IEnumerable<IFieldSymbol> GetMatchedFieldSymbols(INamedTypeSymbol type) {
				foreach (var item in type.GetMembers()) {
					if (item is IFieldSymbol f
						&& f.HasConstantValue
						&& f.ConstantValue is T c
						&& Op.IsTrue(c)
						&& Op.Ceq(Op.And(c, _Value), c)) {
						yield return f;
					}
				}
			}
		}
		#endregion

		#region Source
		public static bool HasSource(this ISymbol symbol) {
			return symbol.Kind == SymbolKind.Namespace
				? ((INamespaceSymbol)symbol).ConstituentNamespaces.Any(n => n.ContainingAssembly.GetSourceType() != AssemblySource.Metadata)
				: symbol.ContainingAssembly.GetSourceType() != AssemblySource.Metadata;
		}

		public static SyntaxNode GetSyntaxNode(this ISymbol symbol, CancellationToken cancellationToken = default) {
			var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault()
				?? (symbol.IsImplicitlyDeclared && symbol.ContainingSymbol != null
					? symbol.ContainingSymbol?.DeclaringSyntaxReferences.FirstOrDefault()
					: null);
			return syntaxReference?.GetSyntax(cancellationToken);
		}

		public static ImmutableArray<SyntaxReference> GetSourceReferences(this ISymbol symbol) {
			var source = symbol.DeclaringSyntaxReferences;
			if (symbol is IMethodSymbol m) {
				AddDefinitionsForPartial(m, i => i.PartialDefinitionPart, ref source);
				AddDefinitionsForPartial(m, i => i.PartialImplementationPart, ref source);
				if (m.MethodKind == MethodKind.Constructor
					&& source.Length == 0
					&& (symbol = m.ContainingType) != null) {
					source = source.AddRange(symbol.DeclaringSyntaxReferences);
				}
			}
			else if (symbol is IPropertySymbol p) {
				AddDefinitionsForPartial(p, GetPropertyPartialDefinitionPart, ref source);
				AddDefinitionsForPartial(p, GetPropertyPartialImplementationPart, ref source);
			}
			else if (symbol is IEventSymbol e) {
                AddDefinitionsForPartial(e, GetEventPartialDefinitionPart, ref source);
                AddDefinitionsForPartial(e, GetEventPartialImplementationPart, ref source);
			}
			return source;
		}

		static void AddDefinitionsForPartial<TSymbol>(TSymbol symbol, Func<TSymbol, TSymbol> partialGetter, ref ImmutableArray<SyntaxReference> locations) where TSymbol : ISymbol {
			var s = partialGetter(symbol);
			if (s != null) {
				locations = locations.AddRange(s.DeclaringSyntaxReferences);
			}
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
				TextEditorHelper.OpenFile(loc.SourceTree.FilePath, pos.Line, pos.Character);
			}
		}

		public static void GoToSource(this SyntaxReference loc) {
			if (loc != null) {
				var pos = loc.SyntaxTree.GetLineSpan(loc.Span).StartLinePosition;
				TextEditorHelper.OpenFile(loc.SyntaxTree.FilePath, pos.Line, pos.Character);
			}
		}

		public static void GoToDefinition(this ISymbol symbol) {
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			var r = symbol.GetSourceReferences();
			if (r.Length == 1) {
				r[0].GoToSource();
			}
			else {
				var ctx = SemanticContext.GetHovered();
				if (ctx != null) {
					if (r.Length == 0
						&& ctx.Document != null
						&& ServicesHelper.Instance.VisualStudioWorkspace.TryGoToDefinition(symbol, ctx.Document.Project, default)) {
						return;
					}
					Controls.SymbolCommands.ShowLocations(ctx, symbol, r);
				}
			}
		}

		public static bool IsAccessible(this ISymbol symbol, bool checkContainingType) {
			return symbol != null
				&& (symbol.DeclaredAccessibility.CeqAny(Accessibility.Public, Accessibility.Protected, Accessibility.ProtectedOrInternal)
					|| symbol.ContainingAssembly.GetSourceType() != AssemblySource.Metadata)
				&& (checkContainingType == false || symbol.ContainingType?.IsAccessible(true) != false);
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
				var tp = target as ITypeParameterSymbol;
				foreach (var item in tp.ConstraintTypes) {
					if (item.CanConvertTo(symbol)) {
						return true;
					}
				}
				return false;
			}
			if (symbol.TypeKind == TypeKind.TypeParameter) {
				var tp = symbol as ITypeParameterSymbol;
				foreach (var item in tp.ConstraintTypes) {
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

		/// <summary>
		/// <para>Matches two types when the following aspects being equal.</para>
		/// <list type="number">
		/// <item>TypeKind</item>
		/// <item>DeclaredAccessibility</item>
		/// <item>Name</item>
		/// <item>Arity</item>
		/// <item>ContainingType</item>
		/// <item>IsGenericType and if true: TypeArguments also</item>
		/// <item>ContainingNamespace</item>
		/// </list>
		/// </summary>
		public static bool MatchWith(this INamedTypeSymbol a, INamedTypeSymbol b) {
			// todo unwrap alias
			return Op.Ceq(a, b) ||
				(a != null && b != null
					&& a.TypeKind == b.TypeKind
					&& a.Name == b.Name
					&& a.Arity == b.Arity
					&& a.ContainingType.MatchWith(b.ContainingType)
					&& (a.IsGenericType == b.IsGenericType || HasSameTypeArguments(a, b))
					&& HasSameName(a.ContainingNamespace, b.ContainingNamespace)
				);
		}

		public static bool HasSameTypeArguments(this INamedTypeSymbol typeA, INamedTypeSymbol typeB) {
			var ta = typeA.TypeArguments;
			var tb = typeB.TypeArguments;
			int length;
			ITypeSymbol aa, ab;
			if ((length = ta.Length) != tb.Length) {
				return false;
			}
			for (int i = 0; i < length; i++) {
				if ((aa = ta[i]).TypeKind != (ab = tb[i]).TypeKind
					|| (aa is INamedTypeSymbol na && ab is INamedTypeSymbol nb && na.MatchWith(nb) == false)) {
					return false;
				}
			}
			return true;
		}

		public static int GetHashCodeForMatch(this INamedTypeSymbol symbol) {
			return unchecked(((int)symbol.TypeKind * 500069)
				^ ((int)symbol.DeclaredAccessibility * 7329097)
				^ symbol.Name.GetHashCode()
				^ (symbol.Arity * 2081236337)
				^ (symbol.ContainingType?.GetHashCodeForMatch() ?? 192901013)
				^ (symbol.ContainingNamespace.Name.GetHashCode() * 500069));
		}

		public static bool OriginallyEquals(this ISymbol a, ISymbol b) {
			return a is null
				? b is null
				: b != null && a.OriginalDefinition.Equals(b.OriginalDefinition);
		}

		public static int CompareSymbol<TSymbol>(TSymbol a, TSymbol b) where TSymbol : ISymbol {
			if (Op.Ceq(a, b)) {
				return 0;
			}
			if (a is null) {
				return -1;
			}
			if (b is null) {
				return 1;
			}
			int d;
			if (Op.IsTrue(d = b.ContainingAssembly.GetSourceType() - a.ContainingAssembly.GetSourceType())) {
				return d;
			}
			INamedTypeSymbol ta = a.ContainingType, tb = b.ContainingType;
			if (!Op.Ceq(ta, tb)) {
				if (ta is null) {
					return -1;
				}
				if (tb is null) {
					return 1;
				}
				if (Op.IsTrue(d = CompareByAccessibilityKindName(ta, tb))
					|| Op.IsTrue(d = ta.TypeKind - tb.TypeKind)) {
					return d;
				}
			}
			return CompareByAccessibilityKindName(a, b);
		}

		public static bool HasSameName(ISymbol a, ISymbol b) {
			if (Op.Ceq(a, b)) {
				return true;
			}
			if (a.Kind != b.Kind || a.Name != b.Name) {
				return false;
			}
			switch (a.Kind) {
				case SymbolKind.NamedType:
					if (((INamedTypeSymbol)a).Arity != ((INamedTypeSymbol)b).Arity) {
						return false;
					}
					break;
				case SymbolKind.Namespace:
					if (((INamespaceSymbol)a).IsGlobalNamespace && ((INamespaceSymbol)b).IsGlobalNamespace) {
						return true;
					}
					break;
				case SymbolKind.Method:
					if (((IMethodSymbol)a).Arity != ((IMethodSymbol)b).Arity) {
						return false;
					}
					break;
			}
			return ((a = a.ContainingSymbol) != null ^ (b = b.ContainingSymbol) != null) == false
				|| a == null
				|| HasSameName(a, b);
		}

		public static bool AreEqual(ITypeSymbol a, ITypeSymbol b, bool ignoreTypeConstraint) {
			if (Op.Ceq(a, b)) {
				return true;
			}
			if (a == null || b == null || a.IsRefLike() != b.IsRefLike()) {
				return false;
			}
			if (a.Equals(b)) {
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
				&& Op.Ceq(ta.OriginalDefinition, tb.OriginalDefinition)) {
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
			if (Op.Ceq(a, b)) {
				return true;
			}
			if (a == null || b == null
				|| a.HasReferenceTypeConstraint != b.HasReferenceTypeConstraint
				|| a.HasValueTypeConstraint != b.HasValueTypeConstraint
				|| a.HasConstructorConstraint != b.HasConstructorConstraint
				|| a.HasUnmanagedTypeConstraint != b.HasUnmanagedTypeConstraint
				|| a.HasNotNullConstraint() != b.HasNotNullConstraint()) {
				return false;
			}
			var ac = a.ConstraintTypes;
			var bc = b.ConstraintTypes;
			if (ac.Length != bc.Length) {
				return false;
			}
			for (int i = ac.Length - 1; i >= 0; i--) {
				if (AreEqual(ac[i], bc[i], true) == false) {
					return false;
				}
			}
			return true;
		}

		public static int CompareByAccessibilityKindName(ISymbol a, ISymbol b) {
			int s;
			if (Op.IsTrue(s = b.DeclaredAccessibility - a.DeclaredAccessibility) // sort by visibility
				|| Op.IsTrue(s = GetSymbolKindSortOrder(a.Kind) - GetSymbolKindSortOrder(b.Kind)) // then by symbol kind
				|| Op.IsTrue(s = a.Name.CompareTo(b.Name))) { // then by name
				return s;
			}
			switch (a.Kind) {
				case SymbolKind.NamedType: return ((INamedTypeSymbol)a).Arity.CompareTo(((INamedTypeSymbol)b).Arity);
				case SymbolKind.Method: return ((IMethodSymbol)a).Arity.CompareTo(((IMethodSymbol)b).Arity);
				default: return 0;
			}

			int GetSymbolKindSortOrder(SymbolKind x) {
				switch (x) {
					case SymbolKind.Assembly: return -1;
					case SymbolKind.NetModule: return 0;
					case SymbolKind.Namespace: return 1;
					case SymbolKind.NamedType: return 2;
					case SymbolKind.ArrayType: return 3;
					case SymbolKind.PointerType:
					case FunctionPointerType:
						return 4;
					case SymbolKind.DynamicType: return 5;
					case SymbolKind.Field: return 6;
					case SymbolKind.Property: return 7;
					case SymbolKind.Event: return 8;
					case SymbolKind.Method: return 9;
					case SymbolKind.TypeParameter: return 10;
					case SymbolKind.Parameter: return 11;
					case SymbolKind.Discard: return 12;
					case SymbolKind.Local: return 13;
					case SymbolKind.RangeVariable: return 14;
					case SymbolKind.Label: return 15;
					case SymbolKind.Preprocessing: return 16;
					case SymbolKind.Alias: return 17;
					case SymbolKind.ErrorType: return 18;
					default: return 19;
				}
			}
		}

		public static bool IsBoundedGenericType(this INamedTypeSymbol type) {
			return type?.IsGenericType == true
				&& type.IsUnboundGenericType == false
				&& type != type.OriginalDefinition;
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
				|| returnType?.CanConvertTo(retType) == false) {
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
			return typeParameters.IsDefaultOrEmpty
				|| method.TypeParameters.Length == typeParameters.Length;
		}

		/// <summary>Returns whether a symbol could have an override.</summary>
		public static bool MayHaveOverride(this ISymbol symbol) {
			return symbol?.ContainingType?.TypeKind == TypeKind.Class &&
				   (symbol.IsVirtual || symbol.IsAbstract || symbol.IsOverride) &&
				   symbol.IsSealed == false;
		}

		public static IEqualityComparer<ISymbol> GetSymbolNameComparer() {
			return Comparers.SymbolNameComparer;
		}
		public static Func<ISymbol, bool> GetSpecificSymbolComparer(ISymbol symbol) {
			return new SpecificSymbolEqualityComparer(symbol).Equals;
		}
		public static IEqualityComparer<INamedTypeSymbol> GetNamedTypeComparer() {
			return Comparers.NamedTypeComparer;
		}
		#endregion

		/// <summary>
		/// This type contains dynamic methods to access properties that are non-public or in later Roslyn versions (after VS 2017)
		/// </summary>
		static partial class NonPublicOrFutureAccessors
		{
			public static readonly Func<ITypeSymbol, bool> GetNamedTypeIsReadOnly =
				typeof(ITypeSymbol).GetProperty("IsReadOnly") != null
				? ReflectionHelper.CreateGetPropertyMethod<ITypeSymbol, bool>("IsReadOnly")
				: ReflectionHelper.CreateGetPropertyMethod<ITypeSymbol, bool>("IsReadOnly", typeof(CSharpCompilation).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.Symbols.NamedTypeSymbol", false));

			public static readonly Func<ITypeSymbol, bool> GetNamedTypeIsRefLikeType =
				typeof(ITypeSymbol).GetProperty("IsRefLikeType") != null
				? ReflectionHelper.CreateGetPropertyMethod<ITypeSymbol, bool>("IsRefLikeType")
				: ReflectionHelper.CreateGetPropertyMethod<ITypeSymbol, bool>("IsRefLikeType", typeof(CSharpCompilation).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.Symbols.NamedTypeSymbol", false));

			public static readonly Func<INamedTypeSymbol, bool> GetNamedTypeIsFileLocal = ReflectionHelper.CreateGetPropertyMethod<INamedTypeSymbol, bool>("IsFileLocal");

			public static readonly Func<ITypeSymbol, bool> GetTypeIsRecord = ReflectionHelper.CreateGetPropertyMethod<ITypeSymbol, bool>("IsRecord");

			public static readonly Func<ITypeSymbol, bool> GetTypeIsNativeIntegerType = ReflectionHelper.CreateGetPropertyMethod<ITypeSymbol, bool>("IsNativeIntegerType");

			public static readonly Func<ITypeSymbol, int> GetTypeNullableAnnotation = ReflectionHelper.CreateGetPropertyMethod<ITypeSymbol, int>("NullableAnnotation");

			public static readonly Func<ITypeSymbol, IParameterSymbol> GetTypeExtensionParameter = ReflectionHelper.CreateGetPropertyMethod<ITypeSymbol, IParameterSymbol>("ExtensionParameter");

			public static readonly Func<INamedTypeSymbol, INamedTypeSymbol> GetNamedTypeNativeIntegerUnderlyingType = ReflectionHelper.CreateGetPropertyMethod<INamedTypeSymbol, INamedTypeSymbol>("NativeIntegerUnderlyingType");

			public static readonly Func<ILocalSymbol, bool> GetLocalIsForEach = ReflectionHelper.CreateGetPropertyMethod<ILocalSymbol, bool>("IsForEach");

			public static readonly Func<ILocalSymbol, bool> GetLocalIsUsing = ReflectionHelper.CreateGetPropertyMethod<ILocalSymbol, bool>("IsUsing");

			public static readonly Func<ILocalSymbol, int> GetLocalNullableAnnotation = ReflectionHelper.CreateGetPropertyMethod<ILocalSymbol, int>("NullableAnnotation");

			public static readonly Func<ILocalSymbol, int> GetLocalScopedKind = ReflectionHelper.CreateGetPropertyMethod<ILocalSymbol, int>("ScopedKind");

			public static readonly Func<IMethodSymbol, bool> GetMethodIsInitOnly = ReflectionHelper.CreateGetPropertyMethod<IMethodSymbol, bool>("IsInitOnly");

			public static readonly Func<IMethodSymbol, bool> GetMethodIsReadOnly = ReflectionHelper.CreateGetPropertyMethod<IMethodSymbol, bool>("IsReadOnly");

			public static readonly Func<IMethodSymbol, int> GetMethodCallingConvention = ReflectionHelper.CreateGetPropertyMethod<IMethodSymbol, int>("CallingConvention");

			public static readonly Func<IEventSymbol, IEventSymbol> GetEventPartialDefinitionPart = ReflectionHelper.CreateGetPropertyMethod<IEventSymbol, IEventSymbol>("PartialDefinitionPart");

			public static readonly Func<IEventSymbol, IEventSymbol> GetEventPartialImplementationPart = ReflectionHelper.CreateGetPropertyMethod<IEventSymbol, IEventSymbol>("PartialImplementationPart");

			public static readonly Func<IPropertySymbol, IPropertySymbol> GetPropertyPartialDefinitionPart = ReflectionHelper.CreateGetPropertyMethod<IPropertySymbol, IPropertySymbol>("PartialDefinitionPart");

			public static readonly Func<IPropertySymbol, IPropertySymbol> GetPropertyPartialImplementationPart = ReflectionHelper.CreateGetPropertyMethod<IPropertySymbol, IPropertySymbol>("PartialImplementationPart");

			public static readonly Func<IParameterSymbol, int> GetParameterScopedKind = ReflectionHelper.CreateGetPropertyMethod<IParameterSymbol, int>("ScopedKind");

			public static readonly Func<ITypeParameterSymbol, bool> GetTypeParameterAllowRefLikeType = ReflectionHelper.CreateGetPropertyMethod<ITypeParameterSymbol, bool>("AllowsRefLikeType");

			public static readonly Func<ITypeParameterSymbol, bool> GetTypeParameterHasNotNullConstraint = ReflectionHelper.CreateGetPropertyMethod<ITypeParameterSymbol, bool>("HasNotNullConstraint");

			public static readonly Func<ITypeSymbol, IMethodSymbol> GetFunctionPointerTypeSignature = ReflectionHelper.CreateGetPropertyMethod<ITypeSymbol, IMethodSymbol>("Signature", typeof(ITypeSymbol).Assembly.GetType("Microsoft.CodeAnalysis.IFunctionPointerTypeSymbol"));

			public static readonly Func<IPropertySymbol, IFieldSymbol> GetPropertyBackingField = ReflectionHelper.CreateGetPropertyMethod<IPropertySymbol, IFieldSymbol>("BackingField", typeof(CSharpCompilation).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.Symbols.SourcePropertySymbol"));

			public static readonly Func<IPropertySymbol, bool> GetPropertyIsRequired = ReflectionHelper.CreateGetPropertyMethod<IPropertySymbol, bool>("IsRequired");

			public static readonly Func<IAssemblySymbol, int> GetAssemblySourceType = CreateAssemblySourceTypeFunc();
			static Func<IAssemblySymbol, int> CreateAssemblySourceTypeFunc() {
				var m = new DynamicMethod("GetAssemblySourceType", typeof(int), new Type[] { typeof(IAssemblySymbol) }, true);
				var il = m.GetILGenerator();
				var isSource = il.DefineLabel();
				var isRetargetSource = il.DefineLabel();
				Label notAssemblySymbol, getUnderlyingAssemblySymbol;
				var a = typeof(CSharpCompilation).Assembly;
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
					notAssemblySymbol = il.DefineLabel();
					getUnderlyingAssemblySymbol = il.DefineLabel();
					// (asm as AssemblySymbol)?.UnderlyingAssemblySymbol is RetargetingAssemblySymbol
					il.Emit(OpCodes.Ldarg_0);
					il.Emit(OpCodes.Isinst, s);
					#region Workaround for https://github.com/wmjordan/Codist/issues/138
					il.Emit(OpCodes.Dup /* AssemblySymbol */);
					il.Emit(OpCodes.Brtrue_S, getUnderlyingAssemblySymbol);
					il.Emit(OpCodes.Pop /* AssemblySymbol */);
					il.Emit(OpCodes.Br_S, notAssemblySymbol);
					il.MarkLabel(getUnderlyingAssemblySymbol);
					#endregion
					il.Emit(OpCodes.Callvirt, ua.GetGetMethod(true));
					il.Emit(OpCodes.Isinst, tr);
					il.Emit(OpCodes.Brtrue_S, isRetargetSource);
					il.MarkLabel(notAssemblySymbol);
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
				return m.CreateDelegate<Func<IAssemblySymbol, int>>();
			}

			public static readonly Func<ExpressionSyntax, int> GetSwitchExpressionArmsCount = CreateSyntaxListCountFunc<ExpressionSyntax>("GetSwitchExpressionArmsCount", "Microsoft.CodeAnalysis.CSharp.Syntax.SwitchExpressionSyntax", "Arms");
			public static readonly Func<CSharpSyntaxNode, int> GetPropertyPatternSubPatternsCount = CreateSyntaxListCountFunc<CSharpSyntaxNode>("GetPropertyPatternClauseSubPatternsCount", "Microsoft.CodeAnalysis.CSharp.Syntax.PropertyPatternClauseSyntax", "Subpatterns");
			public static readonly Func<ExpressionSyntax, int> GetCollectionExpressionElementsCount = CreateSyntaxListCountFunc<CSharpSyntaxNode>("GetCollectionExpressionElementsCount", "Microsoft.CodeAnalysis.CSharp.Syntax.CollectionExpressionSyntax", "Elements");
			public static readonly Func<PatternSyntax, int> GetListPatternsCount = CreateSyntaxListCountFunc<CSharpSyntaxNode>("GetListPatternsCount", "Microsoft.CodeAnalysis.CSharp.Syntax.ListPatternSyntax", "Patterns");

			static Func<TNode, int> CreateSyntaxListCountFunc<TNode>(string methodName, string type, string listPropertyName) where TNode : SyntaxNode {
				var m = new DynamicMethod(methodName, typeof(int), new Type[] { typeof(TNode) }, true);
				var il = m.GetILGenerator();
				var asm = typeof(TNode).Assembly;
				var t = asm.GetType(type);
				if (t is null) {
					goto RETURN_0;
				}
				var list = t.GetProperty(listPropertyName);
				if (list is null) {
					goto RETURN_0;
				}
				var count = list.PropertyType.GetProperty("Count");
				if (count is null) {
					goto RETURN_0;
				}
				var isNotExpression = il.DefineLabel();
				var temp = il.DeclareLocal(list.PropertyType);

				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Isinst, t);
				il.Emit(OpCodes.Dup);
				il.Emit(OpCodes.Brfalse_S, isNotExpression);

				il.Emit(OpCodes.Callvirt, list.GetGetMethod());
				il.Emit(OpCodes.Stloc_S, temp);
				il.Emit(OpCodes.Ldloca_S, temp);
				il.Emit(OpCodes.Call, count.GetGetMethod());
				il.Emit(OpCodes.Ret);

				il.MarkLabel(isNotExpression);
				il.Emit(OpCodes.Pop);
			RETURN_0:
				il.Emit(OpCodes.Ldc_I4_0);
				il.Emit(OpCodes.Ret);

				return m.CreateDelegate<Func<TNode, int>>();
			}
		}

		sealed class SpecificSymbolEqualityComparer : IEquatable<ISymbol>
		{
			readonly ISymbol _Specified;

			public SpecificSymbolEqualityComparer(ISymbol specified) {
				_Specified = specified;
			}

			public bool Equals(ISymbol other) {
				return HasSameName(_Specified, other);
			}
		}
		static partial class Comparers
		{
			internal static readonly GenericEqualityComparer<ISymbol> SymbolNameComparer = new GenericEqualityComparer<ISymbol>(HasSameName, o => o.Name.GetHashCode());

			internal static readonly GenericEqualityComparer<INamedTypeSymbol> NamedTypeComparer = new GenericEqualityComparer<INamedTypeSymbol>(MatchWith, o => o.GetHashCodeForMatch());
		}
	}
}
