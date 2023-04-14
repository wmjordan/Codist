using System.Windows;

namespace Codist
{
	internal static class SharedDictionaryManager
	{
		static ResourceDictionary __Controls, __Menu, __ContextMenu, __VirtualList, __SymbolList;

		internal static ResourceDictionary ThemedControls => __Controls ?? (__Controls = WpfHelper.LoadComponent("controls/ThemedControls.xaml"));

		// to get started with our own context menu styles, see this answer on StackOverflow
		// https://stackoverflow.com/questions/3391742/wpf-submenu-styling?rq=1
		internal static ResourceDictionary ContextMenu => __ContextMenu ?? (__ContextMenu = WpfHelper.LoadComponent("controls/ContextMenu.xaml").MergeWith(ThemedControls));

		// for menu styles, see https://docs.microsoft.com/en-us/dotnet/framework/wpf/controls/menu-styles-and-templates
		internal static ResourceDictionary NavigationBar => __Menu ?? (__Menu = WpfHelper.LoadComponent("controls/NavigationBar.xaml").MergeWith(ThemedControls));

		internal static ResourceDictionary VirtualList => __VirtualList ?? (__VirtualList = WpfHelper.LoadComponent("controls/VirtualList.xaml").MergeWith(ThemedControls));

		internal static ResourceDictionary SymbolList => __SymbolList ?? (__SymbolList = WpfHelper.LoadComponent("controls/SymbolList.xaml").MergeWith(ThemedControls));
	}
}
