using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace VideoSqueeze;

static class Program
{
    const string InstanceId = "VideoSqueeze-6F3B2A1E-9C4D-4E8B-A7F2-5D1C0B9E8A43";
    static readonly string PipeName = $"{InstanceId}-{Environment.UserName}";

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);

    [STAThread]
    static void Main(string[] args)
    {
        // Explorer's right-click menu starts one process per selected file; only the first one opens a window,
        // the rest hand their paths over to it and exit.
        using var mutex = new Mutex(true, $"Local\\{InstanceId}", out bool first);
        if (!first)
        {
            SendToRunningInstance(args);
            return;
        }

        ApplicationConfiguration.Initialize();
        var form = new MainForm(args);
        form.Shown += (_, _) => _ = ListenAsync(form); // needs the window handle for BeginInvoke
        Application.Run(form);
    }

    static void SendToRunningInstance(string[] args)
    {
        try
        {
            AllowSetForegroundWindow(-1); // ASFW_ANY: let the running window come to the front
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            pipe.Connect(5000); // the first instance may still be starting up
            using var w = new StreamWriter(pipe);
            foreach (var a in args) w.WriteLine(Path.GetFullPath(a));
        }
        catch { }
    }

    static async Task ListenAsync(MainForm form)
    {
        while (true)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync();
                using var r = new StreamReader(pipe);
                var paths = new List<string>();
                while (await r.ReadLineAsync() is { } line)
                    if (line.Length > 0) paths.Add(line);
                form.BeginInvoke(() => form.OpenPaths(paths));
            }
            catch (ObjectDisposedException) { return; }
            catch { }
        }
    }
}
