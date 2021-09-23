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

namespace Filentropia
{
    public enum AppState
    {
        NoFolderSelected,
        FolderSelected,
        FolderShared
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AppState appState = AppState.NoFolderSelected;
        private string folderPath;

        // Hmm, tanken är att appen ska tanka upp filerna via ftp till servern, och så ska alla andra
        // som upptäcker förändringarna automatiskt ladda ner nya och förändrade filer. 

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
                        appState = newState;
                        this.folderPath = folderPath;
                        UpdateInterface();
                        return true;
                    }
                    break;
                case AppState.FolderSelected:
                    if (newState == AppState.FolderShared)
                    {
                        // A folder is selected, and now user want to share it. That's ok.
                        appState = newState;
                        UpdateInterface();
                        return true;
                    }
                    else if(newState == AppState.FolderSelected)
                    {
                        // User wanted to select a new folder, still not shared. That's ok.
                        appState = newState;
                        this.folderPath = folderPath;
                        UpdateInterface();
                        return true;
                    }
                    break;
                case AppState.FolderShared:
                    if (newState == AppState.FolderSelected)
                    {
                        // The currently shared folder is unshared, since a new folder is selected. That's ok.
                        appState = newState;
                        UpdateInterface();
                        return true;
                    }
                    else if(newState == AppState.NoFolderSelected)
                    {
                        // The currently shared folder is unshared, since user wanted to unshare it. That's ok.
                        appState = newState;
                        this.folderPath = "none";
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
