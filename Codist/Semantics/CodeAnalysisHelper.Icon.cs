using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Imaging;

namespace Codist;

partial class CodeAnalysisHelper
{
	#region Symbol icon
	public static int GetImageId(this ISymbol symbol) {
		return symbol.Kind switch {
			SymbolKind.Assembly => KnownImageIds.Assembly,
			SymbolKind.DynamicType => KnownImageIds.StatusHelp,
			SymbolKind.Event => GetEventImageId((IEventSymbol)symbol),
			SymbolKind.Field => GetFieldImageId((IFieldSymbol)symbol),
			SymbolKind.Label => KnownImageIds.Label,
			SymbolKind.Local => IconIds.LocalVariable,
			SymbolKind.Method => GetMethodImageId((IMethodSymbol)symbol),
			SymbolKind.NamedType => GetTypeImageId((INamedTypeSymbol)symbol),
			SymbolKind.Namespace => IconIds.Namespace,
			SymbolKind.Parameter => IconIds.Argument,
			SymbolKind.Property => GetPropertyImageId((IPropertySymbol)symbol),
			(SymbolKind)SymbolKindEx.FunctionPointerType => IconIds.FunctionPointer,
			SymbolKind.Discard => IconIds.Discard,
			_ => KnownImageIds.Item,
		};
		int GetEventImageId(IEventSymbol ev) {
			return ev.DeclaredAccessibility switch {
				Accessibility.Public => KnownImageIds.EventPublic,
				Accessibility.Protected
					or Accessibility.ProtectedOrInternal
					or Accessibility.ProtectedAndInternal => KnownImageIds.EventProtected,
				Accessibility.Private => ev.ExplicitInterfaceImplementations.Length != 0
						? IconIds.ExplicitInterfaceEvent
						: KnownImageIds.EventPrivate,
				Accessibility.Internal => KnownImageIds.EventInternal,
				_ => IconIds.Event,
			};
		}

		int GetFieldImageId(IFieldSymbol f) {
			if (f.IsConst) {
				if (f.ContainingType.TypeKind == TypeKind.Enum) {
					return IconIds.EnumField;
				}
				return f.DeclaredAccessibility switch {
					Accessibility.Public => KnownImageIds.ConstantPublic,
					Accessibility.Protected
						or Accessibility.ProtectedOrInternal
						or Accessibility.ProtectedAndInternal => KnownImageIds.ConstantProtected,
					Accessibility.Private => KnownImageIds.ConstantPrivate,
					Accessibility.Internal => KnownImageIds.ConstantInternal,
					_ => KnownImageIds.Constant,
				};
			}
			return f.DeclaredAccessibility switch {
				Accessibility.Public => KnownImageIds.FieldPublic,
				Accessibility.Protected
					or Accessibility.ProtectedOrInternal
					or Accessibility.ProtectedAndInternal => KnownImageIds.FieldProtected,
				Accessibility.Private => KnownImageIds.FieldPrivate,
				Accessibility.Internal => KnownImageIds.FieldInternal,
				_ => IconIds.Field,
			};
		}

		int GetMethodImageId(IMethodSymbol m) {
			return m.MethodKind switch {
				MethodKind.Constructor or MethodKind.StaticConstructor => m.DeclaredAccessibility switch {
					Accessibility.Public => IconIds.PublicConstructor,
					Accessibility.Protected
						or Accessibility.ProtectedOrInternal
						or Accessibility.ProtectedAndInternal => IconIds.ProtectedConstructor,
					Accessibility.Private => IconIds.PrivateConstructor,
					Accessibility.Internal => IconIds.InternalConstructor,
					_ => IconIds.Constructor,
				},
				MethodKind.Destructor => IconIds.Destructor,
				MethodKind.UserDefinedOperator => m.DeclaredAccessibility switch {
					Accessibility.Public => KnownImageIds.OperatorPublic,
					Accessibility.Protected
						or Accessibility.ProtectedOrInternal
						or Accessibility.ProtectedAndInternal => KnownImageIds.OperatorProtected,
					Accessibility.Private => KnownImageIds.OperatorPrivate,
					Accessibility.Internal => KnownImageIds.OperatorInternal,
					_ => KnownImageIds.Operator,
				},
				MethodKind.Conversion => m.MetadataName == "op_Explicit"
					? IconIds.ExplicitConversion
					: IconIds.ImplicitConversion,
				_ => m.DeclaredAccessibility switch {
					Accessibility.Public => KnownImageIds.MethodPublic,
					Accessibility.Protected
						or Accessibility.ProtectedOrInternal
						or Accessibility.ProtectedAndInternal => KnownImageIds.MethodProtected,
					Accessibility.Private => m.ExplicitInterfaceImplementations.Length != 0
							? IconIds.ExplicitInterfaceMethod
							: KnownImageIds.MethodPrivate,
					Accessibility.Internal => KnownImageIds.MethodInternal,
					_ => IconIds.Method,
				},
			};
		}

		int GetTypeImageId(INamedTypeSymbol t) {
			switch (t.TypeKind) {
				case TypeKind.Class:
					return t.DeclaredAccessibility switch {
						Accessibility.Public => KnownImageIds.ClassPublic,
						Accessibility.Protected
							or Accessibility.ProtectedOrInternal
							or Accessibility.ProtectedAndInternal => KnownImageIds.ClassProtected,
						Accessibility.Private => KnownImageIds.ClassPrivate,
						Accessibility.Internal => KnownImageIds.ClassInternal,
						_ => IconIds.Class,
					};
				case TypeKind.Delegate:
					return t.DeclaredAccessibility switch {
						Accessibility.Public => KnownImageIds.DelegatePublic,
						Accessibility.Protected
							or Accessibility.ProtectedOrInternal
							or Accessibility.ProtectedAndInternal => KnownImageIds.DelegateProtected,
						Accessibility.Private => KnownImageIds.DelegatePrivate,
						Accessibility.Internal => KnownImageIds.DelegateInternal,
						_ => IconIds.Delegate,
					};
				case TypeKind.Enum:
					return t.DeclaredAccessibility switch {
						Accessibility.Public => KnownImageIds.EnumerationPublic,
						Accessibility.Protected
							or Accessibility.ProtectedOrInternal
							or Accessibility.ProtectedAndInternal => KnownImageIds.EnumerationProtected,
						Accessibility.Private => KnownImageIds.EnumerationPrivate,
						Accessibility.Internal => KnownImageIds.EnumerationInternal,
						_ => IconIds.Enum,
					};
				case TypeKind.Interface:
					return t.DeclaredAccessibility switch {
						Accessibility.Public => t.IsDisposable() || t.IsAsyncDisposable()
								? IconIds.Disposable
								: KnownImageIds.InterfacePublic,
						Accessibility.Protected
							or Accessibility.ProtectedOrInternal
							or Accessibility.ProtectedAndInternal => KnownImageIds.InterfaceProtected,
						Accessibility.Private => KnownImageIds.InterfacePrivate,
						Accessibility.Internal => KnownImageIds.InterfaceInternal,
						_ => IconIds.Interface,
					};
				case TypeKind.Struct:
					if (t.IsUnion()) {
						return t.DeclaredAccessibility switch {
							Accessibility.Public => KnownImageIds.UnionPublic,
							Accessibility.Protected
								or Accessibility.ProtectedOrInternal
								or Accessibility.ProtectedAndInternal => KnownImageIds.UnionProtected,
							Accessibility.Private => KnownImageIds.UnionPrivate,
							Accessibility.Internal => KnownImageIds.UnionInternal,
							_ => IconIds.Union,
						};
					}
					return t.DeclaredAccessibility switch {
						Accessibility.Public => KnownImageIds.StructurePublic,
						Accessibility.Protected
							or Accessibility.ProtectedOrInternal
							or Accessibility.ProtectedAndInternal => KnownImageIds.StructureProtected,
						Accessibility.Private => KnownImageIds.StructurePrivate,
						Accessibility.Internal => KnownImageIds.StructureInternal,
						_ => IconIds.Structure,
					};
				case (TypeKind)TypeKindEx.Extension:
					return IconIds.ExtensionDeclaration;
				case TypeKind.TypeParameter:
				default: return KnownImageIds.Type;
			}
		}

		int GetPropertyImageId(IPropertySymbol p) {
			return p.DeclaredAccessibility switch {
				Accessibility.Public => KnownImageIds.PropertyPublic,
				Accessibility.Protected
					or Accessibility.ProtectedOrInternal
					or Accessibility.ProtectedAndInternal => KnownImageIds.PropertyProtected,
				Accessibility.Private => p.ExplicitInterfaceImplementations.Length != 0
						? IconIds.ExplicitInterfaceProperty
						: KnownImageIds.PropertyPrivate,
				Accessibility.Internal => KnownImageIds.PropertyInternal,
				_ => KnownImageIds.Property,
			};
		}
	}
	#endregion

