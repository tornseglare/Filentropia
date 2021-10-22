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
using System.Collections.ObjectModel;

namespace Filentropia
{
    // JUST NU: 
    // 1. DONE: FolderListener ska ha en funktion Share() som aktiverar alla lyssnare, samt en Unshare() som tar bort dem igen.
    // 2. YEP: RemoveFolderListener() är väl rätt.
    // <-Nu kan man lägga till en katalog i listan, klicka på Share, och så är den det. :) 

    public enum AppState
    {
        NoFolderSelected,
        FolderSelected,
        FolderShared
    }

    /// <summary>
    /// Sharity is fun name too, but it already exist in linux.
    /// Syncopia sounds nice too.
    /// </summary>
    public partial class MainWindow : Window
    {
        private AppState appState = AppState.NoFolderSelected;
        private string folderOnePath;
        private string folderTwoPath;

        // TODO: Easy enough to add x folders later.
        // To let the FileListenersListBox ui element listen for changes in the list of folderListeners, we let it be of type ObservableCollection.
        private ObservableCollection<FolderListener> folderListeners = new ObservableCollection<FolderListener>();

        // These two folders will be synchronized.
        private FolderListener folderOneListener;
        private FolderListener folderTwoListener;

        // Hmm, tanken är att appen ska tanka upp filerna via ftp till servern, och så ska alla andra
        // som upptäcker förändringarna automatiskt ladda ner nya och förändrade filer. 

        public MainWindow()
        {
            InitializeComponent();

            // NOTE: I don't use ListBox and ItemsSource and Styles, because I don't know how to create clickable buttons inside each element. I want each element to behave like its own entity.
            // Magic happens here, the listbox gets connected with the folderListeners-list.
            // FolderListenersListBox.ItemsSource = folderListeners;

            UpdateInterface();
        }

        public bool AddFolderListener(string folderPath)
        {
            FolderListener fl = folderListeners.Where(f => f.FolderPath == folderPath).FirstOrDefault();

            if (fl == null)
            {
                fl = new FolderListener(folderPath);
                folderListeners.Add(fl);

                FolderListenersStackPanel.Children.Add(new UserControlFolderListener(fl, this));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes the folder listener with the given folderPath.
        /// </summary>
        public void RemoveFolderListener(string folderPath)
        {
            FolderListener fl = folderListeners.Where(f => f.FolderPath == folderPath).FirstOrDefault();

            if (fl != null)
            {
                _ = folderListeners.Remove(fl);
            }

            foreach(UserControlFolderListener l in FolderListenersStackPanel.Children)
            {
                if(l.FolderName == folderPath)
                {
                    FolderListenersStackPanel.Children.Remove(l);
                    break;
                }
            }
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

        private void ShareFolder2Button_Click(object sender, RoutedEventArgs e)
        {
            folderTwoListener = new FolderListener(folderTwoPath);
        }

        private void AddNewFolderButton_Click(object sender, RoutedEventArgs e)
        {
            // Microsoft.WindowsAPICodePack.Dialogs
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if(!AddFolderListener(dialog.FileName))
                {
                    MessageBox.Show("Folder is already in list!");
                }
            }
        }

        private void FolderListenersListBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }
    }
}
