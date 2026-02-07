using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;

namespace Codist;

static class ClassificationStyleHelper
{
	public static ClassificationTag GetClassificationTag(this IClassificationTypeRegistryService registry, string classificationType) {
		var t = registry.GetClassificationType(classificationType);
		return t == null
			? throw new KeyNotFoundException($"Missing ClassificationType ({classificationType})")
			: new ClassificationTag(t);
	}

	public static bool TryGetClassificationTag(this IClassificationTypeRegistryService registry, string classificationType, out ClassificationTag tag) {
		var t = registry.GetClassificationType(classificationType);
		if (t != null) {
			tag = new ClassificationTag(t);
			return true;
		}
		tag = null;
		return false;
	}

	public static IClassificationType CreateClassificationCategory(string classificationType) {
		return new ClassificationCategory(classificationType);
	}

	public static bool IsClassificationCategory(this IClassificationType classificationType) {
		return classificationType is ClassificationCategory;
	}

	public static IEqualityComparer<IClassificationType> GetClassificationTypeComparer() {
		return ClassificationTypeComparer.Instance;
	}

	public static IEnumerable<IClassificationType> GetBaseTypes(this IClassificationType classificationType) {
		return GetBaseTypes(classificationType, new HashSet<IClassificationType>());
	}
	static IEnumerable<IClassificationType> GetBaseTypes(IClassificationType type, HashSet<IClassificationType> dedup) {
		foreach (var item in type.BaseTypes) {
			if (dedup.Add(item)) {
				yield return item;
				foreach (var c in GetBaseTypes(item, dedup)) {
					yield return c;
				}
			}
		}
	}

