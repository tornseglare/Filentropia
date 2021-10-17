using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Diagnostics;

namespace Filentropia
{
    public enum AppState
    {
        NoFolderSelected,
        FolderSelected,
        FolderShared
    }

    public enum eFileEvents
    {
        Deleted,
        Renamed,
        Created,
        Changed,
    }

    /// <summary>
    /// Example of new 'init':
    ///     var fe = new FileEvent() { FileName = "coolfilename.wow" };
    /// 
    /// Now fe.FileName is readonly since it only has a getter.
    /// 
    /// You can create a copy of a record and change its values in this way:
    ///     var changed_fe = fe with { FileName = "muchcoolerfilename.yeah" };
    /// 
    /// https://www.infoworld.com/article/3607372/how-to-work-with-record-types-in-csharp-9.html
    /// 
    /// Why 'record' ? 
    ///     Equals() are comparing every member by default, so I dont have to write my own.
    ///     More here: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-9.0/records
    /// </summary>
    public record FileEvent
    {
        // FileName is the identier for the file.
        // The init means we can set this 'readonly' value when creating a new FileEvent. After creation this value is readonly since it only has a getter.
        // 'private set' makes it settable in this class functions.
        // 'protected set' makes it settable in deriving classes as well.
        public string FileName { get; init; }
        
        public eFileEvents Event { get; init; }
    }

    public record FileNameChangedEvent : FileEvent
    {
        public string OldFileName { get; init; }
    }

    public class FileEvents
    {
        private List<FileEvent> fileEvents = new List<FileEvent>();

        public void FileDeleted(string fileName)
        {
            FileEvent fe = new FileEvent() { Event = eFileEvents.Deleted, FileName = fileName };
            fileEvents.Add(fe);
            Debug.Write(fileEvents);
         }
        
        public void FileCreated(string fileName)
        {
            FileEvent fe = new FileEvent() { Event = eFileEvents.Created, FileName = fileName };
            fileEvents.Add(fe);
            Debug.Write(fileEvents);
        }

        public void FileChanged(string fileName)
        {
            FileEvent fe = new FileEvent() { Event = eFileEvents.Changed, FileName = fileName };

            // Since a file change event happens twice, we just ignore the second event.
            // Please note the danger in this: if a file's content changes (very) frequently, this code will 
            // ignore any later changes as long as the file has not been uploaded. 
            //   <-This is on the other hand not dangerous as long as there is no danger of missing a file change
            //     in the moment when the file gets uploaded.
            //    TODO: To avoid this, pop the element from the list FIRST, and after that, upload the file. 
            //          The effect of doing this is that we do not miss any file change events.
            // 
            if (fileEvents.FindIndex(x => x.FileName == fileName && x.Event == eFileEvents.Changed) == -1)
            {
                fileEvents.Add(fe);
                Debug.Write(fileEvents);
            }
        }

        public void FileRenamed(string oldFileName, string newFileName)
        {
            FileNameChangedEvent fe = new FileNameChangedEvent() {
                Event = eFileEvents.Renamed, FileName = newFileName, OldFileName = oldFileName };

            fileEvents.Add(fe);
            Debug.Write(fileEvents);
        }
    }

    /// <summary>
    /// Keeps track of a single folder and its contents.
    /// </summary>
    public class FolderListener : IDisposable
    {
        private string folderPath;
        private FileSystemWatcher watcher;

        private FileEvents fileEvents = new FileEvents();

        public FolderListener(string folderPath)
        {
            this.folderPath = folderPath;

            try
            {
                SetupFolderListener();
            }
            catch(Exception ex)
            {
                throw new Exception("Could not setup folder listeners for " + folderPath, ex);
            }
        }

        private void RemoveFolderListener()
        {
            if (watcher != null)
            {
                watcher.Dispose();
                watcher = null;
            }
        }

        private void SetupFolderListener()
        {
            RemoveFolderListener();

            watcher = new FileSystemWatcher(folderPath);

            /*watcher.NotifyFilter = NotifyFilters.Attributes
                                | NotifyFilters.CreationTime
                                | NotifyFilters.DirectoryName
                                | NotifyFilters.FileName
                                | NotifyFilters.LastAccess
                                | NotifyFilters.LastWrite
                                | NotifyFilters.Security
                                | NotifyFilters.Size;*/

            // Any file changed, created, renamed or deleted in the folder.
            watcher.Changed += FolderContent_Changed;
            watcher.Created += FolderContent_Created;
            watcher.Renamed += FolderContent_Renamed;
            watcher.Deleted += FolderContent_Deleted;

            // An error happened. Simply re-create the watcher.
            watcher.Error += FolderContent_Error;

            // It's always a good thing to enable the listener. 
            watcher.EnableRaisingEvents = true;
        }

        private void FolderContent_Error(object sender, ErrorEventArgs e)
        {
            MessageBox.Show("A folder listener exception happened: " + e.GetException().Message, "Restarting listener");

            try
            {
                SetupFolderListener();
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Could not recreate the folder listener, no changes you make to the folder will be uploaded. Please restart the app.{Environment.NewLine}Folder: {folderPath}{Environment.NewLine}Exception message: {ex.Message}", "Error");
            }
        }

        private void FolderContent_Deleted(object sender, FileSystemEventArgs e)
        {
            string fileName = e.Name;

            MessageBox.Show("File deleted: " + fileName);

            // Delete file online as well.
            fileEvents.FileDeleted(fileName);
        }

