namespace OkTools.Core;

[PublicAPI]
public sealed class DelegateDisposable : IDisposable
{
    readonly Action _disposeAction;

    public DelegateDisposable(Action disposeAction) => _disposeAction = disposeAction;
    public void Dispose() => _disposeAction();
}
