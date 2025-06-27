using System.Collections.Generic;
using System.Drawing;
using System.Windows.Controls;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using WpfColor = System.Windows.Media.Color;

namespace Codist
{
	internal static class VsImageHelper
	{
		internal const int DefaultIconSize = 16;
		internal const int MiddleIconSize = 24;
		internal const int LargeIconSize = 32;
		internal const int XLargeIconSize = 48;

		/// <summary>
		/// Gets a themed <see cref="Image"/> from a value defined in <see cref="KnownImageIds"/>
		/// </summary>
		/// <param name="imageId">The image id.</param>
		public static System.Windows.FrameworkElement GetImage(int imageId, double size = 0) {
			if (imageId.HasOverlay()) {
				return MakeOverlayImage(imageId, size);
			}
			var moniker = new ImageMoniker {
				Guid = KnownImageIds.ImageCatalogGuid,
				Id = imageId
			};
			if (size < 1) {
				size = DefaultIconSize;
			}
			return new CrispImage {
				Moniker = moniker,
				Height = size,
				Width = size,
			};
		}
		public static System.Windows.FrameworkElement GetImage(string monikerName, int size = 0) {
			return GetImage(KnownMonikerNameMap.Map.TryGetValue(monikerName, out int i) ? i : KnownImageIds.Blank, size);
		}

		public static TControl ReferenceCrispImageBackground<TControl>(this TControl target, object colorKey) where TControl : System.Windows.FrameworkElement {
			if (colorKey != null) {
				target.SetResourceReference(ImageThemingUtilities.ImageBackgroundColorProperty, colorKey);
			}
			else {
				target.ClearValue(ImageThemingUtilities.ImageBackgroundColorProperty);
			}
			return target;
		}
		public static void SetBackgroundForCrispImage(this System.Windows.DependencyObject target, WpfColor color) {
			ImageThemingUtilities.SetImageBackgroundColor(target, color);
		}

		static Grid MakeOverlayImage(int imageId, double size) {
			var (baseImageId, overlayImageId, fullOverlay) = imageId.DeconstructIconOverlay();
			var baseImage = new ImageMoniker { Guid = KnownImageIds.ImageCatalogGuid, Id = baseImageId };
			var overlay = new ImageMoniker { Guid = KnownImageIds.ImageCatalogGuid, Id = overlayImageId };
			if (size < 1) {
				size = DefaultIconSize;
			}
			return new Grid {
				Height = size,
				Width = size,
				Children = {
					new CrispImage { Moniker = baseImage, Height = size, Width = size },
					new CrispImage {
						Moniker = overlay,
						Height = fullOverlay ? size : size *= 0.6,
						Width = size,
						HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
						VerticalAlignment = System.Windows.VerticalAlignment.Bottom
					}
				}
			};
		}

		static class KnownMonikerNameMap
		{
			internal static readonly Dictionary<string, int> Map = CreateMap();

			static Dictionary<string, int> CreateMap() {
				const int FIELD_COUNT_OF_KnownImageIds = 3760;
				var d = new Dictionary<string, int>(FIELD_COUNT_OF_KnownImageIds);
				var intType = typeof(int);
				foreach (var item in typeof(KnownImageIds).GetFields()) {
					if (item.FieldType == intType) {
						d.Add(item.Name, (int)item.GetValue(null));
					}
				}
				return d;
			}
		}
	}
}