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

namespace Filentropia
{
    public enum AppState
    {
        NoFolderSelected,
        FolderSelected,
        FolderShared
    }

    /// <summary>
    /// Sharity is fun name too, but it already exist in linux.
    /// </summary>
    public partial class MainWindow : Window
    {
        private AppState appState = AppState.NoFolderSelected;
        private string folderPath;
        private FileSystemWatcher watcher = null;

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
        public bool SetState(AppState newState, string folderPath = "")
        {
            switch(appState)
            {
                case AppState.NoFolderSelected:
                    if(newState == AppState.FolderSelected)
                    {
                        // Going from no folder selected to folder selected is ok.
                        this.folderPath = folderPath;

                        appState = newState;
                        UpdateInterface();
                        return true;
                    }
                    break;
                case AppState.FolderSelected:
                    if (newState == AppState.FolderShared)
                    {
                        // A folder is selected, and now user want to share it. That's ok.
                        SetupFolderListener();

                        appState = newState;
                        UpdateInterface();

                        return true;
                    }
                    else if(newState == AppState.FolderSelected)
                    {
                        // User wanted to select a new folder, still not shared. That's ok.
                        this.folderPath = folderPath;

                        appState = newState;
                        UpdateInterface();
                        return true;
                    }
                    break;
                case AppState.FolderShared:
                    if (newState == AppState.FolderSelected)
                    {
                        // The currently shared folder is unshared, since a new folder is selected. That's ok.
                        RemoveFolderListener();

                        appState = newState;
                        UpdateInterface();
                        return true;
                    }
                    else if(newState == AppState.NoFolderSelected)
                    {
                        // The currently shared folder is unshared, since user wanted to unshare it. That's ok.
                        RemoveFolderListener();
                        this.folderPath = "none";

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
                    SelectedFolderLabel.Content = "Selected folder: " + folderPath;
                    AddFolderButton.Content = "Select another folder";
                    ShareFolderButton.IsEnabled = true;
                    UnshareFolderButton.IsEnabled = false;
                    break;
                case AppState.FolderShared:
                    SelectedFolderLabel.Content = "Shared folder: " + folderPath;
                    AddFolderButton.IsEnabled = false;
                    ShareFolderButton.IsEnabled = false;
                    UnshareFolderButton.IsEnabled = true;
                    break;
                default:
                    throw new Exception("You forgot adding this state! " + appState);
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

        private bool SetupFolderListener()
        {
            try
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
            catch(Exception ex)
            {
                MessageBox.Show("Folder listener exception: " + ex.Message);
                return false;
            }

            return true;
        }

        private void FolderContent_Error(object sender, ErrorEventArgs e)
        {
            MessageBox.Show("A folder listener exception happened: " + e.GetException().Message, "Restarting listener");

            if (!SetupFolderListener())
            {
                MessageBox.Show("Could not recreate the folder listener, no changes you make to the folder will be uploaded. Please restart the app. ", "Error");
            }
        }

        private void FolderContent_Deleted(object sender, FileSystemEventArgs e)
        {
            string fileName = e.Name;

            MessageBox.Show("File deleted: " + fileName);

            // Delete file online as well.
        }

        private void FolderContent_Renamed(object sender, RenamedEventArgs e)
        {
            string oldFileName = e.OldName;
            string newFileName = e.Name;

            MessageBox.Show("File renamed: " + oldFileName + " -> " + newFileName);

            // Rename file online as well.
        }

        private void FolderContent_Created(object sender, FileSystemEventArgs e)
        {
            string fileName = e.Name;

            MessageBox.Show("File created: " + fileName);

            // Upload new file to server.
        }

        private void FolderContent_Changed(object sender, FileSystemEventArgs e)
        {
            string fileName = e.Name;

            MessageBox.Show("File changed: " + fileName);

            // Upload and overwrite file at server.
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
    }
}
