using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using GdiColor = System.Drawing.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;

namespace Codist
{
	/// <summary>
	/// Used by symbol analysis tools and super quick info to provide color previews
	/// </summary>
	static class ColorHelper
	{
		static class NamedColorCache
		{
			static readonly Dictionary<string, SolidColorBrush> __Cache = GetBrushes();
			static readonly Dictionary<string, Func<SolidColorBrush>> __SystemColors = GetSystemColors();
			internal static SolidColorBrush GetBrush(string name) {
				UIHelper.ParseColor(name, out var c, out var a);
				if (c != WpfColors.Transparent) {
					return new SolidColorBrush(a == 0 ? c : c.Alpha(a)).MakeFrozen();
				}
				var l = name.Length;
				return l >= 3 && l <= 20 && __Cache.TryGetValue(name, out var brush) ? brush : null;
			}
			internal static SolidColorBrush GetSystemBrush(string name) {
				return __SystemColors.TryGetValue(name, out var func) ? func() : null;
			}
			static Dictionary<string, SolidColorBrush> GetBrushes() {
				var c = Array.FindAll(typeof(WpfBrushes).GetProperties(), p => p.PropertyType == typeof(SolidColorBrush));
				var d = new Dictionary<string, SolidColorBrush>(c.Length, StringComparer.OrdinalIgnoreCase);
				foreach (var item in c) {
					d.Add(item.Name, item.GetValue(null) as SolidColorBrush);
				}
				return d;
			}
			static Dictionary<string, Func<SolidColorBrush>> GetSystemColors() {
				var c = Array.FindAll(typeof(System.Windows.SystemColors).GetProperties(), p => p.PropertyType == typeof(SolidColorBrush) || p.PropertyType == typeof(WpfColor));
				var d = new Dictionary<string, Func<SolidColorBrush>>(c.Length, StringComparer.OrdinalIgnoreCase);
				foreach (var item in c) {
					if (item.PropertyType == typeof(SolidColorBrush)) {
						d.Add(item.Name, (Func<SolidColorBrush>)item.GetGetMethod(false).CreateDelegate(typeof(Func<SolidColorBrush>)));
					}
					else {
						var getColor = (Func<WpfColor>)item.GetGetMethod(false).CreateDelegate(typeof(Func<WpfColor>));
						d.Add(item.Name, () => new SolidColorBrush(getColor()));
					}
				}
				c = typeof(System.Drawing.SystemColors).GetProperties();
				foreach (var item in c) {
					var getColor = (Func<GdiColor>)item.GetGetMethod(false).CreateDelegate(typeof(Func<GdiColor>));
					d.Add(item.Name, () => new SolidColorBrush(getColor().ToWpfColor()));
				}
				return d;
			}
		}
		public static SolidColorBrush GetSystemBrush(string symbolName) {
			return NamedColorCache.GetSystemBrush(symbolName);
		}

		public static SolidColorBrush GetBrush(ISymbol symbol, bool includeVsColors) {
			var n = symbol.ContainingType?.Name;
			switch (n) {
				case nameof(System.Windows.SystemColors):
				case nameof(System.Drawing.SystemBrushes):
				case nameof(System.Drawing.SystemPens):
					return NamedColorCache.GetSystemBrush(symbol.Name);
				case nameof(System.Drawing.KnownColor):
					return NamedColorCache.GetBrush(symbol.Name) ?? NamedColorCache.GetSystemBrush(symbol.Name);
				case nameof(System.Drawing.Color):
				case nameof(System.Drawing.Brushes):
				case nameof(System.Drawing.Pens):
				case nameof(Colors):
					return NamedColorCache.GetBrush(symbol.Name);
			}
			if (includeVsColors) {
				switch (n) {
					case nameof(EnvironmentColors):
						return GetVsThemeBrush(VsStatic.EnvironmentColor.Keys, symbol.Name);
					case nameof(CommonDocumentColors):
						return GetVsThemeBrush(VsStatic.CommonDocumentColor.Keys, symbol.Name);
					case nameof(CommonControlsColors):
						return GetVsThemeBrush(VsStatic.CommonControlsColor.Keys, symbol.Name);
					case nameof(InfoBarColors):
						return GetVsThemeBrush(VsStatic.InfoBarColor.Keys, symbol.Name);
					case nameof(StartPageColors):
						return GetVsThemeBrush(VsStatic.StartPageColor.Keys, symbol.Name);
					case nameof(HeaderColors):
						return GetVsThemeBrush(VsStatic.HeaderColor.Keys, symbol.Name);
					case nameof(ThemedDialogColors):
						return GetVsThemeBrush(VsStatic.ThemedDialogColor.Keys, symbol.Name);
					case nameof(ProgressBarColors):
						return GetVsThemeBrush(VsStatic.ProgressBarColor.Keys, symbol.Name);
					case nameof(SearchControlColors):
						return GetVsThemeBrush(VsStatic.SearchControlColor.Keys, symbol.Name);
					case nameof(TreeViewColors):
						return GetVsThemeBrush(VsStatic.TreeViewColor.Keys, symbol.Name);
					case nameof(VsColors):
						return GetVsResourceColor(symbol.Name);
					case nameof(VsBrushes):
						return GetVsResourceBrush(symbol.Name);
				}
			}
			return null;
		}
		public static SolidColorBrush GetBrush(string color) {
			return NamedColorCache.GetBrush(color);
		}

