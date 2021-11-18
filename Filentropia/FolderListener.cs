using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Timers;
using System.Security.Cryptography;

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

    public static class Receivers
    {
        private static readonly List<IReceiver> receivers = new();

        /// <summary>
        /// Returns a list of all receivers excluding exceptThisOne.
        /// </summary>
        public static List<IReceiver> GetReceivers(IReceiver exceptThisOne)
        {
            return receivers.Where(x => x.Equals(exceptThisOne) == false).ToList();
        }

        public static void Add(IReceiver target)
        {
            receivers.Add(target);
        }

        public static void Remove(IReceiver target)
        {
            _ = receivers.Remove(target);
        }
    }

    public interface IReceiver
    {
        bool SendEvent(byte[] fileContent, FileEvent fileEvent);

        bool SendEvent(FileEvent fileEvent);
    }

    /// <summary>
    /// The target is a server with an ip-address, and any files should be sent to it.
    /// 
    /// OBS: Det här är helt nytt: En server måste ju agera en live katalog också, 
    /// med andra ord måste den skicka ut alla file changed events till andra som är online. 
    /// Rätt avancerat mao. Därför får du ha detta i bakhuvudet, men inte egentligen koda server-grejerna just nu, 
    /// utan lägga krutet på FolderTarget. 
    /// </summary>
    public class ServerReceiver : IReceiver
    {
        private string serverIP;

        public ServerReceiver(string serverIP)
        {
            this.serverIP = serverIP;
        }

        public bool SendEvent(byte[] fileContent, FileEvent fileEvent)
        {
            throw new NotImplementedException();
        }

        public bool SendEvent(FileEvent fileEvent)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// The target is a local folder, and any files should be 'sent' to it. 
    /// </summary>
    public class FolderReceiver : IReceiver
    {
        private string folderPath;

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

        public FolderReceiver(string folderPath)
        {
            this.folderPath = folderPath;
        }

        public bool SendEvent(byte[] fileContent, FileEvent fileEvent)
        {
            bool res = fileEvent.Event switch
            {
                eFileEvents.Sync => HandleSyncEvent(fileContent, fileEvent),             // Create the file if it does not exist.
                eFileEvents.Deleted => HandleDeletedEvent(fileEvent),                    // Delete the file.
                eFileEvents.Renamed => HandleRenamedEvent(fileEvent),                    // Rename the file.
                eFileEvents.Created => HandleCreatedEvent(fileContent, fileEvent),       // Create the file.
                eFileEvents.Changed => HandleChangedEvent(fileContent, fileEvent),       // Overwrite with new file. 
                _ => throw new FolderListenerException("Missing case in SendEvent!")
            };

            return res;
        }

        public bool SendEvent(FileEvent fileEvent)
        {
            bool res = fileEvent.Event switch
            {
//                eFileEvents.Sync => HandleSyncEvent(fileEvent),             // Create the file if it does not exist.
                eFileEvents.Deleted => HandleDeletedEvent(fileEvent),       // Delete the file.
                eFileEvents.Renamed => HandleRenamedEvent(fileEvent),       // Rename the file.
                _ => throw new FolderListenerException("Missing or wrong case in SendEvent!")
            };

            return res;
        }

        /// <summary>
        /// Create the file if it does not exist. If it do exist, perform magical steps.
        /// </summary>
        private bool HandleSyncEvent(byte[] fileContent, FileEvent fileEvent)
        {
            // TODO: FolderListener för den här katalogen får nu ett created event, som den måste strunta i! Vi får meddela den att denna filen just skapats eller nåt i den stilen. 
            // TODO: Både created och modified datum sätts till när filen skapas, vilket ju förstör historik. Vi måste sätta dessa specifikt, sådan information måste ju följa med vid kopieringen.

            string targetFileName = folderPath + Path.DirectorySeparatorChar + fileEvent.FileName;

            if (File.Exists(targetFileName))
            {
                // The file exists. 

                //  a. Vi ser nu att filen redan finns.
                //     1. Filen finns redan. Det här är ju ett Sync-event, och vi vill ju inte få våra filer överskrivna hur som helst. 
                //          TODO: Senare, få grejerna att snurra först.
                //          x. Jmfr datum. Nyare fil ersätter äldre. En äldre inkommande fil ignoreras. 
                //          y. En tom fil ersätter inte en fil med innehåll.
                //     2. Skapa en .backup folder, flytta den existerande filen dit.
                //     3. .backup/log.txt får en ny rad om att filen ifråga flyttats hit, och ett datum+tid.
                //     4. Kopiera in den nya filen. 
            }
            else
            {
                // Create the file.
                try
                {
                    using (FileStream file = File.Create(targetFileName))
                    {
                        file.Write(fileContent);
                        file.Close();

                        // Set file's creation time to same as source; we are not interested in when this app actually created the file. Same goes for last write time.
                        //File.SetCreationTime(targetFileName, fileEvent.FileCreationTime);
                        //File.SetLastWriteTime(targetFileName, fileEvent.FileLastWriteTime);

                        FileInfo fi = new FileInfo(targetFileName);
                        DateTime creationTime = fi.CreationTimeUtc;
                        DateTime lastWriteTime = fi.LastWriteTimeUtc;
                        FileAttributes fileAttributes = fi.Attributes;

                        long fileSize = fi.Length;

                        using (var sha512 = SHA512.Create())
                        {
                            using (var stream = File.OpenRead(targetFileName))
                            {
                                byte[] hashBytes = sha512.ComputeHash(stream); // 64 bytes = 512 bits

                                StringBuilder sb = new();
                                foreach (byte bt in hashBytes)
                                {
                                    _ = sb.Append(bt.ToString("x2")); // Convert byte to hexadecimal string, number 13 -> "0D"
                                }

                                string hash = sb.ToString(); // 64 bytes becomes 128 characters. Smaller files than this are wasteful. :-)
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    return false;
                }
            }

            return true;
        }

        private bool HandleCreatedEvent(byte[] fileContent, FileEvent fileEvent)
        {
            // Create the file.
            // 
            //  a. Vi ser nu att filen redan finns.
            //     1. Filen finns redan. Det här är ju ett Created-event, så det är ju mysko.
            //     2. Skapa en .backup folder, flytta den existerande filen dit.
            //     3. .backup/log.txt får en ny rad om att filen ifråga flyttats hit, och ett datum+tid.
            //     4. Kopiera in den nya filen. 
            //  b. Filen finns inte här. Kopiera in den nya filen.
            // 
            // <-Som synes, det är samma förfarande som för Sync eventet, den enda skillnaden är att här i detta fallet är det märkligt. Filen borde inte existera nämligen, 
            //   och den enda förklaringen jag har just nu är att de har skapat filen på varsin dator samtidigt. Anyway, på det här viset är det ändå säkert.
            // 
            return true;
        }

        private bool HandleDeletedEvent(FileEvent fileEvent)
        {
            // Delete the file.
            // 
            //  a. Skapa en .deleted folder, flytta den existerande filen dit.
            //  b. .deleted/log.txt får en ny rad om att filen ifråga raderats av användare q, och datum+tid.
            // 
            // <-Som jag ser det är det mycket säkrare att göra så här. Annars kan man ju råka radera allt i katalogen, varpå det speglas ut till alla! 
            //   Det vore extra katastrofalt eftersom windows soptunna inte används av appen by default, tror jag? Med andra ord skulle endast den som 
            //   raderade alla filer kunna återställa dem! 
            // 
            return true;
        }

        private bool HandleRenamedEvent(FileEvent fileEvent)
        {
            // Rename the file.
            // 
            //  a. Skapa en .renamed folder, samt en fil log.txt där i. 
            //  c. .renamed/log.txt får en ny rad om att filen ifråga bytt namn från x till y, av användare z, datum+tid.
            //  d. Byt namn på filen. 
            // 
            // <-Loggen underlättar debug-arbete.
            // 
            return true;
        }

        private bool HandleChangedEvent(byte[] fileContent, FileEvent fileEvent)
        {
            // Overwrite with new file. 
            // 
            //  a. Filen finns redan.
            //     1. Filen finns redan. Det här är ju ett Changed-event, så vi vill gärna ha ändringarna.
            //     2. Skapa en .backup folder, flytta den existerande filen dit.
            //     3. .backup/log.txt får en ny rad om att filen ifråga kopierats hit, och ett datum+tid.
            //     4. Kopiera in den nya/ändrade filen. 
            //  b. Filen finns inte här. Kopiera in den nya filen.
            // 
            // <-Som synes, det är samma förfarande som för Sync eventet, den enda skillnaden är att här i detta fallet är det märkligt. Filen borde nämligen existera, 
            //   och den enda förklaringen jag har just nu är att mottagaren har raderat filen i samma ögonblick. Anyway, på det här viset blir det annoying men säkert,
            //   mottagaren ser att hans nyss raderade fil dyker upp igen. En van användare inser att någon annan arbetar på filen och avvaktar. 
            // 

            return true;
        }
    }

    /// <summary>
    /// Takes a single file event and send it to a receiver, possibly along with the file's content.
    /// </summary>
    public class SourceToReceiverSyncer
    {
        private IReceiver receiver;
        private string srcFolderPath;
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

        public SourceToReceiverSyncer(string srcFolderPath, IReceiver receiver)
        {
            this.srcFolderPath = srcFolderPath;
            this.receiver = receiver;
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
                eFileEvents.Sync => HandleSyncEvent((FileSyncEvent)fileEvent),  // Send the file to target.
                eFileEvents.Deleted => HandleDeletedEvent(fileEvent),           // Send a delete order to target.
                eFileEvents.Renamed => HandleRenamedEvent(fileEvent),           // Send a rename order to target.
                eFileEvents.Created => HandleCreatedEvent(fileEvent),           // Send the created file to target.
                eFileEvents.Changed => HandleChangedEvent(fileEvent),           // Send the changed file's content to target.
                _ => throw new FolderListenerException("Missing case in HandleFileEvent!")
            };

            return res;
        }

        private bool HandleSyncEvent(FileSyncEvent fileEvent)
        {
            // Target would like to have the file.
            // TODO: 
            //  1. Det är en fjärran server/dator som vill ha filen. 
            //  2. Det är en katalog på samma dator.
            //  <-Vi låtsas att det är en fjärran. På det viset gör vi det mest komplicerat och säkert:
            //  a. Skicka file hash + annan data.
            //  b. Mottagaren ser nu att filen redan finns på hens dator.
            //     1. Filen finns redan. Det här är ju ett Sync-event, så vi får anta att mottagaren inte vill få sina filer överskrivna.
            //     2. Skapa en .backup folder, flytta den existerande filen dit.
            //     3. .backup/log.txt får en ny rad om att filen ifråga flyttats hit, och ett datum+tid.
            //     4. Kopiera in den nya filen. 
            //  c. Filen finns inte på mottagarens dator. Kopiera in den nya filen.
            //     

            //byte[] bytes = GetFile(fileEvent.FileName);
            fileEvent = GetFileInfo(fileEvent);


            if (bytes != null)
            {
                // Cool, we have a file, a sender, and a receiver. Send the stuff.
                receiver.SendEvent(bytes, fileEvent);
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
                receiver.SendEvent(bytes, fileEvent);
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
                byte[] bytes = File.ReadAllBytes(srcFolderPath + Path.DirectorySeparatorChar + fileName);
                return bytes;
            }
            catch(Exception ex)
            {
                lastException = ex;
                return null;
            }
        }

        /// <summary>
        /// Returns a new fileSyncEvent updated with it's files details and hash-string.
        /// </summary>
        /// <param name="fileSyncEvent"></param>
        /// <returns></returns>
        private FileSyncEvent GetFileInfo(FileSyncEvent fileSyncEvent)
        {
            FileInfo fi = new FileInfo(fileSyncEvent.FileName);
            
            FileSyncEvent fse = fileSyncEvent with
            {
                FileCreationTime = fi.CreationTimeUtc,
                FileLastWriteTime = fi.LastWriteTimeUtc,
                FileSize = fi.Length,
                SHA512 = ComputeSHA512(fileSyncEvent.FileName)
            };

            return fse;
        }

        private string ComputeSHA512(string fileName)
        {
            using (var sha512 = SHA512.Create())
            {
                using (var stream = File.OpenRead(fileName))
                {
                    byte[] hashBytes = sha512.ComputeHash(stream); // 64 bytes = 512 bits

                    StringBuilder sb = new();
                    foreach (byte bt in hashBytes)
                    {
                        _ = sb.Append(bt.ToString("x2")); // Convert byte to hexadecimal string, number 13 -> "0D"
                    }

                    string SHA512 = sb.ToString(); // 64 bytes becomes 128 characters. Smaller files than this are wasteful. :-)

                    return SHA512;
                }
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

        // My, mine, this folders listener for file events incoming from other sources.
        private FolderReceiver myFolderTarget;

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

            myFolderTarget = new FolderReceiver(folderPath);
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
                    string[] fileNames = Directory.GetFiles(FolderPath); // including file path. Would be nice to invent a name for path+file. 
                    foreach(string fileName in fileNames)
                    {
                        string fn = Path.GetFileName(fileName);
                        fileEvents.FileShared(fn);
                    }

                    // If there where any files or not, let's start the timer as we need to sync with other folders.
                    RestartTimer();

                    // Since we shared, we are also interested in receiving, so add ourselfes to the list of receivers.
                    Receivers.Add(myFolderTarget);

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
            Receivers.Remove(myFolderTarget);
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
                    // All receivers should now get these events and files, except ourself.
                    foreach (IReceiver receiver in Receivers.GetReceivers(myFolderTarget))
                    {
                        SourceToReceiverSyncer syncer = new(this.FolderPath, receiver);

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
            fileEvents.FileDeleted(e.Name);
        }

        private void FolderContent_Renamed(object sender, RenamedEventArgs e)
        {
            MessageBox.Show("File renamed: " + e.OldName + " -> " + e.Name);

            // Rename file online as well.
            fileEvents.FileRenamed(e.OldName, e.Name);
        }

        private void FolderContent_Created(object sender, FileSystemEventArgs e)
        {
            MessageBox.Show("File created: " + e.Name);

            // Upload new file to server.
            fileEvents.FileCreated(e.Name);
        }

        private void FolderContent_Changed(object sender, FileSystemEventArgs e)
        {
            MessageBox.Show("File changed: " + e.Name);

            // Upload and overwrite file at server.
            fileEvents.FileChanged(e.Name);
        }

        public void Dispose()
        {
            RemoveFolderListener();
            fileEvents = null;
        }
    }

}
