using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;

namespace Filentropia
{
    public enum eFileEvents
    {
        Deleted,    // File got deleted or moved from the folder.
        Renamed,
        Created,
        Changed,
        Sync,       // File is newly shared and must be compared to other shared folders.
    }

    /// <summary>
    /// A FileEvent is a filename and an event happening to this filename.
    /// 
    /// Example of keyword 'init':
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
        public FileEvent()
        {
            TimeOfEvent = DateTime.Now;
        }

        // FileName is the identier for the file.
        // The init means we can set this 'readonly' value when creating a new FileEvent. After creation this value is readonly since it only has a getter.
        // 'private set' makes it settable in this class functions.
        // 'protected set' makes it settable in deriving classes as well.
        public string FileName { get; init; }

        public eFileEvents Event { get; init; }

        public DateTime TimeOfEvent { get; init; }
    }

    /// <summary>
    /// File is newly shared and must be compared to other shared folders. Most of the time the files are equal, so the file content are not sent along, instead the hash of the file is used to compare.
    /// </summary>
    public record FileSyncEvent : FileEvent
    {
        public FileSyncEvent()
        {
            Event = eFileEvents.Sync;
        }

        /// <summary>
        /// The file's creation date and time.
        /// </summary>
        public DateTime FileCreationTime { get; init; }

        /// <summary>
        /// The file's last write datetime.
        /// </summary>
        public DateTime FileLastWriteTime { get; init; }

        /// <summary>
        /// The file's size in bytes.
        /// </summary>
        public long FileSize { get; init; }

        /// <summary>
        /// The SHA512 hash for the file. 64 bytes encoded into a hexadecimal string, 128 characters long.
        /// </summary>
        public string SHA512 { get; init; }
    }

    /// <summary>
    /// A name change event need both old and new filename.
    /// </summary>
    public record FileNameChangedEvent : FileEvent
    {
        public FileNameChangedEvent()
        {
            Event = eFileEvents.Renamed;
        }

        public string OldFileName { get; init; }
    }

    /// <summary>
    /// Keep a list of file events ordered as they come. 
    /// A folder listener has an instance of FileEvents to keep track of what is happening with the files in the folder.
    /// </summary>
    public class FileEvents
    {
        private readonly object fileEventsLock = new();
        private readonly List<FileEvent> fileEvents = new();

        /// <summary>
        /// Returns the list of file events so far, and clear the internal list.
        /// </summary>
        public List<FileEvent> PopList()
        {
            List<FileEvent> fe;

            // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement
            lock (fileEventsLock)
            {
                fe = fileEvents.GetRange(0, fileEvents.Count);
                fileEvents.Clear();
            }

            return fe;
        }

        public void ClearList()
        {
            fileEvents.Clear();
        }

        public void FileDeleted(string fileName)
        {
            int count;
            FileEvent fe;

            lock (fileEventsLock)
            {
                // Delete any older file events for this file as they are pointless now.
                count = fileEvents.RemoveAll(x => x.FileName == fileName);

                fe = new() { Event = eFileEvents.Deleted, FileName = fileName };
                fileEvents.Add(fe);
            }
            Debug.WriteLine("Removed {0} file events for deleted file {1}.", new object[] { count, fileName });
            Debug.WriteLine(fe);
        }

        public void FileCreated(string fileName)
        {
            FileEvent fe = new() { Event = eFileEvents.Created, FileName = fileName };
            Debug.WriteLine(fe);
            lock (fileEventsLock)
            {
                fileEvents.Add(fe);
            }
        }

        public void FileChanged(string fileName)
        {
            // Since a file change event happens twice, we replace with the latest event.
            // This is safer, since if the poller gets inbetween and fetches all file events, this list is empty.
            // The result is an extra upload of the file which will happen later. But we don't run the risk of 
            // missing a file changed event. 
            // 
            FileEvent fe;
            lock (fileEventsLock)
            {
                fe = fileEvents.FirstOrDefault(x => x.FileName == fileName && x.Event == eFileEvents.Changed);

                if (fe == null)
                {
                    fe = new() { Event = eFileEvents.Changed, FileName = fileName };
                    Debug.WriteLine(fe);

                    fileEvents.Add(fe);
                }
                else
                {
                    FileEvent newFe = fe with { TimeOfEvent = DateTime.Now };
                    Debug.WriteLine(fe);

                    _ = fileEvents.Remove(fe);
                    fileEvents.Add(newFe);
                }
            }
            Debug.WriteLine(fe);
        }

        public void FileRenamed(string oldFileName, string newFileName)
        {
            FileNameChangedEvent fe = new()
            {
                FileName = newFileName,
                OldFileName = oldFileName
            };
            Debug.WriteLine(fe);

            lock(fileEventsLock)
            {
                fileEvents.Add(fe);

                // Find any events with the old filename in the list, rename their FileName to the new, and place after this event.
                // Reason: Otherwise the event(s) would not find the now renamed file.
                // 
                // NOTE: It can still happen! If the poller get inbetween and empty the list, it will try to find files whose name are now different.
                // <-En lösning vore att låta pollaren spara misslyckade event, och se om det kommer ett filename change event eller ett delete-event.
                List<FileEvent> fes = fileEvents.FindAll(x => x.FileName == oldFileName);
                int count = fileEvents.RemoveAll(x => x.FileName == oldFileName && x.Equals(fe) == false);

                foreach (FileEvent fileEvent in fes)
                {
                    FileEvent newFileEvent = fileEvent with { FileName = newFileName };
                    fileEvents.Add(newFileEvent);
                }
            }
        }

        public void FileShared(string fileName)
        {
            FileSyncEvent fe = new() { FileName = fileName };
            Debug.WriteLine(fe);

            lock (fileEventsLock)
            {
                fileEvents.Add(fe);
            }
        }
    }
}