	public static TextFormattingRunProperties GetRunProperties(this IClassificationFormatMap formatMap, string classificationType) {
		var t = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(classificationType);
		return t == null ? null : formatMap.GetTextProperties(t);
	}
	public static string Print(this TextFormattingRunProperties format) {
		using var sbr = ReusableStringBuilder.AcquireDefault(100);
		var sb = sbr.Resource;
		if (format.TypefaceEmpty == false) {
			sb.Append(format.Typeface.FontFamily.ToString()).Append('&');
		}
		if (format.FontRenderingEmSizeEmpty == false) {
			sb.Append('#').Append(format.FontRenderingEmSize);
		}
		if (format.BoldEmpty == false) {
			sb.Append(format.Bold ? "+B" : "-B");
		}
		if (format.ItalicEmpty == false) {
			sb.Append(format.Italic ? "+I" : "-I");
		}
		if (format.ForegroundBrushEmpty == false) {
			sb.Append(format.ForegroundBrush.ToString()).Append("@f");
		}
		if (format.ForegroundOpacityEmpty == false) {
			sb.Append('%').Append(format.ForegroundOpacity.ToString("0.0#")).Append('O');
		}
		if (format.BackgroundBrushEmpty == false) {
			sb.Append(format.BackgroundBrush.ToString()).Append("@b");
		}
		if (format.BackgroundOpacityEmpty == false) {
			sb.Append('%').Append(format.BackgroundOpacity.ToString("0.0#")).Append('o');
		}
		if (format.TextDecorationsEmpty == false) {
			sb.Append("TD");
		}
		return sb.ToString();
	}
	#region Format ResourceDictionary
	public static WpfColor GetBackgroundColor(this IEditorFormatMap formatMap) {
		return formatMap.GetProperties(Constants.EditorProperties.TextViewBackground).GetBackgroundColor();
	}
	public static double? GetFontSize(this ResourceDictionary resource) {
		return resource.GetNullable<double>(ClassificationFormatDefinition.FontRenderingSizeId);
	}
	public static bool? GetItalic(this ResourceDictionary resource) {
		return resource.GetNullable<bool>(ClassificationFormatDefinition.IsItalicId);
	}
	public static bool? GetBold(this ResourceDictionary resource) {
		return resource.GetNullable<bool>(ClassificationFormatDefinition.IsBoldId);
	}
	public static double GetOpacity(this ResourceDictionary resource) {
		return resource.GetNullable<double>(ClassificationFormatDefinition.ForegroundOpacityId) ?? 0d;
	}
	public static double GetBackgroundOpacity(this ResourceDictionary resource) {
		return resource.GetNullable<double>(ClassificationFormatDefinition.BackgroundOpacityId) ?? 0d;
	}
	public static WpfBrush GetBrush(this ResourceDictionary resource, string resourceId = EditorFormatDefinition.ForegroundBrushId) {
		return resource.Get<WpfBrush>(resourceId);
	}
	public static WpfBrush GetBackgroundBrush(this ResourceDictionary resource) {
		return resource.Get<WpfBrush>(EditorFormatDefinition.BackgroundBrushId);
	}
	public static TextDecorationCollection GetTextDecorations(this ResourceDictionary resource) {
		return resource.Get<TextDecorationCollection>(ClassificationFormatDefinition.TextDecorationsId);
	}
	public static TextEffectCollection GetTextEffects(this ResourceDictionary resource) {
		return resource.Get<TextEffectCollection>(ClassificationFormatDefinition.TextEffectsId);
	}
	public static Typeface GetTypeface(this ResourceDictionary resource) {
		return resource.Get<Typeface>(ClassificationFormatDefinition.TypefaceId);
	}
	public static WpfColor GetColor(this IEditorFormatMap map, string formatName, string resourceId = EditorFormatDefinition.ForegroundColorId) {
		var p = map.GetProperties(formatName);
		return p?.Contains(resourceId) == true && (p[resourceId] is WpfColor color)
			? color
			: default;
	}
	public static WpfColor GetColor(this ResourceDictionary resource, string resourceId = EditorFormatDefinition.ForegroundColorId) {
		return resource?.Contains(resourceId) == true && (resource[resourceId] is WpfColor color)
			? color
			: default;
	}
	public static WpfColor GetBackgroundColor(this ResourceDictionary resource) {
		return resource.GetColor(EditorFormatDefinition.BackgroundColorId);
	}
	public static ResourceDictionary SetColor(this ResourceDictionary resource, WpfColor color) {
		resource.SetColor(EditorFormatDefinition.ForegroundColorId, color);
		return resource;
	}
	public static ResourceDictionary SetBackgroundColor(this ResourceDictionary resource, WpfColor color) {
		resource.SetColor(EditorFormatDefinition.BackgroundColorId, color);
		return resource;
	}
	public static ResourceDictionary SetBrush(this ResourceDictionary resource, WpfBrush brush) {
		resource.SetBrush(EditorFormatDefinition.ForegroundBrushId, EditorFormatDefinition.ForegroundColorId, brush);
		return resource;
	}
	public static ResourceDictionary SetBackgroundBrush(this ResourceDictionary resource, WpfBrush brush) {
		resource.SetBrush(EditorFormatDefinition.BackgroundBrushId, EditorFormatDefinition.BackgroundColorId, brush);
		return resource;
	}
	public static ResourceDictionary SetBold(this ResourceDictionary resource, bool? bold) {
		resource.SetValue(ClassificationFormatDefinition.IsBoldId, bold);
		return resource;
	}
	public static ResourceDictionary SetItalic(this ResourceDictionary resource, bool? italic) {
		resource.SetValue(ClassificationFormatDefinition.IsItalicId, italic);
		return resource;
	}
	public static ResourceDictionary SetOpacity(this ResourceDictionary resource, double opacity) {
		resource.SetValue(ClassificationFormatDefinition.ForegroundOpacityId, opacity);
		return resource;
	}
	public static ResourceDictionary SetBackgroundOpacity(this ResourceDictionary resource, double opacity) {
		resource.SetValue(ClassificationFormatDefinition.BackgroundOpacityId, opacity);
		return resource;
	}
	public static ResourceDictionary SetFontSize(this ResourceDictionary resource, double? fontSize) {
		resource.SetValue(ClassificationFormatDefinition.FontRenderingSizeId, fontSize);
		return resource;
	}
	public static ResourceDictionary SetTypeface(this ResourceDictionary resource, Typeface typeface) {
		resource.SetValue(ClassificationFormatDefinition.TypefaceId, typeface);
		return resource;
	}
	public static ResourceDictionary SetTextDecorations(this ResourceDictionary resource, TextDecorationCollection decorations) {
		resource.SetValue(ClassificationFormatDefinition.TextDecorationsId, decorations);
		return resource;
	}
	public static void Remove(this IEditorFormatMap map, string formatName, string key) {
		map.GetProperties(formatName).Remove(key);
	}
	public static TextFormattingRunProperties AsFormatProperties(this ResourceDictionary resource) {
		return MergeFormatProperties(resource, TextFormattingRunProperties.CreateTextFormattingRunProperties());
	}

