using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Silk.NET.Windowing;

var windowOptions = WindowOptions.Default;
windowOptions.API = GraphicsAPI.None;
windowOptions.WindowBorder = WindowBorder.Hidden;

var serviceProvider = BuildServiceProvider();
var app = serviceProvider.GetRequiredService<MirrorverseApp>();

using var window = Window.Create(windowOptions);

window.Load += async () => await app.Initialize(window, args);
window.Resize += s => app.Resize(s);
window.Update += t => app.Update(window, t);
window.Render += t => app.Draw(window, t);
window.Run();

static ServiceProvider BuildServiceProvider()
{
    var services = new ServiceCollection();

    services.AddLogging(builder =>
        builder.AddSimpleConsole(options =>
        {
            options.ColorBehavior = LoggerColorBehavior.Enabled;
            options.TimestampFormat = "[hh:mm:ss.FFF] ";
            options.SingleLine = true;
        }));

    services
        .AddSingleton<MirrorverseApp>()
        .AddTransient<OutputDuplicationComponent>()
        .AddTransient<MouseComponent>();

    return services.BuildServiceProvider();
}