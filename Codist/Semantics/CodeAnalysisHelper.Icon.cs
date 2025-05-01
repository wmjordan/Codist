using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Imaging;

namespace Codist
{
	partial class CodeAnalysisHelper
	{
		#region Symbol icon
		public static int GetImageId(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Assembly: return KnownImageIds.Assembly;
				case SymbolKind.DynamicType: return KnownImageIds.StatusHelp;
				case SymbolKind.Event: return GetEventImageId((IEventSymbol)symbol);
				case SymbolKind.Field: return GetFieldImageId((IFieldSymbol)symbol);
				case SymbolKind.Label: return KnownImageIds.Label;
				case SymbolKind.Local: return IconIds.LocalVariable;
				case SymbolKind.Method: return GetMethodImageId((IMethodSymbol)symbol);
				case SymbolKind.NamedType: return GetTypeImageId((INamedTypeSymbol)symbol);
				case SymbolKind.Namespace: return IconIds.Namespace;
				case SymbolKind.Parameter: return IconIds.Argument;
				case SymbolKind.Property: return GetPropertyImageId((IPropertySymbol)symbol);
				case FunctionPointerType: return IconIds.FunctionPointer;
				case SymbolKind.Discard: return IconIds.Discard;
				default: return KnownImageIds.Item;
			}

			int GetEventImageId(IEventSymbol ev) {
				switch (ev.DeclaredAccessibility) {
					case Accessibility.Public: return KnownImageIds.EventPublic;
					case Accessibility.Protected:
					case Accessibility.ProtectedOrInternal:
					case Accessibility.ProtectedAndInternal:
						return KnownImageIds.EventProtected;
					case Accessibility.Private:
						return ev.ExplicitInterfaceImplementations.Length != 0
							? IconIds.ExplicitInterfaceEvent
							: KnownImageIds.EventPrivate;
					case Accessibility.Internal: return KnownImageIds.EventInternal;
					default: return IconIds.Event;
				}
			}

			int GetFieldImageId(IFieldSymbol f) {
				if (f.IsConst) {
					if (f.ContainingType.TypeKind == TypeKind.Enum) {
						return IconIds.EnumField;
					}
					switch (f.DeclaredAccessibility) {
						case Accessibility.Public: return KnownImageIds.ConstantPublic;
						case Accessibility.Protected:
						case Accessibility.ProtectedOrInternal:
						case Accessibility.ProtectedAndInternal:
							return KnownImageIds.ConstantProtected;
						case Accessibility.Private: return KnownImageIds.ConstantPrivate;
						case Accessibility.Internal: return KnownImageIds.ConstantInternal;
						default: return KnownImageIds.Constant;
					}
				}
				switch (f.DeclaredAccessibility) {
					case Accessibility.Public: return KnownImageIds.FieldPublic;
					case Accessibility.Protected:
					case Accessibility.ProtectedOrInternal:
					case Accessibility.ProtectedAndInternal:
						return KnownImageIds.FieldProtected;
					case Accessibility.Private: return KnownImageIds.FieldPrivate;
					case Accessibility.Internal: return KnownImageIds.FieldInternal;
					default: return IconIds.Field;
				}
			}

			int GetMethodImageId(IMethodSymbol m) {
				switch (m.MethodKind) {
					case MethodKind.Constructor:
					case MethodKind.StaticConstructor:
						switch (m.DeclaredAccessibility) {
							case Accessibility.Public: return IconIds.PublicConstructor;
							case Accessibility.Protected:
							case Accessibility.ProtectedOrInternal:
							case Accessibility.ProtectedAndInternal:
								return IconIds.ProtectedConstructor;
							case Accessibility.Private: return IconIds.PrivateConstructor;
							case Accessibility.Internal: return IconIds.InternalConstructor;
							default: return IconIds.Constructor;
						}
					case MethodKind.Destructor:
						return IconIds.Destructor;
					case MethodKind.UserDefinedOperator:
						switch (m.DeclaredAccessibility) {
							case Accessibility.Public: return KnownImageIds.OperatorPublic;
							case Accessibility.Protected:
							case Accessibility.ProtectedOrInternal:
							case Accessibility.ProtectedAndInternal:
								return KnownImageIds.OperatorProtected;
							case Accessibility.Private: return KnownImageIds.OperatorPrivate;
							case Accessibility.Internal: return KnownImageIds.OperatorInternal;
							default: return KnownImageIds.Operator;
						}
					case MethodKind.Conversion:
						return m.MetadataName == "op_Explicit"
							? IconIds.ExplicitConversion
							: IconIds.ImplicitConversion;
				}
				switch (m.DeclaredAccessibility) {
					case Accessibility.Public: return KnownImageIds.MethodPublic;
					case Accessibility.Protected:
					case Accessibility.ProtectedOrInternal:
					case Accessibility.ProtectedAndInternal:
						return KnownImageIds.MethodProtected;
					case Accessibility.Private:
						return m.ExplicitInterfaceImplementations.Length != 0
							? IconIds.ExplicitInterfaceMethod
							: KnownImageIds.MethodPrivate;
					case Accessibility.Internal: return KnownImageIds.MethodInternal;
					default: return IconIds.Method;
				}
			}

