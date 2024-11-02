using System;
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
			var s = new ThemedTipDocument();
			var underlyingType = type.EnumUnderlyingType;
			TextBlock content;
			if (showUnderlyingType) {
				content = new ThemedTipText(R.T_EnumUnderlyingType, true).AddSymbol(underlyingType, true, SymbolFormatter.Instance);
				s.Append(new ThemedTipParagraph(IconIds.Enum, content));
			}
			else {
				content = null;
			}
			if (showMembers == false) {
				qiContent.Add(s);
				return;
			}
			var agg = EnumAggregators.GetAggregator(underlyingType);
			if (agg == null) {
				return;
			}
			bool isFlags = type.GetAttributes().Any(a => a.AttributeClass.MatchTypeName(nameof(FlagsAttribute), "System"));
			IFieldSymbol minField = null, maxField = null;
			Grid g = null;
			int rc = 0; // row count
			foreach (var f in type.FindMembers().OfType<IFieldSymbol>().Where(i => i.ConstantValue != null)) {
				var v = f.ConstantValue;
				if (agg.Count == 0) {
					agg.Init(v);
					minField = maxField = f;
					g = new Grid {
						HorizontalAlignment = HorizontalAlignment.Left,
						ColumnDefinitions = {
							new ColumnDefinition(),
							new ColumnDefinition()
						},
						Margin = WpfHelper.MiddleBottomMargin
					};
					goto NEXT;
				}
				switch (agg.Accept(v)) {
					case 1: maxField = f; break;
					case -1: minField = f; break;
				}
			NEXT:
				if (agg.Count < 64) {
					g.RowDefinitions.Add(new RowDefinition());
					var ft = new ThemedTipText {
						TextAlignment = TextAlignment.Right,
						Foreground = ThemeHelper.SystemGrayTextBrush,
						Margin = WpfHelper.SmallHorizontalMargin,
						FontFamily = ThemeHelper.CodeTextFont
					}.Append("= ", ThemeHelper.SystemGrayTextBrush);
					SymbolFormatter.Instance.ShowFieldConstantText(ft.Inlines, f, isFlags);
					g.Add(new TextBlock { Foreground = ThemeHelper.ToolTipTextBrush }
							.AddSymbol(f, false, SymbolFormatter.Instance)
							.SetGlyph(IconIds.EnumField)
							.SetValue(Grid.SetRow, rc))
						.Add(ft
							.SetValue(Grid.SetRow, rc)
							.SetValue(Grid.SetColumn, 1));
				}
				else if (agg.Count == 64) {
					g.RowDefinitions.Add(new RowDefinition());
					g.Add(new ThemedTipText(R.T_More).SetValue(Grid.SetRow, rc).SetValue(Grid.SetColumnSpan, 2));
				}
				++rc;
			}
			if (agg.Count == 0) {
				return;
			}

			if (showUnderlyingType) {
				content.AppendLine().Append(R.T_EnumFieldCount, true).Append(agg.Count.ToString())
							.AppendLine().Append(R.T_EnumMin, true).AddSymbol(minField, false, SymbolFormatter.Instance)
							.Append(", ").Append(R.T_EnumMax, true).AddSymbol(maxField, false, SymbolFormatter.Instance);
				if (isFlags) {
					var d = Convert.ToString((long)agg.Bits, 2);
					content.AppendLine().Append(R.T_BitCount, true)
						.Append(d.Length.ToText())
						.AppendLine()
						.Append(R.T_EnumAllFlags, true)
						.Append(d);
				}
			}
			qiContent.Add(s);
			if (g != null) {
				qiContent.Add(g);
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
					if (_IsUnsigned ? Op.CgtUn(t, _Max) : Op.Cgt(t, _Max)) {
						_Max = t;
						return 1;
					}
					else if (_IsUnsigned ? Op.CltUn(t, _Min) : Op.Clt(t, _Min)) {
						_Min = t;
						return -1;
					}
				}
				return 0;
			}
		}
	}
}
