namespace OkTools.Core;

[PublicAPI]
public struct DelegateDisposable(Action disposeAction) : IDisposable
{
    public readonly void Dispose() => disposeAction();
}
