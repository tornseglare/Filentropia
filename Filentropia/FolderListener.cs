using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Timers;

namespace Filentropia
{
    /// <summary>
    /// https://docs.microsoft.com/en-us/dotnet/standard/exceptions/how-to-create-localized-exception-messages
    /// </summary>
    [Serializable]
    public class FolderListenerException : Exception
    {
        public string FolderPath { get; }

        public FolderListenerException() { }

        public FolderListenerException(string message)
            : base(message) { }

        public FolderListenerException(string message, Exception inner)
            : base(message, inner) { }

        public FolderListenerException(string message, Exception inner, string folderPath) : base(message, inner)
        {
            FolderPath = folderPath;
        }
    }

    public interface ITarget
    {
        void SendEvent(byte[] fileContent, FileEvent fileEvent);
    }

    /// <summary>
    /// The target is a server with an ip-address, and any files should be sent to it.
    /// 
    /// OBS: Det här är helt nytt: En server måste ju agera en live katalog också, 
    /// med andra ord måste den skicka ut alla file changed events till andra som är online. 
    /// Rätt avancerat mao. Därför får du ha detta i bakhuvudet, men inte egentligen koda server-grejerna just nu, 
    /// utan lägga krutet på FolderTarget. 
    /// </summary>
    public class ServerTarget : ITarget
    {
        private string serverIP;

        public ServerTarget(string serverIP)
        {
            this.serverIP = serverIP;
        }

        public void SendEvent(byte[] fileContent, FileEvent fileEvent)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// The target is a local folder, and any files should be 'sent' to it. 
    /// </summary>
    public class FolderTarget : ITarget
    {
        public void SendEvent(byte[] fileContent, FileEvent fileEvent)
        {
            throw new NotImplementedException();
        }
    }


    /// <summary>
    /// Takes a single file event and send it to a receiver, possibly along with the file's content.
    /// </summary>
    public class SourceToTargetSyncer
    {
        private FolderListener SourceFolder;
        private FolderListener TargetFolder;

        // TODO: TargetFolder är ju feltänk, och SourceFolder behövs inte. Istället ska target anges när en Syncer skapas. Ändra och gör rätt. :)
        private ITarget target;

        private Exception lastException;
        
        public Exception LastException
        {
            get
            {
                Exception exception = lastException;
                lastException = null;
                return exception;
            }
        }

        public SourceToTargetSyncer(FolderListener sourceFolder, FolderListener targetFolder)
        {
            SourceFolder = sourceFolder;
            TargetFolder = targetFolder;
        }

        /// <summary>
        /// Handles the given file event by sending it to the targetFolder.
        /// 
        /// I samtliga fall måste appen ta höjd för att filen används av mottagaren, och vad ska då hända? 
        /// Svar: Inget. Använd beyond compare för att lösa konflikten. Detta är inte git. 
        ///         Det enda filentropia kan göra är följande:
        ///         Renamed: En popup som påpekar att filen användaren arbetar på just bytt namn. Där i en knapp "open log file". Sen lämnas användaren åt sitt öde.
        ///         Deleted: En popup som påpekar att filen raderats av annan användare. Don't worry, your file is still here. If you save your changes the file will even be recreated on your sharemates computers.
        ///         Created: Just här kan detta inte utgöra ett problem. 
        ///         Changed: En popup som påpekar att någon annan ändrat i samma fil, och att en filkonflikt just har uppstått. Där i en knapp "open log file". Sen lämnas användaren åt sitt öde.
        /// </summary>
        public bool HandleFileEvent(FileEvent fileEvent)
        {
            // New format of a switch case didadi. A bit neater and compact. default is replaced with _ for some odd reason.
            bool res = fileEvent.Event switch
            {
                eFileEvents.Sync => HandleSyncEvent(fileEvent),             // Send the file to target.
                eFileEvents.Deleted => HandleDeletedEvent(fileEvent),       // Send a delete order to target.
                eFileEvents.Renamed => HandleRenamedEvent(fileEvent),       // Send a rename order to target.
                eFileEvents.Created => HandleCreatedEvent(fileEvent),       // Send the created file to target.
                eFileEvents.Changed => HandleChangedEvent(fileEvent),       // Send the changed file's content to target.
                _ => throw new FolderListenerException("Missing case in HandleFileEvent!")
            };

            return res;
        }

