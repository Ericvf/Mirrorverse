using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;
using static DisplayHelper;

public class MirrorverseApp : BaseApp
{
    private OutputDuplicationComponent desktopDuplication;
    private MouseComponent mouseComponent;
    private IInputContext input;

    public MirrorverseApp(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }

    public async override Task Initialize(IWindow window, string[] args)
    {
        await base.Initialize(window, args);

        input = window.CreateInput();

        var (screenX, screenY) = GetMonitorResolution();
        window.Position = new Vector2D<int>(screenX / 2, 0);
        window.Size = new Vector2D<int>(screenX / 2, screenY);

        desktopDuplication = Create<OutputDuplicationComponent>();
        desktopDuplication.Initialize(this);

        mouseComponent = Create<MouseComponent>();
        mouseComponent.Initialize(this);
        mouseComponent.Resize(window.Size);
    }

    internal void Update(IWindow window, double time)
    {
        mouseComponent.Position = input.Mice[0].Position;
    }

    private (int x, int y) GetMonitorResolution()
    {
        var monitor = MonitorFromPoint(new POINTSTRUCT(), 0);
        var info = new MONITORINFOEX();
        GetMonitorInfo(new HandleRef(this, monitor), info);
        return (info.rcMonitor.right, info.rcMonitor.bottom);
    }

    public override void Resize(Vector2D<int> windowSize)
    {
        if (mouseComponent != null)
        {
            mouseComponent.Resize(windowSize);
        }

        base.Resize(windowSize);   
    }

    public void Draw(IWindow window, double time)
    {
        var canDraw = PrepareDraw();
        if (canDraw)
        {
            desktopDuplication.Draw(this, time);
            mouseComponent.Draw(this, time);
        }
        else
        {
            Thread.Sleep(1000 / 240);
        }

        base.Draw();
    }
}
