using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using AppHelpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codist
{
	partial class CodeAnalysisHelper
	{
		/// <summary>
		/// Finds all members defined or referenced in <paramref name="project"/> which may have a parameter that is of or derived from <paramref name="type"/>.
		/// </summary>
		public static async Task<List<ISymbol>> FindInstanceAsParameterAsync(this ITypeSymbol type, Project project, CancellationToken cancellationToken = default) {
			var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
			var members = new List<ISymbol>(10);
			ImmutableArray<IParameterSymbol> parameters;
			var assembly = compilation.Assembly;
			foreach (var typeSymbol in compilation.GlobalNamespace.GetAllTypes(cancellationToken)) {
				foreach (var member in typeSymbol.GetMembers()) {
					if (cancellationToken.IsCancellationRequested) {
						return members;
					}
					if (member.Kind != SymbolKind.Field
						&& member.CanBeReferencedByName
						&& (parameters = member.GetParameters()).IsDefaultOrEmpty == false) {
						if (parameters.Any(p => type.CanConvertTo(p.Type) && p.Type.IsCommonClass() == false)
							&& type.CanAccess(member, assembly)) {

							members.Add(member);
						}
					}
				}
			}
			return members;
		}

		/// <summary>
		/// Finds all members defined or referenced in <paramref name="project"/> which may return an instance of <paramref name="type"/>.
		/// </summary>
		public static async Task<List<ISymbol>> FindSymbolInstanceProducerAsync(this ITypeSymbol type, Project project, CancellationToken cancellationToken = default) {
			var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
			var assembly = compilation.Assembly;
			var members = new List<ISymbol>(10);
			foreach (var typeSymbol in compilation.GlobalNamespace.GetAllTypes(cancellationToken)) {
				foreach (var member in typeSymbol.GetMembers()) {
					if (cancellationToken.IsCancellationRequested) {
						return members;
					}
					ITypeSymbol mt;
					if (member.Kind == SymbolKind.Field) {
						if (member.CanBeReferencedByName
							&& (mt = member.GetReturnType()) != null && (mt.CanConvertTo(type) || (mt as INamedTypeSymbol).ContainsTypeArgument(type))
							&& type.CanAccess(member, assembly)) {
							members.Add(member);
						}
					}
					else if (member.CanBeReferencedByName
						&& ((mt = member.GetReturnType()) != null && (mt.CanConvertTo(type) || (mt as INamedTypeSymbol).ContainsTypeArgument(type))
							|| member.Kind == SymbolKind.Method && member.GetParameters().Any(p => p.Type.CanConvertTo(type) && p.RefKind != RefKind.None))
						&& type.CanAccess(member, assembly)) {
						members.Add(member);
					}
				}
			}
			return members;
		}

		public static async Task<List<INamedTypeSymbol>> FindSubInterfaceAsync(this ITypeSymbol type, Project project, CancellationToken cancellationToken = default) {
			var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
			var r = new List<INamedTypeSymbol>();
			foreach (var item in compilation.GlobalNamespace.GetAllTypes(cancellationToken)) {
				if (item.TypeKind != TypeKind.Interface || item == type) {
					continue;
				}
				var inf = item as INamedTypeSymbol;
				if (inf.AllInterfaces.Contains(type)) {
					r.Add(inf);
				}
			}
			return r;
		}

		public static async Task<List<IMethodSymbol>> FindExtensionMethodsAsync(this ITypeSymbol type, Project project, CancellationToken cancellationToken = default) {
			var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
			var members = new List<IMethodSymbol>(10);
			var isValueType = type.IsValueType;
			foreach (var typeSymbol in compilation.GlobalNamespace.GetAllTypes(cancellationToken)) {
				if (typeSymbol.IsStatic == false || typeSymbol.MightContainExtensionMethods == false) {
					continue;
				}
				foreach (var member in typeSymbol.GetMembers()) {
					if (cancellationToken.IsCancellationRequested) {
						return members;
					}
					if (member.IsStatic == false || member.Kind != SymbolKind.Method) {
						continue;
					}
					var m = (IMethodSymbol)member;
					if (m.IsExtensionMethod == false || m.CanBeReferencedByName == false) {
						continue;
					}
					var p = m.Parameters[0];
					if (type.CanConvertTo(p.Type)) {
						members.Add(m);
						continue;
					}
					if (m.IsGenericMethod == false || p.Type.TypeKind != TypeKind.TypeParameter) {
						continue;
					}
					foreach (var item in m.TypeParameters) {
						if (item != p.Type
							|| item.HasValueTypeConstraint && isValueType == false
							|| item.HasReferenceTypeConstraint && isValueType) {
							continue;
						}
						if (item.HasConstructorConstraint) {

						}
						if (item.ConstraintTypes.Length > 0
							&& item.ConstraintTypes.Any(i => i == type || type.CanConvertTo(i)) == false) {
							continue;
						}
						members.Add(m);
					}
				}
			}
			return members;
		}

		/// <summary>
		/// Finds symbol declarations matching <paramref name="symbolName"/> within given <paramref name="project"/>.
		/// </summary>
		public static async Task<IEnumerable<ISymbol>> FindDeclarationsAsync(this Project project, string symbolName, int resultLimit, bool fullMatch, bool matchCase, SymbolFilter filter = SymbolFilter.All, CancellationToken token = default) {
			var symbols = new SortedSet<ISymbol>(CreateSymbolComparer());
			int maxNameLength = 0;
			var predicate = CreateNameFilter(symbolName, fullMatch, matchCase);

			foreach (var symbol in await SymbolFinder.FindSourceDeclarationsAsync(project, predicate, token).ConfigureAwait(false)) {
				if (symbols.Count < resultLimit) {
					symbols.Add(symbol);
				}
				else {
					maxNameLength = symbols.Max.Name.Length;
					if (symbol.Name.Length < maxNameLength) {
						symbols.Remove(symbols.Max);
						symbols.Add(symbol);
					}
				}
			}
			return symbols;
		}

		public static IQueryable<ISymbol> FindRelatedTypes(this SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken) {
			var result = new Dictionary<ISymbol, int>();
			var activeSyntaxTree = semanticModel.SyntaxTree;
			foreach (var item in node.DescendantNodes()) {
				if (item.IsKind(SyntaxKind.IdentifierName) == false) {
					continue;
				}
				if (cancellationToken.IsCancellationRequested) {
					break;
				}
				var s = semanticModel.GetSymbol(item, cancellationToken);
				if (s != null) {
					if (s.Kind == SymbolKind.NamedType && item.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression)
						|| s.Kind == SymbolKind.Method && ((IMethodSymbol)s).IsExtensionMethod) {
						continue;
					}
					var t = s.ContainingType ?? (s.Kind == SymbolKind.NamedType ? s : null);
					if (t != null) {
						AddResult(result, activeSyntaxTree, t);
					}
					s = s.GetReturnType();
					if (s != null) {
						AddResult(result, activeSyntaxTree, s);
					}
				}
			}
			return result.AsQueryable().OrderByDescending(i => i.Value).Select(i => i.Key);

			void AddResult(Dictionary<ISymbol, int> d, SyntaxTree tree, ISymbol s) {
				foreach (var r in s.DeclaringSyntaxReferences) {
					var st = r.SyntaxTree;
					if (st != tree) {
						d[s] = d.TryGetValue(s, out int i) ? ++i : 1;
					}
				}
			}
		}

		public static IEnumerable<ISymbol> FindDeclarationMatchName(this Compilation compilation, string symbolName, bool fullMatch, bool matchCase, CancellationToken cancellationToken = default) {
			var filter = CreateNameFilter(symbolName, fullMatch, matchCase);
			foreach (var type in compilation.GlobalNamespace.GetAllTypes(cancellationToken)) {
				if (type.IsAccessible(true) == false) {
					continue;
				}
				if (filter(type.Name)) {
					yield return type;
				}
				if (cancellationToken.IsCancellationRequested) {
					break;
				}
				foreach (var member in type.GetMembers()) {
					if (member.Kind != SymbolKind.NamedType
						&& member.CanBeReferencedByName
						&& member.IsAccessible(false)
						&& filter(member.GetOriginalName())) {
						yield return member;
					}
				}
			}
		}

		public static IEnumerable<ISymbol> FindMethodBySignature(this Compilation compilation, ISymbol symbol, bool myCodeOnly, CancellationToken cancellationToken = default) {
			var rt = symbol.GetReturnType();
			var pn = symbol.GetParameters();
			var pl = pn.Length;
			foreach (var type in compilation.GlobalNamespace.GetAllTypes(cancellationToken)) {
				if (myCodeOnly && type.HasSource() == false || type.IsAccessible(true) == false || ReferenceEquals(type, symbol)) {
					continue;
				}
				if (cancellationToken.IsCancellationRequested) {
					break;
				}
				var members = type.TypeKind == TypeKind.Delegate && type.DelegateInvokeMethod != null
					? ImmutableArray.Create<ISymbol>(type.DelegateInvokeMethod)
					: type.GetMembers();
				foreach (var member in members) {
					IMethodSymbol m;
					if (member.Kind != SymbolKind.Method
						|| member.CanBeReferencedByName == false
						|| member.IsAccessible(false) == false
						|| ReferenceEquals(member, symbol)) {
						// also find delegates with the same signature
						if (member.Kind != SymbolKind.NamedType
							|| (m = (member as INamedTypeSymbol)?.DelegateInvokeMethod) == null) {
							continue;
						}
					}
					else {
						m = (IMethodSymbol)member;
					}
					if (AreEqual(rt, m.ReturnType, true) == false) {
						continue;
					}
					var mp = m.Parameters;
					if (mp.Length != pl) {
						continue;
					}
					var pm = true;
					for (int i = pl - 1; i >= 0; i--) {
						if (mp[i].RefKind != pn[i].RefKind
							|| AreEqual(mp[i].Type, pn[i].Type, true) == false) {
							pm = false;
							break;
						}
					}
					if (pm) {
						yield return member;
					}
				}
			}
		}

		/// <summary>Finds symbols referenced by given context node.</summary>
		/// <returns>An ordered array of <see cref="KeyValuePair{TKey, TValue}"/> which contains number of occurrences of corresponding symbols.</returns>
		public static KeyValuePair<ISymbol, int>[] FindReferencingSymbols(this SyntaxNode node, SemanticModel semanticModel, bool sourceCodeOnly) {
			var result = new Dictionary<ISymbol, int>();
			foreach (var item in node.DescendantNodes()) {
				if (item.IsKind(SyntaxKind.IdentifierName) == false
					|| item.Kind().IsDeclaration()) {
					continue;
				}
				var s = semanticModel.GetSymbol(item) ?? semanticModel.GetSymbolExt(item);
				if (s == null) {
					continue;
				}
				switch (s.Kind) {
					case SymbolKind.Parameter:
					case SymbolKind.ArrayType:
					case SymbolKind.PointerType:
					case SymbolKind.TypeParameter:
					case SymbolKind.Namespace:
					case SymbolKind.Local:
					case SymbolKind.Discard:
					case SymbolKind.ErrorType:
					case SymbolKind.DynamicType:
					case SymbolKind.RangeVariable:
					case SymbolKind.NamedType:
						continue;
					case SymbolKind.Method:
						if (((IMethodSymbol)s).MethodKind == MethodKind.AnonymousFunction) {
							continue;
						}
						break;
				}
				if (sourceCodeOnly && s.ContainingAssembly.GetSourceType() == AssemblySource.Metadata) {
					continue;
				}
				var ct = s.ContainingType;
				if (ct != null && (ct.IsTupleType || ct.IsAnonymousType)) {
					continue;
				}
				result[s] = result.TryGetValue(s, out int i) ? ++i : 1;
			}
			var a = result.ToArray();
			Array.Sort(a, (x, y) => {
				var i = y.Value.CompareTo(x.Value);
				return i != 0 ? i
					: (i = String.CompareOrdinal(x.Key.ContainingType?.Name, y.Key.ContainingType?.Name)) != 0 ? i
					: String.CompareOrdinal(x.Key.Name, y.Key.Name);
			});
			return a;
		}

		public static async Task<List<(ISymbol, List<(SymbolUsageKind, ReferenceLocation)>)>> FindReferrersAsync(this ISymbol symbol, Project project, Predicate<ISymbol> definitionFilter = null, Predicate<SyntaxNode> nodeFilter = null, CancellationToken cancellationToken = default) {
			var docs = ImmutableHashSet.CreateRange(project.GetRelatedProjectDocuments());
			var d = new Dictionary<ISymbol, List<(SymbolUsageKind, ReferenceLocation)>>(5);
			var t = symbol as INamedTypeSymbol;
			// fix a problem that FindReferencesAsync returns unbounded references for generic type
			if (t != null && (t.IsGenericType == false || t.IsUnboundGenericType || t == t.OriginalDefinition)) {
				t = null;
			}
			foreach (var sr in await SymbolFinder.FindReferencesAsync(symbol, project.Solution, docs, cancellationToken).ConfigureAwait(false)) {
				if (definitionFilter?.Invoke(sr.Definition) == false) {
					continue;
				}
				await GroupReferenceByContainerAsync(d, sr, t?.ToDisplayString(), nodeFilter, cancellationToken).ConfigureAwait(false);
			}
			var r = new List<(ISymbol container, List<(SymbolUsageKind, ReferenceLocation)>)>(d.Count);
			r.AddRange(d.Select(i => (i.Key, i.Value)));
			r.Sort((x, y) => CompareSymbol(x.container, y.container));
			return r;
		}

		static async Task GroupReferenceByContainerAsync(Dictionary<ISymbol, List<(SymbolUsageKind, ReferenceLocation)>> results, ReferencedSymbol reference, string typeSymbol, Predicate<SyntaxNode> nodeFilter = null, CancellationToken cancellationToken = default) {
			var pu = GetPotentialUsageKinds(reference.Definition);
			foreach (var docRefs in reference.Locations.GroupBy(l => l.Document)) {
				var sm = await docRefs.Key.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
				var r = sm.SyntaxTree.GetCompilationUnitRoot(cancellationToken);
				foreach (var location in docRefs) {
					var n = r.FindNode(location.Location.SourceSpan);
					if (n.Span.Contains(location.Location.SourceSpan.Start) == false || nodeFilter?.Invoke(n) == false) {
						continue;
					}
					var c = n.FirstAncestorOrSelf<SyntaxNode>(i => i.Kind().GetDeclarationCategory().HasAnyFlag(DeclarationCategory.Member | DeclarationCategory.Type));
					ISymbol s;
					if (c == null
						// unfortunately we can't compare the symbol s with the original typeSymbol directly,
						// even though they are actually the same
						|| typeSymbol != null && ((s = sm.GetSymbol(n, cancellationToken)) == null || (s?.ToDisplayString() != typeSymbol))) {
						continue;
					}
					s = sm.GetSymbol(c, cancellationToken);
					if (s == null) {
						continue;
					}
					if (s.Kind == SymbolKind.Method) {
						switch (((IMethodSymbol)s).MethodKind) {
							case MethodKind.AnonymousFunction:
								s = s.ContainingSymbol;
								break;
							case MethodKind.EventAdd:
							case MethodKind.EventRemove:
							case MethodKind.PropertyGet:
							case MethodKind.PropertySet:
								s = ((IMethodSymbol)s).AssociatedSymbol;
								break;
						}
					}
					if (results.TryGetValue(s, out var l)) {
						l.Add((GetUsageKind(pu, n), location));
					}
					else {
						results[s] = new List<(SymbolUsageKind, ReferenceLocation)> { (GetUsageKind(pu, n), location) };
					}
				}
			}
		}

		static SymbolUsageKind GetUsageKind(SymbolUsageKind possibleUsage, SyntaxNode node) {
			if (possibleUsage.MatchFlags(SymbolUsageKind.Write)) {
				var n = node.GetNodePurpose();
				var a = n as AssignmentExpressionSyntax;
				if (a != null && (a.Left == node || a.Left.GetLastIdentifier() == node)) {
					return SymbolUsageKind.Write;
				}
				else {
					if (n.IsKind(SyntaxKind.PostIncrementExpression) || n.IsKind(SyntaxKind.PreIncrementExpression)) {
						return SymbolUsageKind.Write;
					}
					var r = n as ArgumentSyntax;
					if (r != null && (r.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || r.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))) {
						return SymbolUsageKind.Write;
					}
				}
			}
			else if (possibleUsage.MatchFlags(SymbolUsageKind.TypeCast)) {
				node = node.GetNodePurpose();
				if (node.IsKind(SyntaxKind.AsExpression) || node.IsKind(SyntaxKind.IsExpression) || node.IsKind(SyntaxKind.IsPatternExpression) || node.IsKind(SyntaxKind.CastExpression)) {
					return SymbolUsageKind.TypeCast;
				}
				if (possibleUsage.MatchFlags(SymbolUsageKind.Catch)
					&& node.IsKind(SyntaxKind.CatchDeclaration)) {
					return SymbolUsageKind.Catch;
				}
				if (possibleUsage.MatchFlags(SymbolUsageKind.TypeParameter)
					&& (node.IsKind(SyntaxKind.TypeArgumentList) || node.IsKind(SyntaxKind.TypeOfExpression))) {
					return SymbolUsageKind.TypeParameter;
				}
			}
			else if (possibleUsage.HasAnyFlag(SymbolUsageKind.Attach | SymbolUsageKind.Detach)) {
				node = node.GetNodePurpose();
				var a = node as AssignmentExpressionSyntax;
				if (a != null) {
					if (a.IsKind(SyntaxKind.AddAssignmentExpression)) {
						return SymbolUsageKind.Attach;
					}
					else if (a.IsKind(SyntaxKind.SubtractAssignmentExpression)) {
						return SymbolUsageKind.Detach;
					}
				}
			}
			else if (possibleUsage.MatchFlags(SymbolUsageKind.Read)) {
				var n = node.GetNodePurpose();
				if (n.IsKind(SyntaxKind.Argument)) {
					return SymbolUsageKind.Read;
				}
				//var last = node.GetLastAncestorExpressionNode();
				//if (last != null && last.Parent.IsKind(SyntaxKind.Argument) == true && (last == node || last is MemberAccessExpressionSyntax)) {
				//	return SymbolUsageKind.Read;
				//}
				var a = n as AssignmentExpressionSyntax;
				if (a != null && a.Right == node) {
					if (a.IsKind(SyntaxKind.AddAssignmentExpression)) {
						return SymbolUsageKind.Attach;
					}
					else if (a.IsKind(SyntaxKind.SubtractAssignmentExpression)) {
						return SymbolUsageKind.Detach;
					}
				}
			}
			return SymbolUsageKind.Normal;
		}

		static SymbolUsageKind GetPotentialUsageKinds(ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Event:
					return SymbolUsageKind.Attach | SymbolUsageKind.Detach;
				case SymbolKind.Field:
					return ((IFieldSymbol)symbol).IsConst ? SymbolUsageKind.Normal : SymbolUsageKind.Write;
				case SymbolKind.Local:
				case SymbolKind.Property:
					return SymbolUsageKind.Write;
				case SymbolKind.NamedType:
					var t = (INamedTypeSymbol)symbol;
					switch (t.TypeKind) {
						case TypeKind.Class:
							return t.Name?.EndsWith("Exception", StringComparison.Ordinal) == true
								? SymbolUsageKind.Catch | SymbolUsageKind.TypeCast | SymbolUsageKind.TypeParameter
								: SymbolUsageKind.TypeCast | SymbolUsageKind.TypeParameter;
					}
					return SymbolUsageKind.TypeCast | SymbolUsageKind.TypeParameter;
				case SymbolKind.Method:
					return SymbolUsageKind.Read;
				default:
					return SymbolUsageKind.Normal;
			}
		}

		static Comparer<ISymbol> CreateSymbolComparer() {
			return Comparer<ISymbol>.Create((x, y) => {
				var l = x.Name.Length - y.Name.Length;
				return l != 0 ? l : x.GetHashCode() - y.GetHashCode();
			});
		}

		static Func<string, bool> CreateNameFilter(string symbolName, bool fullMatch, bool matchCase) {
			if (fullMatch) {
				if (matchCase) {
					return name => name == symbolName;
				}
				else {
					return name => String.Equals(name, symbolName, StringComparison.OrdinalIgnoreCase);
				}
			}
			else {
				if (matchCase) {
					return name => name.IndexOf(symbolName, StringComparison.Ordinal) != -1;
				}
				else {
					return name => name.IndexOf(symbolName, StringComparison.OrdinalIgnoreCase) != -1;
				}
			}
		}
	}

	[Flags]
	enum SymbolUsageKind
	{
		Normal,
		External = 1,
		Container = 1 << 1,
		Read = 1 << 2,
		Write = 1 << 3,
		Attach = 1 << 5,
		Detach = 1 << 6,
		TypeCast = 1 << 7,
		TypeParameter = 1 << 8,
		Catch = 1 << 9,
	}
}
