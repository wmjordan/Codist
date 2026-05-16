using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CLR;
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

			if (_List._ViewMode == ViewMode.Documents) {
				var state = item.FileState;
				if (state != 0) {
					var s = new TextBlock();
					if (state.MatchFlags(FileState.Modified)) {
						s.AddImage(IconIds.Modified).Append(R.T_Modified);
					}
					if (state.MatchFlags(FileState.Pinned)) {
						s.AddImage(IconIds.Pin).Append(R.T_Pinned);
					}
					if (state.MatchFlags(FileState.Virtual)) {
						s.AddImage(IconIds.FileVirtual).Append(R.T_VirtualFile);
					}
					if (state.MatchFlags(FileState.New)) {
						s.AddImage(IconIds.NewFile).Append(R.T_NewFile);
					}
					//if (state.MatchFlags(FileState.Uninitialized)) {
					//	s.AddImage(IconIds.Hibernated).Append("Not loaded");
					//}
					panel.Children.Add(s);
				}
			}

			switch (item.Type) {
				case FileItemType.InaccessibleFolder:
					return panel;
				case FileItemType.OpenedDocument:
					panel.Children.Add(new TextBlock {
						Text = R.T_Folder + Path.GetDirectoryName(item.FullPath),
						TextWrapping = TextWrapping.Wrap
					});
					break;
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
