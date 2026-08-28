using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PdfToImages.Models;
using PdfToImages.Services;

namespace PdfToImages;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ConversionJob> _jobs = new();
    private readonly PdfConverterService _converter = new();
    private string _outputRoot = string.Empty;
    private bool _isConverting;

    public MainWindow()
    {
        InitializeComponent();
        FilesGrid.ItemsSource = _jobs;

        // Default output folder: Documents\PDF-to-Images-Output
        _outputRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PDF-to-Images-Output");
        OutputFolderText.Text = _outputRoot;

        SetVersionText();
    }

    /// <summary>
    /// Reads the real version baked into this exe at publish time (via the
    /// -p:Version=... argument in the CI workflow) instead of a hardcoded
    /// string, so the status bar always matches what was actually built.
    /// </summary>
    private void SetVersionText()
    {
        try
        {
            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            string? version = !string.IsNullOrEmpty(exePath)
                ? FileVersionInfo.GetVersionInfo(exePath).ProductVersion
                : null;

            VersionText.Text = string.IsNullOrWhiteSpace(version) ? "גרסה לא ידועה" : $"גרסה {version}";
        }
        catch
        {
            VersionText.Text = "גרסה לא ידועה";
        }
    }

    // ---------- Adding files ----------

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "בחר קבצי PDF",
            Filter = "קובצי PDF (*.pdf)|*.pdf",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            AddFiles(dialog.FileNames);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        var pdfPaths = paths.Where(p => Path.GetExtension(p).Equals(".pdf", StringComparison.OrdinalIgnoreCase));
        AddFiles(pdfPaths);
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var job = new ConversionJob(path);

            // Enforce unique book names right away - request unique names rather
            // than silently merging into an existing folder.
            string baseName = job.BookName;
            if (_jobs.Any(j => j.BookName.Equals(baseName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    $"השם \"{baseName}\" כבר קיים ברשימה. יש לבחור שם ייחודי. הקובץ נוסף עם שם זמני - נא לערוך אותו בטבלה.",
                    "שם ספר כפול",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                int suffix = 2;
                string candidate;
                do
                {
                    candidate = $"{baseName} ({suffix})";
                    suffix++;
                } while (_jobs.Any(j => j.BookName.Equals(candidate, StringComparison.OrdinalIgnoreCase)));
                job.BookName = candidate;
            }

            try
            {
                job.PageCount = _converter.GetPageCount(path);
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Error;
                job.ErrorDetail = "לא ניתן לקרוא את הקובץ";
                job.PageCount = 0;
            }

            _jobs.Add(job);
        }
    }

    private void ClearList_Click(object sender, RoutedEventArgs e)
    {
        if (_isConverting) return;
        _jobs.Clear();
        OverallProgress.Value = 0;
        OverallProgressText.Text = "מומר: 0 מתוך 0 עמודים";
        OpenOutputButton.Visibility = Visibility.Collapsed;
    }

    // ---------- Editing book names ----------

    private void FilesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.Item is not ConversionJob editedJob) return;

        // Read the new value directly from the editing text box.
        if (e.EditingElement is not TextBox textBox) return;
        string newName = textBox.Text.Trim();

        if (FileNamingService.HasInvalidChars(newName))
        {
            MessageBox.Show(
                "שם הספר מכיל תווים לא חוקיים לשם תיקייה/קובץ.",
                "שם לא תקין",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            e.Cancel = true;
            return;
        }

        bool isDuplicate = _jobs.Any(j => j != editedJob && j.BookName.Equals(newName, StringComparison.OrdinalIgnoreCase));
        if (isDuplicate)
        {
            MessageBox.Show(
                $"השם \"{newName}\" כבר קיים ברשימה. יש לבחור שם ייחודי.",
                "שם ספר כפול",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            e.Cancel = true;
        }
    }

    // ---------- Settings ----------

    private void DpiCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomDpiBox == null) return; // fires once during InitializeComponent
        var item = DpiCombo.SelectedItem as ComboBoxItem;
        CustomDpiBox.Visibility = (item?.Tag as string) == "custom" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        // .NET 8 WPF ships a native folder picker - no WinForms dependency needed
        // (mixing WinForms into a WPF project causes ambiguous-type build errors,
        // since both frameworks define types like Application and DragEventArgs).
        var dialog = new OpenFolderDialog
        {
            Title = "בחר תיקיית יעד",
            InitialDirectory = Directory.Exists(_outputRoot)
                ? _outputRoot
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            _outputRoot = dialog.FolderName;
            OutputFolderText.Text = _outputRoot;
        }
    }

    private int GetSelectedDpi()
    {
        var item = DpiCombo.SelectedItem as ComboBoxItem;
        string? tag = item?.Tag as string;

        if (tag == "custom")
        {
            if (int.TryParse(CustomDpiBox.Text, out int customDpi))
            {
                return Math.Clamp(customDpi, 72, 1200);
            }
            return 300;
        }

        return int.TryParse(tag, out int dpi) ? dpi : 300;
    }

    private ImageFormat GetSelectedFormat()
    {
        var item = FormatCombo.SelectedItem as ComboBoxItem;
        return (item?.Tag as string) == "jpeg" ? ImageFormat.Jpeg : ImageFormat.Png;
    }

    // ---------- Conversion ----------

    private async void ConvertButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isConverting) return;

        if (_jobs.Count == 0)
        {
            MessageBox.Show("נא להוסיף לפחות קובץ PDF אחד לפני תחילת ההמרה.", "אין קבצים",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_outputRoot))
        {
            MessageBox.Show("נא לבחור תיקיית יעד לפני תחילת ההמרה.", "אין תיקיית יעד",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Directory.CreateDirectory(_outputRoot);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"לא ניתן ליצור את תיקיית היעד:\n{ex.Message}", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!HasEnoughDiskSpace(_outputRoot))
        {
            var result = MessageBox.Show(
                "נראה שהשטח הפנוי בכונן היעד עלול שלא להספיק להמרה. להמשיך בכל זאת?",
                "שטח דיסק נמוך",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }

        _isConverting = true;
        ConvertButton.IsEnabled = false;
        OpenOutputButton.Visibility = Visibility.Collapsed;

        var settings = new ConversionSettings
        {
            Dpi = GetSelectedDpi(),
            Format = GetSelectedFormat(),
            OutputRoot = _outputRoot
        };

        int completed = 0;
        int failed = 0;
        int totalPagesWritten = 0;
        var logLines = new List<string>();

        // The overall bar is weighted by page count, not by book count, so a single
        // 400-page book doesn't look "stuck" while nine 5-page books race past it.
        int totalPages = _jobs.Sum(j => j.PageCount);
        int pagesDoneBeforeCurrentJob = 0;
        UpdateOverallProgress(0, totalPages);

        foreach (var job in _jobs)
        {
            if (job.Status == JobStatus.Error && job.PageCount == 0)
            {
                // File failed to open when it was added - skip, already flagged.
                failed++;
                continue;
            }

            job.Status = JobStatus.Converting;
            int jobPageCount = job.PageCount;
            int pagesBeforeThisJob = pagesDoneBeforeCurrentJob; // captured for the closure below

            var progress = new Progress<int>(p =>
            {
                job.ProgressPercent = p;
                int pagesDoneInJob = (int)Math.Round(p / 100.0 * jobPageCount);
                UpdateOverallProgress(pagesBeforeThisJob + pagesDoneInJob, totalPages);
            });

            ConversionResult result;
            try
            {
                result = await _converter.ConvertAsync(job, settings, progress, CancellationToken.None);
            }
            catch (Exception ex)
            {
                result = new ConversionResult { Success = false, ErrorMessage = ex.Message };
            }

            if (result.Success)
            {
                job.Status = JobStatus.Done;
                job.ProgressPercent = 100;
                completed++;
                totalPagesWritten += result.PagesWritten;
            }
            else
            {
                job.Status = JobStatus.Error;
                job.ErrorDetail = result.ErrorMessage;
                failed++;
                logLines.Add($"[{DateTime.Now:HH:mm:ss}] \"{job.BookName}\": {result.ErrorMessage}");
            }

            // Whether the job succeeded or failed partway through, its page range is
            // now behind us - move the baseline forward by its full page count so the
            // bar keeps advancing monotonically toward totalPages.
            pagesDoneBeforeCurrentJob += jobPageCount;
            UpdateOverallProgress(pagesDoneBeforeCurrentJob, totalPages);
        }

        if (logLines.Count > 0)
        {
            try
            {
                string logPath = Path.Combine(_outputRoot, "log-שגיאות.txt");
                File.WriteAllLines(logPath, logLines);
            }
            catch
            {
                // Logging is best-effort - a failure here must not hide the summary dialog.
            }
        }

        _isConverting = false;
        ConvertButton.IsEnabled = true;
        OpenOutputButton.Visibility = Visibility.Visible;

        if (failed == 0)
        {
            MessageBox.Show(
                $"ההמרה הושלמה בהצלחה. {completed} ספרים הומרו, נשמרו {totalPagesWritten} עמודים.",
                "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(
                $"ההמרה הסתיימה עם שגיאות. {completed} ספרים הומרו בהצלחה, {failed} נכשלו. פרטים בקובץ הלוג.",
                "הושלם עם שגיאות", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateOverallProgress(int pagesDone, int totalPages)
    {
        OverallProgress.Value = totalPages > 0 ? pagesDone * 100.0 / totalPages : 0;
        OverallProgressText.Text = $"מומר: {pagesDone} מתוך {totalPages} עמודים";
    }

    private static bool HasEnoughDiskSpace(string targetFolder)
    {
        try
        {
            string root = Path.GetPathRoot(Path.GetFullPath(targetFolder)) ?? "C:\\";
            var drive = new DriveInfo(root);
            // Conservative floor: warn under ~200 MB free.
            return drive.AvailableFreeSpace > 200L * 1024 * 1024;
        }
        catch
        {
            // If we can't determine free space, don't block the user over it.
            return true;
        }
    }

    private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Directory.Exists(_outputRoot))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _outputRoot,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Opening Explorer is a convenience action - a failure here is not fatal.
        }
    }
}
