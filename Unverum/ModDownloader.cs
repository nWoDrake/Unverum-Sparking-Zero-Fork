using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Reflection;
using System.Net.Http;
using System.Threading;
using System.Text.Json;
using SharpCompress.Common;
using System.Text.RegularExpressions;
using SharpCompress.Readers;
using Unverum.UI;
using SharpCompress.Archives.SevenZip;
using System.Linq;
using SharpCompress.Archives;

namespace Unverum
{
    public class ModDownloader
    {
        private string URL_TO_ARCHIVE;
        private string URL;
        private string DL_ID;
        private string MOD_TYPE;
        private string MOD_ID;
        private string fileName;
        private string fileDescription;
        private bool downloadAll;
        private HttpClient client = new();
        private GameBananaAPIV4 response = new();
        public async void BrowserDownload(string game, GameBananaRecord record)
        {
            DownloadWindow downloadWindow = new DownloadWindow(record);
            downloadWindow.ShowDialog();
            if (downloadWindow.YesNo)
            {
                string downloadUrl = null;
                string fileName = null;
                if (record.AllFiles.Count == 1)
                {
                    downloadUrl = record.AllFiles[0].DownloadUrl;
                    fileName = record.AllFiles[0].FileName;
                    fileDescription = record.AllFiles[0].Description;
                }
                else if (record.AllFiles.Count > 1)
                {
                    // Make sure installed-file tags are up to date before showing the file picker
                    InstalledMods.Refresh();
                    UpdateFileBox fileBox = new UpdateFileBox(record.AllFiles, record.Title);
                    fileBox.Activate();
                    fileBox.ShowDialog();
                    downloadAll = fileBox.selectedDownloadAll;
                    downloadUrl = fileBox.chosenFileUrl;
                    fileName = fileBox.chosenFileName;
                    fileDescription = fileBox.chosenFileDescription;
                }
                if (downloadAll)
                {
                    foreach (GameBananaItemFile file in record.AllFiles)
                    {
                        downloadUrl = file.DownloadUrl;
                        fileName = file.FileName;
                        fileDescription = file.Description;
                        if (downloadUrl != null && fileName != null)
                        {
                            var item = DownloadManager.Add(record.Title);
                            item.FileName = fileName;
                            var success = await DownloadFile(downloadUrl, fileName, item);
                            if (success)
                            {
                                item.Status = DownloadStatus.Extracting;
                                await ExtractFile(fileName, game, record);
                                item.Status = DownloadStatus.Completed;
                                InstalledMods.Refresh();
                                record.NotifyInstalled();
                            }
                        }
                    }
                }
                else
                {
                    if (downloadUrl != null && fileName != null)
                    {
                        var item = DownloadManager.Add(record.Title);
                        item.FileName = fileName;
                        var success = await DownloadFile(downloadUrl, fileName, item);
                        if (success)
                        {
                            item.Status = DownloadStatus.Extracting;
                            await ExtractFile(fileName, game, record);
                            item.Status = DownloadStatus.Completed;
                            InstalledMods.Refresh();
                            record.NotifyInstalled();
                        }
                    }
                }
            }
        }
        public async void Download(string line, bool running)
        {
            if (ParseProtocol(line))
            {
                if (await GetData())
                {
                    DownloadWindow downloadWindow = new DownloadWindow(response);
                    downloadWindow.ShowDialog();
                    if (downloadWindow.YesNo)
                    {
                        var item = DownloadManager.Add(response.Title);
                        item.FileName = fileName;
                        var success = await DownloadFile(URL_TO_ARCHIVE, fileName, item);
                        if (success)
                        {
                            item.Status = DownloadStatus.Extracting;
                            await ExtractFile(fileName, response.Game.Name.Replace(":", String.Empty), response);
                            item.Status = DownloadStatus.Completed;
                            InstalledMods.Refresh();
                        }
                    }
                }
            }
            if (running)
                Environment.Exit(0);
        }