		public static SolidColorBrush GetVsResourceBrush(string name) {
			return VsStatic.Brush.Keys.TryGetValue(name, out var key)
				? CurrentResources.Instance.Get<SolidColorBrush>(key)
				: null;
		}
		public static SolidColorBrush GetVsResourceColor(string name) {
			return VsStatic.Color.Keys.TryGetValue(name, out var key)
				? new SolidColorBrush(CurrentResources.Instance.Get<WpfColor>(key))
				: null;
		}

		public static SolidColorBrush GetVsThemeBrush(Guid category, string name) {
			return VsStatic.ThemeResourceKeyProviders.TryGetValue(category, out var kp)
				? GetVsThemeBrush(kp(), name)
				: null;
		}
		public static SolidColorBrush GetVsThemeBrush(Dictionary<string, ThemeResourceKey> keys, string name) {
			return keys.TryGetValue(name, out var key) ? key.GetWpfBrush() : null;
		}

		public static bool IsDark(this WpfColor color) {
			return (299 * color.R + 587 * color.G + 114 * color.B) / 1000 < 128;
		}

		public static WpfColor InvertBrightness(this WpfColor color) {
			if (color.A == 0) {
				return color;
			}
			var g = color.ToGdiColor();
			return FromHsl(g.GetHue(), g.GetSaturation(), 1 - g.GetBrightness()).Alpha(color.A);
		}

		public static WpfColor FromHsl(double hue, double saturation, double luminosity) {
			double v;
			double r, g, b;
			r = g = b = luminosity;   // default to gray
			v = (luminosity <= 0.5) ? (luminosity * (1.0 + saturation)) : (luminosity + saturation - luminosity * saturation);
			if (hue >= 360) {
				hue %= 360;
			}
			else if (hue < 0) {
				hue = hue % 360 + 360;
			}

			if (v > 0) {
				double m, sv, vsf;
				int sextant;
				m = luminosity + luminosity - v;
				sv = (v - m) / v;
				hue /= 60;
				sextant = (int)hue;
				vsf = v * sv * (hue - sextant);
				switch (sextant) {
					case 0:
						r = v;
						g = m + vsf;
						b = m;
						break;
					case 1:
						r = v - vsf;
						g = v;
						b = m;
						break;
					case 2:
						r = m;
						g = v;
						b = m + vsf;
						break;
					case 3:
						r = m;
						g = v - vsf;
						b = v;
						break;
					case 4:
						r = m + vsf;
						g = m;
						b = v;
						break;
					case 5:
						r = v;
						g = m;
						b = v - vsf;
						break;
				}
			}
			return WpfColor.FromRgb((byte)Math.Round(r * 255.0, MidpointRounding.AwayFromZero), (byte)Math.Round(g * 255.0, MidpointRounding.AwayFromZero), (byte)Math.Round(b * 255.0, MidpointRounding.AwayFromZero));
		}

		public static SolidColorBrush ParseColorComponents(string rgbText, bool hsl = false) {
			var parts = rgbText.Split(',');
			if (parts.Length.IsOutside(3, 4)) {
				return null;
			}
			byte x, g, b, a;
			double s, l;
			try {
				if (hsl == false) {
					if (TryParseColorComponent(parts[0], out x) // r
							&& TryParseColorComponent(parts[1], out g)
							&& TryParseColorComponent(parts[2], out b)) {
						if (parts.Length != 4 || !TryParseAlphaComponent(parts[3], out a)) {
							a = 255;
						}
						return new SolidColorBrush(Color.FromArgb(a, x, g, b)).MakeFrozen();
					}
				}
				else {
					if (TryParseColorComponent(parts[0], out x) // h
							&& TryParsePercentComponent(parts[1], out s)
							&& TryParsePercentComponent(parts[2], out l)) {
						if (parts.Length != 4 || !TryParseAlphaComponent(parts[3], out a)) {
							a = 255;
						}
						return new SolidColorBrush(FromHsl(x, s, l).Alpha(a)).MakeFrozen();
					}
				}
			}
			catch {
				// ignore
			}
			return null;

			bool TryParseColorComponent(string input, out byte component) {
				if (input[input.Length - 1] == '%') {
					if (double.TryParse(input.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double percent)) {
						double value = Math.Round(percent * 2.55); // 255 / 100 = 2.55
						component = (byte)value.Clamp(0d, 255d);
						return true;
					}
				}
				else if (byte.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte value)) {
					component = value;
					return true;
				}
				component = default;
				return false;
			}

