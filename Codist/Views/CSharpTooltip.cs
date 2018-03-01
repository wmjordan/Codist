using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Views
{
	sealed class CSharpQuickInfoSource : IQuickInfoSource
	{
		static Brush _InterfaceBrush, _ClassBrush, _StructBrush, _TextBrush, _NumberBrush, _EnumBrush, _KeywordBrush, _MethodBrush, _DelegateBrush, _ParameterBrush;

		readonly IEditorFormatMapService _FormatMapService;
		readonly CSharpQuickInfoSourceProvider _QuickInfoSourceProvider;
		IEditorFormatMap _FormatMap;
		bool _IsDisposed;
		SemanticModel _SemanticModel;
		ITextBuffer _TextBuffer;

		public CSharpQuickInfoSource(CSharpQuickInfoSourceProvider provider, ITextBuffer subjectBuffer, IEditorFormatMapService formatMapService) {
			_QuickInfoSourceProvider = provider;
			_TextBuffer = subjectBuffer;
			_FormatMapService = formatMapService;
			_TextBuffer.Changing += TextBuffer_Changing;
			Config.ConfigUpdated += _ConfigUpdated;
		}

		public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> qiContent, out ITrackingSpan applicableToSpan) {
			// Map the trigger point down to our buffer.
			var subjectTriggerPoint = session.GetTriggerPoint(_TextBuffer.CurrentSnapshot).GetValueOrDefault();
			if (subjectTriggerPoint.Snapshot == null) {
				applicableToSpan = null;
				return;
			}

			var currentSnapshot = subjectTriggerPoint.Snapshot;
			var workspace = currentSnapshot.TextBuffer.GetWorkspace();
			if (workspace == null) {
				goto EXIT;
			}

			var querySpan = new SnapshotSpan(subjectTriggerPoint, 0);
			var semanticModel = _SemanticModel;
			if (semanticModel == null) {
				_SemanticModel = semanticModel = workspace.GetDocument(querySpan).GetSemanticModelAsync().Result;
			}
			var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot();

			//look for occurrences of our QuickInfo words in the span
			var navigator = _QuickInfoSourceProvider._NavigatorService.GetTextStructureNavigator(_TextBuffer);
			var node = unitCompilation.FindNode(new TextSpan(querySpan.Start, querySpan.Length), true, true);
			if (node == null || node.Span.Contains(subjectTriggerPoint.Position) == false) {
				goto EXIT;
			}
			var extent = navigator.GetExtentOfWord(querySpan.Start).Span;
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
				ShowParameterInfo(qiContent, node);
			}
			node = node.Kind() == SyntaxKind.Argument ? (node as ArgumentSyntax).Expression : node;
			var symbol = GetSymbol(node, semanticModel);
			if (symbol == null) {
				ShowMiscInfo(qiContent, currentSnapshot, node);
				goto RETURN;
			}

			if (node is PredefinedTypeSyntax/* void */) {
				goto EXIT;
			}
			var formatMap = _FormatMapService.GetEditorFormatMap(session.TextView);
			if (_FormatMap != formatMap) {
				_FormatMap = formatMap;
				UpdateSyntaxHighlights(formatMap);
			}

			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
				ShowAttributesInfo(qiContent, node, symbol);
			}
			switch (symbol.Kind) {
				case SymbolKind.Event:
					ShowEventInfo(qiContent, node, symbol as IEventSymbol);
					break;
				case SymbolKind.Field:
					ShowFieldInfo(qiContent, node, symbol as IFieldSymbol);
					break;
				case SymbolKind.Local:
					var loc = symbol as ILocalSymbol;
					if (loc.HasConstantValue) {
						ShowConstInfo(qiContent, node, symbol, loc.ConstantValue);
					}
					break;
				case SymbolKind.Method:
					ShowMethodInfo(qiContent, node, symbol as IMethodSymbol);
					if (node.Parent.IsKind(SyntaxKind.Attribute)) {
						if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
							ShowAttributesInfo(qiContent, node, symbol.ContainingType);
						}
						ShowTypeInfo(qiContent, node.Parent, symbol.ContainingType as INamedTypeSymbol);
					}
					break;
				case SymbolKind.NamedType:
					ShowTypeInfo(qiContent, node, symbol as INamedTypeSymbol);
					break;
				case SymbolKind.Property:
					ShowPropertyInfo(qiContent, node, symbol as IPropertySymbol);
					break;
			}
			RETURN:
			ShowSelectionInfo(session, qiContent, subjectTriggerPoint);
			applicableToSpan = qiContent.Count > 0
				? currentSnapshot.CreateTrackingSpan(extent.Start, extent.Length, SpanTrackingMode.EdgeInclusive)
				: null;
			return;
			EXIT:
			ShowSelectionInfo(session, qiContent, subjectTriggerPoint);
			applicableToSpan = qiContent.Count > 0
				? currentSnapshot.CreateTrackingSpan(session.TextView.GetTextElementSpan(subjectTriggerPoint), SpanTrackingMode.EdgeInclusive)
				: null;
		}

		static void ShowMiscInfo(IList<object> qiContent, ITextSnapshot currentSnapshot, SyntaxNode node) {
			StackPanel infoBox = null;
			var nodeKind = node.Kind();
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues) && nodeKind == SyntaxKind.NumericLiteralExpression) {
				infoBox = ShowNumericForm(node);
			}
			else if (nodeKind == SyntaxKind.SwitchStatement) {
				qiContent.Add((node as SwitchStatementSyntax).Sections.Count + " sections");
			}
			else if (nodeKind == SyntaxKind.StringLiteralExpression) {
				infoBox = ShowStringInfo(node.GetFirstToken().ValueText);
			}
			else if (node.Kind() == SyntaxKind.Block) {
				var lines = currentSnapshot.GetLineNumberFromPosition(node.Span.End) - currentSnapshot.GetLineNumberFromPosition(node.SpanStart) + 1;
				if (lines > 100) {
					qiContent.Add(new TextBlock { Text = lines + " lines", FontWeight = FontWeight.FromOpenTypeWeight(800) });
				}
				else if (lines > 10) {
					qiContent.Add(lines + " lines");
				}
			}
			if (infoBox != null) {
				qiContent.Add(infoBox);
			}
		}

		void ShowAttributesInfo(IList<object> qiContent, SyntaxNode node, ISymbol symbol) {
			var attrs = symbol.GetAttributes();
			if (attrs.Length > 0) {
				qiContent.Add(ShowAttributes(attrs, node.SpanStart));
			}
		}

		static void ShowSelectionInfo(IQuickInfoSession session, IList<object> qiContent, SnapshotPoint point) {
			var selection = session.TextView.Selection;
			if (selection.IsEmpty != false) {
				return;
			}
			var p1 = selection.Start.Position;
			var p2 = selection.End.Position;
			if (p1 <= point && point <= p2) {
				var c = 0;
				foreach (var item in selection.SelectedSpans) {
					c += item.Length;
				}
				var y1 = point.Snapshot.GetLineNumberFromPosition(p1);
				var y2 = point.Snapshot.GetLineNumberFromPosition(p2) + 1;
				qiContent.Add(String.Join(
					" ",
					"Selection:",
					c.ToString(),
					(c > 1 ? "characters, " : "character, "),
					(y2 - y1).ToString(),
					(y2 - y1 > 1 ? "lines" : "line")));
			}
		}

		void ShowPropertyInfo(IList<object> qiContent, SyntaxNode node, IPropertySymbol property) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
								&& (property.DeclaredAccessibility != Accessibility.Public || property.IsAbstract || property.IsStatic || property.IsOverride || property.IsVirtual)) {
				ShowDeclarationModifier(qiContent, property, "Property", node.SpanStart);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, node, property, property.ExplicitInterfaceImplementations.Select(i => i.Type), m => m.Type, m => m.Parameters);
			}
		}

		void ShowEventInfo(IList<object> qiContent, SyntaxNode node, IEventSymbol ev) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)) {
				if (ev.DeclaredAccessibility != Accessibility.Public || ev.IsAbstract || ev.IsStatic || ev.IsOverride || ev.IsVirtual) {
					ShowDeclarationModifier(qiContent, ev, "Event", node.SpanStart);
				}
				var invoke = ev.Type.GetMembers("Invoke").FirstOrDefault() as IMethodSymbol;
				if (invoke != null && invoke.Parameters.Length == 2) {
					qiContent.Add(new StackPanel().MakeHorizontal().Add(ToUIText(invoke.Parameters[1].Type.ToMinimalDisplayParts(_SemanticModel, node.SpanStart), "Event argument: ", true)));
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, node, ev, ev.ExplicitInterfaceImplementations.Select(i => i.Type), m => m.Type, m => m.AddMethod.Parameters);
			}
		}

		void ShowFieldInfo(IList<object> qiContent, SyntaxNode node, IFieldSymbol field) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
								&& (field.DeclaredAccessibility != Accessibility.Public || field.IsReadOnly || field.IsVolatile || field.IsStatic)
								&& field.ContainingType.TypeKind != TypeKind.Enum) {
				ShowFieldDeclaration(qiContent, field);
			}
			if (field.HasConstantValue) {
				ShowConstInfo(qiContent, node, field, field.ConstantValue);
			}
		}

		void ShowMethodInfo(IList<object> qiContent, SyntaxNode node, IMethodSymbol method) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
								&& (method.DeclaredAccessibility != Accessibility.Public || method.IsAbstract || method.IsStatic || method.IsVirtual || method.IsOverride || method.IsExtern)
								&& method.ContainingType.TypeKind != TypeKind.Interface) {
				ShowDeclarationModifier(qiContent, method, "Method", node.SpanStart);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ExtensionMethod) && method.IsExtensionMethod) {
				ShowExtensionMethod(qiContent, method, node.SpanStart);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, node, method, method.ExplicitInterfaceImplementations.Select(i => i.ReturnType), m => m.ReturnType, m => m.Parameters);
			}
		}

		void ShowTypeInfo(IList<object> qiContent, SyntaxNode node, INamedTypeSymbol typeSymbol) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType)) {
				if (typeSymbol.TypeKind == TypeKind.Enum) {
					ShowEnumInfo(qiContent, node, typeSymbol, true);
				}
				else {
					ShowBaseType(qiContent, typeSymbol, node.SpanStart);
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Interfaces)) {
				ShowInterfaces(qiContent, typeSymbol, node.SpanStart);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& typeSymbol.TypeKind == TypeKind.Class
				&& (typeSymbol.DeclaredAccessibility != Accessibility.Public || typeSymbol.IsAbstract || typeSymbol.IsStatic || typeSymbol.IsSealed)) {
				ShowDeclarationModifier(qiContent, typeSymbol, "Class", node.SpanStart);
			}
		}

		void ShowConstInfo(IList<object> qiContent, SyntaxNode node, ISymbol symbol, object value) {
			var sv = value as string;
			if (sv != null) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.String)) {
					qiContent.Add(ShowStringInfo(sv));
				}
			}
			else if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues)) {
				var s = ShowNumericForms(value, NumericForm.None);
				if (s != null) {
					qiContent.Add(s);
					ShowEnumInfo(qiContent, node, symbol.ContainingType, false);
				}
			}
		}

		void ShowInterfaceImplementation<TSymbol>(IList<object> qiContent, SyntaxNode node, TSymbol symbol, IEnumerable<ITypeSymbol> explicitImplementations, Func<TSymbol, ITypeSymbol> returnTypeGetter, Func<TSymbol, ImmutableArray<IParameterSymbol>> parameterGetter)
			where TSymbol : class, ISymbol {
			if (symbol.IsStatic || symbol.DeclaredAccessibility != Accessibility.Public) {
				return;
			}
			var interfaces = symbol.ContainingType.AllInterfaces;
			if (interfaces.Length == 0) {
				return;
			}
			List<ITypeSymbol> types = new List<ITypeSymbol>();
			StackPanel info = null;
			var returnType = returnTypeGetter(symbol);
			var parameters = parameterGetter(symbol);
			foreach (var intf in interfaces) {
				foreach (var member in intf.GetMembers()) {
					if (member.Kind != symbol.Kind || member.DeclaredAccessibility != Accessibility.Public || member.Name != symbol.Name) {
						continue;
					}

					var memberSymbol = member as TSymbol;
					if (returnType != null && returnType.Equals(returnTypeGetter(memberSymbol)) == false) {
						continue;
					}
					var memberParameters = parameterGetter(memberSymbol);
					if (memberParameters.Length != parameters.Length) {
						continue;
					}
					for (int i = parameters.Length - 1; i >= 0; i--) {
						var pi = parameters[i];
						var mi = memberParameters[i];
						if (pi.Type.Equals(mi.Type) == false
							|| pi.RefKind != mi.RefKind) {
							goto NEXT;
						}
					}
					types.Add(intf);
					break;
					NEXT:;
				}
			}
			if (types.Count > 0) {
				var p = new StackPanel().AddText("Implements:", true);
				foreach (var item in types) {
					p.Add(ToUIText(item.ToMinimalDisplayParts(_SemanticModel, node.SpanStart)));
				}
				info = new StackPanel().Add(p);
			}
			if (explicitImplementations != null) {
				types.Clear();
				types.AddRange(explicitImplementations);
				if (types.Count > 0) {
					if (info == null) {
						info = new StackPanel();
					}
					var p = new StackPanel().AddText("Explicit implements:", true);
					foreach (var item in types) {
						p.Add(ToUIText(item.ToMinimalDisplayParts(_SemanticModel, node.SpanStart)));
					}
					info.Add(p);
				}
			}
			if (info != null) {
				qiContent.Add(info);
			}
		}

		void IDisposable.Dispose() {
			if (!_IsDisposed) {
				_TextBuffer.Changing -= TextBuffer_Changing;
				Config.ConfigUpdated -= _ConfigUpdated; ;
				GC.SuppressFinalize(this);
				_IsDisposed = true;
			}
		}

		static Brush GetFormatBrush(string name, IEditorFormatMap formatMap) {
			return formatMap.GetProperties(name)?[EditorFormatDefinition.ForegroundBrushId] as Brush;
		}

		static ISymbol GetSymbol(SyntaxNode node, SemanticModel semanticModel) {
			return semanticModel.GetSymbolInfo(node).Symbol
					?? semanticModel.GetDeclaredSymbol(node)
					?? (node is AttributeArgumentSyntax
						? semanticModel.GetSymbolInfo((node as AttributeArgumentSyntax).Expression).Symbol
						: null)
					?? (node is SimpleBaseTypeSyntax || node is TypeConstraintSyntax
						? semanticModel.GetSymbolInfo(node.FindNode(node.Span, false, true)).Symbol
						: null)
					?? (node.Parent is MemberAccessExpressionSyntax
						? semanticModel.GetSymbolInfo(node.Parent).CandidateSymbols.FirstOrDefault()
						: null)
					?? (node.Parent is ArgumentSyntax
						? semanticModel.GetSymbolInfo((node.Parent as ArgumentSyntax).Expression).CandidateSymbols.FirstOrDefault()
						: null);
		}

		static bool IsCommonClassName(string name) {
			return name == "Object" || name == "ValueType" || name == "Enum" || name == "MulticastDelegate";
		}

		static void ShowExtensionMethod(IList<object> qiContent, IMethodSymbol method, int position) {
			var info = ToUIText(method.ContainingType.ToDisplayParts(), "Defined in: ", true);
			string asmName = method.ContainingAssembly?.Modules?.FirstOrDefault()?.Name
				?? method.ContainingAssembly?.Name;
			if (asmName != null) {
				info.AddText(" (" + asmName + ")");
			}
			qiContent.Add(info);
		}

		static void ShowFieldDeclaration(IList<object> qiContent, IFieldSymbol field) {
			var info = new StackPanel().MakeHorizontal().AddText("Field declaration: ", true);
			ShowAccessibilityInfo(field, info);
			if (field.IsVolatile) {
				info.AddText("volatile ", _KeywordBrush);
			}
			if (field.IsStatic) {
				info.AddText("static ", _KeywordBrush);
			}
			if (field.IsReadOnly) {
				info.AddText("readonly ", _KeywordBrush);
			}
			qiContent.Add(info);
		}

		static StackPanel ShowNumericForm(SyntaxNode node) {
			return ShowNumericForms(node.GetFirstToken().Value, node.Parent.Kind() == SyntaxKind.UnaryMinusExpression ? NumericForm.Negative : NumericForm.None);
		}

		static StackPanel ShowNumericForms(object value, NumericForm form) {
			if (value is int) {
				var v = (int)value;
				if (form == NumericForm.Negative) {
					v = -v;
				}
				var bytes = new byte[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
				return ToUIText(form == NumericForm.Unsigned ? ((uint)v).ToString() : v.ToString(), bytes);
			}
			else if (value is long) {
				var v = (long)value;
				if (form == NumericForm.Negative) {
					v = -v;
				}
				var bytes = new byte[] { (byte)(v >> 56), (byte)(v >> 48), (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
				return ToUIText(form == NumericForm.Unsigned ? ((ulong)v).ToString() : v.ToString(), bytes);
			}
			else if (value is byte) {
				return ToUIText(((byte)value).ToString(), new byte[] { (byte)value });
			}
			else if (value is short) {
				var v = (short)value;
				if (form == NumericForm.Negative) {
					v = (short)-v;
				}
				var bytes = new byte[] { (byte)(v >> 8), (byte)v };
				return ToUIText(form == NumericForm.Unsigned ? ((ushort)v).ToString() : v.ToString(), bytes);
			}
			else if (value is uint) {
				return ShowNumericForms((int)(uint)value, NumericForm.Unsigned);
			}
			else if (value is ulong) {
				return ShowNumericForms((long)(ulong)value, NumericForm.Unsigned);
			}
			else if (value is ushort) {
				return ShowNumericForms((short)(ushort)value, NumericForm.Unsigned);
			}
			else if (value is sbyte) {
				return ToUIText(((sbyte)value).ToString(), new byte[] { (byte)(sbyte)value });
			}
			return null;
		}

		static StackPanel ShowStringInfo(string sv) {
			return new StackPanel()
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(sv.Length.ToString()).AddText("chars", true))
				//.Add(new StackPanel().MakeHorizontal().AddReadOnlyNumericTextBox(System.Text.Encoding.UTF8.GetByteCount(sv).ToString()).AddText("UTF-8 bytes", true))
				//.Add(new StackPanel().MakeHorizontal().AddReadOnlyNumericTextBox(System.Text.Encoding.Default.GetByteCount(sv).ToString()).AddText("System bytes", true))
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(sv.GetHashCode().ToString()).AddText("Hash code", true));
		}

		static string ToBinString(byte[] bytes) {
			using (var sbr = ReusableStringBuilder.AcquireDefault((bytes.Length << 3) + bytes.Length)) {
				var sb = sbr.Resource;
				for (int i = 0; i < bytes.Length; i++) {
					ref var b = ref bytes[i];
					if (b == 0 && sb.Length == 0) {
						continue;
					}
					if (sb.Length > 0) {
						sb.Append(' ');
					}
					sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
				}
				return sb.Length == 0 ? "00000000" : sb.ToString();
			}
		}

		static string ToHexString(byte[] bytes) {
			switch (bytes.Length) {
				case 1: return bytes[0].ToString("X2");
				case 2: return bytes[0].ToString("X2") + bytes[1].ToString("X2");
				case 4:
					return bytes[0].ToString("X2") + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + bytes[3].ToString("X2");
				case 8:
					return bytes[0].ToString("X2") + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + bytes[3].ToString("X2") + " "
						+ bytes[4].ToString("X2") + bytes[5].ToString("X2") + " " + bytes[6].ToString("X2") + bytes[7].ToString("X2");
				default:
					return string.Empty;
			}
		}

		static StackPanel ToUIText(string dec, byte[] bytes) {
			var s = new StackPanel()
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(dec).AddText(" DEC", true))
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(ToHexString(bytes)).AddText(" HEX", true))
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(ToBinString(bytes)).AddText(" BIN", true));
			return s;
		}

		static StackPanel ToUIText(ImmutableArray<SymbolDisplayPart> parts) {
			return ToUIText(parts, null, false, Int32.MinValue);
		}

		static StackPanel ToUIText(ImmutableArray<SymbolDisplayPart> parts, string title, bool bold) {
			return ToUIText(parts, title, bold, Int32.MinValue);
		}

		static StackPanel ToUIText(ImmutableArray<SymbolDisplayPart> parts, string title, bool bold, int argumentIndex) {
			var stack = new StackPanel { Orientation = Orientation.Horizontal };
			if (title != null) {
				stack.AddText(title, bold);
			}
			foreach (var part in parts) {
				switch (part.Kind) {
					case SymbolDisplayPartKind.ClassName:
						stack.AddText(part.Symbol.Name, argumentIndex == Int32.MinValue, false, _ClassBrush);
						break;
					case SymbolDisplayPartKind.EnumName:
						stack.AddText(part.Symbol.Name, argumentIndex == Int32.MinValue, false, _EnumBrush);
						break;
					case SymbolDisplayPartKind.InterfaceName:
						stack.AddText(part.Symbol.Name, argumentIndex == Int32.MinValue, false, _InterfaceBrush);
						break;
					case SymbolDisplayPartKind.MethodName:
						stack.AddText(part.Symbol.Name, argumentIndex != Int32.MinValue, false, _MethodBrush);
						break;
					case SymbolDisplayPartKind.ParameterName:
						if ((part.Symbol as IParameterSymbol).Ordinal == argumentIndex) {
							stack.AddText(part.Symbol.Name, true, true, _ParameterBrush);
						}
						else {
							stack.AddText(part.Symbol.Name, false, false, _ParameterBrush);
						}
						break;
					case SymbolDisplayPartKind.StructName:
						stack.AddText(part.Symbol.Name, argumentIndex == Int32.MinValue, false, _StructBrush);
						break;
					case SymbolDisplayPartKind.DelegateName:
						stack.AddText(part.Symbol.Name, argumentIndex == Int32.MinValue, false, _DelegateBrush);
						break;
					case SymbolDisplayPartKind.StringLiteral:
						stack.AddText(part.ToString(), false, false, _TextBrush);
						break;
					case SymbolDisplayPartKind.Keyword:
						stack.AddText(part.ToString(), false, false, _KeywordBrush);
						break;
					default:
						stack.AddText(part.ToString());
						break;
				}
			}
			return stack;
		}

		static void UpdateSyntaxHighlights(IEditorFormatMap formatMap) {
			System.Diagnostics.Trace.Assert(formatMap != null, "format map is null");
			_InterfaceBrush = GetFormatBrush(Constants.CodeInterfaceName, formatMap);
			_ClassBrush = GetFormatBrush(Constants.CodeClassName, formatMap);
			_TextBrush = GetFormatBrush(Constants.CodeString, formatMap);
			_EnumBrush = GetFormatBrush(Constants.CodeEnumName, formatMap);
			_DelegateBrush = GetFormatBrush(Constants.CodeDelegateName, formatMap);
			_NumberBrush = GetFormatBrush(Constants.CodeNumber, formatMap);
			_StructBrush = GetFormatBrush(Constants.CodeStructName, formatMap);
			_KeywordBrush = GetFormatBrush(Constants.CodeKeyword, formatMap);
			_MethodBrush = GetFormatBrush(Constants.CSharpMethodName, formatMap);
			_ParameterBrush = GetFormatBrush(Constants.CSharpParameterName, formatMap);
		}

		void _ConfigUpdated(object sender, EventArgs e) {
			if (_FormatMap != null) {
				UpdateSyntaxHighlights(_FormatMap);
			}
		}

		StackPanel ShowAttributes(ImmutableArray<AttributeData> attrs, int position) {
			var stack = new StackPanel();
			stack.AddText(attrs.Length > 1 ? "Attributes:" : "Attribute:", true);
			foreach (var item in attrs) {
				var a = item.AttributeClass.Name;
				var attrStack = new StackPanel()
					.MakeHorizontal()
					.AddText("[")
					.AddText(a.EndsWith("Attribute", StringComparison.Ordinal) ? a.Substring(0, a.Length - 9) : a, _ClassBrush)
					.AddText("(");
				int i = 0;
				foreach (var arg in item.ConstructorArguments) {
					if (++i > 1) {
						attrStack.AddText(", ");
					}
					ToUIText(attrStack, arg, position);
				}
				var namedArguments = item.NamedArguments;
				foreach (var arg in namedArguments) {
					if (++i > 1) {
						attrStack.AddText(", ");
					}
					attrStack.AddText(arg.Key + "=", false, true);
					ToUIText(attrStack, arg.Value, position);
				}
				attrStack.AddText(")]");
				stack.Children.Add(attrStack);
			}
			return stack;
		}

		void ShowBaseType(IList<object> qiContent, ITypeSymbol typeSymbol, int position) {
			var baseType = typeSymbol.BaseType;
			if (baseType != null) {
				var name = baseType.Name;
				if (IsCommonClassName(name) == false) {
					var info = ToUIText(baseType.ToMinimalDisplayParts(_SemanticModel, position), "Base type: ", true);
					while (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseTypeInheritence) && (baseType = baseType.BaseType) != null) {
						name = baseType.Name;
						if (IsCommonClassName(name) == false) {
							info.AddText(" - ").AddText(name, _ClassBrush);
						}
					}
					qiContent.Add(info);
				}
			}
		}

		void ShowEnumInfo(IList<object> qiContent, SyntaxNode node, INamedTypeSymbol type, bool fromEnum) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType)) {
				var t = type.EnumUnderlyingType;
				if (t != null) {
					var s = new StackPanel()
						.Add(ToUIText(t.ToMinimalDisplayParts(_SemanticModel, node.SpanStart), "Underlying type: ", true));
					if (fromEnum == false) {
						qiContent.Add(s);
						return;
					}
					var c = 0;
					object min = null, max = null, bits = null;
					IFieldSymbol minName = null, maxName = null;
					var p = 0L;
					foreach (var m in type.GetMembers()) {
						var f = m as IFieldSymbol;
						if (f == null) {
							continue;
						}
						++c;
						var v = f.ConstantValue;
						if (min == null) {
							min = max = bits = v;
							minName = maxName = f;
							continue;
						}
						if (UnsafeArithmeticHelper.IsGreaterThan(v, max)) {
							max = v;
							maxName = f;
						}
						if (UnsafeArithmeticHelper.IsLessThan(v, min)) {
							min = v;
							minName = f;
						}
						bits = UnsafeArithmeticHelper.Or(v, bits);
					}
					if (min == null) {
						return;
					}
					s.Add(new StackPanel().MakeHorizontal().AddText("Enum fields: ", true).AddText(c.ToString()))
						.Add(new StackPanel()
							.MakeHorizontal()
							.AddText("Min: ", true)
							.AddText(min.ToString() + "(")
							.AddText(minName.Name, _EnumBrush)
							.AddText(")"))
						.Add(new StackPanel()
							.MakeHorizontal()
							.AddText("Max: ", true)
							.AddText(max.ToString() + "(")
							.AddText(maxName.Name, _EnumBrush)
							.AddText(")"));
					if (type.GetAttributes().FirstOrDefault(a => a.AttributeClass.ToDisplayString() == "System.FlagsAttribute") != null) {
						s.Add(new StackPanel().MakeHorizontal().AddText("All flags: ", true).AddText(Convert.ToString(Convert.ToInt64(bits), 2)));
					}
					qiContent.Add(s);
				}
			}
		}

		void ShowInterfaces(IList<object> output, ITypeSymbol type, int position) {
			const string SystemDisposable = "IDisposable";
			var showAll = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfacesInheritence);
			var interfaces = showAll ? type.AllInterfaces : type.Interfaces;
			if (interfaces.Length == 0) {
				return;
			}
			var stack = new StackPanel();
			stack.AddText(interfaces.Length > 1 ? "Interfaces:" : "Interface:", true);
			INamedTypeSymbol disposable = null;
			foreach (var item in interfaces) {
				if (item.Name == SystemDisposable) {
					disposable = item;
					continue;
				}
				stack.Children.Add(ToUIText(item.ToMinimalDisplayParts(_SemanticModel, position)));
			}
			if (disposable == null && showAll == false) {
				foreach (var item in type.AllInterfaces) {
					if (item.Name == SystemDisposable) {
						disposable = item;
						break;
					}
				}
			}
			if (disposable != null) {
				stack.Children.Insert(1, ToUIText(disposable.ToMinimalDisplayParts(_SemanticModel, position)));
			}
			output.Add(stack);
		}

		void ShowDeclarationModifier(IList<object> qiContent, ISymbol symbol, string type, int position) {
			var info = new StackPanel().MakeHorizontal().AddText(type, true).AddText(" declaration: ");
			ShowAccessibilityInfo(symbol, info);
			if (symbol.IsAbstract) {
				info.AddText("abstract ", _KeywordBrush);
			}
			else if (symbol.IsStatic) {
				info.AddText("static ", _KeywordBrush);
			}
			else if (symbol.IsVirtual) {
				info.AddText("virtual ", _KeywordBrush);
			}
			else if (symbol.IsOverride) {
				info.AddText(symbol.IsSealed ? "sealed override " : "override ", _KeywordBrush);
				var t = ((symbol as IMethodSymbol) ?? (symbol as IPropertySymbol) ?? (ISymbol)(symbol as IEventSymbol))?.ContainingType;
				if (t != null) {
					info.Add(ToUIText(t.ToMinimalDisplayParts(_SemanticModel, position)));
				}
			}
			else if (symbol.IsSealed) {
				info.AddText("sealed ", _KeywordBrush);
			}
			if (symbol.IsExtern) {
				info.AddText("extern ", _KeywordBrush);
			}
			qiContent.Add(info);
		}

		static void ShowAccessibilityInfo(ISymbol symbol, StackPanel info) {
			switch (symbol.DeclaredAccessibility) {
				case Accessibility.Private: info.AddText("", _KeywordBrush); break;
				case Accessibility.ProtectedAndInternal: info.AddText("protected internal ", _KeywordBrush); break;
				case Accessibility.Protected: info.AddText("protected ", _KeywordBrush); break;
				case Accessibility.Internal: info.AddText("internal ", _KeywordBrush); break;
				case Accessibility.ProtectedOrInternal: info.AddText("protected or internal ", _KeywordBrush); break;
			}
		}

		void ShowParameterInfo(IList<object> qiContent, SyntaxNode node) {
			var argument = node;
			if (node.Kind() == SyntaxKind.NullLiteralExpression) {
				argument = node.Parent;
			}
			int depth = 0;
			do {
				var n = argument as ArgumentSyntax;
				if (n != null) {
					var al = n.Parent as BaseArgumentListSyntax;
					var ap = al.Arguments.IndexOf(n);
					if (ap != -1) {
						var symbol = _SemanticModel.GetSymbolInfo(al.Parent);
						if (symbol.Symbol != null) {
							qiContent.Add(ToUIText(symbol.Symbol.ToMinimalDisplayParts(_SemanticModel, node.SpanStart), "Argument of ", false, ap));
						}
						else if (symbol.CandidateSymbols.Length > 0) {
							qiContent.Add(ToUIText(symbol.CandidateSymbols[0].ToMinimalDisplayParts(_SemanticModel, node.SpanStart), "Maybe argument of ", false, ap));
						}
						else {
							qiContent.Add("Argument " + ap);
						}
					}
					return;
				}
				else if (depth > 3) {
					return;
				}
				++depth;
			} while ((argument = argument.Parent) != null);
		}

		void TextBuffer_Changing(object sender, TextContentChangingEventArgs e) {
			_SemanticModel = null;
		}
		void ToUIText(StackPanel attrStack, TypedConstant v, int position) {
			switch (v.Kind) {
				case TypedConstantKind.Primitive:
					if (v.Value is bool) {
						attrStack.AddText((bool)v.Value ? "true" : "false", _KeywordBrush);
					}
					else {
						attrStack.AddText(v.ToCSharpString(), _NumberBrush);
					}
					break;
				case TypedConstantKind.Enum:
					var en = v.ToCSharpString();
					if (en.IndexOf('|') != -1) {
						var items = v.Type.GetMembers().Where(i => {
							var field = i as IFieldSymbol;
							return field != null
								&& field.HasConstantValue != false
								&& UnsafeArithmeticHelper.Equals(UnsafeArithmeticHelper.And(v.Value, field.ConstantValue), field.ConstantValue)
								&& UnsafeArithmeticHelper.IsZero(field.ConstantValue) == false;
						});
						var flags = items.ToArray();
						for (int i = 0; i < flags.Length; i++) {
							if (i > 0) {
								attrStack.AddText(" | ");
							}
							attrStack.AddText(v.Type.Name + "." + flags[i].Name, _EnumBrush);
						}
					}
					else {
						attrStack.AddText(v.Type.Name + en.Substring(en.LastIndexOf('.')), _EnumBrush);
					}
					break;
				case TypedConstantKind.Type:
					attrStack.AddText("typeof", _KeywordBrush).AddText("(");
					attrStack.Children.Add(ToUIText((v.Value as ITypeSymbol).ToMinimalDisplayParts(_SemanticModel, position)));
					attrStack.AddText(")");
					break;
				case TypedConstantKind.Array:
					attrStack.AddText("{");
					bool c = false;
					foreach (var item in v.Values) {
						if (c == false) {
							c = true;
						}
						else {
							attrStack.AddText(", ");
						}
						ToUIText(attrStack, item, position);
					}
					attrStack.AddText("}");
					break;
				default:
					attrStack.AddText(v.ToCSharpString());
					break;
			}
		}
		enum NumericForm
		{
			None,
			Negative,
			Unsigned
		}

		internal sealed class CSharpQuickInfoController : IIntellisenseController
		{
			CSharpQuickInfoControllerProvider _Provider;
			IQuickInfoSession _Session;
			IList<ITextBuffer> _SubjectBuffers;
			ITextView _TextView;

			internal CSharpQuickInfoController(ITextView textView, IList<ITextBuffer> subjectBuffers, CSharpQuickInfoControllerProvider provider) {
				_TextView = textView;
				_SubjectBuffers = subjectBuffers;
				_Provider = provider;
				_TextView.MouseHover += OnTextViewMouseHover;
			}

			public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) {
			}

			public void Detach(ITextView textView) {
				if (_TextView == textView) {
					_TextView.MouseHover -= OnTextViewMouseHover;
					_TextView = null;
				}
			}

			public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) {
			}

			void OnTextViewMouseHover(object sender, MouseHoverEventArgs e) {
				if (Config.Instance.QuickInfoOptions != QuickInfoOptions.None) {
					return;
				}
				//find the mouse position by mapping down to the subject buffer
				SnapshotPoint? point = _TextView.BufferGraph.MapDownToFirstMatch
					 (new SnapshotPoint(_TextView.TextSnapshot, e.Position),
					PointTrackingMode.Positive,
					snapshot => _SubjectBuffers.Contains(snapshot.TextBuffer),
					PositionAffinity.Predecessor);

				if (point != null && !_Provider.QuickInfoBroker.IsQuickInfoActive(_TextView)) {
					var triggerPoint = point.Value.Snapshot.CreateTrackingPoint(point.Value.Position, PointTrackingMode.Positive);
					_Session = _Provider.QuickInfoBroker.TriggerQuickInfo(_TextView, triggerPoint, true);
				}
			}
		}

		[Export(typeof(IIntellisenseControllerProvider))]
		[Name("C# QuickInfo Controller")]
		[ContentType("CSharp")]
		internal sealed class CSharpQuickInfoControllerProvider : IIntellisenseControllerProvider
		{
			[Import]
			internal IQuickInfoBroker QuickInfoBroker { get; set; }

			public IIntellisenseController TryCreateIntellisenseController(ITextView textView, IList<ITextBuffer> subjectBuffers) {
				return new CSharpQuickInfoController(textView, subjectBuffers, this);
			}
		}

		[Export(typeof(IQuickInfoSourceProvider))]
		[Name("C# QuickInfo Source")]
		[Order(After = "Default Quick Info Presenter")]
		[ContentType("CSharp")]
		internal sealed class CSharpQuickInfoSourceProvider : IQuickInfoSourceProvider
		{
			[Import]
			IEditorFormatMapService _EditorFormatMapService = null;

			[Import]
			internal ITextStructureNavigatorSelectorService _NavigatorService = null;
			[Import]
			internal ITextBufferFactoryService _TextBufferFactoryService = null;

			public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
				return new CSharpQuickInfoSource(this, textBuffer, _EditorFormatMapService);
			}
		}

	}
}