        private async Task<bool> GetData()
        {
            try
            {
                string responseString = await client.GetStringAsync(URL);
                response = JsonSerializer.Deserialize<GameBananaAPIV4>(responseString);
                fileName = response.Files.Where(x => x.Id == DL_ID).ToArray()[0].FileName;
                fileDescription = response.Files.Where(x => x.Id == DL_ID).ToArray()[0].Description;
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error while fetching data {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
        private void ReportUpdateProgress(DownloadProgress progress, DownloadItem item)
        {
            item.Percentage = progress.Percentage * 100;
            item.DownloadedBytes = progress.DownloadedBytes;
            item.TotalBytes = progress.TotalBytes;
        }

        private bool ParseProtocol(string line)
        {
            try
            {
                line = line.Replace("unverum:", "");
                string[] data = line.Split(',');
                URL_TO_ARCHIVE = data[0];
                // Used to grab file info from dictionary
                var match = Regex.Match(URL_TO_ARCHIVE, @"\d*$");
                DL_ID = match.Value;
                MOD_TYPE = data[1];
                MOD_ID = data[2];
                URL = $"https://gamebanana.com/apiv6/{MOD_TYPE}/{MOD_ID}?_csvProperties=_sName,_aGame,_sProfileUrl,_aPreviewMedia,_sDescription,_aSubmitter,_aCategory,_aSuperCategory,_aFiles,_tsDateUpdated,_aAlternateFileSources,_bHasUpdates,_aLatestUpdates";
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error while parsing {line}: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
        private async Task ExtractFile(string fileName, string game, GameBananaRecord record)
        {
            await Task.Run(() =>
            {
                switch (game)
                {
                    case "Demon Slayer The Hinokami Chronicles":
                        game = "Demon Slayer";
                        break;
                    case "THE IDOLM@STER STARLIT SEASON":
                        game = "IDOLM@STER";
                        break;
                    case "Dragon Ball: Sparking! ZERO":
                        game = "Dragon Ball Sparking! ZERO";
                        break;
                }
                string _ArchiveSource = $@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}";
                string _ArchiveType = Path.GetExtension(fileName);
                string ArchiveDestination = $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{game}{Global.s}{string.Concat(record.Title.Split(Path.GetInvalidFileNameChars()))}";
                // Find a unique destination if it already exists
                var counter = 2;
                while (Directory.Exists(ArchiveDestination))
                {
                    ArchiveDestination = $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{game}{Global.s}{string.Concat(record.Title.Split(Path.GetInvalidFileNameChars()))} ({counter})";
                    ++counter;
                }
                if (File.Exists(_ArchiveSource))
                {
                    try
                    {
                        if (Path.GetExtension(_ArchiveSource).Equals(".7z", StringComparison.InvariantCultureIgnoreCase))
                        {
                            using (var archive = SevenZipArchive.Open(_ArchiveSource))
                            {
                                var reader = archive.ExtractAllEntries();
                                while (reader.MoveToNextEntry())
                                {
                                    if (!reader.Entry.IsDirectory)
                                        reader.WriteEntryToDirectory(ArchiveDestination, new ExtractionOptions()
                                        {
                                            ExtractFullPath = true,
                                            Overwrite = true
                                        });
                                }
                            }
                        }
                        else
                        {
                            using (Stream stream = File.OpenRead(_ArchiveSource))
                            using (var reader = ReaderFactory.Open(stream))
                            {
                                while (reader.MoveToNextEntry())
                                {
                                    if (!reader.Entry.IsDirectory)
                                    {
                                        reader.WriteEntryToDirectory(ArchiveDestination, new ExtractionOptions()
                                        {
                                            ExtractFullPath = true,
                                            Overwrite = true
                                        });
                                    }
                                }
                            }
                        }
                        if (!File.Exists($@"{ArchiveDestination}{Global.s}mod.json"))
                        {
                            Metadata metadata = new Metadata();
                            metadata.submitter = record.Owner.Name;
                            metadata.description = record.Description;
                            metadata.filedescription = fileDescription;
                            metadata.filename = fileName;
                            metadata.preview = record.Image;
                            metadata.homepage = record.Link;
                            metadata.avi = record.Owner.Avatar;
                            metadata.upic = record.Owner.Upic;
                            metadata.cat = record.CategoryName;
                            metadata.caticon = record.Category.Icon;
                            metadata.lastupdate = record.DateUpdated;
                            string metadataString = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText($@"{ArchiveDestination}{Global.s}mod.json", metadataString);
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Couldn't extract {fileName}: {e.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                // Check if folder output folder exists, if not nothing was extracted
                if (!Directory.Exists(ArchiveDestination))
                {
                    MessageBox.Show($"Didn't extract {fileName} due to improper format", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    // Only delete if successfully extracted
                    File.Delete(_ArchiveSource);
                }
            });

        }
        private async Task ExtractFile(string fileName, string game, GameBananaAPIV4 record)
        {
            await Task.Run(() =>
            {
                switch (game)
                {
                    case "Demon Slayer The Hinokami Chronicles":
                        game = "Demon Slayer";
                        break;
                    case "THE IDOLM@STER STARLIT SEASON":
                        game = "IDOLM@STER";
                        break;
                }
                string _ArchiveSource = $@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}";
                string _ArchiveType = Path.GetExtension(fileName);
                string ArchiveDestination = $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{game}{Global.s}{string.Concat(record.Title.Split(Path.GetInvalidFileNameChars()))}";
                // Find a unique destination if it already exists
                var counter = 2;
                while (Directory.Exists(ArchiveDestination))
                {
                    ArchiveDestination = $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{game}{Global.s}{string.Concat(record.Title.Split(Path.GetInvalidFileNameChars()))} ({counter})";
                    ++counter;
                }
                if (File.Exists(_ArchiveSource))
                {
                    try
                    {
                        if (Path.GetExtension(_ArchiveSource).Equals(".7z", StringComparison.InvariantCultureIgnoreCase))
                        {
                            using (var archive = SevenZipArchive.Open(_ArchiveSource))
                            {
                                var reader = archive.ExtractAllEntries();
                                while (reader.MoveToNextEntry())
                                {
                                    if (!reader.Entry.IsDirectory)
                                        reader.WriteEntryToDirectory(ArchiveDestination, new ExtractionOptions()
                                        {
                                            ExtractFullPath = true,
                                            Overwrite = true
                                        });
                                }
                            }
                        }
                        else
                        {
                            using (Stream stream = File.OpenRead(_ArchiveSource))
                            using (var reader = ReaderFactory.Open(stream))
                            {
                                while (reader.MoveToNextEntry())
                                {
                                    if (!reader.Entry.IsDirectory)
                                    {
                                        reader.WriteEntryToDirectory(ArchiveDestination, new ExtractionOptions()
                                        {
                                            ExtractFullPath = true,
                                            Overwrite = true
                                        });
                                    }
                                }
                            }
                        }
                        if (!File.Exists($@"{ArchiveDestination}{Global.s}mod.json"))
                        {
                            Metadata metadata = new Metadata();
                            metadata.submitter = record.Owner.Name;
                            metadata.description = record.Description;
                            metadata.filedescription = fileDescription;
                            metadata.filename = fileName;
                            metadata.preview = record.Image;
                            metadata.homepage = record.Link;
                            metadata.avi = record.Owner.Avatar;
                            metadata.upic = record.Owner.Upic;
                            metadata.cat = record.CategoryName;
                            metadata.caticon = record.Category.Icon;
                            metadata.lastupdate = record.DateUpdated;
                            string metadataString = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText($@"{ArchiveDestination}{Global.s}mod.json", metadataString);
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Couldn't extract {fileName}: {e.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                // Check if folder output folder exists, if not nothing was extracted
                if (!Directory.Exists(ArchiveDestination))
                {
                    MessageBox.Show($"Didn't extract {fileName} due to improper format", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    // Only delete if successfully extracted
                    File.Delete(_ArchiveSource);
                }
            });

        }
        private async Task<bool> DownloadFile(string uri, string fileName, DownloadItem item)
        {
            try
            {
                // Create the downloads folder if necessary
                Directory.CreateDirectory($@"{Global.assemblyLocation}{Global.s}Downloads");
                // Download the file if it doesn't already exist
                if (File.Exists($@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}"))
                {
                    try
                    {
                        File.Delete($@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}");
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Couldn't delete the already existing {Global.assemblyLocation}/Downloads/{fileName} ({e.Message})", 
                            "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        item.Status = DownloadStatus.Error;
                        return false;
                    }
                }
                item.Status = DownloadStatus.Downloading;
                var progress = new Progress<DownloadProgress>(p => ReportUpdateProgress(p, item));
                // Write and download the file
                using (var fs = new FileStream(
                    $@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await client.DownloadAsync(uri, fs, fileName, progress, item.CancellationTokenSource.Token);
                }
                return true;
            }
            catch (OperationCanceledException)
            {
                // Remove the file as it will be a partially downloaded one
                try
                {
                    File.Delete($@"{Global.assemblyLocation}{Global.s}Downloads{Global.s}{fileName}");
                }
                catch { }
                item.Status = DownloadStatus.Cancelled;
                return false;
            }
            catch (Exception e)
            {
                item.Status = DownloadStatus.Error;
                MessageBox.Show($"Error whilst downloading {fileName}. {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

    }
}