using Files.Dialogs;
using Files.Filesystem;
using Files.Helpers;
using Files.UserControls;
using Microsoft.Toolkit.Uwp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Storage;
using static Files.Dialogs.ConfirmDeleteDialog;

namespace Files.Commands
{
    public partial class ItemOperations
    {
        public static async void DeleteItemWithStatus(StorageDeleteOption deleteOption)
        {
            var deleteFromRecycleBin = App.CurrentInstance.FilesystemViewModel.WorkingDirectory.StartsWith(App.AppSettings.RecycleBinPath);
            if (deleteFromRecycleBin)
            {
                // Permanently delete if deleting from recycle bin
                deleteOption = StorageDeleteOption.PermanentDelete;
            }

            // Get selected items before showing the prompt to prevent deleting items selected after the prompt
            var CurrentInstance = App.CurrentInstance;

            PostedStatusBanner bannerResult = null;
            if (deleteOption == StorageDeleteOption.PermanentDelete)
            {
                bannerResult = App.CurrentInstance.StatusBarControl.OngoingTasksControl.PostBanner(null,
                CurrentInstance.FilesystemViewModel.WorkingDirectory,
                0,
                StatusBanner.StatusBannerSeverity.Ongoing,
                StatusBanner.StatusBannerOperation.Delete);
            }
            else
            {
                bannerResult = App.CurrentInstance.StatusBarControl.OngoingTasksControl.PostBanner(null,
                CurrentInstance.FilesystemViewModel.WorkingDirectory,
                0,
                StatusBanner.StatusBannerSeverity.Ongoing,
                StatusBanner.StatusBannerOperation.Recycle);
            }

            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                await DeleteItem(deleteOption, CurrentInstance, bannerResult.Progress);
                bannerResult.Remove();

                sw.Stop();

                if (sw.Elapsed.TotalSeconds >= 10)
                {
                    if (deleteOption == StorageDeleteOption.PermanentDelete)
                    {
                        App.CurrentInstance.StatusBarControl.OngoingTasksControl.PostBanner(
                        "Deletion Complete",
                        "The operation has completed.",
                        0,
                        StatusBanner.StatusBannerSeverity.Success,
                        StatusBanner.StatusBannerOperation.Delete);
                    }
                    else
                    {
                        App.CurrentInstance.StatusBarControl.OngoingTasksControl.PostBanner(
                        "Recycle Complete",
                        "The operation has completed.",
                        0,
                        StatusBanner.StatusBannerSeverity.Success,
                        StatusBanner.StatusBannerOperation.Recycle);
                    }
                }

                App.CurrentInstance.NavigationToolbar.CanGoForward = false;
            }
            catch (UnauthorizedAccessException)
            {
                bannerResult.Remove();
                App.CurrentInstance.StatusBarControl.OngoingTasksControl.PostBanner(
                    "AccessDeniedDeleteDialog/Title".GetLocalized(), 
                    "AccessDeniedDeleteDialog/Text".GetLocalized(),
                    0, 
                    StatusBanner.StatusBannerSeverity.Error, 
                    StatusBanner.StatusBannerOperation.Delete);
            }
            catch (FileNotFoundException)
            {
                bannerResult.Remove();
                App.CurrentInstance.StatusBarControl.OngoingTasksControl.PostBanner(
                    "FileNotFoundDialog/Title".GetLocalized(),
                    "FileNotFoundDialog/Text".GetLocalized(),
                    0,
                    StatusBanner.StatusBannerSeverity.Error,
                    StatusBanner.StatusBannerOperation.Delete);
            }
            catch (IOException)
            {
                bannerResult.Remove();
                App.CurrentInstance.StatusBarControl.OngoingTasksControl.PostActionBanner(
                    "FileInUseDeleteDialog/Title".GetLocalized(),
                    "FileInUseDeleteDialog/Text".GetLocalized(),
                    "FileInUseDeleteDialog/PrimaryButtonText".GetLocalized(),
                    "FileInUseDeleteDialog/SecondaryButtonText".GetLocalized(), () => { DeleteItemWithStatus(deleteOption); });
            }
        }

        private static async Task DeleteItem(StorageDeleteOption deleteOption, IShellPage AppInstance, IProgress<uint> progress)
        {
            var deleteFromRecycleBin = AppInstance.FilesystemViewModel.WorkingDirectory.StartsWith(App.AppSettings.RecycleBinPath);

            List<ListedItem> selectedItems = new List<ListedItem>();
            foreach (ListedItem selectedItem in AppInstance.ContentPage.SelectedItems)
            {
                selectedItems.Add(selectedItem);
            }

            if (App.AppSettings.ShowConfirmDeleteDialog == true) //check if the setting to show a confirmation dialog is on
            {
                var dialog = new ConfirmDeleteDialog(deleteFromRecycleBin, deleteOption);
                await dialog.ShowAsync();

                if (dialog.Result != MyResult.Delete) //delete selected  item(s) if the result is yes
                {
                    return; //return if the result isn't delete
                }
                deleteOption = dialog.PermanentlyDelete;
            }

            int itemsDeleted = 0;
            foreach (ListedItem storItem in selectedItems)
            {
                uint progressValue = (uint)(itemsDeleted * 100.0 / selectedItems.Count);
                if (selectedItems.Count > 3) { progress.Report((uint)progressValue); }

                var deleted = (FilesystemResult)false;
                if (storItem.PrimaryItemAttribute == StorageItemTypes.File)
                {
                    var res = await ItemViewModel.GetFileFromPathAsync(storItem.ItemPath, AppInstance);
                    deleted = res ? await res.Result.DeleteAsync(deleteOption).AsTask().Wrap() : res;
                }
                else
                {
                    var res = await ItemViewModel.GetFolderFromPathAsync(storItem.ItemPath, AppInstance);
                    deleted = res ? await res.Result.DeleteAsync(deleteOption).AsTask().Wrap() : res;
                }

                if (deleted.ErrorCode == FilesystemErrorCode.ERROR_UNAUTHORIZED)
                {
                    if (deleteOption == StorageDeleteOption.Default)
                    {
                        // Try again with fulltrust process
                        if (App.Connection != null)
                        {
                            var response = await App.Connection.SendMessageAsync(new ValueSet() {
                                { "Arguments", "FileOperation" },
                                { "fileop", "MoveToBin" },
                                { "filepath", storItem.ItemPath } });
                            deleted = (FilesystemResult)(response.Status == Windows.ApplicationModel.AppService.AppServiceResponseStatus.Success);
                        }
                    }
                    else
                    {
                        // Try again with DeleteFileFromApp
                        if (!NativeDirectoryChangesHelper.DeleteFileFromApp(storItem.ItemPath))
                        {
                            Debug.WriteLine(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                        }
                        else
                        {
                            deleted = (FilesystemResult)true;
                        }
                    }
                }
                else if (deleted.ErrorCode == FilesystemErrorCode.ERROR_INUSE)
                {
                    // TODO: retry or show dialog
                }

                if (deleteFromRecycleBin)
                {
                    // Recycle bin also stores a file starting with $I for each item
                    var iFilePath = Path.Combine(Path.GetDirectoryName(storItem.ItemPath), Path.GetFileName(storItem.ItemPath).Replace("$R", "$I"));
                    var res = await ItemViewModel.GetFileFromPathAsync(iFilePath);
                    if (res)
                    {
                        await res.Result.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wrap();
                    }
                }

                if (deleted)
                {
                    await AppInstance.FilesystemViewModel.RemoveFileOrFolder(storItem);
                    itemsDeleted++;
                }
            }
        }
    }
}