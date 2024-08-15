using System.Diagnostics;

record LongTask(string Name, bool Weak
#   if ENABLE_TASK_CREATION_TRACKING
    , StackTrace Creation
#   endif
    )
{
    static int s_nextId;
    public readonly int Serial = Interlocked.Increment(ref s_nextId);

    Task? _task;
    public Task Task
    {
        get => _task ?? throw new InvalidOperationException("No task assigned");

        internal set
        {
            if (_task != null)
                throw new InvalidOperationException("Task already assigned");
            _task = value;
        }
    }
}

class LongTasks
{
    readonly Dictionary<int, LongTask> _tasks = new();
    readonly CancellationTokenSource _stopTokenSource;

    public LongTasks(CancellationToken stopToken)
    {
        _stopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
    }

    public IReadOnlyList<LongTask> GetTasksSnapshot()
    {
        lock (_tasks)
            return [.._tasks.Values];
    }

    public void AbortAll()
    {
        lock (_tasks)
        {
            _stopTokenSource.Cancel();
            _tasks.Clear();
        }
    }

    // use this for fire-and-forget functions that shouldn't fail, but we definitely want to catch when they do so can
    // improve error handling of them. def worse to have them fail quietly in the background.
    //
    // weak = don't care if it's still running at the end

    public LongTask RunWeak(string taskName, Func<CancellationToken, Task> action, CancellationToken? stopToken = null) =>
        Run(taskName, true, action, stopToken);

    public LongTask Run(string taskName, Func<CancellationToken, Task> action, CancellationToken? stopToken = null) =>
        Run(taskName, false, action, stopToken);

    LongTask Run(string taskName, bool weak, Func<CancellationToken, Task> action, CancellationToken? stopToken = null)
    {
        stopToken ??= _stopTokenSource.Token;

        // doing this through ctx so that in the future i can do a little nicer handling of a background task
        // failure, like adding extra supporting data to a log file or whatever.

        var task = new LongTask(taskName, true, new StackTrace());
        var assigned = false;

        async Task LongAction()
        {
            // just in case it's possible that a very fast task startup may happen before task assignment to the record
            // ReSharper disable once AccessToModifiedClosure LoopVariableIsNeverChangedInsideLoop
            while (!assigned)
                await Task.Delay(0, stopToken.Value);

            lock (_tasks)
                _tasks.Add(task.Serial, task);

            try
            {
                await action(stopToken.Value);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception runException)
            {
                Environment.FailFast(
                    $"Failure in {nameof(LongTasks)}.{nameof(Run)}('{taskName}')",
                    new LongTaskException(task, runException));
            }

            lock (_tasks)
                _tasks.Remove(task.Serial);
        }

        task.Task = Task.Factory
            .StartNew(LongAction, stopToken.Value, TaskCreationOptions.LongRunning, TaskScheduler.Default)
            .Unwrap();
        assigned = true;
        return task;
    }
}

class LongTaskException : Exception
{
    readonly LongTask _ltask;

    public LongTaskException(LongTask ltask, Exception runException)
        : base(ltask.Name, runException)
    {
        _ltask = ltask;
    }

    public override string StackTrace => _ltask.Creation.ToString();
}