			int GetTypeImageId(INamedTypeSymbol t) {
				switch (t.TypeKind) {
					case TypeKind.Class:
						switch (t.DeclaredAccessibility) {
							case Accessibility.Public: return KnownImageIds.ClassPublic;
							case Accessibility.Protected:
							case Accessibility.ProtectedOrInternal:
							case Accessibility.ProtectedAndInternal:
								return KnownImageIds.ClassProtected;
							case Accessibility.Private: return KnownImageIds.ClassPrivate;
							case Accessibility.Internal: return KnownImageIds.ClassInternal;
							default: return IconIds.Class;
						}
					case TypeKind.Delegate:
						switch (t.DeclaredAccessibility) {
							case Accessibility.Public: return KnownImageIds.DelegatePublic;
							case Accessibility.Protected:
							case Accessibility.ProtectedOrInternal:
							case Accessibility.ProtectedAndInternal:
								return KnownImageIds.DelegateProtected;
							case Accessibility.Private: return KnownImageIds.DelegatePrivate;
							case Accessibility.Internal: return KnownImageIds.DelegateInternal;
							default: return IconIds.Delegate;
						}
					case TypeKind.Enum:
						switch (t.DeclaredAccessibility) {
							case Accessibility.Public: return KnownImageIds.EnumerationPublic;
							case Accessibility.Protected:
							case Accessibility.ProtectedOrInternal:
							case Accessibility.ProtectedAndInternal:
								return KnownImageIds.EnumerationProtected;
							case Accessibility.Private: return KnownImageIds.EnumerationPrivate;
							case Accessibility.Internal: return KnownImageIds.EnumerationInternal;
							default: return IconIds.Enum;
						}
					case TypeKind.Interface:
						switch (t.DeclaredAccessibility) {
							case Accessibility.Public:
								return t.IsDisposable() || t.IsAsyncDisposable()
									? IconIds.Disposable
									: KnownImageIds.InterfacePublic;
							case Accessibility.Protected:
							case Accessibility.ProtectedOrInternal:
							case Accessibility.ProtectedAndInternal:
								return KnownImageIds.InterfaceProtected;
							case Accessibility.Private: return KnownImageIds.InterfacePrivate;
							case Accessibility.Internal: return KnownImageIds.InterfaceInternal;
							default: return IconIds.Interface;
						}
					case TypeKind.Struct:
						switch (t.DeclaredAccessibility) {
							case Accessibility.Public: return KnownImageIds.StructurePublic;
							case Accessibility.Protected:
							case Accessibility.ProtectedOrInternal:
							case Accessibility.ProtectedAndInternal:
								return KnownImageIds.StructureProtected;
							case Accessibility.Private: return KnownImageIds.StructurePrivate;
							case Accessibility.Internal: return KnownImageIds.StructureInternal;
							default: return IconIds.Structure;
						}
					case Extension:
						return IconIds.ExtensionDeclaration;
					case TypeKind.TypeParameter:
					default: return KnownImageIds.Type;
				}
			}

			int GetPropertyImageId(IPropertySymbol p) {
				switch (p.DeclaredAccessibility) {
					case Accessibility.Public: return KnownImageIds.PropertyPublic;
					case Accessibility.Protected:
					case Accessibility.ProtectedOrInternal:
					case Accessibility.ProtectedAndInternal:
						return KnownImageIds.PropertyProtected;
					case Accessibility.Private:
						return p.ExplicitInterfaceImplementations.Length != 0
							? IconIds.ExplicitInterfaceProperty
							: KnownImageIds.PropertyPrivate;
					case Accessibility.Internal: return KnownImageIds.PropertyInternal;
					default: return KnownImageIds.Property;
				}
			}
		}
		#endregion

