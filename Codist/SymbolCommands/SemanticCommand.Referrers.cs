using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using R = Codist.Properties.Resources;

namespace Codist.SymbolCommands
{
	abstract class ReferrerAnalysisCommandBase : AnalysisListCommandBase<List<(ISymbol, List<(SymbolUsageKind, ReferenceLocation)>)>>
	{
		static readonly OptionDescriptor[] __Options = [
			PredefinedOptionDescriptors.ExtractMatch,
			PredefinedOptionDescriptors.MatchTypeArgument,
			PredefinedOptionDescriptors.CurrentFileScope,
			PredefinedOptionDescriptors.CurrentProjectScope,
			PredefinedOptionDescriptors.RelatedProjectsScope,
		];

		string _ResultLabel;
		protected override string ResultLabel => _ResultLabel;
		protected override bool UseCtrlRestriction => true;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;

		public override void UpdateList(SymbolMenu resultList, List<(ISymbol, List<(SymbolUsageKind, ReferenceLocation)>)> data) {
			var referrers = data;
			if (referrers == null) {
				_ResultLabel = R.T_Referrers + "0";
				return;
			}

			resultList.ContainerType = SymbolListType.SymbolReferrers;
			resultList.ExtIconProvider = ExtIconProvider.Default.GetExtIconsWithUsage;

			var containerType = Symbol.ContainingType;
			foreach (var (referrer, occurrence) in referrers) {
				var i = resultList.Add(referrer, false);
				i.Location = occurrence.FirstOrDefault().Item2.Location;
				foreach (var item in occurrence) {
					i.Usage |= item.Item1;
				}
				if (referrer.ContainingType != containerType) {
					i.Hint = (referrer.ContainingType ?? referrer).ToDisplayString(CodeAnalysisHelper.MemberNameFormat);
				}
				if (occurrence.Count > 1) {
					i.Hint += " @" + occurrence.Count;
				}
			}

			_ResultLabel = R.T_Referrers + referrers.Count.ToText();
		}

		protected static bool IsTypeReference(SyntaxNode node) {
			var p = node.Parent.UnqualifyExceptNamespace();
			switch (p.Kind()) {
				case SyntaxKind.TypeOfExpression:
				case SyntaxKind.SimpleMemberAccessExpression:
				case SyntaxKind.CatchDeclaration:
				case SyntaxKind.CastExpression:
				case SyntaxKind.IsExpression:
				case SyntaxKind.IsPatternExpression:
				case SyntaxKind.AsExpression:
				case SyntaxKind.InvocationExpression:
					return true;
				case SyntaxKind.GenericName:
					return p.Parent.IsKind(SyntaxKind.ObjectCreationExpression) || IsTypeReference(p);
				case SyntaxKind.TypeArgumentList:
					p = p.Parent;
					goto case SyntaxKind.GenericName;
				case SyntaxKind.QualifiedName:
					return IsTypeReference(p);
				case SyntaxKind.DeclarationPattern:
					return p.Parent.IsAnyKind(SyntaxKind.IsPatternExpression, SyntaxKind.CasePatternSwitchLabel);
			}
			return false;
		}
	}

	sealed class FindReferrersCommand : ReferrerAnalysisCommandBase
	{
		bool _MatchTypeArgument;

		public override int ImageId => IconIds.FindReferrers;
		public override string Title => R.CMD_FindReferrers;
		public override string Description => R.CMDT_FindReferrers;
		protected override ISymbol ResultSymbol => _MatchTypeArgument ? Symbol : Symbol.OriginalDefinition;

