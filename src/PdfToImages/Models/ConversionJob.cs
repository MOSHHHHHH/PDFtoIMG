using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfToImages.Models;

public enum JobStatus
{
    Pending,
    Converting,
    Done,
    Error
}

/// <summary>
/// Represents a single PDF file queued for conversion, and its live state
/// while the conversion is running. Implements INotifyPropertyChanged so
/// the UI (bound via WPF data binding) updates live without extra code.
/// </summary>
public class ConversionJob : INotifyPropertyChanged
{
    public string SourcePath { get; }

    public ConversionJob(string sourcePath)
    {
        SourcePath = sourcePath;
        BookName = Path.GetFileNameWithoutExtension(sourcePath);
    }

    private string _bookName = string.Empty;
    /// <summary>
    /// The book name - defaults to the file name without extension, but the
    /// user can rename it. This becomes both the output folder name and the
    /// prefix for every exported page image.
    /// </summary>
    public string BookName
    {
        get => _bookName;
        set { _bookName = value; OnPropertyChanged(); }
    }

    private int _pageCount;
    public int PageCount
    {
        get => _pageCount;
        set { _pageCount = value; OnPropertyChanged(); }
    }

    private JobStatus _status = JobStatus.Pending;
    public JobStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    private string? _errorDetail;
    public string? ErrorDetail
    {
        get => _errorDetail;
        set { _errorDetail = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    private int _progressPercent;
    public int ProgressPercent
    {
        get => _progressPercent;
        set { _progressPercent = value; OnPropertyChanged(); }
    }

    /// <summary>Hebrew text shown in the status column of the file list.</summary>
    public string StatusText => Status switch
    {
        JobStatus.Pending => "ממתין",
        JobStatus.Converting => "ממיר...",
        JobStatus.Done => "הושלם",
        JobStatus.Error => $"שגיאה: {ErrorDetail}",
        _ => string.Empty
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