		#region Node icon
		public static int GetImageId(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration:
				case RecordDeclaration:
					return GetClassIcon((BaseTypeDeclarationSyntax)node);
				case SyntaxKind.EnumDeclaration: return GetEnumIcon((EnumDeclarationSyntax)node);
				case SyntaxKind.StructDeclaration:
				case RecordStructDeclaration:
					return GetStructIcon((BaseTypeDeclarationSyntax)node);
				case SyntaxKind.InterfaceDeclaration: return GetInterfaceIcon((InterfaceDeclarationSyntax)node);
				case SyntaxKind.MethodDeclaration: return GetMethodIcon((MethodDeclarationSyntax)node);
				case SyntaxKind.ConstructorDeclaration: return GetConstructorIcon((ConstructorDeclarationSyntax)node);
				case SyntaxKind.PropertyDeclaration: return GetPropertyIconExt((BasePropertyDeclarationSyntax)node);
				case SyntaxKind.IndexerDeclaration: return GetPropertyIcon((BasePropertyDeclarationSyntax)node);
				case SyntaxKind.OperatorDeclaration: return GetOperatorIcon((OperatorDeclarationSyntax)node);
				case SyntaxKind.ConversionOperatorDeclaration: return GetConversionIcon((ConversionOperatorDeclarationSyntax)node);
				case SyntaxKind.FieldDeclaration: return GetFieldIcon((FieldDeclarationSyntax)node);
				case SyntaxKind.EnumMemberDeclaration: return IconIds.EnumField;
				case SyntaxKind.VariableDeclarator: return node.Parent.Parent.GetImageId();
				case SyntaxKind.VariableDeclaration:
				case SyntaxKind.LocalDeclarationStatement: return IconIds.LocalVariable;
				case SyntaxKind.NamespaceDeclaration:
				case FileScopedNamespaceDeclaration: return IconIds.Namespace;
				case SyntaxKind.ArgumentList:
				case SyntaxKind.AttributeArgumentList: return IconIds.Argument;
				case SyntaxKind.DoStatement: return IconIds.DoWhile;
				case SyntaxKind.FixedStatement: return IconIds.Pin;
				case SyntaxKind.ForEachStatement: return IconIds.ForEach;
				case SyntaxKind.ForStatement: return IconIds.For;
				case SyntaxKind.IfStatement: return IconIds.If;
				case SyntaxKind.LockStatement: return KnownImageIds.Lock;
				case SyntaxKind.SwitchStatement:
				case SwitchExpression:
					return IconIds.Switch;
				case SyntaxKind.SwitchSection:
				case SyntaxKind.CaseSwitchLabel:
				case SyntaxKind.DefaultSwitchLabel:
					return IconIds.SwitchSection;
				case SyntaxKind.TryStatement: return IconIds.TryCatch;
				case SyntaxKind.UsingStatement: return IconIds.Using;
				case SyntaxKind.WhileStatement: return IconIds.While;
				case SyntaxKind.ParameterList: return IconIds.Argument;
				case SyntaxKind.ParenthesizedExpression: return IconIds.ParenthesizedExpression;
				case SyntaxKind.ParenthesizedLambdaExpression:
				case SyntaxKind.SimpleLambdaExpression: return IconIds.LambdaExpression;
				case SyntaxKind.DelegateDeclaration: return GetDelegateIcon((DelegateDeclarationSyntax)node);
				case SyntaxKind.EventDeclaration: return GetEventIcon((BasePropertyDeclarationSyntax)node);
				case SyntaxKind.EventFieldDeclaration: return GetEventFieldIcon((EventFieldDeclarationSyntax)node);
				case SyntaxKind.UnsafeStatement: return IconIds.Unsafe;
				case SyntaxKind.XmlElement:
				case SyntaxKind.XmlEmptyElement: return KnownImageIds.XMLElement;
				case SyntaxKind.XmlComment: return KnownImageIds.XMLCommentTag;
				case SyntaxKind.DestructorDeclaration: return IconIds.Destructor;
				case SyntaxKind.UncheckedStatement: return KnownImageIds.CheckBoxUnchecked;
				case SyntaxKind.CheckedStatement: return KnownImageIds.CheckBoxChecked;
				case SyntaxKind.ReturnStatement: return IconIds.Return;
				case SyntaxKind.ExpressionStatement: return GetExpressionIcon(((ExpressionStatementSyntax)node).Expression);
				case SyntaxKind.Attribute: return IconIds.Attribute;
				case SyntaxKind.YieldReturnStatement: return KnownImageIds.Yield;
				case SyntaxKind.GotoStatement:
				case SyntaxKind.GotoCaseStatement:
				case SyntaxKind.GotoDefaultStatement: return IconIds.GoTo;
				case SyntaxKind.LocalFunctionStatement: return IconIds.LocalFunction;
				case SyntaxKind.RegionDirectiveTrivia: return IconIds.Region;
				case SyntaxKind.EndRegionDirectiveTrivia: return KnownImageIds.ToolstripPanelBottom;
				case CodeAnalysisHelper.ExtensionDeclaration: return IconIds.ExtensionDeclaration;
			}
			return KnownImageIds.UnknownMember;

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
				switch (accessibility) {
					case Accessibility.Public: return isConst ? KnownImageIds.ConstantPublic : KnownImageIds.FieldPublic;
					case Accessibility.Internal: return isConst ? KnownImageIds.ConstantInternal : KnownImageIds.FieldInternal;
					case Accessibility.Protected: return isConst ? KnownImageIds.ConstantProtected : KnownImageIds.FieldProtected;
					case Accessibility.Private: return isConst ? KnownImageIds.ConstantPrivate : KnownImageIds.FieldPrivate;
				}
				return syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration)
					? isConst ? KnownImageIds.ConstantPublic : KnownImageIds.FieldPublic
					: isConst ? KnownImageIds.ConstantPrivate : KnownImageIds.FieldPrivate;
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
				switch (exp.Kind()) {
					case SyntaxKind.InvocationExpression: return KnownImageIds.InvokeMethod;
				}
				return exp is AssignmentExpressionSyntax ? KnownImageIds.Assign
					: exp is BinaryExpressionSyntax || exp is PrefixUnaryExpressionSyntax || exp is PostfixUnaryExpressionSyntax ? KnownImageIds.Operator
					: KnownImageIds.Action;
			}
		}
		#endregion

	}
}