			bool TryParsePercentComponent(string input, out double component) {
				if (input[input.Length - 1] == '%') {
					if (double.TryParse(input.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double percent)) {
						component = (percent / 100d).Clamp(0d, 1d);
						return true;
					}
				}
				component = default;
				return false;
			}

			bool TryParseAlphaComponent(string input, out byte component) {
				if (TrimIfEndOfPercent(ref input)) {
					if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent)) {
						double value = Math.Round(percent * 2.55); // 255 / 100 = 2.55
						component = (byte)value.Clamp(0d, 255d);
						return true;
					}
				}
				else if (byte.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte intValue)) {
					component = intValue;
					return true;
				}
				else if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double floatValue)) {
					double value = Math.Round(floatValue * 255);
					component = (byte)value.Clamp(0d, 255d);
					return true;
				}
				component = default;
				return false;
			}

			bool TrimIfEndOfPercent(ref string input) {
				for (int i = input.Length - 1; i >= 0; i--) {
					switch (input[i]) {
						case ' ':
						case '\t': continue;
						case '%':
							input = input.Substring(0, i);
							return true;
						default:
							return false;
					}
				}
				return false;
			}
		}

		static class CurrentResources
		{
			public static readonly System.Windows.ResourceDictionary Instance = System.Windows.Application.Current.Resources;
		}

		static class VsStatic
		{
			public static class Color
			{
				public static readonly Dictionary<string, object> Keys = InitPropertyValues<object>(typeof(VsColors));
			}
			public static class Brush
			{
				public static readonly Dictionary<string, object> Keys = InitPropertyValues<object>(typeof(VsBrushes));
			}

			public static readonly Dictionary<Guid, Func<Dictionary<string, ThemeResourceKey>>> ThemeResourceKeyProviders = new Dictionary<Guid, Func<Dictionary<string, ThemeResourceKey>>> {
				{ CommonControlsColors.Category, () => CommonControlsColor.Keys },
				{ CommonDocumentColors.Category, () => CommonDocumentColor.Keys },
				{ EnvironmentColors.Category, () => EnvironmentColor.Keys },
				{ InfoBarColors.Category, () => InfoBarColor.Keys },
				{ HeaderColors.Category, () => HeaderColor.Keys },
				{ ThemedDialogColors.Category, () => ThemedDialogColor.Keys },
				{ ProgressBarColors.Category, () => ProgressBarColor.Keys },
				{ SearchControlColors.Category, () => SearchControlColor.Keys },
				{ StartPageColors.Category, () => StartPageColor.Keys },
				{ TreeViewColors.Category, () => TreeViewColor.Keys },
			};

			public static class CommonControlsColor
			{
				public static readonly Dictionary<string, ThemeResourceKey> Keys = InitPropertyValues<ThemeResourceKey>(typeof(CommonControlsColors));
			}
			public static class CommonDocumentColor
			{
				public static readonly Dictionary<string, ThemeResourceKey> Keys = InitPropertyValues<ThemeResourceKey>(typeof(CommonDocumentColors));
			}
			public static class EnvironmentColor
			{
				public static readonly Dictionary<string, ThemeResourceKey> Keys = InitPropertyValues<ThemeResourceKey>(typeof(EnvironmentColors));
			}
			public static class InfoBarColor
			{
				public static readonly Dictionary<string, ThemeResourceKey> Keys = InitPropertyValues<ThemeResourceKey>(typeof(InfoBarColors));
			}
			public static class HeaderColor
			{
				public static readonly Dictionary<string, ThemeResourceKey> Keys = InitPropertyValues<ThemeResourceKey>(typeof(HeaderColors));
			}
			public static class ThemedDialogColor
			{
				public static readonly Dictionary<string, ThemeResourceKey> Keys = InitPropertyValues<ThemeResourceKey>(typeof(ThemedDialogColors));
			}
			public static class ProgressBarColor
			{
				public static readonly Dictionary<string, ThemeResourceKey> Keys = InitPropertyValues<ThemeResourceKey>(typeof(ProgressBarColors));
			}
			public static class SearchControlColor
			{
				public static readonly Dictionary<string, ThemeResourceKey> Keys = InitPropertyValues<ThemeResourceKey>(typeof(SearchControlColors));
			}
			public static class StartPageColor
			{
				public static readonly Dictionary<string, ThemeResourceKey> Keys = InitPropertyValues<ThemeResourceKey>(typeof(StartPageColors));
			}
			public static class TreeViewColor
			{
				public static readonly Dictionary<string, ThemeResourceKey> Keys = InitPropertyValues<ThemeResourceKey>(typeof(TreeViewColors));
			}

			static Dictionary<string, TProperty> InitPropertyValues<TProperty>(Type type) {
				var properties = type.GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
				var d = new Dictionary<string, TProperty>(properties.Length);
				var pt = typeof(TProperty);
				foreach (var propertyInfo in properties) {
					if (propertyInfo.PropertyType == pt
						&& propertyInfo.GetValue(null) is TProperty p) {
						d[propertyInfo.Name] = p;
					}
				}
				return d;
			}
		}
	}
}
