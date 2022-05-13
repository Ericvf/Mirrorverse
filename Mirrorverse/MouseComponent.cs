using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Maths;
using System.Numerics;
using Matrix = System.Numerics.Matrix4x4;

public unsafe class MouseComponent : Component
{
    const int spriteSize = 10;
    const uint VertexCount = 3;

    private readonly ILogger<MouseComponent> logger;
    private ComPtr<ID3D11VertexShader> vertexShader = default;
    private ComPtr<ID3D11PixelShader> pixelShader = default;
    private ComPtr<ID3D11InputLayout> inputLayout = default;
    private ComPtr<ID3D11Buffer> vertexBuffer = default;
    private ComPtr<ID3D11Buffer> constantBuffer = default;

    private ModelViewProjectionConstantBuffer constantBufferData;
    private Vector2D<int> windowSize;
    private int halfWindowWidth;
    private int halfWindowHeight;
    private int aspectRatio;

    public Vector2 Position { get; set; }

    public MouseComponent(ILogger<MouseComponent> logger)
    {
        this.logger = logger;
    }

    public override void Initialize(IApp app)
    {
        var device = app.GraphicsContext.device.GetPinnableReference();
        var compilerApi = D3DCompiler.GetApi();

        // Create vertex shader
        var compileFlags = 0u;
#if DEBUG
        compileFlags |= (1 << 0) | (1 << 2);
#endif

        logger.LogInformation("CreateVertexShader");
        ID3D10Blob* vertexShaderBlob;
        ID3D10Blob* errorMsgs;
        fixed (char* fileName = Helpers.GetAssetFullPath(@"Shaders\SimpleShader.hlsl"))
        {
            compilerApi.CompileFromFile(fileName
            , null
            , null
            , "VS"
            , "vs_4_0"
            , compileFlags
            , 0
            , &vertexShaderBlob
            , &errorMsgs)
            .ThrowHResult();
        }

        device->CreateVertexShader(
            vertexShaderBlob->GetBufferPointer()
            , vertexShaderBlob->GetBufferSize()
            , null
            , vertexShader.GetAddressOf())
            .ThrowHResult();

        if (errorMsgs != null)
            errorMsgs->Release();

        // Pixel shader
        logger.LogInformation("CreatePixelShader");
        ID3D10Blob* pixelShaderBlob;
        fixed (char* fileName = Helpers.GetAssetFullPath(@"Shaders\SimpleShaderPS.hlsl"))
        {
            compilerApi.CompileFromFile(fileName
                , null
                , null
                , "PS"
                , "ps_4_0"
                , compileFlags
                , 0
                , &pixelShaderBlob
                , &errorMsgs)
            .ThrowHResult();
        }

        device
            ->CreatePixelShader(
                pixelShaderBlob->GetBufferPointer()
                , pixelShaderBlob->GetBufferSize()
                , null
                , pixelShader.GetAddressOf())
            .ThrowHResult();

        pixelShaderBlob->Release();

        // Create input layout
        var lpPOSITION = (byte*)SilkMarshal.StringToPtr("POSITION", NativeStringEncoding.LPStr);
        var lpCOLOR = (byte*)SilkMarshal.StringToPtr("COLOR", NativeStringEncoding.LPStr);

        var inputLayouts = stackalloc InputElementDesc[]
        {
            new InputElementDesc
            {
                SemanticName = lpPOSITION,
                SemanticIndex = 0,
                Format = Format.FormatR32G32B32Float,
                InputSlot = 0,
                AlignedByteOffset = 0,
                InputSlotClass = InputClassification.InputPerVertexData,
                InstanceDataStepRate = 0
            },
            new InputElementDesc
            {
                SemanticName = lpCOLOR,
                SemanticIndex = 0,
                Format = Format.FormatR32G32B32A32Float,
                InputSlot = 0,
                AlignedByteOffset = 12,
                InputSlotClass = InputClassification.InputPerVertexData,
                InstanceDataStepRate = 0
            },
        };

        logger.LogInformation("CreateInputLayout");
        device
            ->CreateInputLayout(
                inputLayouts
                , 2
                , vertexShaderBlob->GetBufferPointer()
                , vertexShaderBlob->GetBufferSize()
                , inputLayout.GetAddressOf())
            .ThrowHResult();

        SilkMarshal.Free((nint)lpPOSITION);
        SilkMarshal.Free((nint)lpCOLOR);

        vertexShaderBlob->Release();

        // Vertex buffer
        var bufferDesc = new BufferDesc();
        bufferDesc.Usage = Usage.UsageDefault;
        bufferDesc.ByteWidth = (uint)sizeof(VertexPositionColor) * VertexCount;
        bufferDesc.BindFlags = (uint)BindFlag.BindVertexBuffer;
        bufferDesc.CPUAccessFlags = 0;

        var vertices = stackalloc VertexPositionColor[]
        {
            new VertexPositionColor { Position = new Vector3(0, 0, 0.0f), Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f) },
            new VertexPositionColor { Position = new Vector3(1 * spriteSize, -2 * spriteSize, 0.0f), Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f) },
            new VertexPositionColor { Position = new Vector3(-1 * spriteSize, -2 * spriteSize, 0.0f), Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f) },
        };

        var subresourceData = new SubresourceData();
        subresourceData.PSysMem = vertices;

        logger.LogInformation("CreateBuffer (Vertex buffer)");
        device
            ->CreateBuffer(ref bufferDesc, ref subresourceData, vertexBuffer.GetAddressOf())
            .ThrowHResult();

        // Create constantBuffer
        var cbufferDesc = new BufferDesc();
        cbufferDesc.Usage = Usage.UsageDefault;
        cbufferDesc.ByteWidth = (uint)Helpers.RoundUp(sizeof(ModelViewProjectionConstantBuffer), 16);
        cbufferDesc.BindFlags = (uint)BindFlag.BindConstantBuffer;
        cbufferDesc.CPUAccessFlags = 0;

        device->CreateBuffer(ref cbufferDesc, null, constantBuffer.GetAddressOf())
            .ThrowHResult();
    }

    internal void Resize(Vector2D<int> windowSize)
    {
        this.windowSize = windowSize;

        halfWindowWidth = windowSize.X / 2;
        halfWindowHeight = windowSize.Y / 2;
        aspectRatio = windowSize.X / windowSize.Y;
    }

    public override void Draw(IApp app, double time)
    {
        uint stride = (uint)sizeof(VertexPositionColor);
        uint offset = 0;

        var deviceContext = app.GraphicsContext.deviceContext.GetPinnableReference();
        deviceContext->VSSetShader(vertexShader, null, 0);
        deviceContext->PSSetShader(pixelShader, null, 0);
        deviceContext->IASetInputLayout(inputLayout);
        deviceContext->IASetPrimitiveTopology(D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);

        var rotationMatrix = Matrix.CreateRotationZ(45 * MathF.PI / 180);

        var modelMatrix = rotationMatrix * Matrix.CreateTranslation(
            new Vector3(
                halfWindowWidth + Position.X,
                halfWindowHeight - Position.Y, 0));

        var viewMatrix = Matrix.CreateLookAt(Vector3.UnitZ, Vector3.Zero, Vector3.UnitY);
        var projectionMatrix = Matrix.CreateOrthographic(windowSize.X, windowSize.Y, 0.01f, 1000f);

        constantBufferData.model = Matrix.Transpose(modelMatrix);
        constantBufferData.view = Matrix.Transpose(viewMatrix);
        constantBufferData.projection = Matrix.Transpose(projectionMatrix);

        fixed (ModelViewProjectionConstantBuffer* bufferData = &constantBufferData)
        {
            deviceContext->UpdateSubresource((ID3D11Resource*)constantBuffer.GetPinnableReference(), 0, null, bufferData, 0, 0);
        }

        deviceContext->VSSetConstantBuffers(0, 1, constantBuffer.GetAddressOf());
        deviceContext->IASetVertexBuffers(0, 1, vertexBuffer.GetAddressOf(), ref stride, ref offset);
        deviceContext->Draw(VertexCount, 0);
    }
}
