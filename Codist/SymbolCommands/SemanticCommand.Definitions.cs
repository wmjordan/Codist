using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Utilities;
using R = Codist.Properties.Resources;

namespace Codist.SymbolCommands
{
	sealed class CopySymbolCommand : SemanticCommandBase
	{
		public override int ImageId => IconIds.Copy;
		public override string Title => R.CMD_CopySymbol;
		public override string Description => R.CMDT_CopySymbol;
		public override bool CanRefresh => false;

		public override IEnumerable<SemanticCommandBase> GetSubCommands() {
			if (Symbol is IFieldSymbol f && f.ConstantValue != null) {
				yield return new CopyConstantValueCommand { Value = f.ConstantValue };
			}
			yield return new CopyTypeQualifiedSymbolNameCommand { Symbol = Symbol, Context = Context };
			if (Symbol != null) {
				if (Symbol.IsQualifiable()) {
					yield return new CopyFullyQualifiedSymbolNameCommand { Symbol = Symbol, Context = Context };
				}
				if (Symbol.Kind != SymbolKind.Namespace) {
					yield return new CopySymbolDefinitionCommand { Symbol = Symbol, Context = Context };
				}
			}
		}

		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			if (Options.MatchFlags(CommandOptions.Alternative)) {
				CopyQualifiedSymbolName(Symbol, true);
			}
			else {
				TryCopy(Symbol.GetOriginalName());
			}
		}

		static void TryCopy(string content) {
			try {
				System.Windows.Clipboard.SetDataObject(content);
			}
			catch (SystemException) {
				// ignore failure
			}
		}

		static void CopyQualifiedSymbolName(ISymbol symbol, bool fullyQualified) {
			var s = symbol.OriginalDefinition;
			string t;
			switch (s.Kind) {
				case SymbolKind.Namespace:
				case SymbolKind.NamedType:
					t = s.ToDisplayString(CodeAnalysisHelper.QualifiedTypeNameFormat);
					break;
				case SymbolKind.Method:
					var m = s as IMethodSymbol;
					if (m.ReducedFrom != null) {
						s = m.ReducedFrom;
					}
					if (m.MethodKind == MethodKind.Constructor) {
						s = m.ContainingType;
						goto case SymbolKind.NamedType;
					}
					else if (m.MethodKind == MethodKind.ExplicitInterfaceImplementation) {
						t = m.Name;
						break;
					}
					goto default;
				default:
					t = s.ToDisplayString(fullyQualified ? CodeAnalysisHelper.QualifiedTypeNameFormat : CodeAnalysisHelper.TypeMemberNameFormat);
					break;
			}
			TryCopy(t);
		}

		sealed class CopyTypeQualifiedSymbolNameCommand : SemanticCommandBase
		{
			public override int ImageId => IconIds.Class;
			public override string Title => R.CMDT_CopyQualifiedName;
			public override string Description => R.CMDT_CopyQualifiedName;

