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
				readonly ColorButton _MatchColorButton, _CaseMismatchColorButton, _SymbolReferenceButton, _SymbolWriteButton, _SymbolDefinitionButton;
				readonly IntegerBox _MarkerSize, _MaxMatch, _MaxDocumentLength, _MaxSearchCharLength;
				readonly WrapPanel _MatchSelectionOptions;

				public PageControl() {
					var o = Config.Instance.MarkerOptions;
					var mo = Config.Instance.ScrollbarMarker;
					var so = Config.Instance.SymbolReferenceMarkerSettings;
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
									new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin, VerticalAlignment = VerticalAlignment.Center }.Append(R.OT_MatchSelectionColor),
									new ColorButton(mo.MatchMarker, R.T_Color, UpdateMatchColor).Set(ref _MatchColorButton)
								),
								new StackPanel().MakeHorizontal().Add(
									new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin, VerticalAlignment = VerticalAlignment.Center }.Append(R.OT_CaseMismatchSelectionColor),
									new ColorButton(mo.CaseMismatchMarker, R.T_Color, UpdateCaseMismatchColor).Set(ref _CaseMismatchColorButton)
								),
								new StackPanel { Margin = WpfHelper.SmallMargin }.MakeHorizontal().Add(
									new TextBlock { MinWidth = 150, Margin = WpfHelper.SmallHorizontalMargin, Text = R.OT_MaxMatch },
									new IntegerBox(mo.MaxMatch) { Minimum = 0, Step = 1000, Margin = WpfHelper.SmallHorizontalMargin }
										.Set(ref _MaxMatch)
										.UseVsTheme()
								),
								new StackPanel { Margin = WpfHelper.SmallMargin }.MakeHorizontal().Add(
									new TextBlock { MinWidth = 150, Margin = WpfHelper.SmallHorizontalMargin, Text = R.OT_MaxDocumentLength },
									new IntegerBox(mo.MaxDocumentLength) { Minimum = 0, Step = 1, Unit = "KB", Margin = WpfHelper.SmallHorizontalMargin }
										.Set(ref _MaxDocumentLength)
										.UseVsTheme()
								),
								new StackPanel { Margin = WpfHelper.SmallMargin }.MakeHorizontal().Add(
									new TextBlock { MinWidth = 150, Margin = WpfHelper.SmallHorizontalMargin, Text = R.OT_MaxSearchCharLength },
									new IntegerBox(mo.MaxSearchCharLength) { Minimum = 1, Step = 1, Margin = WpfHelper.SmallHorizontalMargin }
										.Set(ref _MaxSearchCharLength)
										.UseVsTheme()
								),
							}
						}.WrapMargin(SubOptionMargin),
						_SpecialComment = o.CreateOptionBox(MarkerOptions.SpecialComment, UpdateConfig, R.OT_TaggedComments)
							.SetLazyToolTip(() => R.OT_TaggedCommentsTip),
						_DisableChangeTracker = o.CreateOptionBox(MarkerOptions.DisableChangeTracker, UpdateConfig, R.OT_DisableChangeTracker)
							.SetLazyToolTip(() => R.OT_DisableChangeTrackerTip),
						new WrapPanel {
							Children = {
								new TextBlock { MinWidth = 150, Margin = WpfHelper.SmallHorizontalMargin, Text = R.OT_MarkerSize },
								new IntegerBox(mo.MarkerSize) { Minimum = 2, Maximum = 8, Margin = WpfHelper.SmallHorizontalMargin }
									.Set(ref _MarkerSize)
									.UseVsTheme(),
							},
							Margin = WpfHelper.SmallMargin
						},

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
								new StackPanel { Margin = WpfHelper.SmallMargin }.MakeHorizontal().Add(
									new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin, VerticalAlignment = VerticalAlignment.Center }.Append(R.OT_MatchSymbolColor),
									new ColorButton(so.ReferenceMarker, R.T_Color, UpdateSymbolReferenceColor).Set(ref _SymbolReferenceButton)
								),
								new StackPanel { Margin = WpfHelper.SmallMargin }.MakeHorizontal().Add(
									new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin, VerticalAlignment = VerticalAlignment.Center }.Append(R.OT_WriteSymbolColor),
									new ColorButton(so.WriteMarker, R.T_Color, UpdateSymbolWriteColor).Set(ref _SymbolWriteButton)
									),
								new StackPanel { Margin = WpfHelper.SmallMargin }.MakeHorizontal().Add(
									new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin, VerticalAlignment = VerticalAlignment.Center }.Append(R.OT_SymbolDefinitionColor),
									new ColorButton(so.SymbolDefinition, R.T_Color, UpdateSymbolDefinitionColor).Set(ref _SymbolDefinitionButton)
									)
							}
						}.WrapMargin(SubOptionMargin)
					);
					_Options = [_LineNumber, _Selection, _MatchSelection, _KeyboardControl, _SpecialComment, _DisableChangeTracker, _MarkerDeclarationLine, _LongMemberDeclaration, _TypeDeclaration, _MethodDeclaration, _RegionDirective, _CompilerDirective, _SymbolReference];
					_MatchSelection.BindDependentOptionControls(_KeyboardControl, _MatchSelectionOptions);
					_MarkerSize.ValueChanged += UpdateMatchMarginValue;
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
					_MatchColorButton.DefaultColor = () => MarkerConfig.DefaultMatchColor;
					_CaseMismatchColorButton.DefaultColor = () => MarkerConfig.DefaultCaseMismatchColor;
					_SymbolReferenceButton.DefaultColor = () => SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor;
					_SymbolWriteButton.DefaultColor = () => SymbolReferenceMarkerStyle.DefaultWriteMarkerColor;
					_SymbolDefinitionButton.DefaultColor = () => SymbolReferenceMarkerStyle.DefaultSymbolDefinitionColor;
				}

				void UpdateMatchMarginValue(object sender, DependencyPropertyChangedEventArgs e) {
					if (IsConfigUpdating) {
						return;
					}
					if (sender == _MaxMatch) {
						Config.Instance.ScrollbarMarker.MaxMatch = _MaxMatch.Value;
					}
					else if (sender == _MaxDocumentLength) {
						Config.Instance.ScrollbarMarker.MaxDocumentLength = _MaxDocumentLength.Value;
					}
					else if (sender == _MaxSearchCharLength) {
						Config.Instance.ScrollbarMarker.MaxSearchCharLength = _MaxSearchCharLength.Value;
					}
					else if (sender == _MarkerSize) {
						Config.Instance.ScrollbarMarker.MarkerSize = _MarkerSize.Value;
					}
					Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
				}

				protected override void LoadConfig(Config config) {
					var o = config.MarkerOptions;
					Array.ForEach(_Options, i => i.UpdateWithOption(o));
					LoadColors();
					_MaxMatch.Value = config.ScrollbarMarker.MaxMatch;
					_MaxDocumentLength.Value = config.ScrollbarMarker.MaxDocumentLength;
					_MaxSearchCharLength.Value = config.ScrollbarMarker.MaxSearchCharLength;
					_MarkerSize.Value = config.ScrollbarMarker.MarkerSize;
				}

				void LoadColors() {
					Color c;
					var mo = Config.Instance.ScrollbarMarker;
					_MatchColorButton.Color = (c = mo.MatchMarker).A != 0 ? c : MarkerConfig.DefaultMatchColor;
					_CaseMismatchColorButton.Color = (c = mo.CaseMismatchMarker).A != 0 ? c : MarkerConfig.DefaultCaseMismatchColor;

					var so = Config.Instance.SymbolReferenceMarkerSettings;
					_SymbolReferenceButton.Color = (c = so.ReferenceMarker).A != 0 ? c : SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor;
					_SymbolWriteButton.Color = (c = so.WriteMarker).A != 0 ? c : SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor;
					_SymbolDefinitionButton.Color = (c = so.SymbolDefinition).A != 0 ? c : SymbolReferenceMarkerStyle.DefaultSymbolDefinitionColor;
				}

				void UpdateConfig(MarkerOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					if (CodistPackage.VsVersion.Major >= 17) {
						MatchMargin.ExcludeGlobalOption();
					}
					Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
				}

				void UpdateMatchColor(Color color) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.ScrollbarMarker.MatchColor = color.ToHexString();
					if (color.A == 0) {
						_MatchColorButton.Color = MarkerConfig.DefaultMatchColor;
					}
					Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
				}

				void UpdateCaseMismatchColor(Color color) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.ScrollbarMarker.CaseMismatchColor = color.ToHexString();
					if (color.A == 0) {
						_CaseMismatchColorButton.Color = MarkerConfig.DefaultCaseMismatchColor;
					}
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
			}
		}
	}
}
