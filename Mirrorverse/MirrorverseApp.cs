using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.Windowing;

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

        window.WindowBorder = WindowBorder.Hidden;
        window.Position = new Vector2D<int>(2560, 0);
        window.Size = new Vector2D<int>(2560, 1440);

        desktopDuplication = Create<OutputDuplicationComponent>();
        desktopDuplication.Initialize(this);
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
