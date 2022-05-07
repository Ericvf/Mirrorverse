using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Maths;
using Silk.NET.Windowing;

public unsafe class BaseApp : IApp
{
    public const Format GraphicsFormat = Format.FormatR8G8B8A8Unorm; // FormatR16G16B16A16Float;
    private ComPtr<ID3D11RenderTargetView> backBufferRenderTargetView = default;
    private ComPtr<ID3D11Resource> backBufferTexture = default;
    private ComPtr<ID3D11Texture2D> depthStencilTexture = default;
    private ComPtr<ID3D11DepthStencilView> depthStencilView = default;
    private ComPtr<ID3D11DepthStencilState> depthStencilState = default;

    private GraphicsContext graphicsContext = default;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<BaseApp> logger;
    private readonly D3D11 dx11api;
    private bool resetDevice = false;
    private bool deviceLost = true;

    public GraphicsContext GraphicsContext => graphicsContext;

    public Viewport windowViewport;

    public BaseApp(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        this.logger = serviceProvider.GetRequiredService<ILogger<BaseApp>>();
        this.dx11api = D3D11.GetApi();
    }

    public virtual void Resize(Vector2D<int> windowSize)
    {
        deviceLost = true;

        if (windowSize.X + windowSize.Y > 0)
        {
            windowViewport.Width = windowSize.X;
            windowViewport.Height = windowSize.Y;
            windowViewport.MinDepth = 0f;
            windowViewport.MaxDepth = 1f;
            resetDevice = true;
            deviceLost = false;
        }
    }

    public virtual Task Initialize(IWindow window, string[] args)
    {
        InitializeWindow(window, ref graphicsContext);
        Resize(window.Size);
        ResetBuffers();
        resetDevice = false;
        return Task.CompletedTask;
    }

    public void InitializeWindow(IWindow window, ref GraphicsContext graphicsContext)
    {
        // Init device and swapchain
        SwapChainDesc swapChainDesc;
        swapChainDesc.BufferCount = 1;
        swapChainDesc.BufferDesc.Format = GraphicsFormat; //Format.FormatR8G8B8A8Unorm;
        swapChainDesc.BufferUsage = DXGI.UsageRenderTargetOutput;
        swapChainDesc.OutputWindow = window.Native.Win32.Value.Hwnd;
        swapChainDesc.SampleDesc.Count = 1;
        swapChainDesc.SampleDesc.Quality = 0;
        swapChainDesc.Windowed = 1;

        var createDeviceFlags = CreateDeviceFlag.CreateDeviceBgraSupport | CreateDeviceFlag.CreateDeviceDebug;

        logger.LogInformation("CreateDeviceAndSwapChain");
        dx11api.CreateDeviceAndSwapChain(
            null
            , D3DDriverType.D3DDriverTypeHardware
            , 0
            , (uint)createDeviceFlags
            , null
            , 0
            , D3D11.SdkVersion
            , &swapChainDesc
            , graphicsContext.swapChain.GetAddressOf()
            , graphicsContext.device.GetAddressOf()
            , null
            , graphicsContext.deviceContext.GetAddressOf())
            .ThrowHResult();
    }

    public virtual bool PrepareDraw()
    {
        if (deviceLost)
            return false;

        if (resetDevice)
        {
            ResetBuffers();
            resetDevice = false;
        }

        var deviceContext = graphicsContext.deviceContext.GetPinnableReference();

        deviceContext->RSSetViewports(1, ref windowViewport);

        deviceContext->OMSetDepthStencilState(depthStencilState.GetPinnableReference(), 1);

        deviceContext->OMSetRenderTargets(1, backBufferRenderTargetView.GetAddressOf(), depthStencilView.GetPinnableReference());

        var backgroundColor = stackalloc[] { 0.850f, 0.900f, 0.950f, 1.0f };

        deviceContext->ClearRenderTargetView(backBufferRenderTargetView.GetPinnableReference(), backgroundColor);

        deviceContext->ClearDepthStencilView(depthStencilView.GetPinnableReference(), (uint)(ClearFlag.ClearDepth | ClearFlag.ClearStencil), 1.0f, 0);

        return true;
    }

