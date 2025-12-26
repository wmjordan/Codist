using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using R = Codist.Properties.Resources;

namespace Codist.SymbolCommands
{
	abstract class ListMembersCommand<TListData> : AnalysisListCommandBase<TListData>
	{
		public override int ImageId => IconIds.ListMembers;
		public override string Title => R.CMD_ListMembers;
		public override string Description => R.CMDT_ListTypeMembers;
	}

	sealed class ListNamespaceMembersCommand : ListMembersCommand<ISymbol[]>
	{
		string _ResultLabel;

		protected override string ResultLabel => _ResultLabel;

		public override Task<ISymbol[]> PrepareListDataAsync(CancellationToken cancellationToken) {
			return ((INamespaceSymbol)Symbol).GetNamespacesAndTypesAsync(Context.Document.Project, cancellationToken);
		}
		public override void UpdateList(SymbolMenu resultList, ISymbol[] data) {
			resultList.ContainerType = SymbolListType.TypeList;
			resultList.AddRange(data.Select(s => new SymbolItem(s, resultList, false)));
			resultList.ExtIconProvider = ExtIconProvider.Default.GetExtIcons;
			_ResultLabel = R.T_NamespaceMembers.Replace("{count}", data.Length.ToString());
		}
	}

	class ListTypeMembersCommand : ListMembersCommand<ImmutableArray<(string type, IImmutableList<ISymbol> members)>>
	{
		string _ResultLabel;
		protected override string ResultLabel => _ResultLabel;

		public override async Task<ImmutableArray<(string type, IImmutableList<ISymbol> members)>> PrepareListDataAsync(CancellationToken cancellationToken) {
			return Symbol.ListMembers();
		}

		public override void UpdateList(SymbolMenu resultList, ImmutableArray<(string type, IImmutableList<ISymbol> members)> data) {
			resultList.SetupForSpecialTypes(Symbol as ITypeSymbol);
			SetExtIconProvider(resultList);
			var items = ImmutableArray.CreateBuilder<SymbolItem>(data.Sum(i => i.members.Count));
			foreach (var item in data) {
				var t = item.type;
				items.AddRange(item.members.Select(s => new SymbolItem(s, resultList, false) { Hint = t }));
			}
			resultList.AddRange(items);
			int count = data[0].members.Count;
			_ResultLabel = R.T_Members.Replace("{count}", count.ToString()).Replace("{inherited}", (items.Count - count).ToString());
		}

		void SetExtIconProvider(SymbolMenu resultList) {
			if (resultList.IconProvider is null && Symbol.Kind == SymbolKind.NamedType) {
				switch (((INamedTypeSymbol)Symbol).TypeKind) {
					case TypeKind.Interface:
						resultList.ExtIconProvider = ExtIconProvider.InterfaceMembers.GetExtIcons; break;
					case TypeKind.Class:
					case TypeKind.Struct:
						resultList.ExtIconProvider = ExtIconProvider.Default.GetExtIcons; break;
				}
			}
		}
	}

	sealed class ListReturnTypeMembersCommand : ListTypeMembersCommand
	{
		public override string Title => R.CMD_ListMembersOf;
		protected override ISymbol ResultSymbol => Symbol.GetReturnType().ResolveElementType();

		public override Task<ImmutableArray<(string type, IImmutableList<ISymbol> members)>> PrepareListDataAsync(CancellationToken cancellationToken) {
			return Task.FromResult(ResultSymbol.ListMembers());
		}
	}

	sealed class ListSpecialGenericReturnTypeMembersCommand : ListTypeMembersCommand
	{
		string _ResultLabel;
		public override string Title => R.CMD_ListMembersOf;
		protected override string ResultLabel => _ResultLabel;

		public override Task<ImmutableArray<(string type, IImmutableList<ISymbol> members)>> PrepareListDataAsync(CancellationToken cancellationToken) {
			return Task.FromResult(((ISymbol)Symbol.GetReturnType().ResolveElementType().ResolveSingleGenericTypeArgument()).ListMembers());
		}

		protected override void SetupListTitle(SymbolMenu resultList, ImmutableArray<(string type, IImmutableList<ISymbol> members)> data) {
			_ResultLabel = R.T_Members.Replace("{count}", data[0].members.Count.ToString()).Replace("{inherited}", (data.Sum(i => i.members.Count) - data[0].members.Count).ToString());
			base.SetupListTitle(resultList, data);
		}
	}

	sealed class ListEventArgsMembersCommand : ListTypeMembersCommand
	{
		string _ResultLabel;
		public override string Title => R.CMD_ListMembersOf;
		public override string Description => R.CMDT_ListEventArgumentMember;
		protected override string ResultLabel => _ResultLabel;

		public override Task<ImmutableArray<(string type, IImmutableList<ISymbol> members)>> PrepareListDataAsync(CancellationToken cancellationToken) {
			var eventArgsType = ((IEventSymbol)Symbol).GetEventArgsType();
			if (eventArgsType != null) {
				return Task.FromResult(((ISymbol)eventArgsType).ListMembers());
			}
			return Task.FromResult(ImmutableArray<(string, IImmutableList<ISymbol>)>.Empty);
		}

		protected override void SetupListTitle(SymbolMenu resultList, ImmutableArray<(string type, IImmutableList<ISymbol> members)> data) {
			base.SetupListTitle(resultList, data);
			_ResultLabel = data.Length > 0
				? R.T_Members
					.Replace("{count}", data[0].members.Count.ToString())
					.Replace("{inherited}", (data.Sum(i => i.members.Count) - data[0].members.Count).ToString())
				: null;
		}
	}
}
