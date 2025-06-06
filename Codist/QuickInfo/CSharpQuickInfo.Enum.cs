using System;
using System.Collections.Immutable;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	partial class CSharpQuickInfo
	{
		static void ShowEnumQuickInfo(InfoContainer qiContent, INamedTypeSymbol type, bool showUnderlyingType, bool showMembers) {
			qiContent.Add(new EnumInfoBlock(type, showUnderlyingType, showMembers));
		}

		sealed class EnumInfoBlock : InfoBlock
		{
			readonly INamedTypeSymbol _UnderlyingType;
			readonly bool _IsFlags;
			readonly int _FieldCount;
			readonly IFieldSymbol _MinField, _MaxField;
			readonly IFieldSymbol[] _Fields;

			public EnumInfoBlock(INamedTypeSymbol type, bool showUnderlyingType, bool showMembers) {
				var agg = EnumAggregators.GetAggregator(_UnderlyingType = type.EnumUnderlyingType);
				if (agg == null) {
					return;
				}
				_IsFlags = type.GetAttributes().Any(a => a.AttributeClass.MatchTypeName(nameof(FlagsAttribute), "System"));
				var members = type.GetMembers();
				var fields = ImmutableArray.CreateBuilder<IFieldSymbol>(members.Length);
				foreach (var member in members) {
					var f = member as IFieldSymbol;
					if (f is null || f.DeclaredAccessibility != Accessibility.Public) {
						continue;
					}
					var v = f.ConstantValue;
					if (v is null) {
						continue;
					}
					if (agg.Count == 0) {
						agg.Init(v);
						_MaxField = _MinField = f;
					}
					switch (agg.Accept(v)) {
						case 1: _MaxField = f; break;
						case -1: _MinField = f; break;
					}
					if (agg.Count < 64) {
						fields.Add(f);
					}
					++_FieldCount;
				}
				if (!showUnderlyingType) {
					_UnderlyingType = null;
				}
				if (showMembers) {
					if (fields.Count != 0) {
						_Fields = fields.ToArray();
					}
				}
				else {
					_MinField = null;
				}
			}

			public override UIElement ToUI() {
				var stackPanel = new StackPanel();
				var doc = new ThemedTipDocument();
				if (_UnderlyingType != null) {
					var content = new ThemedTipText(R.T_EnumUnderlyingType, true).AddSymbol(_UnderlyingType, true, SymbolFormatter.Instance);
					var p = new ThemedTipParagraph(IconIds.Enum, content);
					if (_MinField != null) {
						content.AppendLine().Append(R.T_EnumFieldCount, true).Append(_FieldCount.ToString())
							.AppendLine().Append(R.T_EnumMin, true).AddSymbol(_MinField, false, SymbolFormatter.Instance)
							.Append(", ").Append(R.T_EnumMax, true).AddSymbol(_MaxField, false, SymbolFormatter.Instance);
					}
					doc.Append(p);
				}
				if (_Fields != null) {
					var g = new Grid {
						HorizontalAlignment = HorizontalAlignment.Left,
						ColumnDefinitions = {
							new ColumnDefinition(),
							new ColumnDefinition()
						},
						Margin = WpfHelper.MiddleBottomMargin
					};
					int rc = 0;
					for (; rc < _Fields.Length; rc++) {
						IFieldSymbol f = _Fields[rc];
						g.RowDefinitions.Add(new RowDefinition());
						var ft = new ThemedTipText {
							TextAlignment = TextAlignment.Right,
							Foreground = ThemeCache.SystemGrayTextBrush,
							Margin = WpfHelper.SmallHorizontalMargin,
							FontFamily = ThemeCache.CodeTextFont
						}.Append("= ", ThemeCache.SystemGrayTextBrush);
						SymbolFormatter.Instance.ShowFieldConstantText(ft.Inlines, f, _IsFlags);
						g.Add(new TextBlock { Foreground = ThemeCache.ToolTipTextBrush }
								.AddSymbol(f, false, SymbolFormatter.Instance)
								.SetGlyph(IconIds.EnumField)
								.SetValue(Grid.SetRow, rc))
							.Add(ft
								.SetValue(Grid.SetRow, rc)
								.SetValue(Grid.SetColumn, 1));
					}
					if (rc < _FieldCount) {
						g.RowDefinitions.Add(new RowDefinition());
						g.Add(new ThemedTipText(R.T_More).SetValue(Grid.SetRow, rc).SetValue(Grid.SetColumnSpan, 2));
					}
					stackPanel.Add(doc);
					stackPanel.Add(g);
					return stackPanel;
				}
				return doc;
			}
		}

		static class EnumAggregators
		{
			public static IEnumAggregator GetAggregator(INamedTypeSymbol namedType) {
				switch (namedType.SpecialType) {
					case SpecialType.System_SByte: return new EnumAggregator<sbyte>(false);
					case SpecialType.System_Byte: return new EnumAggregator<byte>(true);
					case SpecialType.System_Int16: return new EnumAggregator<short>(false);
					case SpecialType.System_UInt16: return new EnumAggregator<ushort>(true);
					case SpecialType.System_Int32: return new EnumAggregator<int>(false);
					case SpecialType.System_UInt32: return new EnumAggregator<uint>(true);
					case SpecialType.System_Int64: return new EnumAggregator<long>(false);
					case SpecialType.System_UInt64: return new EnumAggregator<ulong>(true);
				}
				return null;
			}
		}

		interface IEnumAggregator
		{
			void Init(object x);
			int Accept(object x);
			ulong Bits { get; }
			object Max { get; }
			object Min { get; }
			int Count { get; }
		}

		sealed class EnumAggregator<T>
			: IEnumAggregator
			where T : struct, IComparable, IConvertible
		{
			readonly bool _IsUnsigned;
			int _Count;
			T _Bits, _Min, _Max;

			public EnumAggregator(bool isUnsigned) {
				_IsUnsigned = isUnsigned;
			}

			public ulong Bits => Op.ConvU8(_Bits);
			public object Min => _Min;
			public object Max => _Max;
			public int Count => _Count;

			public void Init(object x) {
				if (x is T t && _Count == 0) {
					_Bits = _Min = _Max = t;
					_Count = 1;
				}
			}

			public int Accept(object x) {
				if (x is T t) {
					++_Count;
					_Bits = Op.Or(_Bits, t);
					if (_IsUnsigned) {
						if (Op.CgtUn(t, _Max)) {
							_Max = t;
							return 1;
						}
						if (Op.CltUn(t, _Min)) {
							_Min = t;
							return -1;
						}
					}
					else {
						if (Op.Cgt(t, _Max)) {
							_Max = t;
							return 1;
						}
						if (Op.Clt(t, _Min)) {
							_Min = t;
							return -1;
						}
					}
				}
				return 0;
			}
		}
	}
}
