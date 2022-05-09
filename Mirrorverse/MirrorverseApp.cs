using Microsoft.Extensions.Logging;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;
using static DisplayHelper;

public class MirrorverseApp : BaseApp
{
    private readonly ILogger<MirrorverseApp> logger;
    private DesktopDuplicationComponent desktopDuplication;


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

        desktopDuplication = Create<DesktopDuplicationComponent>();
        desktopDuplication.Initialize(this);

    }

    private (int x, int y) GetMonitorResolution()
    {
        var monitor = MonitorFromPoint(new POINTSTRUCT(), 0);
        var info = new MONITORINFOEX();
        GetMonitorInfo(new HandleRef(this, monitor), info);
        return (info.rcMonitor.right, info.rcMonitor.bottom);
    }

    public unsafe void Draw(IWindow window, double time)
    {
        HandleFPS(window, time);

        //desktopDuplication.Draw(this, time);

        //if (desktopDuplication.IsDrawn)
        //{
        PrepareDraw();

        //    var deviceContext = GraphicsContext.deviceContext.GetPinnableReference();
        //    deviceContext->PSSetShaderResources(0, 1, desktopDuplication.RenderTarget.GetAddressOf());
        //    deviceContext->Draw(6, 0);

        base.Draw();
        //}
        //else
        //{
        //    Thread.Sleep(1000 / 60);
        //}
    }

    double timeDelta = 0;
    int fpsIncrement = 0;

    private void HandleFPS(IWindow window, double time)
    {
        timeDelta += time;
        if (timeDelta > 1)
        {
            timeDelta = 0;
            window.Title = $"FPS: {fpsIncrement}";
            fpsIncrement = 0;
        }
        fpsIncrement++;
    }
}
