using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.CodeAnalysis;

namespace Codist.Controls
{
	sealed class SymbolItem /*: INotifyPropertyChanged*/
	{
		UIElement _Icon;
		int _ImageId;
		TextBlock _Content;
		string _Hint;
		readonly bool _IncludeContainerType;

		//public event PropertyChangedEventHandler PropertyChanged;
		public int ImageId => _ImageId != 0 ? _ImageId : (_ImageId = Symbol != null ? Symbol.GetImageId() : SyntaxNode != null ? SyntaxNode.GetImageId() : -1);
		public UIElement Icon => _Icon ?? (_Icon = Container.IconProvider?.Invoke(this) ?? ThemeHelper.GetImage(ImageId != -1 ? ImageId : 0));
		public UIElement ExtIcon => Container.ExtIconProvider?.Invoke(this);
		public string Hint {
			get => _Hint ?? (_Hint = Symbol != null ? GetSymbolConstaintValue(Symbol) : String.Empty);
			set => _Hint = value;
		}
		public SymbolUsageKind Usage { get; set; }
		public bool IsExternal => Usage == SymbolUsageKind.External
			|| Container.ContainerType == SymbolListType.None && Symbol?.ContainingAssembly.GetSourceType() == AssemblySource.Metadata;
		public TextBlock Content {
			get => _Content ?? (_Content = Symbol != null
				? CreateContentForSymbol(Symbol, _IncludeContainerType, true)
				: SyntaxNode != null
					? new ThemedMenuText().Append(SyntaxNode.GetDeclarationSignature(), SymbolFormatter.Instance.GetBrush(SyntaxNode))
					: new ThemedMenuText());
			set => _Content = value;
		}
		public Location Location { get; set; }
		public SyntaxNode SyntaxNode { get; private set; }
		public ISymbol Symbol { get; private set; }
		public SymbolList Container { get; }

		public SymbolItem(SymbolList list) {
			Container = list;
			Content = new ThemedMenuText();
			_ImageId = -1;
		}
		public SymbolItem(Location location, SymbolList list) {
			Container = list;
			Location = location;
			if (location.IsInSource) {
				var filePath = location.SourceTree.FilePath;
				_Content = new ThemedMenuText(Path.GetFileNameWithoutExtension(filePath)).Append(Path.GetExtension(filePath), ThemeHelper.SystemGrayTextBrush);
				_Hint = Path.GetFileName(Path.GetDirectoryName(filePath));
				_ImageId = IconIds.FileEmpty;
			}
			else {
				var m = location.MetadataModule;
				_Content = new ThemedMenuText(Path.GetFileNameWithoutExtension(m.Name)).Append(Path.GetExtension(m.Name), ThemeHelper.SystemGrayTextBrush);
				_Hint = String.Empty;
				_ImageId = IconIds.Module;
			}
		}
		public SymbolItem(ISymbol symbol, SymbolList list, ISymbol containerSymbol)
			: this (symbol, list, false) {
			_ImageId = containerSymbol.GetImageId();
			_Content = CreateContentForSymbol(containerSymbol, false, true);
		}
		public SymbolItem(ISymbol symbol, SymbolList list, bool includeContainerType) {
			Symbol = symbol;
			Container = list;
			_IncludeContainerType = includeContainerType;
		}

		public SymbolItem(SyntaxNode node, SymbolList list) {
			SyntaxNode = node;
			Container = list;
		}

		public bool GoToSource() {
			if (Location != null && Location.IsInSource) {
				Location.GoToSource();
				return true;
			}
			if (SyntaxNode != null) {
				RefreshSyntaxNode();
				SyntaxNode.GetIdentifierToken().GetLocation().GoToSource();
				return true;
			}
			if (Symbol != null) {
				RefreshSymbol();
				if (Symbol.Kind == SymbolKind.Namespace) {
					Container.SemanticContext.FindMembers(Symbol, _Content.GetParent<ListBoxItem>().NullIfMouseOver());
					return false;
				}
				var s = Symbol.GetSourceReferences();
				switch (s.Length) {
					case 0:
						if (Container.SemanticContext.Document != null) {
							return ServicesHelper.Instance.VisualStudioWorkspace.TryGoToDefinition(Symbol, Container.SemanticContext.Document.Project, default);
						}
						return false;
					case 1:
						s[0].GoToSource();
						return true ;
					default:
						Container.SemanticContext.ShowLocations(Symbol, s, _Content.GetParent<ListBoxItem>().NullIfMouseOver());
						return false;
				}
			}
			return false;
		}
		public bool SelectIfContainsPosition(int position) {
			if (IsExternal || SyntaxNode == null || SyntaxNode.FullSpan.Contains(position, true) == false) {
				return false;
			}
			Container.SelectedValue = this;
			return true;
		}
		static ThemedMenuText CreateContentForSymbol(ISymbol symbol, bool includeType, bool includeParameter) {
			var t = new ThemedMenuText();
			if (includeType && symbol.ContainingType != null) {
				t.Append(symbol.ContainingType.Name + symbol.ContainingType.GetParameterString() + ".", ThemeHelper.SystemGrayTextBrush);
			}
			t.Append(symbol.GetOriginalName(), SymbolFormatter.Instance.GetBrush(symbol));
			if (includeParameter) {
				t.Append(symbol.GetParameterString(), ThemeHelper.SystemGrayTextBrush);
			}
			return t;
		}

		static string GetSymbolConstaintValue(ISymbol symbol) {
			if (symbol.Kind == SymbolKind.Field) {
				var f = symbol as IFieldSymbol;
				if (f.HasConstantValue) {
					return f.ConstantValue?.ToString();
				}
			}
			return null;
		}
		internal void SetSymbolToSyntaxNode() {
			Symbol = SyncHelper.RunSync(() => Container.SemanticContext.GetSymbolAsync(SyntaxNode));
		}
		internal void RefreshSyntaxNode() {
			var node = Container.SemanticContext.RelocateDeclarationNode(SyntaxNode);
			if (node != null && node != SyntaxNode) {
				SyntaxNode = node;
			}
		}
		internal void RefreshSymbol() {
			if (Symbol.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
				var symbol = Container.SemanticContext.RelocateSymbolAsync(Symbol).GetAwaiter().GetResult();
				if (symbol != null && symbol != Symbol) {
					Symbol = symbol;
				}
			}
		}

		internal void ShowSourceReference(TextBlock text) {
			var sourceTree = Location.SourceTree;
			var sourceSpan = Location.SourceSpan;
			var sourceText = sourceTree.GetText();
			var t = sourceText.ToString(new Microsoft.CodeAnalysis.Text.TextSpan(sourceSpan.Start > 100 ? sourceSpan.Start - 100 : 0, sourceSpan.Start > 100 ? 100 : sourceSpan.Start));
			int i = t.LastIndexOfAny(new[] { '\r', '\n' });
			text.Append(i != -1 ? t.Substring(i).TrimStart() : t.TrimStart())
				.Append(sourceText.ToString(sourceSpan), true);
			t = sourceText.ToString(new Microsoft.CodeAnalysis.Text.TextSpan(sourceSpan.End, sourceTree.Length - sourceSpan.End > 100 ? 100 : sourceTree.Length - sourceSpan.End));
			i = t.IndexOfAny(new[] { '\r', '\n' });
			text.Append(i != -1 ? t.Substring(0, i).TrimEnd() : t.TrimEnd());
		}

		public override string ToString() {
			return Content.GetText();
		}
	}
}
