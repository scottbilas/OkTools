using Vezel.Cathode;

class StaleApp : IDisposable
{
    public StaleApp(StaleCliArguments opts)
    {
    }

    void IDisposable.Dispose()
    {
    }

// TEMP
#pragma warning disable CA1822
    public async Task<CliExitCode> Run()
#pragma warning restore CA1822
    {
        return await Task.FromResult(CliExitCode.Success);
    }
}