        private bool HandleSyncEvent(FileEvent fileEvent)
        {
            // Target would like to have the file.
            // TODO: 
            //  1. Det är en fjärran server/dator som vill ha filen. 
            //  2. Det är en katalog på samma dator.
            //  <-Vi låtsas att det är en fjärran. På det viset gör vi det mest komplicerat och säkert:
            //  a. Skicka filen.
            //  b. Mottagaren ser nu att filen redan finns på hens dator.
            //     1. Filen finns redan. Det här är ju ett Sync-event, så vi får anta att mottagaren inte vill få sina filer överskrivna.
            //     2. Skapa en .backup folder, flytta den existerande filen dit.
            //     3. .backup/log.txt får en ny rad om att filen ifråga flyttats hit, och ett datum+tid.
            //     4. Kopiera in den nya filen. 
            //  c. Filen finns inte på mottagarens dator. Kopiera in den nya filen.
            //     

            byte[] bytes = GetFile(fileEvent.FileName);

            if (bytes != null)
            {
                // Cool, we have a file, a sender, and a receiver. 
                // TargetFolder
            }
            else
            {
                return false;
            }

            return true;
        }

        private bool HandleCreatedEvent(FileEvent fileEvent)
        {
            // Target want the new file as well.
            // TODO:
            //  a. Skicka filen.
            //  b. Mottagaren ser nu att filen redan finns på hens dator.
            //     1. Filen finns redan. Det här är ju ett Sync-event, så vi får anta att mottagaren inte vill få sina filer överskrivna.
            //     2. Skapa en .backup folder, flytta den existerande filen dit.
            //     3. .backup/log.txt får en ny rad om att filen ifråga flyttats hit, och ett datum+tid.
            //     4. Kopiera in den nya filen. 
            //  c. Filen finns inte på mottagarens dator. Kopiera in den nya filen.
            // 
            // <-Som synes, det är samma förfarande som för Sync eventet, den enda skillnaden är att här i detta fallet är det märkligt. Filen borde inte existera nämligen, 
            //   och den enda förklaringen jag har just nu är att de har skapat filen på varsin dator samtidigt. Anyway, på det här viset är det ändå säkert.
            // 
            return true;
        }

        private bool HandleDeletedEvent(FileEvent fileEvent)
        {
            // Target must detele the file as well.
            // TODO: 
            //  a. Skicka ett delete-event.
            //  b. Mottagaren skapar en .deleted folder, flytta den existerande filen dit.
            //  c. .deleted/log.txt får en ny rad om att filen ifråga raderats av användare q, och datum+tid.
            // 
            // <-Som jag ser det är det mycket säkrare att göra så här. Annars kan man ju råka radera allt i katalogen, varpå det speglas ut till alla! 
            //   Det vore extra katastrofalt eftersom windows soptunna inte används av appen by default, tror jag? Med andra ord skulle endast den som 
            //   raderade alla filer kunna återställa dem! 
            // 
            return true;
        }

        private bool HandleRenamedEvent(FileEvent fileEvent)
        {
            // Target must rename the file as well.
            // TODO:
            //  a. Skicka ett rename-event.
            //  b. Mottagaren skapar en .renamed folder, samt en fil log.txt där i. 
            //  c. .renamed/log.txt får en ny rad om att filen ifråga bytt namn från x till y, av användare z, datum+tid.
            //  d. Mottagaren byter namn på filen. 
            // 
            // <-Loggen underlättar debug-arbete.
            // 
            return true;
        }

        private bool HandleChangedEvent(FileEvent fileEvent)
        {
            // Target want the newly changed file.
            // TODO: 
            //  a. Skicka filen.
            //  b. Mottagaren ser nu att filen redan finns på hens dator.
            //     1. Filen finns redan. Det här är ju ett Changed-event, så mottagaren vill gärna ha ändringarna.
            //     2. Skapa en .backup folder, flytta den existerande filen dit.
            //     3. .backup/log.txt får en ny rad om att filen ifråga kopierats hit, och ett datum+tid.
            //     4. Kopiera in den nya/ändrade filen. 
            //  c. Filen finns inte på mottagarens dator. Kopiera in den nya filen.
            // 
            // <-Som synes, det är samma förfarande som för Sync eventet, den enda skillnaden är att här i detta fallet är det märkligt. Filen borde nämligen existera, 
            //   och den enda förklaringen jag har just nu är att mottagaren har raderat filen i samma ögonblick. Anyway, på det här viset blir det annoying men säkert,
            //   mottagaren ser att hans nyss raderade fil dyker upp igen. En van användare inser att någon annan arbetar på filen och avvaktar. 
            // 

            byte[] bytes = GetFile(fileEvent.FileName);

            if(bytes != null)
            {
                // Cool, we have a file, a sender, and a receiver. 
                // TargetFolder
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <summary>
        ///  We don't work async here as the order of events are important. For example, if a file is Changed, then Renamed, 
        ///  we don't want the Renamed event to come first just because the file is big and takes a long time to read.
        /// </summary>
        private byte[] GetFile(string fileName)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(fileName);
                return bytes;
            }
            catch(Exception ex)
            {
                lastException = ex;
                return null;
            }
        }
    }

