using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows;

namespace HyoPDF.App;

internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Global\HyoT_HyoPDF";
    private const string PipeName = "HyoT.HyoPDF.OpenFile";

    private readonly Mutex? _mutex;
    private CancellationTokenSource? _listenerCts;

    public bool IsFirstInstance { get; }

    private SingleInstance(bool isFirst, Mutex? mutex)
    {
        IsFirstInstance = isFirst;
        _mutex = mutex;
    }

    public static SingleInstance Acquire()
    {
        var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return new SingleInstance(false, null);
        }

        return new SingleInstance(true, mutex);
    }

    public bool TryForwardArguments(IReadOnlyList<string> args)
    {
        var paths = args
            .Where(IsOpenablePath)
            .Select(NormalizePath)
            .Where(File.Exists)
            .ToList();

        if (paths.Count == 0)
            return false;

        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(3000);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            foreach (var path in paths)
                writer.WriteLine(path);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void StartListening(Action<string> onOpenFile)
    {
        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                    while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
                    {
                        var path = NormalizePath(line);
                        if (!File.Exists(path))
                            continue;

                        Application.Current?.Dispatcher.BeginInvoke(() => onOpenFile(path));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    try
                    {
                        await Task.Delay(200, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }, token);
    }

    public static string NormalizePath(string path)
    {
        path = path.Trim().Trim('"');
        if (path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            path = new Uri(path).LocalPath;

        return Path.GetFullPath(path);
    }

    public static bool IsOpenablePath(string? arg) =>
        !string.IsNullOrWhiteSpace(arg) &&
        !arg.StartsWith('-') &&
        (arg.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || File.Exists(arg));

    public void Dispose()
    {
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}
