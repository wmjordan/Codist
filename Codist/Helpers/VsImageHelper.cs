using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Controls;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using WpfColor = System.Windows.Media.Color;

namespace Codist;

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
		var moniker = GetImageMoniker(imageId);
		if (size < 1) {
			size = DefaultIconSize;
		}
		return new CrispImage {
			Moniker = moniker,
			Height = size,
			Width = size,
		};
	}

	public static int GetImageIdForFile(string fileName) {
		return VsImageService.GetImageMonikerForFile(fileName).Id;
	}

	public static CrispImage GetImageForFile(string fileName, double size = 0) {
		if (size < 1) {
			size = DefaultIconSize;
		}
		return new CrispImage {
			Moniker = VsImageService.GetImageMonikerForFile(fileName),
			Height = size,
			Width = size,
		};
	}

	public static ImageMoniker GetImageMoniker(int imageId) {
		return new ImageMoniker {
			Guid = KnownImageIds.ImageCatalogGuid,
			Id = imageId
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

	static int GetFileIconId(string extName) {
		return extName?.ToLowerInvariant() switch {
			#region Source Code Files
			".cs" => IconIds.CSFileNode,
			".vb" => IconIds.VBFileNode,
			".cpp" or ".cc" or ".cxx" or ".cp" => IconIds.CPPFileNode,
			".h" or ".hpp" or ".hxx" or ".hh" => IconIds.CPPHeaderFile,
			".c" => IconIds.CFile,
			".fs" or ".fsi" or ".fsx" => IconIds.FSFileNode,
			".py" or ".pyw" => IconIds.PYFileNode,
			".ts" or ".tsx" => IconIds.TSFileNode,
			".js" => IconIds.JSFile,
			".jsx" => IconIds.JSXFile,
			".php" => IconIds.PHPFile,
			".asm" or ".s" => IconIds.ASMFile,
			#endregion

			#region Web & UI Files
			".html" or ".htm" or ".xhtml" => IconIds.HTMLFile,
			".css" or ".scss" or ".less" => IconIds.CSS,
			".xaml" => IconIds.WPFFile,
			".razor" or ".aspx" or ".ashx" or ".asmx" or ".svc" => IconIds.WebFile,
			#endregion

			#region Data & Configuration Files
			".json" or ".yaml" or ".yml" or ".targets" => IconIds.SettingsFile,
			".xml" or ".xsl" or ".xslt" => IconIds.XMLFile,
			".xsd" => IconIds.XMLSchemaFile,
			".resx" => IconIds.ResxFile,
			".config" or ".conf" or ".ini" or ".props" => IconIds.ConfigurationFile,
			".dtd" => IconIds.XMLDTDFile,
			".md" or ".markdown" => IconIds.MarkdownFile,
			".txt" or ".log" => IconIds.TextFile,
			".db" or ".sqlite" or ".mdf" or ".ldf" => IconIds.DatabaseFile,
			#endregion

			#region Project & Solution Files
			".csproj" => IconIds.CSProjectNode,
			".vbproj" => IconIds.VBProjectNode,
			".fsproj" => IconIds.FSProjectNode,
			".vcxproj" or ".vcproj" => IconIds.CPPProjectNode,
			".pyproj" => IconIds.PYProjectNode,
			".tsproj" or ".esproj" or ".njsproj" => IconIds.TSProjectNode,
			".sln" or ".slnx" => IconIds.VisualStudioFile,
			#endregion

			#region Image & Resource Files
			".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" => IconIds.ImageFile,
			".ico" => IconIds.IconFile,
			".cur" or ".ani" => IconIds.CursorFile,
			".tif" or ".tiff" => IconIds.TifFile,
			#endregion

			#region Build Artifacts & Binary Files
			".dll" or ".sys" or ".bin" or ".dat" => IconIds.BinaryFile,
			".exe" or ".com" => IconIds.ExecutableFile,
			".cmd" or ".bat" or ".wsf" or ".ps" or ".ps1" or ".bash" or ".sh" or ".zsh" or ".ksh" => IconIds.ConsoleFile,
			".tmp" or ".temp" or ".obj" => IconIds.IntermediateFile,
			".pdb" => IconIds.PDBFile,
			".dmp" => IconIds.CrashDumpFile,
			#endregion

			#region Office Files
			".doc" or ".docx" or ".rtf" => IconIds.WordFile,
			".xls" or ".xlsx" or ".xlsm" or ".csv" => IconIds.ExcelFile,
			".ppt" or ".pptx" => IconIds.PowerPointFile,
			".accdb" or ".mdb" => IconIds.AccessFile,
			".msg" or ".pst" or ".ost" => IconIds.OutlookFile,
			".vsdx" or ".vsd" => IconIds.VisioFile,
			".mpp" => IconIds.ProjectFile,
			#endregion

			#region Miscellaneous Files
			".jar" => IconIds.JARFile,
			".zip" or ".rar" or ".7z" or ".cab" or ".gz" or ".tar" => IconIds.CompressedFile,
			".pfx" or ".snk" or ".cer" => IconIds.SignatureFile,
			".manifest" => IconIds.ManifestFile,
			".suo" or ".user" or ".vssettings" or ".vsct" or ".vsixmanifest" or ".vsixlangpack" => IconIds.VisualStudioFile,
			".lnk" => IconIds.SymlinkFile,
			".chm" or ".hlp" => IconIds.CompiledHelpFile,
			".pdf" or ".epub" or ".mobi" or ".djvu" => IconIds.EBookFile,
			".mp4" or ".avi" or ".wmv" or ".flv" or ".mts" or ".mpg" or ".mpeg" or ".wav" or ".mp3" or ".ogg" or ".flac" or ".ape" or ".m4a" or ".aac" or ".m3u" or ".m3u8" or ".cue" => IconIds.MediaFile,
			".bak" => IconIds.BackupFile,
			#endregion

			_ => IconIds.OtherFile
		};
	}

	static class VsImageService
	{
		// hack: Due to incompatibility of VS SDK accross VS 15 through 18,
		//   we can't use ServicesHelper.Get<IVsImageService2, SVsImageService>() to obtain an instance of IVsImageService.
		//   Hence a reflection hack is used.
		static readonly object __ServiceObject = Package.GetGlobalService(typeof(SVsImageService));
		static readonly Func<object, string, ImageMoniker> __GetImageMonikerForFile = ReflectionHelper.CreateMethodInvoker<string, ImageMoniker>(__ServiceObject, nameof(IVsImageService2.GetImageMonikerForFile));

		public static ImageMoniker GetImageMonikerForFile(string fileName) {
			var moniker = __GetImageMonikerForFile(__ServiceObject, fileName);
			return moniker.Id < 0
				? new ImageMoniker { // use fallback icon mapper
					Guid = KnownImageIds.ImageCatalogGuid,
					Id = GetFileIconId(System.IO.Path.GetExtension(fileName))
				}
				: moniker;
		}
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