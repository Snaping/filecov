using System.Collections.Concurrent;
using FileCov.Contracts;

namespace FileCov.App.Services;

public class ConversionEngine : IConversionEngine
{
    private readonly PluginLoader _pluginLoader;
    private readonly SemaphoreSlim _semaphore;
    private readonly BlockingCollection<ConversionTask> _queue;
    private readonly List<ConversionTask> _activeTasks;
    private readonly List<ConversionLogEntry> _logs;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ConversionResult>> _completionSources;
    private readonly List<ConversionTask> _allTasks;
    private readonly object _lock;
    private bool _isRunning;
    private bool _isPaused;
    private readonly ManualResetEventSlim _pauseEvent;
    private CancellationTokenSource _loopCts;
    private int _maxConcurrency;

    public int MaxConcurrency
    {
        get => _maxConcurrency;
        set => _maxConcurrency = value;
    }

    public event EventHandler<ConversionTaskEventArgs>? TaskStatusChanged;

    public ConversionEngine(PluginLoader pluginLoader)
    {
        _pluginLoader = pluginLoader;
        _maxConcurrency = 2;
        _semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
        _queue = new BlockingCollection<ConversionTask>();
        _activeTasks = new List<ConversionTask>();
        _logs = new List<ConversionLogEntry>();
        _completionSources = new ConcurrentDictionary<Guid, TaskCompletionSource<ConversionResult>>();
        _allTasks = new List<ConversionTask>();
        _lock = new object();
        _isRunning = false;
        _isPaused = false;
        _pauseEvent = new ManualResetEventSlim(true);
        _loopCts = new CancellationTokenSource();
    }

    public Task<ConversionResult> SubmitTaskAsync(ConversionTask task)
    {
        task.Status = _isPaused ? ConversionStatus.Paused : ConversionStatus.Waiting;
        var tcs = new TaskCompletionSource<ConversionResult>();
        _completionSources[task.Id] = tcs;

        lock (_lock)
        {
            _allTasks.Add(task);
        }

        _queue.Add(task);

        if (!_isRunning)
        {
            StartProcessingLoop();
        }

        return tcs.Task;
    }

    private void StartProcessingLoop()
    {
        _isRunning = true;
        _isPaused = false;
        _pauseEvent.Set();
        _loopCts = new CancellationTokenSource();

        _ = Task.Run(() => StartProcessingLoopAsync());
    }

