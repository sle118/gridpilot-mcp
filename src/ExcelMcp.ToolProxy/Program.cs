using System.Diagnostics;
using System.Text;

namespace ExcelMcp.ToolProxy;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ProxyOptions options;
        try
        {
            options = ProxyOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MCP proxy configuration error: {ex.Message}");
            return 2;
        }

        await using var logger = new ProxyLogger(options.LogPath);
        await logger.WriteLineAsync("proxy", $"launch command={options.Command} args=[{string.Join(", ", options.CommandArguments)}]");

        var psi = new ProcessStartInfo
        {
            FileName = options.Command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in options.CommandArguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            await logger.WriteLineAsync("proxy", "failed to start wrapped process");
            return 3;
        }

        var stdinTask = PumpBinaryAsync(
            source: Console.OpenStandardInput(),
            destination: process.StandardInput.BaseStream,
            sniffer: new McpFrameSniffer(),
            logger,
            $"{options.Label}.stdin",
            closeDestinationWhenDone: true);

        var stdoutTask = PumpBinaryAsync(
            source: process.StandardOutput.BaseStream,
            destination: Console.OpenStandardOutput(),
            sniffer: new McpFrameSniffer(),
            logger,
            $"{options.Label}.stdout",
            closeDestinationWhenDone: false);

        var stderrTask = PumpTextAsync(
            source: process.StandardError,
            destination: Console.Error,
            logger,
            $"{options.Label}.stderr");

        await process.WaitForExitAsync().ConfigureAwait(false);
        await Task.WhenAll(stdinTask, stdoutTask, stderrTask).ConfigureAwait(false);
        await logger.WriteLineAsync("proxy", $"wrapped exit code={process.ExitCode}");
        return process.ExitCode;
    }

    private static async Task PumpBinaryAsync(
        Stream source,
        Stream destination,
        McpFrameSniffer sniffer,
        ProxyLogger logger,
        string category,
        bool closeDestinationWhenDone)
    {
        var buffer = new byte[4096];
        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                await logger.WriteLineAsync(category, $"eof state={sniffer.DescribeState()}").ConfigureAwait(false);
                break;
            }

            await logger.WriteLineAsync(
                category,
                $"chunk bytes={bytesRead} state_before={sniffer.DescribeState()} preview={DescribeChunk(buffer.AsSpan(0, bytesRead))}")
                .ConfigureAwait(false);
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
            await destination.FlushAsync().ConfigureAwait(false);

            try
            {
                foreach (var frame in sniffer.Append(buffer.AsSpan(0, bytesRead)))
                {
                    await logger.WriteLineAsync(category, $"frame={frame}").ConfigureAwait(false);
                }

                await logger.WriteLineAsync(category, $"state_after={sniffer.DescribeState()}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await logger.WriteLineAsync(
                    category,
                    $"sniffer_error={ex.GetType().Name}: {ex.Message} state={sniffer.DescribeState()}")
                    .ConfigureAwait(false);
                sniffer = new McpFrameSniffer();
            }
        }

        if (closeDestinationWhenDone)
        {
            await destination.FlushAsync().ConfigureAwait(false);
            destination.Close();
        }
    }

    private static async Task PumpTextAsync(
        StreamReader source,
        TextWriter destination,
        ProxyLogger logger,
        string category)
    {
        while (true)
        {
            var line = await source.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
            {
                await logger.WriteLineAsync(category, "eof").ConfigureAwait(false);
                break;
            }

            await destination.WriteLineAsync(line).ConfigureAwait(false);
            await destination.FlushAsync().ConfigureAwait(false);
            await logger.WriteLineAsync(category, line).ConfigureAwait(false);
        }
    }

    private static string DescribeChunk(ReadOnlySpan<byte> bytes)
    {
        const int maxPreviewBytes = 160;
        var previewBytes = bytes[..Math.Min(bytes.Length, maxPreviewBytes)];
        var ascii = new StringBuilder(previewBytes.Length);
        foreach (var value in previewBytes)
        {
            ascii.Append(value switch
            {
                (byte)'\r' => "\\r",
                (byte)'\n' => "\\n",
                >= 32 and <= 126 => (char)value,
                _ => '.'
            });
        }

        var hex = BitConverter.ToString(previewBytes.ToArray());
        var suffix = bytes.Length > maxPreviewBytes ? "..." : string.Empty;
        return $"ascii=\"{ascii}\" hex={hex}{suffix}";
    }
}
