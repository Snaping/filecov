using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using FileCov.Contracts;
using FileCov.App.Services;
using Microsoft.Win32;

namespace FileCov.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly PluginLoader _pluginLoader;
    private readonly ConversionEngine _engine;
    private readonly NotificationService _notificationService;

    public ObservableCollection<ConversionTask> Tasks { get; } = new();
    public ObservableCollection<IConverter> AvailableConverters { get; }

    private ConversionTask? _selectedTask;
    public ConversionTask? SelectedTask
    {
        get => _selectedTask;
        set { _selectedTask = value; OnPropertyChanged(); }
    }

    private int _maxConcurrency = 2;
    public int MaxConcurrency
    {
        get => _maxConcurrency;
        set
        {
            if (_maxConcurrency != value)
            {
                _maxConcurrency = value;
                _engine.MaxConcurrency = value;
                OnPropertyChanged();
            }
        }
    }

    private double _overallProgress;
    public double OverallProgress
    {
        get => _overallProgress;
        set { _overallProgress = value; OnPropertyChanged(); }
    }

    private string _remainingTimeText = "";
    public string RemainingTimeText
    {
        get => _remainingTimeText;
        set { _remainingTimeText = value; OnPropertyChanged(); }
    }

    private string _statusText = "0/0 已完成";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set { _isProcessing = value; OnPropertyChanged(); }
    }

    private string _outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "FileCov_Output");
    public string OutputDirectory
    {
        get => _outputDirectory;
        set { _outputDirectory = value; OnPropertyChanged(); }
    }

    private string _pageSize = "A4";
    public string PageSize
    {
        get => _pageSize;
        set { _pageSize = value; OnPropertyChanged(); }
    }

    private int _imageQuality = 85;
    public int ImageQuality
    {
        get => _imageQuality;
        set { _imageQuality = value; OnPropertyChanged(); }
    }

    public ICommand AddFilesCommand { get; }
    public ICommand StartAllCommand { get; }
    public ICommand PauseAllCommand { get; }
    public ICommand ResumeAllCommand { get; }
    public ICommand CancelAllCommand { get; }
    public ICommand ExportLogsCommand { get; }
    public ICommand OpenOutputFolderCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand RemoveTaskCommand { get; }
    public ICommand PauseTaskCommand { get; }
    public ICommand ResumeTaskCommand { get; }
    public ICommand CancelTaskCommand { get; }

    public MainViewModel(PluginLoader pluginLoader, ConversionEngine engine, NotificationService notificationService)
    {
        _pluginLoader = pluginLoader;
        _engine = engine;
        _notificationService = notificationService;

        _engine.MaxConcurrency = _maxConcurrency;
        _engine.TaskStatusChanged += OnTaskStatusChanged;

        AvailableConverters = new ObservableCollection<IConverter>(_pluginLoader.GetConverters());

        if (!Directory.Exists(OutputDirectory))
        {
            Directory.CreateDirectory(OutputDirectory);
        }

        AddFilesCommand = new RelayCommand(OnAddFiles);
        StartAllCommand = new RelayCommand(OnStartAll, () => Tasks.Any(t => t.Status == ConversionStatus.Waiting || t.Status == ConversionStatus.Paused));
        PauseAllCommand = new RelayCommand(OnPauseAll, () => Tasks.Any(t => t.Status == ConversionStatus.Waiting || t.Status == ConversionStatus.Processing));
        ResumeAllCommand = new RelayCommand(OnResumeAll, () => Tasks.Any(t => t.Status == ConversionStatus.Paused));
        CancelAllCommand = new RelayCommand(OnCancelAll, () => Tasks.Any(t => t.Status != ConversionStatus.Completed && t.Status != ConversionStatus.Failed && t.Status != ConversionStatus.Cancelled));
        ExportLogsCommand = new RelayCommand(OnExportLogs, () => Tasks.Any());
        OpenOutputFolderCommand = new RelayCommand(OnOpenOutputFolder, () => Directory.Exists(OutputDirectory));
        BrowseOutputCommand = new RelayCommand(OnBrowseOutput);
        RemoveTaskCommand = new RelayCommand<ConversionTask>(OnRemoveTask);
        PauseTaskCommand = new RelayCommand<ConversionTask>(OnPauseTask, t => t != null && (t.Status == ConversionStatus.Waiting));
        ResumeTaskCommand = new RelayCommand<ConversionTask>(OnResumeTask, t => t != null && t.Status == ConversionStatus.Paused);
        CancelTaskCommand = new RelayCommand<ConversionTask>(OnCancelTask, t => t != null && (t.Status == ConversionStatus.Waiting || t.Status == ConversionStatus.Processing || t.Status == ConversionStatus.Paused));
    }

    private void OnAddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = BuildFilterString()
        };

        if (dialog.ShowDialog() == true)
        {
            AddFiles(dialog.FileNames);
        }
    }

    private string BuildFilterString()
    {
        var extensions = new HashSet<string>();
        foreach (var converter in AvailableConverters)
        {
            foreach (var ext in converter.SupportedInputExtensions)
            {
                extensions.Add(ext);
            }
        }

        var filterParts = extensions.Select(e => $"*{e}").ToList();
        var allFiles = string.Join(";", filterParts);
        return $"支持的文件|{allFiles}|所有文件|*.*";
    }

    public void AddFiles(string[] paths)
    {
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;

            var converter = _pluginLoader.GetConverterForFile(path);
            if (converter == null) continue;

            var task = new ConversionTask
            {
                InputPath = path,
                TargetFormat = converter.OutputExtension,
                Status = ConversionStatus.Waiting,
                Parameters = new ConversionParameters
                {
                    PageSize = PageSize,
                    ImageQuality = ImageQuality,
                    OutputDirectory = OutputDirectory
                }
            };

            Tasks.Add(task);
            _engine.SubmitTaskAsync(task);
        }

        UpdateProgress();
    }

    private void OnRemoveTask(ConversionTask? task)
    {
        if (task == null) return;
        if (task.Status == ConversionStatus.Processing)
        {
            task.CancellationTokenSource.Cancel();
        }
        Tasks.Remove(task);
        UpdateProgress();
    }

    private void OnStartAll()
    {
        foreach (var task in Tasks.Where(t => t.Status == ConversionStatus.Paused))
        {
            _engine.ResumeTask(task);
        }

        foreach (var task in Tasks.Where(t => t.Status == ConversionStatus.Waiting))
        {
            _engine.SubmitTaskAsync(task);
        }

        UpdateProgress();
    }

    private void OnPauseAll()
    {
        _engine.PauseAll();
        UpdateProgress();
    }

    private void OnResumeAll()
    {
        _engine.ResumeAll();
        UpdateProgress();
    }

    private void OnCancelAll()
    {
        _engine.CancelAll();
        UpdateProgress();
    }

    private void OnPauseTask(ConversionTask? task)
    {
        if (task == null) return;
        _engine.PauseTask(task);
        UpdateProgress();
    }

    private void OnResumeTask(ConversionTask? task)
    {
        if (task == null) return;
        _engine.ResumeTask(task);
        UpdateProgress();
    }

    private void OnCancelTask(ConversionTask? task)
    {
        if (task == null) return;
        _engine.CancelTask(task);
        UpdateProgress();
    }

    private void OnExportLogs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV文件|*.csv",
            DefaultExt = ".csv",
            FileName = $"FileCov_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("时间,任务ID,输入文件,输出文件,状态,错误信息,耗时");

        foreach (var task in Tasks)
        {
            var statusStr = task.Status.ToString();
            var outputPath = task.Result?.OutputPath ?? "";
            var error = task.ErrorMessage ?? "";
            var duration = task.EndTime.HasValue ? (task.EndTime.Value - task.StartTime).ToString(@"hh\:mm\:ss") : "";
            sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{task.Id},{task.InputPath},{outputPath},{statusStr},{error},{duration}");
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
    }

    private void OnOpenOutputFolder()
    {
        if (!Directory.Exists(OutputDirectory))
        {
            Directory.CreateDirectory(OutputDirectory);
        }
        _notificationService.OpenFolder(OutputDirectory);
    }

    private void OnBrowseOutput()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择输出目录"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputDirectory = dialog.FolderName;
        }
    }

    private void OnTaskStatusChanged(object? sender, ConversionTaskEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            UpdateProgress();
        });
    }

    public void UpdateProgress()
    {
        if (Tasks.Count == 0)
        {
            OverallProgress = 0;
            StatusText = "0/0 已完成";
            RemainingTimeText = "";
            IsProcessing = false;
            return;
        }

        var completed = Tasks.Count(t => t.Status == ConversionStatus.Completed);
        var failed = Tasks.Count(t => t.Status == ConversionStatus.Failed);
        var cancelled = Tasks.Count(t => t.Status == ConversionStatus.Cancelled);
        var total = Tasks.Count;

        OverallProgress = (double)(completed + failed + cancelled) / total * 100;
        StatusText = $"{completed}/{total} 已完成";
        IsProcessing = Tasks.Any(t => t.Status == ConversionStatus.Processing || t.Status == ConversionStatus.Waiting);

        var remaining = Tasks.Count(t => t.Status == ConversionStatus.Processing || t.Status == ConversionStatus.Waiting);
        if (remaining > 0 && completed > 0)
        {
            var completedTasks = Tasks.Where(t => t.Status == ConversionStatus.Completed && t.EndTime.HasValue).ToList();
            if (completedTasks.Any())
            {
                var avgTime = completedTasks.Average(t => (t.EndTime!.Value - t.StartTime).TotalSeconds);
                var estimatedSeconds = avgTime * remaining / Math.Max(MaxConcurrency, 1);
                var eta = TimeSpan.FromSeconds(estimatedSeconds);
                RemainingTimeText = eta.TotalHours >= 1
                    ? $"预计剩余 {eta.Hours}小时{eta.Minutes}分"
                    : eta.TotalMinutes >= 1
                        ? $"预计剩余 {eta.Minutes}分{eta.Seconds}秒"
                        : $"预计剩余 {eta.Seconds}秒";
            }
            else
            {
                RemainingTimeText = "计算中...";
            }
        }
        else
        {
            RemainingTimeText = remaining == 0 ? "已完成" : "";
        }

        CommandManager.InvalidateRequerySuggested();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private class RelayCommand<T> : ICommand where T : class
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter as T) ?? true;

        public void Execute(object? parameter) => _execute(parameter as T);
    }
}
