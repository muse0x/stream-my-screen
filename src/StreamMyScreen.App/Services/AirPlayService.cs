using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace StreamMyScreen.App.Services;

public class AirPlayService
{
    private Process? _process;
    private readonly string _enginePath;
    private CancellationTokenSource? _watcherCts;

    public bool IsRunning => _process != null && !_process.HasExited;
    public string ServerName { get; }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public AirPlayService(string serverName = "StreamMyScreen")
    {
        ServerName = serverName;
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // Check relative paths from AppDomain or project root
        var candidates = new[]
        {
            Path.Combine(baseDir, "engine", "uxplay", "uxplay-windows.exe"),
            Path.Combine(baseDir, "..", "engine", "uxplay", "uxplay-windows.exe"),
            Path.Combine(baseDir, "..", "..", "..", "..", "engine", "uxplay", "uxplay-windows.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "engine", "uxplay", "uxplay-windows.exe")
        };

        _enginePath = candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    public bool Start()
    {
        if (IsRunning)
        {
            return true;
        }

        if (!File.Exists(_enginePath))
        {
            return false;
        }

        try
        {
            var workingDir = Path.GetDirectoryName(_enginePath)!;
            var argsFile = Path.Combine(workingDir, "arguments.txt");
            try
            {
                File.WriteAllText(argsFile, $"-n \"{ServerName}\" -nh -vs direct3d11 -fps 60\n");
            }
            catch { }

            var startInfo = new ProcessStartInfo
            {
                FileName = _enginePath,
                Arguments = $"-n \"{ServerName}\" -nh -vs direct3d11 -fps 60",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = Process.Start(startInfo);
            if (_process != null && !_process.HasExited)
            {
                StartWindowWatcher(_process.Id);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private void StartWindowWatcher(int pid)
    {
        _watcherCts?.Cancel();
        _watcherCts = new CancellationTokenSource();
        var ct = _watcherCts.Token;

        Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    EnumWindows((hWnd, lParam) =>
                    {
                        GetWindowThreadProcessId(hWnd, out var windowPid);
                        if (windowPid == pid)
                        {
                            var sb = new StringBuilder(256);
                            GetWindowText(hWnd, sb, 256);
                            var title = sb.ToString();
                            if (title.Contains("AirPlay", StringComparison.OrdinalIgnoreCase) ||
                                title.Contains("uxplay", StringComparison.OrdinalIgnoreCase))
                            {
                                SetWindowText(hWnd, "Stream My Screen");
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                }
                catch { }

                try
                {
                    await Task.Delay(1000, ct);
                }
                catch
                {
                    break;
                }
            }
        }, ct);
    }

    public void Stop()
    {
        _watcherCts?.Cancel();
        _watcherCts = null;

        if (_process == null || _process.HasExited)
        {
            return;
        }

        try
        {
            _process.Kill(true);
            _process.WaitForExit(2000);
            _process.Dispose();
            _process = null;
        }
        catch
        {
            // Ignore error on kill
        }
    }
}
