using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using CLR;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codist
{
	partial class CodeAnalysisHelper
	{
		public static ImmutableArray<(string type, IImmutableList<ISymbol> members)> FindMembers(this ISymbol symbol) {
			var r = ImmutableArray.CreateBuilder<(string type, IImmutableList<ISymbol> members)>();
			r.Add((null, ListMembersByOrder(symbol)));
			if (symbol is INamedTypeSymbol type) {
				switch (type.TypeKind) {
					case TypeKind.Class:
						while ((type = type.BaseType) != null && type.IsCommonBaseType() == false) {
							r.Add((type.ToDisplayString(MemberNameFormat), ListMembersByOrder(type)));
						}
						break;
					case TypeKind.Interface:
						foreach (var item in type.AllInterfaces) {
							r.Add((item.ToDisplayString(MemberNameFormat), ListMembersByOrder(item)));
						}
						break;
				}
			}
			return r.ToImmutable();

			IImmutableList<ISymbol> ListMembersByOrder(ISymbol source) {
				var nsOrType = source as INamespaceOrTypeSymbol;
				var members = nsOrType.FindMembers().ToImmutableArray();
				if (source.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)source).TypeKind == TypeKind.Enum) {
					// sort enum members by value
					return members.Sort(CompareByFieldIntegerConst);
				}
				else {
					return members.Sort(CompareByAccessibilityKindName);
				}
			}
		}

		public static IEnumerable<ISymbol> FindMembers(this INamespaceOrTypeSymbol type) {
			return type.GetMembers().Where(m => {
				if (m.IsImplicitlyDeclared) {
					return false;
				}
				if (m.Kind == SymbolKind.Method) {
					var ms = (IMethodSymbol)m;
					if (ms.AssociatedSymbol != null) {
						return false;
					}
					switch (ms.MethodKind) {
						case MethodKind.PropertyGet:
						case MethodKind.PropertySet:
						case MethodKind.EventAdd:
						case MethodKind.EventRemove:
							return false;
					}
				}
				return true;
			});
		}

		/// <summary>
		/// Finds all members defined or referenced in <paramref name="project"/> which may have a parameter that is of or derived from <paramref name="type"/>.
		/// </summary>
		public static async Task<List<ISymbol>> FindInstanceAsParameterAsync(this ITypeSymbol type, Project project, bool strictMatch, CancellationToken cancellationToken = default) {
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
						&& (parameters = member.GetParameters()).IsDefaultOrEmpty == false
						&& parameters.Any(strictMatch
								? (Func<IParameterSymbol, bool>)(p => p.Type == type)
								: (p => type.CanConvertTo(p.Type) && p.Type.IsCommonBaseType() == false))
							&& type.CanAccess(member, assembly)) {
						members.Add(member);
					}
				}
			}
			return members;
		}

		/// <summary>
		/// Finds all members defined or referenced in <paramref name="project"/> which may return an instance of <paramref name="type"/>.
		/// </summary>
		public static async Task<List<ISymbol>> FindSymbolInstanceProducerAsync(this ITypeSymbol type, Project project, bool strict, CancellationToken cancellationToken = default) {
			var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
			var assembly = compilation.Assembly;
			var members = new List<ISymbol>(10);
			var paramComparer = strict
				? (Func<IParameterSymbol, bool>)(p => p.Type == type && p.RefKind != RefKind.None)
				: (p => p.Type.CanConvertTo(type) && p.RefKind != RefKind.None);
			foreach (var typeSymbol in compilation.GlobalNamespace.GetAllTypes(cancellationToken)) {
				foreach (var member in typeSymbol.GetMembers()) {
					if (cancellationToken.IsCancellationRequested) {
						return members;
					}
					ITypeSymbol mt;
					if (member.Kind == SymbolKind.Field) {
						if (member.CanBeReferencedByName
							&& (mt = member.GetReturnType()) != null && (mt == type || strict == false && mt.CanConvertTo(type) || (mt as INamedTypeSymbol).ContainsTypeArgument(type))
							&& type.CanAccess(member, assembly)) {
							members.Add(member);
						}
					}
					else if (member.CanBeReferencedByName
						&& ((mt = member.GetReturnType()) != null && (mt == type || strict == false && mt.CanConvertTo(type) || (mt as INamedTypeSymbol).ContainsTypeArgument(type))
							|| member.Kind == SymbolKind.Method && member.GetParameters().Any(paramComparer))
						&& type.CanAccess(member, assembly)) {
						members.Add(member);
					}
				}
			}
			return members;
		}

		/// <summary>Returns interfaces derived from the given interface <paramref name="type"/> in specific <paramref name="project"/>.</summary>
		public static async Task<List<INamedTypeSymbol>> FindDerivedInterfacesAsync(this INamedTypeSymbol type, Project project, CancellationToken cancellationToken = default) {
			var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
			var r = new List<INamedTypeSymbol>(7);
			var d = new SourceSymbolDeduper();
			foreach (var item in compilation.GlobalNamespace.GetAllTypes(cancellationToken)) {
				if (item.TypeKind == TypeKind.Interface
					&& item != type
					&& item.AllInterfaces.Contains(type)
					&& d.TryAdd(item)) {
					r.Add(item);
				}
			}
			return r;
		}

		public static async Task<List<IMethodSymbol>> FindExtensionMethodsAsync(this ITypeSymbol type, Project project, bool strict, CancellationToken cancellationToken = default) {
			var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
			var members = new List<IMethodSymbol>(10);
			var isValueType = type.IsValueType;
			var d = new SourceSymbolDeduper();
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
					if (type.CanConvertTo(p.Type) && d.TryAdd(m)) {
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
						var constraintTypes = item.ConstraintTypes;
						if (constraintTypes.Length == 0) {
							if (strict) {
								continue;
							}
						}
						else if (constraintTypes.Any(i => i == type || type.CanConvertTo(i)) == false) {
							continue;
						}

						if (d.TryAdd(m)) {
							members.Add(m);
						}
					}
				}
			}
			return members;
		}

		/// <summary>
		/// Finds symbol declarations matching <paramref name="keywords"/> within given <paramref name="project"/>.
		/// </summary>
		public static async Task<IReadOnlyCollection<ISymbol>> FindDeclarationsAsync(this Project project, string keywords, int resultLimit, bool fullMatch, bool matchCase, CancellationToken token = default) {
			var symbols = new SortedSet<ISymbol>(CreateSymbolComparer());
			int maxNameLength = 0;
			var predicate = CreateNameFilter(keywords, fullMatch, matchCase);
			var d = new SourceSymbolDeduper();

			foreach (var symbol in await SymbolFinder.FindSourceDeclarationsAsync(project, predicate, token).ConfigureAwait(false)) {
				if (symbols.Count < resultLimit) {
					if (d.TryAdd(symbol)) {
						symbols.Add(symbol);
					}
				}
				else {
					maxNameLength = symbols.Max.Name.Length;
					if (symbol.Name.Length < maxNameLength) {
						symbols.Remove(symbols.Max);
						if (d.TryAdd(symbol)) {
							symbols.Add(symbol);
						}
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

		public static IEnumerable<ISymbol> FindDeclarationMatchName(this Compilation compilation, string keywords, bool fullMatch, bool matchCase, CancellationToken cancellationToken = default) {
			var filter = CreateNameFilter(keywords, fullMatch, matchCase);
			var d = new SourceSymbolDeduper();
			foreach (var type in compilation.GlobalNamespace.GetAllTypes(cancellationToken)) {
				if (type.IsAccessible(true) == false) {
					continue;
				}
				if (filter(type.Name) && d.TryAdd(type)) {
					yield return type;
				}
				if (cancellationToken.IsCancellationRequested) {
					break;
				}
				foreach (var member in type.GetMembers()) {
					if (member.Kind != SymbolKind.NamedType
						&& member.CanBeReferencedByName
						&& member.IsAccessible(false)
						&& filter(member.GetOriginalName())
						&& d.TryAdd(member)) {
						yield return member;
					}
				}
			}
		}

		public static IEnumerable<ISymbol> FindMethodBySignature(this Compilation compilation, ISymbol symbol, bool myCodeOnly, CancellationToken cancellationToken = default) {
			var rt = symbol.GetReturnType();
			var pn = symbol.GetParameters();
			var pl = pn.Length;
			var d = new SourceSymbolDeduper();
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
					if (pm && d.TryAdd(member)) {
						yield return member;
					}
				}
			}
		}

		/// <summary>Finds namespaces in related projects having the same fully qualified name.</summary>
		/// <returns>Namespaces having the same name in current solution</returns>
		public static async Task<ImmutableArray<INamespaceSymbol>> FindSimilarNamespacesAsync(this INamespaceSymbol symbol, Project project, CancellationToken cancellationToken = default) {
			var r = ImmutableArray.CreateBuilder<INamespaceSymbol>();
			if (symbol.IsGlobalNamespace) {
				foreach (var p in GetRelatedProjects(project)) {
					if (p.SupportsCompilation == false) {
						continue;
					}
					var n = (await p.GetCompilationAsync(cancellationToken).ConfigureAwait(false)).GlobalNamespace;
					if (n != null) {
						r.Add(n);
					}
				}
				return r.ToImmutable();
			}
			var ns = ImmutableArray.CreateBuilder<string>();
			do {
				ns.Add(symbol.Name);
			} while ((symbol = symbol.ContainingNamespace) != null && symbol.IsGlobalNamespace == false);
			ns.Reverse();
			foreach (var p in GetRelatedProjects(project)) {
				if (p.SupportsCompilation == false) {
					continue;
				}
				var n = (await p.GetCompilationAsync(cancellationToken)).GlobalNamespace;
				foreach (var item in ns) {
					if ((n = n.GetNamespaceMembers().FirstOrDefault(m => m.Name == item)) == null) {
						break;
					}
				}
				if (n != null) {
					r.Add(n);
				}
			}
			return r.ToImmutable();
		}

		/// <summary>Finds symbols referenced by given context node.</summary>
		/// <returns>An ordered array of <see cref="KeyValuePair{TKey, TValue}"/> which contains number of occurrences of corresponding symbols.</returns>
		public static KeyValuePair<ISymbol, int>[] FindReferencingSymbols(this SyntaxNode node, SemanticModel semanticModel, bool sourceCodeOnly, CancellationToken cancellationToken = default) {
			var result = new Dictionary<ISymbol, int>();
			foreach (var item in node.DescendantNodes()) {
				if (item.IsKind(SyntaxKind.IdentifierName) == false
					|| item.Kind().IsDeclaration()) {
					continue;
				}
				var s = semanticModel.GetSymbol(item, cancellationToken) ?? semanticModel.GetSymbolExt(item, cancellationToken);
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
			// hack: fix FindReferencesAsync returning unbounded references for generic type or method
			string sign = null;
			Predicate<SymbolUsageKind> usageFilter = null;
			switch (symbol.Kind) {
				case SymbolKind.NamedType:
					// hack: In VS 2017 with Roslyn 2.10, we don't need this,
					//       but in VS 2019, we have to do that, otherwise we will get nothing.
					//       The same to SymbolKind.Method.
					if ((symbol as INamedTypeSymbol).IsBoundedGenericType()
						|| symbol.GetContainingTypes().Any(t => t.IsBoundedGenericType())) {
						sign = symbol.ToDisplayString();
						symbol = symbol.OriginalDefinition;
					}
					break;
				case SymbolKind.Method:
					var m = symbol as IMethodSymbol;
					if (m.IsBoundedGenericMethod() || m.GetContainingTypes().Any(t => t.IsBoundedGenericType())) {
						sign = symbol.ToDisplayString();
						symbol = symbol.OriginalDefinition;
					}
					else if (m.IsExtensionMethod) {
						symbol = m.ReducedFrom ?? m;
					}
					else if (m.MethodKind == MethodKind.PropertyGet) {
						usageFilter = u => u != SymbolUsageKind.Write;
					}
					else if (m.MethodKind == MethodKind.PropertySet || m.IsInitOnly()) {
						usageFilter = u => u == SymbolUsageKind.Write;
					}
					break;
			}
			foreach (var sr in await SymbolFinder.FindReferencesAsync(symbol, project.Solution, docs, cancellationToken).ConfigureAwait(false)) {
				if (definitionFilter?.Invoke(sr.Definition) == false) {
					continue;
				}
				await GroupReferenceByContainerAsync(d, sr, sign, nodeFilter, usageFilter, cancellationToken).ConfigureAwait(false);
			}
			if (d.Count == 0) {
				return null;
			}
			var r = new List<(ISymbol container, List<(SymbolUsageKind, ReferenceLocation)>)>(d.Count);
			r.AddRange(d.Select(i => (i.Key, i.Value)));
			r.Sort((x, y) => CompareSymbol(x.container, y.container));
			return r;
		}

		static async Task GroupReferenceByContainerAsync(Dictionary<ISymbol, List<(SymbolUsageKind usage, ReferenceLocation loc)>> results, ReferencedSymbol reference, string symbolSignature, Predicate<SyntaxNode> nodeFilter = null, Predicate<SymbolUsageKind> usageFilter = null, CancellationToken cancellationToken = default) {
			var pu = GetPotentialUsageKinds(reference.Definition);
			foreach (var docRefs in reference.Locations.GroupBy(l => l.Document)) {
				var sm = await docRefs.Key.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
				if (sm.IsCSharp() == false) {
					continue;
				}
				var r = sm.SyntaxTree.GetCompilationUnitRoot(cancellationToken);
				foreach (var location in docRefs) {
					var ss = location.Location.SourceSpan;
					var n = r.FindNode(ss);
					if (n.Span.Contains(ss.Start) == false || nodeFilter?.Invoke(n) == false) {
						continue;
					}
					var c = n.FirstAncestorOrSelf<SyntaxNode>(i => i.Kind().GetDeclarationCategory().HasAnyFlag(DeclarationCategory.Member | DeclarationCategory.Type));
					ISymbol s;
					if (c == null
						// unfortunately we can't compare the symbol s with the original typeSymbol directly,
						// even though they are actually the same
						|| symbolSignature != null && ((s = sm.GetSymbol(n, cancellationToken)) == null || (s?.ToDisplayString() != symbolSignature))) {
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
					var u = GetUsageKind(pu, n);
					if (usageFilter != null && usageFilter(u) == false) {
						continue;
					}
					if (results.TryGetValue(s, out var l)) {
						var sf = location.Location.SourceTree.FilePath;
						foreach (var (usage, loc) in l) {
							if (usage == u
								&& loc.Location.SourceSpan == ss
								&& loc.Location.SourceTree.FilePath == sf) {
								goto NEXT;
							}
						}
						l.Add((u, location));
					}
					else {
						results[s] = new List<(SymbolUsageKind, ReferenceLocation)> { (u, location) };
					}
				NEXT:;
				}
			}
		}

		public static SymbolUsageKind GetUsageKind(SymbolUsageKind possibleUsage, SyntaxNode node) {
			if (possibleUsage.MatchFlags(SymbolUsageKind.Write)) {
				var n = node.GetNodePurpose();
				if (n is AssignmentExpressionSyntax a && (a.Left == node || a.Left.GetLastIdentifier() == node)) {
					return a.Right.IsKind(SyntaxKind.NullLiteralExpression)
						? SymbolUsageKind.Write | SymbolUsageKind.SetNull
						: SymbolUsageKind.Write;
				}
				else if (n.IsKind(SyntaxKind.PostIncrementExpression)
					|| n.IsKind(SyntaxKind.PreIncrementExpression)
					|| n is ArgumentSyntax r && (r.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || r.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))) {
					return SymbolUsageKind.Write;
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
			else if (possibleUsage.HasAnyFlag(SymbolUsageKind.Attach | SymbolUsageKind.Detach | SymbolUsageKind.Trigger)) {
				node = node.GetNodePurpose();
				if (node is AssignmentExpressionSyntax a) {
					if (a.IsKind(SyntaxKind.AddAssignmentExpression)) {
						return SymbolUsageKind.Attach;
					}
					if (a.IsKind(SyntaxKind.SubtractAssignmentExpression)) {
						return SymbolUsageKind.Detach;
					}
				}
				else if (node.IsKind(SyntaxKind.ConditionalAccessExpression) || node.IsKind(SyntaxKind.SimpleMemberAccessExpression)) {
					return SymbolUsageKind.Trigger;
				}
			}
			else if (possibleUsage.MatchFlags(SymbolUsageKind.Delegate)) {
				var n = node.GetNodePurpose();
				// todo detect delegate usage buried under calculation expressions
				if (n.IsKind(SyntaxKind.Argument)) {
					return SymbolUsageKind.Delegate;
				}
				//var last = node.GetLastAncestorExpressionNode();
				//if (last != null && last.Parent.IsKind(SyntaxKind.Argument) == true && (last == node || last is MemberAccessExpressionSyntax)) {
				//	return SymbolUsageKind.Read;
				//}
				if (n is AssignmentExpressionSyntax a && a.Right == node) {
					switch (a.Kind()) {
						case SyntaxKind.AddAssignmentExpression: return SymbolUsageKind.Attach;
						case SyntaxKind.SubtractAssignmentExpression: return SymbolUsageKind.Detach;
						case SyntaxKind.SimpleAssignmentExpression: return SymbolUsageKind.Delegate;
					}
				}
			}
			return SymbolUsageKind.Normal;
		}

		public static SymbolUsageKind GetPotentialUsageKinds(ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Event:
					return SymbolUsageKind.Attach | SymbolUsageKind.Detach | SymbolUsageKind.Trigger;
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
					return SymbolUsageKind.Delegate;
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

		static readonly char[] __SplitArray = new char[] { ' ' };
		static string[] SplitKeywords(string text) {
			return text.Split(__SplitArray, StringSplitOptions.RemoveEmptyEntries);
		}
		public static Func<string, bool> CreateNameFilter(string keywords, bool fullMatch, bool matchCase) {
			var k = SplitKeywords(keywords);
			if (k.Length == 1 || fullMatch) {
				keywords = k[0];
				if (fullMatch) {
					if (matchCase) {
						return name => name == keywords;
					}
					else {
						return name => String.Equals(name, keywords, StringComparison.OrdinalIgnoreCase);
					}
				}
				else {
					if (matchCase) {
						return name => name.IndexOf(keywords, StringComparison.Ordinal) != -1;
					}
					else {
						return name => name.IndexOf(keywords, StringComparison.OrdinalIgnoreCase) != -1;
					}
				}
			}
			return name => {
				int i = 0;
				var c = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
				foreach (var item in k) {
					if ((i = name.IndexOf(item, i, c)) == -1) {
						return false;
					}
					i += item.Length;
				}
				return true;
			};
		}
	}
}