			public override async Task ExecuteAsync(CancellationToken cancellationToken) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				CopyQualifiedSymbolName(Symbol, false);
			}
		}
		sealed class CopyFullyQualifiedSymbolNameCommand : SemanticCommandBase
		{
			public override int ImageId => IconIds.Namespace;
			public override string Title => R.CMDT_CopyFullyQualifiedName;
			public override string Description => R.CMDT_CopyFullyQualifiedName;

			public override async Task ExecuteAsync(CancellationToken cancellationToken) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				CopyQualifiedSymbolName(Symbol, true);
			}
		}
		sealed class CopySymbolDefinitionCommand : SemanticCommandBase
		{
			public override int ImageId => IconIds.Definition;
			public override string Title => R.CMDT_CopyDefinition;
			public override string Description => R.CMDT_CopyDefinition;
			public override bool CanRefresh => true;

			public override async Task ExecuteAsync(CancellationToken cancellationToken) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				TryCopy(SymbolDefinitionFormatter.GetDefinition(Symbol.OriginalDefinition, Context.SemanticModel, CodeAnalysisHelper.DefinitionNameFormat, !UIHelper.IsCtrlDown));
			}
		}
		sealed class CopyConstantValueCommand : SemanticCommandBase
		{
			public override int ImageId => IconIds.Constant;
			public override string Title => R.CMD_CopyConstantValue;
			public override string Description => R.CMD_CopyConstantValue;
			public object Value { get; set; }
			public override async Task ExecuteAsync(CancellationToken cancellationToken) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				TryCopy(Value?.ToString() ?? "null");
			}
		}
	}

	sealed class GotoDefinitionCommand : SemanticCommandBase
	{
		public override int ImageId => IconIds.GoToDefinition;
		public override string Title => R.CMD_GoToDefinition;

		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			var locs = Symbol.GetSourceReferences();
			if (locs.Length == 1) {
				locs[0].GoToSource();
			}
			else {
				new SymbolCommands.ListSymbolLocationsCommand { Symbol = Symbol, Context = Context }.Show(locs);
			}
		}
	}

	class GoToReturnTypeDefinitionCommand : SemanticCommandBase
	{
		public override int ImageId => IconIds.GoToReturnType;
		public override string Title => R.CMD_GoTo;
		public override string Description => R.CMDT_GoToTypeDefinition;

		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			Symbol.GetReturnType().ResolveElementType().GoToSource();
		}
	}

	sealed class GoToSpecialGenericSymbolReturnTypeCommand : GoToReturnTypeDefinitionCommand
	{
		public override async Task ExecuteAsync(CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			Symbol.GetReturnType().ResolveElementType().ResolveSingleGenericTypeArgument().GoToSource();
		}
	}

	sealed class SymbolDefinitionFormatter
	{
		readonly SemanticModel _SemanticModel;
		readonly SymbolDisplayFormat _DisplayFormat;
		readonly bool _IncludeXmlDoc;

		SymbolDefinitionFormatter(SemanticModel semanticModel, SymbolDisplayFormat displayFormat, bool includeXmlDoc) {
			_SemanticModel = semanticModel;
			_DisplayFormat = displayFormat;
			_IncludeXmlDoc = includeXmlDoc;
		}

		public static string GetDefinition(ISymbol symbol, SemanticModel semanticModel, SymbolDisplayFormat displayFormat, bool includeXmlDoc) {
			if (symbol.Kind == SymbolKind.NamedType) {
				return new SymbolDefinitionFormatter(semanticModel, displayFormat, includeXmlDoc)
					.GetDefinition((INamedTypeSymbol)symbol);
			}

			if (includeXmlDoc) {
				using var sbr = ReusableStringBuilder.AcquireDefault(100);
				AppendXmlDoc(symbol, semanticModel, sbr.Resource, 0);
				return sbr.Resource.Append(symbol.ToDisplayString(displayFormat)).ToString();
			}

			return symbol.ToDisplayString(displayFormat);
		}

		string GetDefinition(INamedTypeSymbol type) {
			using var sbr = ReusableStringBuilder.AcquireDefault(100);
			switch (type.TypeKind) {
				case TypeKind.Dynamic:
				case TypeKind.Enum:
				case TypeKind.Interface:
				case TypeKind.Struct:
				case TypeKind.Class:
					GetTypeDefinition(sbr.Resource, type, 0);
					return sbr.Resource.ToString();
				default:
					if (_IncludeXmlDoc) {
						AppendXmlDoc(type, _SemanticModel, sbr.Resource, 0);
					}
					return sbr.Resource.Append(type.ToDisplayString(_DisplayFormat)).ToString();
			}
		}

		void GetTypeDefinition(StringBuilder sb, INamedTypeSymbol t, int indent) {
			if (_IncludeXmlDoc) {
				AppendXmlDoc(t, _SemanticModel, sb, indent);
			}
			sb.Append('\t', indent);
			if (t.TypeKind == TypeKind.Delegate) {
				sb.Append(t.ToDisplayString(_DisplayFormat))
					.Append(';')
					.AppendLine();
				return;
			}
			if (t.ContainingType != null && t.DeclaredAccessibility != Accessibility.Private) {
				sb.Append(t.GetAccessibility());
			}
			sb.Append(t.ToDisplayString(_DisplayFormat));
			GetBaseTypeList(sb, t, _DisplayFormat);
			sb.AppendLine(" {");
			indent++;
			foreach (var member in t.GetMembers()) {
				if (member.DeclaredAccessibility == Accessibility.Private
					|| member.IsCompilerGenerated()
					|| !member.CanBeReferencedByName
						&& (member is not IMethodSymbol m || !m.MethodKind.CeqAny(MethodKind.Constructor, MethodKind.StaticConstructor, MethodKind.Destructor))
						&& member.GetExplicitInterfaceImplementations().Count == 0) {
					continue;
				}

				if (member.Kind == SymbolKind.NamedType) {
					GetTypeDefinition(sb, member as INamedTypeSymbol, indent);
					continue;
				}

				if (_IncludeXmlDoc) {
					AppendXmlDoc(member, _SemanticModel, sb, indent);
				}
				sb.Append('\t', indent)
					.Append(member.ToDisplayString(_DisplayFormat));
				if (member.Kind != SymbolKind.Property) {
					sb.Append(';');
				}
				sb.AppendLine();
			}
			sb.Append('\t', indent - 1).Append('}').AppendLine().ToString();
		}

		static void GetBaseTypeList(StringBuilder sb, INamedTypeSymbol t, SymbolDisplayFormat format) {
			INamedTypeSymbol baseType;
			if (t.TypeKind == TypeKind.Enum) {
				baseType = t.EnumUnderlyingType;
			}
			else {
				baseType = t.BaseType;
				if (baseType?.SpecialType == SpecialType.System_Object) {
					baseType = null;
				}
			}
			var interfaces = t.Interfaces;
			if (baseType != null || interfaces.Length != 0) {
				var typeFormat = format.WithKindOptions(SymbolDisplayKindOptions.None);
				sb.Append(" : ");
				if (baseType != null) {
					sb.Append(baseType.ToDisplayString(typeFormat));
					if (interfaces.Length != 0) {
						sb.Append(", ");
					}
				}
				for (int i = 0; i < interfaces.Length; i++) {
					if (i != 0) {
						sb.Append(", ");
					}
					sb.Append(interfaces[i].ToDisplayString(typeFormat));
				}
			}
		}

		static IEnumerable<string> FormatToSourceLikeComment(string xml, SemanticModel sm) {
			var xdoc = XDocument.Parse(xml);
			var memberElement = xdoc.Root;
			string line;

			foreach (var element in memberElement.Descendants()) {
				var crefAttr = element.Attribute("cref");
				if (crefAttr is null) {
					continue;
				}
				var crefValue = crefAttr.Value;
				if (String.IsNullOrEmpty(crefValue)) {
					continue;
				}
				var resolvedSymbol = DocumentationCommentId.GetSymbolsForDeclarationId(crefValue, sm.Compilation).FirstOrDefault();
				crefValue = resolvedSymbol != null
					? resolvedSymbol.ToDisplayString(CodeAnalysisHelper.TypeMemberNameFormat)
					: CleanUpUnresolvedCref(crefValue);
				crefAttr.Value = crefValue.Replace('<', '{').Replace('>', '}');
			}

			foreach (var item in memberElement.Elements().Select(n => n.ToString())) {
				using var sr = new StringReader(item);
				while (!String.IsNullOrEmpty(line = sr.ReadLine())) {
					yield return line.Trim();
				}
			}
		}

		static void AppendXmlDoc(ISymbol s, SemanticModel sm, StringBuilder sb, int indent) {
			var xml = s.GetDocumentationCommentXml();
			if (!String.IsNullOrEmpty(xml)) {
				foreach (var line in FormatToSourceLikeComment(xml, sm)) {
					sb.Append('\t', indent)
						.Append("/// ")
						.AppendLine(line);
				}
			}
		}

		static string CleanUpUnresolvedCref(string crefValue) {
			if (crefValue.Length > 2 && crefValue[1] == ':') {
				crefValue = crefValue.Substring(2);
			}
			int lastDot = crefValue.LastIndexOf('.');
			if (lastDot > 0 && lastDot < crefValue.Length - 1) {
				return crefValue.Substring(lastDot + 1);
			}
			return crefValue;
		}
	}
}
