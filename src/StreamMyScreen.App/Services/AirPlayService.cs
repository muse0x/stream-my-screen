using System.Diagnostics;
using System.IO;

namespace StreamMyScreen.App.Services;

public class AirPlayService
{
    private Process? _process;
    private readonly string _enginePath;

    public bool IsRunning => _process != null && !_process.HasExited;
    public string ServerName { get; }

    public AirPlayService(string serverName = "StreamMyScreen - PC")
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
            var startInfo = new ProcessStartInfo
            {
                FileName = _enginePath,
                Arguments = $"-n \"{ServerName}\" -vs direct3d11 -fps 60",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = Process.Start(startInfo);
            return _process != null && !_process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public void Stop()
    {
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