    public virtual void Draw()
    {
        GraphicsContext.swapChain.GetPinnableReference()
            ->Present(0, 0)
            .ThrowHResult();
    }

    private void ResetBuffers()
    {
        var device = graphicsContext.device.GetPinnableReference();

        if (backBufferRenderTargetView.Handle != null)
        {
            backBufferRenderTargetView.Release();
            backBufferTexture.Release();

            graphicsContext.swapChain.GetPinnableReference()->ResizeBuffers(2, (uint)windowViewport.Width, (uint)windowViewport.Height, Silk.NET.DXGI.Format.FormatR8G8B8A8Unorm, 0);
        }

        graphicsContext.swapChain.GetPinnableReference()
            ->GetBuffer(0, ID3D11Texture2D.Guid.Pointer(), (void**)backBufferTexture.GetAddressOf())
            .ThrowHResult();

        logger.LogInformation("CreateRenderTargetView (back buffer)");
        device
            ->CreateRenderTargetView(backBufferTexture, null, backBufferRenderTargetView.GetAddressOf())
            .ThrowHResult();


        Texture2DDesc depthStencilTextureDesc;
        depthStencilTextureDesc.Width = (uint)windowViewport.Width;
        depthStencilTextureDesc.Height = (uint)windowViewport.Height;
        depthStencilTextureDesc.MipLevels = 1;
        depthStencilTextureDesc.ArraySize = 1;
        depthStencilTextureDesc.Format = Format.FormatR32Typeless;
        depthStencilTextureDesc.SampleDesc.Count = 1;
        depthStencilTextureDesc.SampleDesc.Quality = 0;
        depthStencilTextureDesc.Usage = Usage.UsageDefault;
        depthStencilTextureDesc.BindFlags = (uint)(BindFlag.BindDepthStencil);
        depthStencilTextureDesc.CPUAccessFlags = 0;
        depthStencilTextureDesc.MiscFlags = 0;


        var depthStencilViewDesc = new DepthStencilViewDesc(
            viewDimension: DsvDimension.DsvDimensionTexture2D,
            format: Format.FormatD32Float);

        depthStencilViewDesc.Texture2D.MipSlice = 0;

        device
            ->CreateTexture2D(ref depthStencilTextureDesc, null, depthStencilTexture.GetAddressOf())
            .ThrowHResult();

        device
            ->CreateDepthStencilView((ID3D11Resource*)depthStencilTexture.GetPinnableReference(), ref depthStencilViewDesc, depthStencilView.GetAddressOf())
            .ThrowHResult();

        DepthStencilDesc depthStencilDesc;
        depthStencilDesc.DepthEnable = 1;
        depthStencilDesc.DepthWriteMask = DepthWriteMask.DepthWriteMaskAll;
        depthStencilDesc.DepthFunc = ComparisonFunc.ComparisonLess;
        depthStencilDesc.StencilEnable = 1;
        depthStencilDesc.StencilReadMask = 0xFF;
        depthStencilDesc.StencilWriteMask = 0xFF;
        depthStencilDesc.FrontFace.StencilFailOp = StencilOp.StencilOpKeep;
        depthStencilDesc.FrontFace.StencilDepthFailOp = StencilOp.StencilOpIncr;
        depthStencilDesc.FrontFace.StencilPassOp = StencilOp.StencilOpKeep;
        depthStencilDesc.FrontFace.StencilFunc = ComparisonFunc.ComparisonAlways;
        depthStencilDesc.BackFace.StencilFailOp = StencilOp.StencilOpKeep;
        depthStencilDesc.BackFace.StencilDepthFailOp = StencilOp.StencilOpDecr;
        depthStencilDesc.BackFace.StencilPassOp = StencilOp.StencilOpKeep;
        depthStencilDesc.BackFace.StencilFunc = ComparisonFunc.ComparisonAlways;

        device
            ->CreateDepthStencilState(ref depthStencilDesc, depthStencilState.GetAddressOf())
            .ThrowHResult();
    }

    public T Create<T>()
        where T : IComponent
    {
        var component = serviceProvider.GetRequiredService<T>();
        return component;
    }
}
