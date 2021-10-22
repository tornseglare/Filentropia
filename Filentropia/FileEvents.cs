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
        Deleted,
        Renamed,
        Created,
        Changed,
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
        // FileName is the identier for the file.
        // The init means we can set this 'readonly' value when creating a new FileEvent. After creation this value is readonly since it only has a getter.
        // 'private set' makes it settable in this class functions.
        // 'protected set' makes it settable in deriving classes as well.
        public string FileName { get; init; }

        public eFileEvents Event { get; init; }
    }

    /// <summary>
    /// A name change event need both old and new filename.
    /// </summary>
    public record FileNameChangedEvent : FileEvent
    {
        public string OldFileName { get; init; }
    }

    /// <summary>
    /// Keep a list of file events ordered as they come. 
    /// A folder listener has an instance of FileEvents to keep track of what is happening with the files in the folder.
    /// </summary>
    public class FileEvents
    {
        private List<FileEvent> fileEvents = new List<FileEvent>();

        public void ClearList()
        {
            fileEvents.Clear();
        }

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
            FileNameChangedEvent fe = new FileNameChangedEvent()
            {
                Event = eFileEvents.Renamed,
                FileName = newFileName,
                OldFileName = oldFileName
            };

            fileEvents.Add(fe);
            Debug.Write(fileEvents);
        }
    }
}