	#region Node icon
	public static int GetImageId(this SyntaxNode node) {
		return node.Kind() switch {
			SyntaxKind.ClassDeclaration
				or (SyntaxKind)SyntaxKindEx.RecordDeclaration => GetClassIcon((BaseTypeDeclarationSyntax)node),
			SyntaxKind.EnumDeclaration => GetEnumIcon((EnumDeclarationSyntax)node),
			SyntaxKind.StructDeclaration
				or (SyntaxKind)SyntaxKindEx.RecordStructDeclaration
				or (SyntaxKind)SyntaxKindEx.UnionDeclaration => GetStructIcon((BaseTypeDeclarationSyntax)node),
			SyntaxKind.InterfaceDeclaration => GetInterfaceIcon((InterfaceDeclarationSyntax)node),
			SyntaxKind.MethodDeclaration => GetMethodIcon((MethodDeclarationSyntax)node),
			SyntaxKind.ConstructorDeclaration => GetConstructorIcon((ConstructorDeclarationSyntax)node),
			SyntaxKind.PropertyDeclaration => GetPropertyIconExt((BasePropertyDeclarationSyntax)node),
			SyntaxKind.IndexerDeclaration => GetPropertyIcon((BasePropertyDeclarationSyntax)node),
			SyntaxKind.OperatorDeclaration => GetOperatorIcon((OperatorDeclarationSyntax)node),
			SyntaxKind.ConversionOperatorDeclaration => GetConversionIcon((ConversionOperatorDeclarationSyntax)node),
			SyntaxKind.FieldDeclaration => GetFieldIcon((FieldDeclarationSyntax)node),
			SyntaxKind.EnumMemberDeclaration => IconIds.EnumField,
			SyntaxKind.VariableDeclarator => node.Parent.Parent.GetImageId(),
			SyntaxKind.VariableDeclaration
				or SyntaxKind.LocalDeclarationStatement => IconIds.LocalVariable,
			SyntaxKind.NamespaceDeclaration
				or (SyntaxKind)SyntaxKindEx.FileScopedNamespaceDeclaration => IconIds.Namespace,
			SyntaxKind.ArgumentList
				or SyntaxKind.AttributeArgumentList => IconIds.Argument,
			SyntaxKind.DoStatement => IconIds.DoWhile,
			SyntaxKind.FixedStatement => IconIds.Pin,
			SyntaxKind.ForEachStatement => IconIds.ForEach,
			SyntaxKind.ForStatement => IconIds.For,
			SyntaxKind.IfStatement => IconIds.If,
			SyntaxKind.LockStatement => KnownImageIds.Lock,
			SyntaxKind.SwitchStatement
				or (SyntaxKind)SyntaxKindEx.SwitchExpression => IconIds.Switch,
			SyntaxKind.SwitchSection
				or SyntaxKind.CaseSwitchLabel
				or SyntaxKind.DefaultSwitchLabel
				or (SyntaxKind)SyntaxKindEx.ConstantPattern
				or (SyntaxKind)SyntaxKindEx.CasePatternSwitchLabel
				or (SyntaxKind)SyntaxKindEx.RelationPattern
				or (SyntaxKind)SyntaxKindEx.OrPattern
				or (SyntaxKind)SyntaxKindEx.AndPattern
				or (SyntaxKind)SyntaxKindEx.NotPattern
				or (SyntaxKind)SyntaxKindEx.SlicePattern
				or (SyntaxKind)SyntaxKindEx.DiscardPattern
				or (SyntaxKind)SyntaxKindEx.TypePattern => IconIds.SwitchSection,
			SyntaxKind.TryStatement => IconIds.TryCatch,
			SyntaxKind.UsingStatement => IconIds.Using,
			SyntaxKind.WhileStatement => IconIds.While,
			SyntaxKind.ParameterList => IconIds.Argument,
			SyntaxKind.ParenthesizedExpression => IconIds.ParenthesizedExpression,
			SyntaxKind.ParenthesizedLambdaExpression
				or SyntaxKind.SimpleLambdaExpression => IconIds.LambdaExpression,
			SyntaxKind.DelegateDeclaration => GetDelegateIcon((DelegateDeclarationSyntax)node),
			SyntaxKind.EventDeclaration => GetEventIcon((BasePropertyDeclarationSyntax)node),
			SyntaxKind.EventFieldDeclaration => GetEventFieldIcon((EventFieldDeclarationSyntax)node),
			SyntaxKind.UnsafeStatement => IconIds.Unsafe,
			SyntaxKind.XmlElement
				or SyntaxKind.XmlEmptyElement => KnownImageIds.XMLElement,
			SyntaxKind.XmlComment => KnownImageIds.XMLCommentTag,
			SyntaxKind.DestructorDeclaration => IconIds.Destructor,
			SyntaxKind.UncheckedStatement => KnownImageIds.CheckBoxUnchecked,
			SyntaxKind.CheckedStatement => KnownImageIds.CheckBoxChecked,
			SyntaxKind.ReturnStatement => IconIds.Return,
			SyntaxKind.ExpressionStatement => GetExpressionIcon(((ExpressionStatementSyntax)node).Expression),
			SyntaxKind.Attribute => IconIds.Attribute,
			SyntaxKind.YieldReturnStatement => KnownImageIds.Yield,
			SyntaxKind.GotoStatement
				or SyntaxKind.GotoCaseStatement
				or SyntaxKind.GotoDefaultStatement => IconIds.GoTo,
			SyntaxKind.LocalFunctionStatement => IconIds.LocalFunction,
			SyntaxKind.RegionDirectiveTrivia => IconIds.Region,
			SyntaxKind.EndRegionDirectiveTrivia => KnownImageIds.ToolstripPanelBottom,
			(SyntaxKind)SyntaxKindEx.ExtensionDeclaration => IconIds.ExtensionDeclaration,
			_ => KnownImageIds.UnknownMember,
		};
		int GetClassIcon(BaseTypeDeclarationSyntax syntax) {
			bool isPartial = false;
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return KnownImageIds.ClassPublic;
					case SyntaxKind.ProtectedKeyword: return KnownImageIds.ClassProtected;
					case SyntaxKind.InternalKeyword: return KnownImageIds.ClassInternal;
					case SyntaxKind.PrivateKeyword: return KnownImageIds.ClassPrivate;
					case SyntaxKind.PartialKeyword: isPartial = true; break;
				}
			}
			return isPartial ? IconIds.PartialClass
				: syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.ClassInternal
				: KnownImageIds.ClassPrivate;
		}
		int GetStructIcon(BaseTypeDeclarationSyntax syntax) {
			bool isPartial = false;
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return KnownImageIds.StructurePublic;
					case SyntaxKind.ProtectedKeyword: return KnownImageIds.StructureProtected;
					case SyntaxKind.InternalKeyword: return KnownImageIds.StructureInternal;
					case SyntaxKind.PrivateKeyword: return KnownImageIds.StructurePrivate;
					case SyntaxKind.PartialKeyword: isPartial = true; break;
				}
			}
			return isPartial ? IconIds.PartialStruct
				: syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.StructureInternal
				: KnownImageIds.StructurePrivate;
		}
		int GetEnumIcon(EnumDeclarationSyntax syntax) {
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return KnownImageIds.EnumerationPublic;
					case SyntaxKind.InternalKeyword: return KnownImageIds.EnumerationInternal;
					case SyntaxKind.PrivateKeyword: return KnownImageIds.EnumerationPrivate;
				}
			}
			return syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.EnumerationInternal : KnownImageIds.EnumerationPrivate;
		}
		int GetInterfaceIcon(InterfaceDeclarationSyntax syntax) {
			bool isPartial = false;
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return KnownImageIds.InterfacePublic;
					case SyntaxKind.InternalKeyword: return KnownImageIds.InterfaceInternal;
					case SyntaxKind.PrivateKeyword: return KnownImageIds.InterfacePrivate;
					case SyntaxKind.PartialKeyword: isPartial = true; break;
				}
			}
			return isPartial ? IconIds.PartialInterface
				: syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.InterfaceInternal
				: KnownImageIds.InterfacePrivate;
		}
		int GetEventIcon(BasePropertyDeclarationSyntax syntax) {
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return KnownImageIds.EventPublic;
					case SyntaxKind.InternalKeyword: return KnownImageIds.EventInternal;
					case SyntaxKind.ProtectedKeyword: return KnownImageIds.EventProtected;
					case SyntaxKind.PrivateKeyword: return KnownImageIds.EventPrivate;
				}
			}
			return syntax.ExplicitInterfaceSpecifier != null ? IconIds.ExplicitInterfaceEvent
				: syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.EventInternal
				: syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration) ? KnownImageIds.EventPublic
				: KnownImageIds.EventPrivate;
		}
		int GetEventFieldIcon(EventFieldDeclarationSyntax syntax) {
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return KnownImageIds.EventPublic;
					case SyntaxKind.InternalKeyword: return KnownImageIds.EventInternal;
					case SyntaxKind.ProtectedKeyword: return KnownImageIds.EventProtected;
					case SyntaxKind.PrivateKeyword: return KnownImageIds.EventPrivate;
				}
			}
			return syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration)
				? KnownImageIds.EventPublic
				: KnownImageIds.EventPrivate;
		}
		int GetDelegateIcon(DelegateDeclarationSyntax syntax) {
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return KnownImageIds.DelegatePublic;
					case SyntaxKind.InternalKeyword: return KnownImageIds.DelegateInternal;
					case SyntaxKind.ProtectedKeyword: return KnownImageIds.DelegateProtected;
					case SyntaxKind.PrivateKeyword: return KnownImageIds.DelegatePrivate;
				}
			}
			return syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration)
				? KnownImageIds.DelegateInternal
				: KnownImageIds.DelegatePrivate;
		}
		int GetFieldIcon(FieldDeclarationSyntax syntax) {
			bool isConst = false;
			var accessibility = Accessibility.NotApplicable;
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.ConstKeyword: isConst = true; break;
					case SyntaxKind.PublicKeyword: accessibility = Accessibility.Public; break;
					case SyntaxKind.InternalKeyword:
						if (accessibility != Accessibility.Protected) {
							accessibility = Accessibility.Internal;
						}
						break;
					case SyntaxKind.ProtectedKeyword: accessibility = Accessibility.Protected; break;
					case SyntaxKind.PrivateKeyword: accessibility = Accessibility.Private; break;
				}
			}
			return accessibility switch {
				Accessibility.Public => isConst ? KnownImageIds.ConstantPublic : KnownImageIds.FieldPublic,
				Accessibility.Internal => isConst ? KnownImageIds.ConstantInternal : KnownImageIds.FieldInternal,
				Accessibility.Protected => isConst ? KnownImageIds.ConstantProtected : KnownImageIds.FieldProtected,
				Accessibility.Private => isConst ? KnownImageIds.ConstantPrivate : KnownImageIds.FieldPrivate,
				_ => syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration)
						? isConst ? KnownImageIds.ConstantPublic : KnownImageIds.FieldPublic
						: isConst ? KnownImageIds.ConstantPrivate : KnownImageIds.FieldPrivate,
			};
		}
		int GetMethodIcon(MethodDeclarationSyntax syntax) {
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return KnownImageIds.MethodPublic;
					case SyntaxKind.InternalKeyword: return KnownImageIds.MethodInternal;
					case SyntaxKind.ProtectedKeyword: return KnownImageIds.MethodProtected;
					case SyntaxKind.PrivateKeyword: return KnownImageIds.MethodPrivate;
				}
			}
			return syntax.ExplicitInterfaceSpecifier != null ? IconIds.ExplicitInterfaceMethod
				: syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration) ? KnownImageIds.MethodPublic
				: KnownImageIds.MethodPrivate;
		}
		int GetConstructorIcon(ConstructorDeclarationSyntax syntax) {
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return IconIds.PublicConstructor;
					case SyntaxKind.InternalKeyword: return IconIds.InternalConstructor;
					case SyntaxKind.ProtectedKeyword: return IconIds.ProtectedConstructor;
					case SyntaxKind.PrivateKeyword: return IconIds.PrivateConstructor;
				}
			}
			return IconIds.PrivateConstructor;
		}
		int GetPropertyIconExt(BasePropertyDeclarationSyntax syntax) {
			bool autoProperty = syntax.Modifiers.Any(i => i.IsKind(SyntaxKind.AbstractKeyword)) == false
				&& syntax.AccessorList?.Accessors.All(i => i.Body == null && i.ExpressionBody == null) == true;
			if (autoProperty) {
				return GetPropertyIcon(syntax);
			}
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return IconIds.PublicPropertyMethod;
					case SyntaxKind.InternalKeyword: return IconIds.InternalPropertyMethod;
					case SyntaxKind.ProtectedKeyword: return IconIds.ProtectedPropertyMethod;
					case SyntaxKind.PrivateKeyword: return IconIds.PrivatePropertyMethod;
				}
			}
			return syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration)
				? IconIds.PublicPropertyMethod
				: IconIds.PrivatePropertyMethod;
		}
		int GetPropertyIcon(BasePropertyDeclarationSyntax syntax) {
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return KnownImageIds.PropertyPublic;
					case SyntaxKind.InternalKeyword: return KnownImageIds.PropertyInternal;
					case SyntaxKind.ProtectedKeyword: return KnownImageIds.PropertyProtected;
					case SyntaxKind.PrivateKeyword: return KnownImageIds.PropertyPrivate;
				}
			}
			return syntax.ExplicitInterfaceSpecifier != null ? IconIds.ExplicitInterfaceProperty
				: syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration) ? KnownImageIds.PropertyPublic
				: KnownImageIds.PropertyPrivate;
		}
		int GetOperatorIcon(OperatorDeclarationSyntax syntax) {
			foreach (var modifier in syntax.Modifiers) {
				switch (modifier.Kind()) {
					case SyntaxKind.PublicKeyword: return KnownImageIds.OperatorPublic;
					case SyntaxKind.InternalKeyword: return KnownImageIds.OperatorInternal;
					case SyntaxKind.ProtectedKeyword: return KnownImageIds.OperatorProtected;
					case SyntaxKind.PrivateKeyword: return KnownImageIds.OperatorPrivate;
				}
			}
			return syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration)
				? KnownImageIds.OperatorPublic
				: KnownImageIds.OperatorPrivate;
		}

		int GetConversionIcon(ConversionOperatorDeclarationSyntax syntax) {
			return syntax.ImplicitOrExplicitKeyword.IsKind(SyntaxKind.ExplicitKeyword)
				? IconIds.ExplicitConversion
				: IconIds.ImplicitConversion;
		}

		int GetExpressionIcon(ExpressionSyntax exp) {
			return exp.Kind() switch {
				SyntaxKind.InvocationExpression => KnownImageIds.InvokeMethod,
				_ => exp is AssignmentExpressionSyntax ? KnownImageIds.Assign
						: exp is BinaryExpressionSyntax
							|| exp is PrefixUnaryExpressionSyntax
							|| exp is PostfixUnaryExpressionSyntax ? KnownImageIds.Operator
						: KnownImageIds.Action,
			};
		}
	}
	#endregion

}
