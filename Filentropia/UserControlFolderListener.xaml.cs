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
        protected string folderName;
        protected bool shared = false;

        protected MainWindow mainWindow;

        public string FolderName
        {
            get 
            {
                return folderName;
            }
        }

        public UserControlFolderListener(string folderName, MainWindow mainWindow)
        {
            InitializeComponent();

            this.folderName = folderName;
            this.mainWindow = mainWindow;
            FolderNameLabel.Content = folderName;

            UpdateInterface();
        }

        private void UpdateInterface()
        {
            if(shared)
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
            shared = true;
            UpdateInterface();
        }

        private void UnShareFolderButton_Click(object sender, RoutedEventArgs e)
        {
            shared = false;
            UpdateInterface();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.RemoveFolderListener(folderName);
        }
    }
}
