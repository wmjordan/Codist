using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Codist.Interop
{
	[ComImport]
	[Guid("0D915B59-2ED7-472A-9DE8-9161737EA1C5")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[TypeIdentifier]
	public interface SVsColorThemeService
	{
	}

	[ComImport]
	[Guid("EAB552CF-7858-4F05-8435-62DB6DF60684")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[TypeIdentifier]
	public interface IVsColorThemeService
	{
		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		void NotifyExternalThemeChanged();

		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		uint GetCurrentVsColorValue([In] int vsSysColor);

		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		uint GetCurrentColorValue([In] ref Guid rguidColorCategory, [In][MarshalAs(UnmanagedType.LPWStr)] string pszColorName, [In]uint dwColorType);

		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		uint GetCurrentEncodedColor([In] ref Guid rguidColorCategory, [In][MarshalAs(UnmanagedType.LPWStr)] string pszColorName, [In] uint dwColorType);

		[DispId(0x60010004)]
		IVsColorThemes Themes {
			[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
			[return: MarshalAs(UnmanagedType.Interface)]
			get;
		}

		[DispId(0x60010005)]
		IVsColorNames ColorNames {
			[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
			[return: MarshalAs(UnmanagedType.Interface)]
			get;
		}

		[DispId(0x60010006)]
		IVsColorTheme CurrentTheme {
			[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
			[return: MarshalAs(UnmanagedType.Interface)]
			get;
		}
	}

	[ComImport]
	[Guid("98192AFE-75B9-4347-82EC-FF312C1995D8")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[TypeIdentifier]
	public interface IVsColorThemes
	{
		[MethodImpl(MethodImplOptions.InternalCall)]
		[return: MarshalAs(UnmanagedType.Interface)]
		IVsColorTheme GetThemeFromId([In] Guid ThemeId);

		[DispId(0)]
		IVsColorTheme this[[In] int index] {
			[MethodImpl(MethodImplOptions.InternalCall)]
			[return: MarshalAs(UnmanagedType.Interface)]
			get;
		}

		[DispId(0x60010002)]
		int Count {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}

		[MethodImpl(MethodImplOptions.InternalCall)]
		[return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "System.Runtime.InteropServices.CustomMarshalers.EnumeratorToEnumVariantMarshaler, CustomMarshalers, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
		IEnumerator GetEnumerator();
	}

	[ComImport]
	[Guid("413D8344-C0DB-4949-9DBC-69C12BADB6AC")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[TypeIdentifier]
	public interface IVsColorTheme
	{
		[MethodImpl(MethodImplOptions.InternalCall)]
		void Apply();

		[DispId(0)]
		IVsColorEntry this[[In][ComAliasName("Microsoft.Internal.VisualStudio.Shell.Interop.ColorName")] ColorName Name] {
			[MethodImpl(MethodImplOptions.InternalCall)]
			[return: MarshalAs(UnmanagedType.Interface)]
			get;
		}

		[DispId(0x60010002)]
		Guid ThemeId {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}

		[DispId(0x60010003)]
		string Name {
			[MethodImpl(MethodImplOptions.InternalCall)]
			[return: MarshalAs(UnmanagedType.BStr)]
			get;
		}

		[DispId(0x60010004)]
		bool IsUserVisible {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("BBE70639-7AD9-4365-AE36-9877AF2F973B")]
	[TypeIdentifier]
	public interface IVsColorEntry
	{
		[DispId(0x60010000)]
		ColorName ColorName {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}

		[DispId(0x60010001)]
		byte BackgroundType {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}

		[DispId(0x60010002)]
		byte ForegroundType {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}

		[DispId(0x60010003)]
		uint Background {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}

		[DispId(0x60010004)]
		uint Foreground {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}

		[DispId(0x60010005)]
		uint BackgroundSource {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}

		[DispId(0x60010006)]
		uint ForegroundSource {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	[TypeIdentifier("EF2A7BE1-84AF-4E47-A2CF-056DF55F3B7A", "Microsoft.Internal.VisualStudio.Shell.Interop.ColorName")]
	public struct ColorName
	{
		public Guid Category;

		[MarshalAs(UnmanagedType.BStr)]
		public string Name;
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("92144F7A-61DE-439B-AA66-13BE7CDEC857")]
	[TypeIdentifier]
	public interface IVsColorNames
	{
		[MethodImpl(MethodImplOptions.InternalCall)]
		ColorName GetNameFromVsColor([In] int vsSysColor);

		[DispId(0)]
		ColorName this[[In] int index] {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}

		[DispId(0x60010002)]
		int Count {
			[MethodImpl(MethodImplOptions.InternalCall)]
			get;
		}

		[MethodImpl(MethodImplOptions.InternalCall)]
		[return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "System.Runtime.InteropServices.CustomMarshalers.EnumeratorToEnumVariantMarshaler, CustomMarshalers, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
		IEnumerator GetEnumerator();
	}
}
