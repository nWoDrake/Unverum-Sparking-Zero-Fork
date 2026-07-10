using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Unverum
{
    public enum DownloadStatus
    {
        Waiting,
        Downloading,
        Extracting,
        Completed,
        Cancelled,
        Error
    }

    /// <summary>
    /// Represents a single (parallel) download shown in the Downloads tab.
    /// </summary>
    public class DownloadItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public CancellationTokenSource CancellationTokenSource { get; } = new();

        private string _title;
        public string Title
        {
            get => _title;
            set { _title = value; Notify(nameof(Title)); }
        }

        private string _fileName;
        public string FileName
        {
            get => _fileName;
            set { _fileName = value; Notify(nameof(FileName)); }
        }

        private double _percentage;
        public double Percentage
        {
            get => _percentage;
            set
            {
                _percentage = value;
                Notify(nameof(Percentage));
                Notify(nameof(ProgressText));
            }
        }

        private long _downloadedBytes;
        public long DownloadedBytes
        {
            get => _downloadedBytes;
            set { _downloadedBytes = value; Notify(nameof(ProgressText)); }
        }

        private long _totalBytes;
        public long TotalBytes
        {
            get => _totalBytes;
            set { _totalBytes = value; Notify(nameof(ProgressText)); }
        }

        private DownloadStatus _status = DownloadStatus.Waiting;
        public DownloadStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                Notify(nameof(Status));
                Notify(nameof(StatusText));
                Notify(nameof(IsActive));
                Notify(nameof(IsDone));
            }
        }

        public string ProgressText => TotalBytes > 0
            ? $"{Math.Round(Percentage, 2)}% ({StringConverters.FormatSize(DownloadedBytes)} of {StringConverters.FormatSize(TotalBytes)})"
            : String.Empty;

        public string StatusText => Status switch
        {
            DownloadStatus.Waiting => "Waiting...",
            DownloadStatus.Downloading => "Downloading",
            DownloadStatus.Extracting => "Extracting...",
            DownloadStatus.Completed => "Completed",
            DownloadStatus.Cancelled => "Cancelled",
            DownloadStatus.Error => "Error",
            _ => String.Empty
        };

        public bool IsActive => Status == DownloadStatus.Waiting || Status == DownloadStatus.Downloading;
        public bool IsDone => !IsActive && Status != DownloadStatus.Extracting;

        public void Cancel()
        {
            try
            {
                CancellationTokenSource.Cancel();
            }
            catch { }
        }
    }

    /// <summary>
    /// Holds all active and recent downloads, displayed in the Downloads tab.
    /// </summary>
    public static class DownloadManager
    {
        public static ObservableCollection<DownloadItem> Downloads { get; } = new();

        public static DownloadItem Add(string title)
        {
            var item = new DownloadItem { Title = title, Status = DownloadStatus.Waiting };
            Application.Current.Dispatcher.Invoke(() => Downloads.Insert(0, item));
            return item;
        }

        public static void ClearCompleted()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var item in Downloads.Where(x => x.IsDone).ToList())
                    Downloads.Remove(item);
            });
        }
    }
}
