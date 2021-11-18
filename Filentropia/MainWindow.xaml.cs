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
    /* JUST NU: Se rad 131 i FolderListener.cs
     * 
     * TODO: Det vore najs med ett fil-typs-filter, tänk hiskeliga visual studio projekt. (Men det kräver ju stöd för katalog-struktur, vilket jag inte har)
     * 
      -DONE: När ett file event har ägt rum så kan en simpel timer skapas, som startas om varje gång ett event händer.  
       På det viset blir det mindre 'trafik', dvs. den samlar ihop flera events innan den börjar arbeta.
       Timern får ha en max-tid på säg 10 sekunder, sen börjar den beta av listan ändå.
      -Som nämnts nedan, när en fil byter namn kan tidigare event misslyckas med sitt jobb eftersom EventDispatcher
       körs kanske flera sekunder senare. Ex: Fil ändras, och byter sedan namn direkt efter. Då misslyckas file-change-eventet
       hitta filen. Lösningen är att misslyckade event sparas och senare event får på något vis fogas samman med det äldre. 
       
        Deleted - Kastar samtliga tidigare event eftersom de blir meningslösa att utföra. [DONE]
        Renamed - Uppdaterar samtliga tidigare Changed-event med det nya filnamnet, så de kan hitta filen. 
                    Lägger sig sedan FÖRE tidigare Changed event.
        Created - Filen skapas, inget konstigt. 
        Changed - Kastar tidigare Changed-event, det räcker med att ett sådant event händer.
                    Uppdateras med nytt filnamn ifall ett Renamed-event händer.
        Sync    - Filen har just delats, och den måste kolla med alla andra kataloger ifall filen ifråga redan finns. 
                    a. Om inte, enkelt, kopiera upp den. 
                    b. Finns, krångligt. Titta på senast ändrad, ta den nyaste.
                        x. Filen är äldre och en nyare laddas ner. 
                            Den skapar då en underkatalog .duplicates och lägger den gamla filen som ersätts däri, för att undvika katastrof.
                        y. Filen är nyare och laddas alltså upp. 
                            Mottagaren ansvarar själv för att skapa en egen underkatalog .duplicates. 
    
    */

    // JUST NU: 
    // 
    //
    // 1. DONE: FolderListener ska ha en funktion Share() som aktiverar alla lyssnare, samt en Unshare() som tar bort dem igen.
    // 2. YEP: RemoveFolderListener() är väl rätt.
    // <-Nu kan man lägga till en katalog i listan, klicka på Share, och så är den det. :) 
    // 3. DONE: När en katalog nyss delats mha. Share() ska ett nytt fileevent skapas för varje fil i katalogen: Sync
    //      <-Detta betyder bara att den ska kolla med alla andra kataloger ifall filen ifråga redan finns. 
    //          a. Om inte, enkelt, kopiera upp den. 
    //          b. Finns, krångligt. Titta på senast ändrad, ta den nyaste.
    //                  <-Den får väl skapa en backup kanske? Skapa en underkatalog duplicates och lägg de gamla filerna som ersätts däri.
    //      <-Om varje katalog gör så när de blir Shared() så är de ensamma ansvariga för 'sitt' och därmed kan x*y kataloger delas 
    //        'utan problem'. :-)
    //      <-Hur sätta upp själva 'avbetningen' av listorna på ett snyggt sätt? Busy-looping är ju inge snyggt..
    // OBS: Man får ju alltid ta risken att en fil inte längre finns, för att den tagits bort eller bytt namn en sekund senare. 
    //  <-Så när en fil ska kopieras upp till servern, och den inte finns, så kastar den helt enkelt eventet. 
    //    Ett senare event föklarar med all säkerhet vad som hänt ändå, tex. en rename eller delete. 
    //    <-OBS: En file change och sedan en rename kan göra att den försöker ladda upp innehållet, men misslyckas då den inte 
    //      kan finna filen (eftersom den döpts om). Sedan byta namn, och det går ju bra. Därmed har den missat att ladda upp en ändring!
    //      <-Enda praktiska lösningen blir nog att spara file-changed event som misslyckas att laddas upp, och uppdatera det med 
    //        filename-changes som kommer senare. Sedan får den försöka igen genom att lägga in eventet på nytt, fast med det nya
    //        filnamnet.
    // 

    /// <summary>
    /// Sharity is fun name too, but it already exist in linux.
    /// Syncopia sounds nice too.
    /// </summary>
    public partial class MainWindow : Window
    {
        // To let the FileListenersListBox ui element listen for changes in the list of folderListeners, we let it be of type ObservableCollection.
        private ObservableCollection<FolderListener> folderListeners = new ObservableCollection<FolderListener>();

        // Hmm, tanken är att appen ska tanka upp filerna via ftp till servern, och så ska alla andra
        // som upptäcker förändringarna automatiskt ladda ner nya och förändrade filer. 

        public MainWindow()
        {
            InitializeComponent();

            // NOTE: I don't use ListBox and ItemsSource and Styles, because I don't know how to create clickable buttons inside each element. I want each element to behave like its own entity.
            // Magic happens here, the listbox gets connected with the folderListeners-list.
            // FolderListenersListBox.ItemsSource = folderListeners;
        }

        public bool AddFolderListener(string folderPath)
        {
            FolderListener fl = folderListeners.Where(f => f.FolderPath == folderPath).FirstOrDefault();

            if (fl == null)
            {
                fl = new FolderListener(folderPath);
                folderListeners.Add(fl);

                _ = FolderListenersStackPanel.Children.Add(new UserControlFolderListener(fl, this));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes the folder listener with the given folderPath.
        /// </summary>
        public void RemoveFolderListener(string folderPath)
        {
            FolderListener fl = folderListeners.FirstOrDefault(f => f.FolderPath == folderPath);

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

        private void AddNewFolderButton_Click(object sender, RoutedEventArgs e)
        {
            // Microsoft.WindowsAPICodePack.Dialogs
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if(!AddFolderListener(dialog.FileName))
                {
                    _ = MessageBox.Show("Folder is already in list!");
                }
            }
        }
    }
}
