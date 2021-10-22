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

namespace Filentropia
{
    /// <summary>
    /// Interaction logic for UserControlFolderListener.xaml
    /// </summary>
    public partial class UserControlFolderListener : UserControl
    {
        protected FolderListener folderListener;
        protected MainWindow mainWindow;

        public string FolderName
        {
            get 
            {
                return folderListener.FolderPath;
            }
        }

        public UserControlFolderListener(FolderListener folderListener, MainWindow mainWindow)
        {
            InitializeComponent();

            this.folderListener = folderListener;
            this.mainWindow = mainWindow;
            FolderNameLabel.Content = FolderName;

            UpdateInterface();
        }

        private void UpdateInterface()
        {
            if(folderListener.Shared)
            {
                ShareFolderButton.Visibility = Visibility.Hidden;
                UnShareFolderButton.Visibility = Visibility.Visible;
                CloseButton.Visibility = Visibility.Hidden;
            }
            else
            {
                ShareFolderButton.Visibility = Visibility.Visible;
                UnShareFolderButton.Visibility = Visibility.Hidden;
                CloseButton.Visibility = Visibility.Visible;
            }
        }

        private void ShareFolderButton_Click(object sender, RoutedEventArgs e)
        {
            folderListener.Share();
            UpdateInterface();
        }

        private void UnShareFolderButton_Click(object sender, RoutedEventArgs e)
        {
            folderListener.Unshare();
            UpdateInterface();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.RemoveFolderListener(folderListener.FolderPath);
        }
    }
}
