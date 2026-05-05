using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using R = Codist.Properties.Resources;

namespace Codist.FileBrowser;

sealed partial class FileList
{
	sealed class FileItemToTooltipConverter(FileList list) : IValueConverter
	{
		readonly FileList _List = list;

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is not FileItem item) return null;

			var panel = new StackPanel { Margin = WpfHelper.MiddleMargin }.LimitSize();

			panel.Children.Add(new TextBlock {
				Text = item.Type switch {
					FileItemType.Folder => R.T_Folder,
					FileItemType.EmptyFolder => R.T_EmptyFolder,
					FileItemType.InaccessibleFolder => R.T_UnauthorizedFolder,
					FileItemType.Solution => R.T_Solution,
					FileItemType.Project => R.T_Project,
					FileItemType.UnloadedProject => R.T_UnloadedProject,
					_ => R.T_File
				} + item.Name,
				FontWeight = FontWeights.Bold,
				Margin = WpfHelper.MiddleBottomMargin,
				TextWrapping = TextWrapping.Wrap
			});

			if (item.Type == FileItemType.InaccessibleFolder) {
				return panel;
			}

			if (_List._ViewMode != ViewMode.File && item.Type == FileItemType.File) {
				panel.Children.Add(new TextBlock { Text = R.T_Folder + Path.GetDirectoryName(item.FullPath) });
			}

			panel.Children.Add(new TextBlock { Text = R.T_CreateTime + item.CreationTime.ToString("yyyy-MM-dd HH:mm:ss") });
			panel.Children.Add(new TextBlock { Text = R.T_UpdateTime + item.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") });

			if (item.IsFile) {
				panel.Children.Add(new TextBlock { Text = R.T_FileSize + item.FormattedFileSize });
			}

			return panel;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

}
