using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Codist.Controls;
using Codist.Margins;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class ScrollBarMarkerPage : OptionPageFactory
		{
			public override string Name => R.T_ScrollbarMarkers;
			public override Features RequiredFeature => Features.ScrollbarMarkers;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly OptionBox<MarkerOptions> _LineNumber, _Selection, _MatchSelection, _KeyboardControl, _SpecialComment, _MarkerDeclarationLine, _LongMemberDeclaration, _TypeDeclaration, _MethodDeclaration, _RegionDirective, _CompilerDirective, _SymbolReference, _DisableChangeTracker;
				readonly OptionBox<MarkerOptions>[] _Options;
				readonly ColorButton _SymbolReferenceButton, _SymbolWriteButton, _SymbolDefinitionButton;
				readonly IntegerBox _MaxMatch, _MaxDocumentLength, _MaxSearchCharLength;
				readonly WrapPanel _MatchSelectionOptions;

				public PageControl() {
					var o = Config.Instance.MarkerOptions;
					SetContents(
						new TitleBox(R.OT_AllLanguages),
						new DescriptionBox(R.OT_AllLanguagesNote),
						_LineNumber = o.CreateOptionBox(MarkerOptions.LineNumber, UpdateConfig, R.OT_LineNumber)
							.SetLazyToolTip(() => R.OT_LineNumberTip),
						_Selection = o.CreateOptionBox(MarkerOptions.Selection, UpdateConfig, R.OT_Selection)
							.SetLazyToolTip(() => R.OT_SelectionTip),
						_MatchSelection = o.CreateOptionBox(MarkerOptions.MatchSelection, UpdateConfig, R.OT_MatchSelection)
							.SetLazyToolTip(() => R.OT_MatchSelectionTip),
						_KeyboardControl = o.CreateOptionBox(MarkerOptions.KeyboardControlMatch, UpdateConfig, R.OT_KeyboardControlMatchSelection).WrapMargin(SubOptionMargin),
						new DescriptionBox(R.OT_KeyboardControlMatchSelectionNote),
						_MatchSelectionOptions = new WrapPanel {
							Children = {
								new StackPanel().MakeHorizontal().Add(
									new TextBlock { MinWidth = 150, Margin = WpfHelper.SmallHorizontalMargin, Text = R.OT_MaxMatch },
									new IntegerBox(Config.Instance.MatchMargin.MaxMatch) { Minimum = 0, Step = 1000, Margin = WpfHelper.SmallHorizontalMargin }.Set(ref _MaxMatch).UseVsTheme()
								),
								new StackPanel().MakeHorizontal().Add(
									new TextBlock { MinWidth = 150, Margin = WpfHelper.SmallHorizontalMargin, Text = R.OT_MaxDocumentLength },
									new IntegerBox(Config.Instance.MatchMargin.MaxDocumentLength) { Minimum = 0, Step = 1, Unit = "KB", Margin = WpfHelper.SmallHorizontalMargin }.Set(ref _MaxDocumentLength).UseVsTheme()
								),
								new StackPanel().MakeHorizontal().Add(
									new TextBlock { MinWidth = 150, Margin = WpfHelper.SmallHorizontalMargin, Text = R.OT_MaxSearchCharLength },
									new IntegerBox(Config.Instance.MatchMargin.MaxSearchCharLength) { Minimum = 1, Step = 1, Margin = WpfHelper.SmallHorizontalMargin }.Set(ref _MaxSearchCharLength).UseVsTheme()
								),
							}
						}.WrapMargin(SubOptionMargin),
						_SpecialComment = o.CreateOptionBox(MarkerOptions.SpecialComment, UpdateConfig, R.OT_TaggedComments)
							.SetLazyToolTip(() => R.OT_TaggedCommentsTip),
						_DisableChangeTracker = o.CreateOptionBox(MarkerOptions.DisableChangeTracker, UpdateConfig, R.OT_DisableChangeTracker)
							.SetLazyToolTip(() => R.OT_DisableChangeTrackerTip),

						new TitleBox(R.OT_CSharp),
						new DescriptionBox(R.OT_CSharpMarkerNote),
						_MarkerDeclarationLine = o.CreateOptionBox(MarkerOptions.MemberDeclaration, UpdateConfig, R.OT_MemberDeclarationLine)
							.SetLazyToolTip(() => R.OT_MemberDeclarationLineTip),
						_LongMemberDeclaration = o.CreateOptionBox(MarkerOptions.LongMemberDeclaration, UpdateConfig, R.OT_LongMethodName)
							.SetLazyToolTip(() => R.OT_LongMethodNameTip),
						_TypeDeclaration = o.CreateOptionBox(MarkerOptions.TypeDeclaration, UpdateConfig, R.OT_TypeName)
							.SetLazyToolTip(() => R.OT_TypeNameTip),
						_MethodDeclaration = o.CreateOptionBox(MarkerOptions.MethodDeclaration, UpdateConfig, R.OT_MethodDeclarationSpot)
							.SetLazyToolTip(() => R.OT_MethodDeclarationSpotTip),
						_RegionDirective = o.CreateOptionBox(MarkerOptions.RegionDirective, UpdateConfig, R.OT_RegionName)
							.SetLazyToolTip(() => R.OT_RegionNameTip),
						_CompilerDirective = o.CreateOptionBox(MarkerOptions.CompilerDirective, UpdateConfig, R.OT_CompilerDirective)
							.SetLazyToolTip(() => R.OT_CompilerDirectiveTip),
						_SymbolReference = o.CreateOptionBox(MarkerOptions.SymbolReference, UpdateConfig, R.OT_MatchSymbol)
							.SetLazyToolTip(() => R.OT_MatchSymbolTip),
						new WrapPanel {
							Children = {
								new StackPanel().MakeHorizontal().Add(
									new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_MatchSymbolColor),
									new ColorButton(SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor, R.T_Color, UpdateSymbolReferenceColor).Set(ref _SymbolReferenceButton)
								),
								new StackPanel().MakeHorizontal().Add(
									new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_WriteSymbolColor),
									new ColorButton(SymbolReferenceMarkerStyle.DefaultWriteMarkerColor, R.T_Color, UpdateSymbolWriteColor).Set(ref _SymbolWriteButton)
									),
								new StackPanel().MakeHorizontal().Add(
									new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_SymbolDefinitionColor),
									new ColorButton(SymbolReferenceMarkerStyle.DefaultSymbolDefinitionColor, R.T_Color, UpdateSymbolDefinitionColor).Set(ref _SymbolDefinitionButton)
									)
							}
						}.WrapMargin(SubOptionMargin)
					);
					_Options = [_LineNumber, _Selection, _MatchSelection, _KeyboardControl, _SpecialComment, _DisableChangeTracker, _MarkerDeclarationLine, _LongMemberDeclaration, _TypeDeclaration, _MethodDeclaration, _RegionDirective, _CompilerDirective, _SymbolReference];
					_MatchSelection.BindDependentOptionControls(_KeyboardControl, _MatchSelectionOptions);
					_MaxMatch.ValueChanged += UpdateMatchMarginValue;
					_MaxDocumentLength.ValueChanged += UpdateMatchMarginValue;
					_MaxSearchCharLength.ValueChanged += UpdateMatchMarginValue;
					var subOptions = new[] { _LongMemberDeclaration, _TypeDeclaration, _MethodDeclaration, _RegionDirective };
					foreach (var item in subOptions) {
						item.WrapMargin(SubOptionMargin);
					}
					_MarkerDeclarationLine.BindDependentOptionControls(subOptions);
					_SymbolReference.BindDependentOptionControls(_SymbolReferenceButton, _SymbolWriteButton, _SymbolDefinitionButton);
					_DisableChangeTracker.IsEnabled = CodistPackage.VsVersion.Major >= 17;
					_SymbolReferenceButton.DefaultColor = () => SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor;
					_SymbolWriteButton.DefaultColor = () => SymbolReferenceMarkerStyle.DefaultWriteMarkerColor;
					_SymbolDefinitionButton.DefaultColor = () => SymbolReferenceMarkerStyle.DefaultSymbolDefinitionColor;
					LoadColors();
				}

				void UpdateMatchMarginValue(object sender, DependencyPropertyChangedEventArgs e) {
					if (IsConfigUpdating) {
						return;
					}
					if (sender == _MaxMatch) {
						Config.Instance.MatchMargin.MaxMatch = _MaxMatch.Value;
					}
					else if (sender == _MaxDocumentLength) {
						Config.Instance.MatchMargin.MaxDocumentLength = _MaxDocumentLength.Value;
					}
					else if (sender == _MaxSearchCharLength) {
						Config.Instance.MatchMargin.MaxSearchCharLength = _MaxSearchCharLength.Value;
					}
					Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
				}

				protected override void LoadConfig(Config config) {
					var o = config.MarkerOptions;
					Array.ForEach(_Options, i => i.UpdateWithOption(o));
					LoadColors();
					_MaxMatch.Value = config.MatchMargin.MaxMatch;
					_MaxDocumentLength.Value = config.MatchMargin.MaxDocumentLength;
					_MaxSearchCharLength.Value = config.MatchMargin.MaxSearchCharLength;
				}

				void LoadColors() {
					Color c;
					_SymbolReferenceButton.Color = (c = Config.Instance.SymbolReferenceMarkerSettings.ReferenceMarker).A != 0 ? c : SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor;
					_SymbolWriteButton.Color = (c = Config.Instance.SymbolReferenceMarkerSettings.WriteMarker).A != 0 ? c : SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor;
					_SymbolDefinitionButton.Color = (c = Config.Instance.SymbolReferenceMarkerSettings.SymbolDefinition).A != 0 ? c : SymbolReferenceMarkerStyle.DefaultSymbolDefinitionColor;
				}

				void UpdateConfig(MarkerOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
				}

				void UpdateSymbolReferenceColor(Color color) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.SymbolReferenceMarkerSettings.ReferenceMarkerColor = color.ToHexString();
					if (color.A == 0) {
						_SymbolReferenceButton.Color = SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor;
					}
					Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
				}

				void UpdateSymbolWriteColor(Color color) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.SymbolReferenceMarkerSettings.WriteMarkerColor = color.ToHexString();
					if (color.A == 0) {
						_SymbolWriteButton.Color = SymbolReferenceMarkerStyle.DefaultWriteMarkerColor;
					}
					Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
				}

				void UpdateSymbolDefinitionColor(Color color) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.SymbolReferenceMarkerSettings.SymbolDefinitionColor = color.ToHexString();
					if (color.A == 0) {
						_SymbolDefinitionButton.Color = SymbolReferenceMarkerStyle.DefaultSymbolDefinitionColor;
					}
					Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
				}

				internal void Refresh() {
					Color c;
					_SymbolReferenceButton.Color = (c = Config.Instance.SymbolReferenceMarkerSettings.ReferenceMarker).A != 0 ? c : SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor;
					_SymbolWriteButton.Color = (c = Config.Instance.SymbolReferenceMarkerSettings.WriteMarker).A != 0 ? c : SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor;
					_SymbolDefinitionButton.Color = (c = Config.Instance.SymbolReferenceMarkerSettings.SymbolDefinition).A != 0 ? c : SymbolReferenceMarkerStyle.DefaultSymbolDefinitionColor;
				}
			}
		}
	}
}
