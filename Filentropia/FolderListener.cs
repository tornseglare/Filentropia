using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Diagnostics;
using System.Windows;

namespace Filentropia
{
    /// <summary>
    /// Keeps track of a single folder and its contents.
    /// </summary>
    public class FolderListener : IDisposable
    {
        private FileSystemWatcher watcher;

        private FileEvents fileEvents = new FileEvents();

        public string FolderPath { get; init; }

        public bool Shared
        {
            get
            {
                if (watcher == null)
                    return false;
                else
                    return true;
            }
        }

        public FolderListener(string folderPath)
        {
            this.FolderPath = folderPath;
        }

        /// <summary>
        /// Start recording file events for the folder.
        /// </summary>
        public void Share()
        {
            if (watcher == null)
            {
                try
                {
                    SetupFolderListener();
                }
                catch (Exception ex)
                {
                    throw new Exception("Could not setup folder listeners for " + FolderPath, ex);
                }
            }
        }

        /// <summary>
        /// Stop sharing. Empty the fileEvents list at once, discarding any pending uploads.
        /// </summary>
        public void Unshare()
        {
            RemoveFolderListener();

            fileEvents.ClearList();
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

            watcher = new FileSystemWatcher(FolderPath);

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
            catch (Exception ex)
            {
                MessageBox.Show($"Could not recreate the folder listener, no changes you make to the folder will be uploaded. Please restart the app.{Environment.NewLine}Folder: {FolderPath}{Environment.NewLine}Exception message: {ex.Message}", "Error");
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

}