		public override Task<List<(ISymbol, List<(SymbolUsageKind, ReferenceLocation)>)>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var docs = MakeDocumentListFromOption(Options);
			_MatchTypeArgument = MatchTypeArgument;
			Predicate<ISymbol> df = StrictMatch ? (Symbol is IMethodSymbol ms && ms.MethodKind == MethodKind.ReducedExtension ? ms.ReducedFrom : Symbol).Equals : default;
			Predicate<ISymbol> of = _MatchTypeArgument ? Symbol.Equals : null;
			return Symbol.FindReferrersAsync(Context.Document.Project, docs, df, of, null, cancellationToken);
		}
	}

	sealed class FindConstructorReferrersCommand : ReferrerAnalysisCommandBase
	{
		static readonly OptionDescriptor[] __Options = [
			new OptionDescriptor(IconIds.EditMatches, CommandOptions.ExtractMatch, R.CMDT_FindDirectCallers),
			PredefinedOptionDescriptors.MatchTypeArgument,
			PredefinedOptionDescriptors.CurrentFileScope,
			PredefinedOptionDescriptors.CurrentProjectScope,
			PredefinedOptionDescriptors.RelatedProjectsScope
		];

		bool _MatchTypeArgument;

		public override int ImageId => IconIds.FindReferrers;
		public override string Title => R.CMD_FindCallers;
		public override string Description => R.CMDT_FindCallers;
		public override IEnumerable<OptionDescriptor> OptionDescriptors => __Options;
		protected override ISymbol ResultSymbol => _MatchTypeArgument ? Symbol : Symbol.OriginalDefinition;

		public override Task<List<(ISymbol, List<(SymbolUsageKind, ReferenceLocation)>)>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var docs = MakeDocumentListFromOption(Options);
			_MatchTypeArgument = MatchTypeArgument;
			var symbol = Context.SemanticModel.GetSymbolOrFirstCandidate(Node.GetObjectCreationNode(), cancellationToken);

			Predicate<ISymbol> df = StrictMatch ? (symbol is IMethodSymbol ms && ms.MethodKind == MethodKind.ReducedExtension ? ms.ReducedFrom : symbol).Equals : default;
			Predicate<ISymbol> of = _MatchTypeArgument ? symbol.Equals : null;
			return symbol.FindReferrersAsync(Context.Document.Project, docs, df, of, null, cancellationToken);
		}
	}

	sealed class FindObjectInitializersCommand : ReferrerAnalysisCommandBase
	{
		public override int ImageId => IconIds.FindReferrers;
		public override string Title => R.CMD_FindConstructorCallers;
		public override string Description => R.CMDT_FindConstructorCallers;

		public override Task<List<(ISymbol, List<(SymbolUsageKind, ReferenceLocation)>)>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var docs = MakeDocumentListFromOption(Options);

			Predicate<ISymbol> defFilter = StrictMatch ? Symbol.Equals : default;
			Predicate<ISymbol> symbolFilter;

			if (Symbol is INamedTypeSymbol typeSymbol && typeSymbol.GetPrimaryConstructor() != null) {
				Predicate<SyntaxNode> nodeFilter = n => !IsTypeReference(n);
				symbolFilter = MatchTypeArgument ? Symbol.Equals : null;
				return Symbol.FindReferrersAsync(Context.Document.Project, docs, defFilter, symbolFilter, nodeFilter, cancellationToken);
			}
			else {
				symbolFilter = s => s.Kind == SymbolKind.Method;
				return Symbol.FindReferrersAsync(Context.Document.Project, docs, defFilter, symbolFilter, null, cancellationToken);
			}
		}
	}

	sealed class FindTypeReferrersCommand : ReferrerAnalysisCommandBase
	{

		public override int ImageId => IconIds.FindTypeReferrers;
		public override string Title => R.CMD_FindTypeReferrers;
		public override string Description => R.CMDT_FindTypeReferrers;
		protected override ISymbol ResultSymbol => Symbol.Kind == SymbolKind.Method ? Symbol.ContainingType : Symbol;

		public override Task<List<(ISymbol, List<(SymbolUsageKind, ReferenceLocation)>)>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var docs = MakeDocumentListFromOption(Options);
			var targetSymbol = ResultSymbol;

			Predicate<ISymbol> df = StrictMatch ? targetSymbol.Equals : default;
			Predicate<ISymbol> of = MatchTypeArgument ? targetSymbol.Equals : null;
			Predicate<SyntaxNode> nodeFilter = IsTypeReference;

			return targetSymbol.FindReferrersAsync(Context.Document.Project, docs, df, of, nodeFilter, cancellationToken);
		}
	}
}
