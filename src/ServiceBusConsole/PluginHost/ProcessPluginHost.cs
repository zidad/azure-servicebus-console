using System.ComponentModel;
using System.Diagnostics;

namespace ServiceBusConsole.PluginHost;

/// <summary>Runs real processes for plugins and opens files and URLs with the system's default application.</summary>
public sealed class ProcessPluginHost(string stateDirectory) : IPluginHost
{
    public string StateDirectory { get; } = stateDirectory;

    public async Task<ProcessResult> RunProcessAsync(ProcessSpec spec, CancellationToken ct)
    {
        var info = new ProcessStartInfo(spec.FileName)
        {
            RedirectStandardInput = spec.StandardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = spec.WorkingDirectory ?? "",
        };
        foreach (var argument in spec.Arguments) info.ArgumentList.Add(argument);
        foreach (var (name, value) in spec.Environment)
        {
            if (value is null) info.Environment.Remove(name);
            else info.Environment[name] = value;
        }

        using var process = Start(info);
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(ct);
            var error = process.StandardError.ReadToEndAsync(ct);
            if (spec.StandardInput is { } input)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), ct);
                process.StandardInput.Close();
            }
            await process.WaitForExitAsync(ct);
            return new ProcessResult(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }
    }

    public Task OpenAsync(string pathOrUrl, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            using var _ = Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
            return Task.CompletedTask;
        }
        var opener = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
        return OpenWithAsync(opener, pathOrUrl, ct);
    }

    private async Task OpenWithAsync(string opener, string pathOrUrl, CancellationToken ct) =>
        (await RunProcessAsync(new ProcessSpec(opener, [pathOrUrl]), ct)).EnsureSuccess(opener);

    private static Process Start(ProcessStartInfo info)
    {
        try
        {
            return Process.Start(info) ?? throw new InvalidOperationException($"Could not start {info.FileName}.");
        }
        catch (Win32Exception e)
        {
            throw new InvalidOperationException($"Could not start {info.FileName}: {e.Message}", e);
        }
    }

    private static void Kill(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }
}