	public static TextFormattingRunProperties MergeFormatProperties(this ResourceDictionary resource, TextFormattingRunProperties properties) {
		foreach (System.Collections.DictionaryEntry item in resource) {
			switch (item.Key.ToString()) {
				case EditorFormatDefinition.ForegroundBrushId:
					properties = properties.SetForegroundBrush(item.Value as WpfBrush); break;
				case EditorFormatDefinition.ForegroundColorId:
					properties = properties.SetForeground((WpfColor)item.Value); break;
				case ClassificationFormatDefinition.ForegroundOpacityId:
					properties = properties.SetForegroundOpacity((double)item.Value); break;
				case EditorFormatDefinition.BackgroundBrushId:
					properties = properties.SetBackgroundBrush(item.Value as WpfBrush); break;
				case EditorFormatDefinition.BackgroundColorId:
					properties = properties.SetBackground((WpfColor)item.Value); break;
				case ClassificationFormatDefinition.BackgroundOpacityId:
					properties = properties.SetBackgroundOpacity((double)item.Value); break;
				case ClassificationFormatDefinition.IsBoldId:
					properties = properties.SetBold((bool)item.Value); break;
				case ClassificationFormatDefinition.IsItalicId:
					properties = properties.SetItalic((bool)item.Value); break;
				case ClassificationFormatDefinition.TextDecorationsId:
					properties = properties.SetTextDecorations(item.Value as TextDecorationCollection); break;
				case ClassificationFormatDefinition.TypefaceId:
					properties = properties.SetTypeface(item.Value as Typeface); break;
				case ClassificationFormatDefinition.FontHintingSizeId:
					properties = properties.SetFontHintingEmSize((double)item.Value); break;
				case ClassificationFormatDefinition.FontRenderingSizeId:
					properties = properties.SetFontRenderingEmSize((double)item.Value); break;
				case ClassificationFormatDefinition.TextEffectsId:
					properties = properties.SetTextEffects(item.Value as TextEffectCollection); break;
			}
		}
		return properties;
	}
	#endregion

	/// <summary>
	/// A dummy classification type simply to serve the purpose of grouping classification types in the configuration list
	/// </summary>
	sealed class ClassificationCategory(string classification) : IClassificationType
	{
		public string Classification { get; } = classification;
		public IEnumerable<IClassificationType> BaseTypes => Array.Empty<IClassificationType>();

		public bool IsOfType(string type) { return false; }
	}

	sealed class ClassificationTypeComparer : IEqualityComparer<IClassificationType>
	{
		public static readonly ClassificationTypeComparer Instance = new();

		public bool Equals(IClassificationType x, IClassificationType y) {
			return x.Classification == y.Classification;
		}

		public int GetHashCode(IClassificationType obj) {
			return obj.Classification?.GetHashCode() ?? 0;
		}
	}

}
