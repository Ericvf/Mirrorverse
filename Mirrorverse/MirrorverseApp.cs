using Microsoft.Extensions.Logging;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;
using static DisplayHelper;

public class MirrorverseApp : BaseApp
{
    private readonly ILogger<MirrorverseApp> logger;
    private OutputDuplicationComponent desktopDuplication;

    public MirrorverseApp(IServiceProvider serviceProvider, ILogger<MirrorverseApp> logger)
        : base(serviceProvider)
    {
        this.logger = logger;
    }

    public async override Task Initialize(IWindow window, string[] args)
    {
        await base.Initialize(window, args);

        var (screenX, screenY) = GetMonitorResolution();
        window.Position = new Vector2D<int>(screenX / 2, 0);
        window.Size = new Vector2D<int>(screenX / 2, screenY);

        desktopDuplication = Create<OutputDuplicationComponent>();
        desktopDuplication.Initialize(this);
    }

    private (int x, int y) GetMonitorResolution()
    {
        var monitor = MonitorFromPoint(new POINTSTRUCT(), 0);
        var info = new MONITORINFOEX();
        GetMonitorInfo(new HandleRef(this, monitor), info);
        return (info.rcMonitor.right, info.rcMonitor.bottom);
    }

    public void Draw(IWindow window, double time)
    {
        var canDraw = PrepareDraw();
        if (canDraw)
        {
            desktopDuplication.Draw(this, time);
        }
        else
        {
            Thread.Sleep(1000 / 240);
        }
        base.Draw();
    }
}