    /// <summary>
    /// Keeps track of file events of a the files in a single folder. If the folder is shared, all events are forwarded to listeners.
    /// </summary>
    public class FolderListener : IDisposable
    {
        // Active folder listeners reside in this fancy list.
        private static readonly List<FolderListener> ActiveFolderListeners = new();

        private FileSystemWatcher watcher;
        private FileEvents fileEvents = new();
        private readonly Timer fileEventTimer = new();

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
            FolderPath = folderPath;

            // The timer should not fire more than once.
            fileEventTimer.AutoReset = false;
            fileEventTimer.Elapsed += FileEventTimer_Elapsed;
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

                    // Add every single file and their path to the fileEvents list as Shared.
                    string[] fileNames = Directory.GetFiles(FolderPath);
                    foreach(string fileName in fileNames)
                    {
                        //string fn = Path.GetFileName(fileName);
                        fileEvents.FileShared(fileName);
                    }

                    // If there where any files or not, let's start the timer as we need to sync with other folders.
                    RestartTimer();

                }
                catch (Exception ex)
                {
                    throw new FolderListenerException("Could not setup folder listeners", ex, FolderPath);
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

        /// <summary>
        /// (Re)start the timer to fire the Elapsed event x seconds from now. 
        /// x is minimum 2 seconds, maximum 2.5 seconds.
        /// </summary>
        public void RestartTimer()
        {
            Random rand = new();
            fileEventTimer.Interval = 2000 + rand.Next(0, 500);
            fileEventTimer.Enabled = true;
        }

        /// <summary>
        /// Time to handle accumulated fileEvents. 
        /// </summary>
        private void FileEventTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (ActiveFolderListeners.Any(l => l != this) == false)
            {
                // We are the only folder around as of yet, so no point in trying to share anything. 
                // (more important, PopList() would empty list of events and just share them to no-one.)
            }
            else
            {
                // Fetch all events so far.
                List<FileEvent> fes = fileEvents.PopList();

                if (fes.Count > 0)
                {
                    // Other FolderListeners should now get these events, and files.
                    foreach (FolderListener fl in ActiveFolderListeners.Where(l => l != this))
                    {
                        SourceToTargetSyncer syncer = new(this, fl);

                        foreach (FileEvent fileEvent in fes)
                        {
                            // TODO: Handle result?
                            _ = syncer.HandleFileEvent(fileEvent);
                        }
                    }
                }
            }

            // TODO: När man delar ut en ensam katalog så får ju inte dess Shared-filer eller några event överhuvudtaget försvinna! Det gör de nu eftersom loopen ovan inte gör något, men PopList() har tagit alla event.
            // <-SetupFolderListener() borde be samtliga andra kataloger om att skicka ut en fullständig Shared-lista. 
            // (ActiveFolderListeners is the list to use ju)

            // TODO: När ett event kommer utifrån, och filen ändras här, så ska det eventet ignoreras. Svårt! 
            //   -När en fil laddas ner från servern och sparas här, så får man ta change-date och jämföra med event som 
            //    kommer in. Får sparas i en lista och jämföras med file-event som kommer. 
            //    TODO: Om man skriver över en fil, är det ett FolderContent_Created eller FolderContent_Changed event? 

            RestartTimer();
        }

        private void RemoveFolderListener()
        {
            _ = ActiveFolderListeners.Remove(this);

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

            // Finally add ourselfes to the fancy list of active folder listeners.
            ActiveFolderListeners.Add(this);
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
            MessageBox.Show("File deleted: " + e.Name);

            // Delete file online as well.
            fileEvents.FileDeleted(e.FullPath);
        }

        private void FolderContent_Renamed(object sender, RenamedEventArgs e)
        {
            MessageBox.Show("File renamed: " + e.OldName + " -> " + e.Name);

            // Rename file online as well.
            fileEvents.FileRenamed(e.OldFullPath, e.FullPath);
        }

        private void FolderContent_Created(object sender, FileSystemEventArgs e)
        {
            MessageBox.Show("File created: " + e.Name);

            // Upload new file to server.
            fileEvents.FileCreated(e.FullPath);
        }

        private void FolderContent_Changed(object sender, FileSystemEventArgs e)
        {
            MessageBox.Show("File changed: " + e.Name);

            // Upload and overwrite file at server.
            fileEvents.FileChanged(e.FullPath);
        }

        public void Dispose()
        {
            RemoveFolderListener();
            fileEvents = null;
        }
    }

}