        private void FolderContent_Renamed(object sender, RenamedEventArgs e)
        {
            string oldFileName = e.OldName;
            string newFileName = e.Name;

            MessageBox.Show("File renamed: " + oldFileName + " -> " + newFileName);

            // Rename file online as well.
            fileEvents.FileRenamed(oldFileName, newFileName);
        }

        private void FolderContent_Created(object sender, FileSystemEventArgs e)
        {
            string fileName = e.Name;

            MessageBox.Show("File created: " + fileName);

            // Upload new file to server.
            fileEvents.FileCreated(fileName);
        }

        private void FolderContent_Changed(object sender, FileSystemEventArgs e)
        {
            string fileName = e.Name;

            MessageBox.Show("File changed: " + fileName);

            // Upload and overwrite file at server.
            fileEvents.FileChanged(fileName);
        }

        public void Dispose()
        {
            RemoveFolderListener();
            fileEvents = null;
        }
    }

    /// <summary>
    /// Sharity is fun name too, but it already exist in linux.
    /// </summary>
    public partial class MainWindow : Window
    {
        private AppState appState = AppState.NoFolderSelected;
        private string folderOnePath;
        private string folderTwoPath;

        // These two folders will be synchronized.
        private FolderListener folderOneListener;
        private FolderListener folderTwoListener;

        // Hmm, tanken är att appen ska tanka upp filerna via ftp till servern, och så ska alla andra
        // som upptäcker förändringarna automatiskt ladda ner nya och förändrade filer. 

        // TODO: File modified event happens twice on every save, and might be a hogger. 
        // ..add file to a list, fileEvents, and merge file modification events for same file. 
        // Also, upload changes in this list once every say five seconds.

        public MainWindow()
        {
            InitializeComponent();
            UpdateInterface();
        }

        // Allow transitions between states, some of them.
        public bool SetState(AppState newState, string _folderPath = "")
        {
            switch(appState)
            {
                case AppState.NoFolderSelected:
                    if(newState == AppState.FolderSelected)
                    {
                        // Going from no folder selected to folder selected is ok.
                        folderOnePath = _folderPath;

                        appState = newState;
                        UpdateInterface();
                        return true;
                    }
                    break;
                case AppState.FolderSelected:
                    if (newState == AppState.FolderShared)
                    {
                        // A folder is selected, and now user want to share it. That's ok.
                        folderOneListener = new FolderListener(folderOnePath);

                        appState = newState;
                        UpdateInterface();

                        return true;
                    }
                    else if(newState == AppState.FolderSelected)
                    {
                        // User wanted to select a new folder, still not shared. That's ok.
                        folderOnePath = _folderPath;

                        appState = newState;
                        UpdateInterface();
                        return true;
                    }
                    break;
                case AppState.FolderShared:
                    if (newState == AppState.FolderSelected)
                    {
                        // The currently shared folder is unshared, since a new folder is selected. That's ok.
                        folderOneListener.Dispose();
                        folderOneListener = null;

                        appState = newState;
                        UpdateInterface();
                        return true;
                    }
                    else if(newState == AppState.NoFolderSelected)
                    {
                        // The currently shared folder is unshared, since user wanted to unshare it. That's ok.
                        folderOneListener.Dispose();
                        folderOneListener = null;
                        folderOnePath = null;

                        appState = newState;
                        UpdateInterface();
                        return true;
                    }

                    break;
                default:
                    throw new Exception("You forgot adding this state! " + newState);
            }

            // The transition was not ok.
            Console.WriteLine("Transition between " + appState + " to " + newState + " is not allowed.");
            return false;
        }

        private void UpdateInterface()
        {
            switch (appState)
            {
                case AppState.NoFolderSelected:
                    SelectedFolderLabel.Content = "Selected folder: " + "none";
                    AddFolderButton.Content = "Select folder";
                    AddFolderButton.IsEnabled = true;
                    ShareFolderButton.IsEnabled = false;
                    UnshareFolderButton.IsEnabled = false;
                    break;
                case AppState.FolderSelected:
                    SelectedFolderLabel.Content = "Selected folder: " + folderOnePath;
                    AddFolderButton.Content = "Select another folder";
                    ShareFolderButton.IsEnabled = true;
                    UnshareFolderButton.IsEnabled = false;
                    break;
                case AppState.FolderShared:
                    SelectedFolderLabel.Content = "Shared folder: " + folderOnePath;
                    AddFolderButton.IsEnabled = false;
                    ShareFolderButton.IsEnabled = false;
                    UnshareFolderButton.IsEnabled = true;
                    break;
                default:
                    throw new Exception("You forgot adding this state! " + appState);
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            // Microsoft.WindowsAPICodePack.Dialogs
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;

            if(dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                //SelectedFolderLabel.Content = "Selected folder: " + dialog.FileName;
                if (!SetState(AppState.FolderSelected, dialog.FileName))
                    MessageBox.Show("Uh uh");
                else
                    MessageBox.Show("Folder selected, yey!");
            }
        }

        private void ShareFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SetState(AppState.FolderShared))
                MessageBox.Show("Uh uh");
            else
                MessageBox.Show("Folder shared!");
        }

        private void UnshareFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SetState(AppState.NoFolderSelected))
                MessageBox.Show("Uh uh");
            else
                MessageBox.Show("Folder is no longer shared!");

        }

        private void SelectFolder2Button_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                folderTwoPath = dialog.FileName;
            }
        }
    }
}