    private async Task StartProcessingLoopAsync()
    {
        try
        {
            while (_isRunning)
            {
                try
                {
                    _pauseEvent.Wait(_loopCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                ConversionTask task;
                try
                {
                    task = _queue.Take(_loopCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                if (task.Status != ConversionStatus.Waiting)
                {
                    continue;
                }

                try
                {
                    await _semaphore.WaitAsync(_loopCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = ProcessWithSemaphoreReleaseAsync(task);
            }
        }
        finally
        {
            _isRunning = false;
        }
    }

    private async Task ProcessWithSemaphoreReleaseAsync(ConversionTask task)
    {
        try
        {
            lock (_lock)
            {
                _activeTasks.Add(task);
            }
            await ProcessTaskAsync(task);
        }
        finally
        {
            lock (_lock)
            {
                _activeTasks.Remove(task);
            }
            _semaphore.Release();
        }
    }

    private async Task ProcessTaskAsync(ConversionTask task)
    {
        task.StartTime = DateTime.UtcNow;

        var converter = _pluginLoader.GetConverterForFile(task.InputPath);
        if (converter == null)
        {
            task.Status = ConversionStatus.Failed;
            task.ErrorMessage = $"No converter found for file: {task.InputPath}";
            task.EndTime = DateTime.UtcNow;
            var failResult = new ConversionResult
            {
                Success = false,
                ErrorMessage = task.ErrorMessage,
                Duration = task.EndTime.Value - task.StartTime
            };
            task.Result = failResult;
            _completionSources.TryRemove(task.Id, out var failTcs);
            failTcs?.TrySetResult(failResult);
            AddLog(task);
            OnTaskStatusChanged(task);
            return;
        }

        task.Status = ConversionStatus.Processing;
        OnTaskStatusChanged(task);

        var progress = new Progress<double>(p => task.Progress = p);

        try
        {
            var result = await converter.ConvertAsync(
                task.InputPath,
                task.Parameters,
                task.CancellationTokenSource.Token,
                progress);

            task.EndTime = DateTime.UtcNow;
            result.Duration = task.EndTime.Value - task.StartTime;
            task.Result = result;
            task.Status = ConversionStatus.Completed;
            _completionSources.TryRemove(task.Id, out var successTcs);
            successTcs?.TrySetResult(result);
        }
        catch (OperationCanceledException)
        {
            task.Status = ConversionStatus.Cancelled;
            task.EndTime = DateTime.UtcNow;
            task.ErrorMessage = "Conversion was cancelled";
            var cancelResult = new ConversionResult
            {
                Success = false,
                ErrorMessage = task.ErrorMessage,
                Duration = task.EndTime.Value - task.StartTime
            };
            task.Result = cancelResult;
            _completionSources.TryRemove(task.Id, out var cancelTcs);
            cancelTcs?.TrySetCanceled(task.CancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            task.Status = ConversionStatus.Failed;
            task.ErrorMessage = ex.Message;
            task.EndTime = DateTime.UtcNow;
            var errorResult = new ConversionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = task.EndTime.Value - task.StartTime
            };
            task.Result = errorResult;
            _completionSources.TryRemove(task.Id, out var errorTcs);
            errorTcs?.TrySetException(ex);
        }
        finally
        {
            AddLog(task);
            OnTaskStatusChanged(task);
        }
    }

    public void PauseAll()
    {
        _isPaused = true;
        _pauseEvent.Reset();

        lock (_lock)
        {
            foreach (var task in _allTasks)
            {
                if (task.Status == ConversionStatus.Waiting)
                {
                    task.Status = ConversionStatus.Paused;
                }
            }
        }
    }

    public void ResumeAll()
    {
        lock (_lock)
        {
            foreach (var task in _allTasks)
            {
                if (task.Status == ConversionStatus.Paused)
                {
                    task.Status = ConversionStatus.Waiting;
                    _queue.Add(task);
                }
            }
        }

        _isPaused = false;
        _pauseEvent.Set();
    }

    public void CancelAll()
    {
        lock (_lock)
        {
            foreach (var task in _activeTasks.ToList())
            {
                task.CancellationTokenSource.Cancel();
            }
        }

        _loopCts.Cancel();

        while (_queue.TryTake(out var queuedTask))
        {
            if (queuedTask.Status == ConversionStatus.Waiting || queuedTask.Status == ConversionStatus.Paused)
            {
                queuedTask.Status = ConversionStatus.Cancelled;
                _completionSources.TryRemove(queuedTask.Id, out var tcs);
                tcs?.TrySetCanceled();
            }
        }

        lock (_lock)
        {
            foreach (var task in _allTasks)
            {
                if (task.Status == ConversionStatus.Waiting || task.Status == ConversionStatus.Paused)
                {
                    task.Status = ConversionStatus.Cancelled;
                    _completionSources.TryRemove(task.Id, out var tcs);
                    tcs?.TrySetCanceled();
                }
            }
        }

        _isRunning = false;
        _isPaused = false;
        _pauseEvent.Set();
    }

    public void PauseTask(ConversionTask task)
    {
        if (task.Status == ConversionStatus.Waiting)
        {
            task.Status = ConversionStatus.Paused;
            OnTaskStatusChanged(task);
        }
    }

    public void ResumeTask(ConversionTask task)
    {
        if (task.Status == ConversionStatus.Paused)
        {
            task.Status = ConversionStatus.Waiting;
            _queue.Add(task);
            OnTaskStatusChanged(task);
        }
    }

    public void CancelTask(ConversionTask task)
    {
        if (task.Status == ConversionStatus.Processing)
        {
            task.CancellationTokenSource.Cancel();
            return;
        }

        if (task.Status == ConversionStatus.Waiting || task.Status == ConversionStatus.Paused)
        {
            task.Status = ConversionStatus.Cancelled;
            _completionSources.TryRemove(task.Id, out var tcs);
            tcs?.TrySetCanceled();
            OnTaskStatusChanged(task);
        }
    }

    public IReadOnlyList<ConversionLogEntry> GetLogs()
    {
        lock (_lock)
        {
            return _logs.AsReadOnly();
        }
    }

    private void AddLog(ConversionTask task)
    {
        var entry = new ConversionLogEntry
        {
            Timestamp = DateTime.UtcNow,
            TaskId = task.Id,
            InputPath = task.InputPath,
            OutputPath = task.Result?.OutputPath,
            Status = task.Status,
            ErrorMessage = task.ErrorMessage,
            Duration = task.EndTime.HasValue ? task.EndTime.Value - task.StartTime : TimeSpan.Zero
        };

        lock (_lock)
        {
            _logs.Add(entry);
        }
    }

    private void OnTaskStatusChanged(ConversionTask task)
    {
        TaskStatusChanged?.Invoke(this, new ConversionTaskEventArgs(task));
    }
}
