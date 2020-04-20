using SharpDX.Windows;
using System;
using SharpDX.DXGI;
using SharpDX.D3DCompiler;
using D3D11 = SharpDX.Direct3D11;
using SharpDX.Direct3D11;
using SharpDX;
using System.Windows.Forms;

namespace deleteSharpDX {
    class Program {
        static void Main(string[] args) {
            using (var demo = new MySharpDXForm()) {

            }
        }
    }
    class MySharpDXForm : IDisposable {
        private D3D11.Device _d3DDevice;
        private D3D11.DeviceContext _d3DDeviceContext;
        private SwapChain _swapChain;
        private InputLayout _inputLayout;
        private ShaderSignature _inputShaderSignature;
        private EffectMatrixVariable mfxWorldViewProj;
        /// <summary>
        /// 自定义顶点结构
        /// </summary>
        struct MyFuncVertex {
            public Vector3 Position;
            public Vector4 Color;
            public Vector3 Normal;
            public Vector3 TangentU;
            public Vector2 TexC;
            public MyFuncVertex(Vector3 pos, Vector4 color, Vector3 nor, Vector3 tan, Vector2 tex) {
                Position = pos;
                Normal = nor;
                TangentU = tan;
                TexC = tex;
                Color = color;
            }
        }
        /// <summary>
        /// 定义网格
        /// </summary>
        struct MeshData {
            public MyFuncVertex[] Vertices;
            public int[] Indices;
            public MeshData(int xNum, int zNum) {
                Vertices = new MyFuncVertex[xNum * zNum];
                Indices = new int[(xNum - 1) * (zNum - 1) * 6];
            }
        }
        MeshData meshData;
        private D3D11.InputElement[] _inputElementsForMesh = new D3D11.InputElement[] {
                new D3D11.InputElement("POSITION", 0, Format.R32G32B32_Float, 0),
                new D3D11.InputElement("COLOR", 0, Format.R32G32B32A32_Float, 0),
                new D3D11.InputElement("NORMAL", 0, Format.R32G32B32_Float, 0),
                new D3D11.InputElement("TANGENTU", 0, Format.R32G32B32_Float, 0),
                new D3D11.InputElement("TEXC", 0, Format.R32G32_Float, 0),
        };

        private Matrix proj;
        private Matrix view;
        private Matrix world;
        private Matrix worldViewProj;

        private Vector3 targetViewDir;
        Vector3 camPos  = new Vector3(0.0f, 2.0f, -5.0f);
        Vector3 camTo   = new Vector3(2f, 0f, 0f);
        Vector3 camUp   = new Vector3(0f, 1f, 0f);
        private int dx;
        private int dy;
        private float targetX = 0f;
        private float targetY = 0f;
        private float targetZ = 5f;
        private System.Drawing.Point preMouse;
        private bool _resized;
        private bool rotateFlag;
        private EffectPass mfxPassW;
        private EffectPass mfxPassS;
        private EffectPass mfxPass;
        private RenderForm _renderForm;

        /// <summary>
        /// 初始化
        /// </summary>
        public MySharpDXForm() {
            GenerateFXY(5f, 5f, 10, 10);
            GenerateMesh(10, 10);
            _renderForm = new RenderForm();
            _renderForm.ClientSize = new System.Drawing.Size(800, 600);
            _renderForm.KeyDown += _renderForm_KeyDown;
            _renderForm.Text = "愉快的学习SharpDX";
            _renderForm.Icon = null;
            _renderForm.ResizeBegin += (object sender, EventArgs e) => { _resized = true; };
            _renderForm.MouseDown += _renderForm_MouseDown;
            _renderForm.MouseUp += _renderForm_MouseUp;
            _renderForm.MouseMove += _renderForm_MouseMove;
            ModeDescription backBufferDesc = new ModeDescription(_renderForm.ClientSize.Width, _renderForm.ClientSize.Height, new Rational(60, 1), Format.R8G8B8A8_UNorm);
            SwapChainDescription swapChainDesc = new SwapChainDescription() {
                ModeDescription = backBufferDesc,
                SampleDescription = new SampleDescription(1, 0),
                Usage = Usage.RenderTargetOutput,
                BufferCount = 1,
                OutputHandle = _renderForm.Handle,
                IsWindowed = true,
                Flags = SwapChainFlags.AllowModeSwitch,
                SwapEffect = SwapEffect.Discard,
            };
            D3D11.Device.CreateWithSwapChain(
                SharpDX.Direct3D.DriverType.Hardware,
                D3D11.DeviceCreationFlags.Debug,
                swapChainDesc, out _d3DDevice, out _swapChain);
            _d3DDeviceContext = _d3DDevice.ImmediateContext;
            using (var effectByteCode = ShaderBytecode.CompileFromFile("../../MyShader.fx","fx_5_0")) {
                var effect = new Effect(_d3DDevice, effectByteCode);
                var technique = effect.GetTechniqueByName("ColorTech");
                mfxPassW = technique.GetPassByName("P0");
                mfxPassS = technique.GetPassByName("P1");
                mfxPass = mfxPassW;
                var passSignature = mfxPassW.Description.Signature;
                _inputShaderSignature = ShaderSignature.GetInputSignature(passSignature);
                mfxWorldViewProj = effect.GetVariableByName("worldViewProj").AsMatrix();
            }
            _inputLayout = new D3D11.InputLayout(_d3DDevice, _inputShaderSignature, _inputElementsForMesh);
            var FxyVertexBuffer = D3D11.Buffer.Create<MyFuncVertex>(_d3DDevice, BindFlags.VertexBuffer, meshData.Vertices);
            var indexBufferDesc = new BufferDescription(Utilities.SizeOf<int>() * meshData.Indices.Length, ResourceUsage.Default, BindFlags.IndexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            var FxyIndexBuffer = D3D11.Buffer.Create<int>(_d3DDevice, meshData.Indices, indexBufferDesc);
            _d3DDeviceContext.InputAssembler.InputLayout = _inputLayout;
            _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            _d3DDeviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(FxyVertexBuffer, Utilities.SizeOf<MyFuncVertex>(), 0));
            _d3DDeviceContext.InputAssembler.SetIndexBuffer(FxyIndexBuffer, Format.R32_UInt, 0);
            proj = Matrix.Identity;
            view = Matrix.LookAtLH(camPos, camTo, camUp);
            world = Matrix.Identity;
            _resized = true;
            Texture2D backBuffer = null;
            RenderTargetView renderView = null;
            Texture2D depthBuffer = null;
            DepthStencilView depthView = null;
            RenderLoop.Run(_renderForm, () => {
                targetViewDir = new Vector3(targetX, targetY, targetZ);
                view = Matrix.LookAtLH(camPos, camPos + targetViewDir, camUp);
                if (_resized) {
                    Utilities.Dispose(ref backBuffer);
                    Utilities.Dispose(ref renderView);
                    Utilities.Dispose(ref depthBuffer);
                    Utilities.Dispose(ref depthView);
                    _swapChain.ResizeBuffers(swapChainDesc.BufferCount, _renderForm.ClientSize.Width, _renderForm.ClientSize.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
                    backBuffer = Texture2D.FromSwapChain<Texture2D>(_swapChain, 0);
                    renderView = new RenderTargetView(_d3DDevice, backBuffer);
                    depthBuffer = new Texture2D(_d3DDevice, new Texture2DDescription() {
                        Format = Format.D32_Float_S8X24_UInt,
                        ArraySize = 1,
                        MipLevels = 1,
                        Width = _renderForm.ClientSize.Width,
                        Height = _renderForm.ClientSize.Height,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.DepthStencil,
                        CpuAccessFlags = CpuAccessFlags.None,
                        OptionFlags = ResourceOptionFlags.None
                    });
                    depthView = new DepthStencilView(_d3DDevice, depthBuffer);
                    _d3DDeviceContext.Rasterizer.SetViewport(new Viewport(0, 0, _renderForm.ClientSize.Width, _renderForm.ClientSize.Height, 0.0f, 1.0f));
                    _d3DDeviceContext.OutputMerger.SetTargets(depthView, renderView);
                    proj = Matrix.PerspectiveFovLH((float)Math.PI / 4f, _renderForm.ClientSize.Width / (float)_renderForm.ClientSize.Height, 0.1f, 100f);
                    _resized = false;
                }
                _d3DDeviceContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                _d3DDeviceContext.ClearRenderTargetView(renderView, SharpDX.Color.Black);
                var viewPort = new Viewport(0, 0, _renderForm.ClientSize.Width, _renderForm.ClientSize.Height);
                _d3DDeviceContext.Rasterizer.SetViewport(viewPort);
                var viewProj = Matrix.Multiply(view, proj);
                worldViewProj = world * viewProj;
                mfxWorldViewProj.SetMatrix(worldViewProj);
                mfxPassS.Apply(_d3DDeviceContext);
                _d3DDeviceContext.DrawIndexed(meshData.Indices.Length/2, 0, 0);
                mfxPassW.Apply(_d3DDeviceContext);
                _d3DDeviceContext.DrawIndexed(meshData.Indices.Length/2, meshData.Indices.Length / 2, 0);
                _swapChain.Present(0, PresentFlags.None);
            });
        }
        public void Dispose() {
            _swapChain.Dispose();
            _d3DDevice.Dispose();
            _d3DDeviceContext.Dispose();
        }
        #region Some Unimmportant Methods
        private void _renderForm_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e) {
            rotateFlag = false;
            preMouse = e.Location;
        }
        private void _renderForm_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e) {
            rotateFlag = true;
            preMouse = e.Location;
        }
        private void _renderForm_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (rotateFlag) {
                dx = e.Location.X - preMouse.X;
                dy = e.Location.Y - preMouse.Y;
                targetX -= dx / 10000f;
                targetY += dy / 10000f;
            }
        }
        private void _renderForm_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) {
            switch (e.KeyCode) {
                //元素 P-点 L-线 T-三角形
                case System.Windows.Forms.Keys.P:
                    _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.PointList;
                    break;
                case System.Windows.Forms.Keys.L:
                    _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.LineList;
                    break;
                case System.Windows.Forms.Keys.T:
                    _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
                    break;
                //全屏
                case System.Windows.Forms.Keys.F5:
                    _swapChain.SetFullscreenState(true, null);
                    _resized = true;
                    break;
                case System.Windows.Forms.Keys.F6:
                    _swapChain.SetFullscreenState(false, null);
                    _resized = true;
                    break;
                //移动摄像机：
                case System.Windows.Forms.Keys.W:
                    camPos.Z += 0.1f;
                    break;
                case System.Windows.Forms.Keys.S:
                    camPos.Z -= 0.1f;
                    break;
                case System.Windows.Forms.Keys.A:
                    camPos.X -= 0.1f;
                    break;
                case System.Windows.Forms.Keys.D:
                    camPos.X += 0.1f;
                    break;
                case System.Windows.Forms.Keys.Q:
                    camPos.Y += 0.1f;
                    break;
                case System.Windows.Forms.Keys.E:
                    camPos.Y -= 0.1f;
                    break;
                case System.Windows.Forms.Keys.Escape:
                    _renderForm.Close();
                    break;
                case System.Windows.Forms.Keys.Z:
                    mfxPass = mfxPassW;
                    break;
                case System.Windows.Forms.Keys.X:
                    mfxPass = mfxPassS;
                    break;
                default:
                    break;
            }
        }
        #endregion
        #region 三维函数顶点
        void GenerateMesh(int xNum, int zNum) {
            int k = 0;
            for (int quadX = 0; quadX < xNum-1; quadX++) {
                for (int quadZ = 0; quadZ < zNum-1; quadZ++) {
                    meshData.Indices[k + 0] = quadX * zNum + quadZ;
                    meshData.Indices[k + 1] = quadX * zNum + quadZ + 1;
                    meshData.Indices[k + 2] = (quadX + 1) * zNum + quadZ + 1;

                    meshData.Indices[k + 3] = (quadX + 1) * zNum + quadZ + 1;
                    meshData.Indices[k + 4] = (quadX + 1) * zNum + quadZ;
                    meshData.Indices[k + 5] = quadX * zNum + quadZ;
                    k += 6;
                }
            }
        }
        void GenerateFXY(float xlength, float zlength, int xNum, int zNum) {
            int index;
            meshData = new MeshData(xNum, zNum);
            var dx = xlength / xNum;
            var dz = zlength / zNum;
            // for texturing 纹理坐标
            var du = 1f / xNum;
            var dv = 1f / zNum;
            for (int xIndex = 0; xIndex < xNum; xIndex++) {
                for (int zIndex = 0; zIndex < zNum; zIndex++) {
                    index = xIndex * zNum + zIndex;
                    // 位置坐标
                    meshData.Vertices[index].Position   = new Vector3(xIndex, getNum(xIndex, zIndex), zIndex);
                    if (meshData.Vertices[index].Position.Y < 5) {
                        meshData.Vertices[index].Color = new Vector4(1, 1, 1, 1);
                    } else if (meshData.Vertices[index].Position.Y < 10) {
                        meshData.Vertices[index].Color = new Vector4(1, 0, 1, 1);
                    } else if (meshData.Vertices[index].Position.Y < 15) {
                        meshData.Vertices[index].Color = new Vector4(1, 1, 0, 1);
                    }
                    // 光照
                    meshData.Vertices[index].Normal     = new Vector3(0f, 1f, 0f);
                    meshData.Vertices[index].TangentU   = new Vector3(1f, 0f, 0f);
                    // 纹理坐标
                    meshData.Vertices[index].TexC.X     = xIndex * du;
                    meshData.Vertices[index].TexC.Y     = zIndex * dv;
                }
            }
        }

        float getNum(float x, float z) {
            return (float)Math.Sqrt(x * x + z * z);
        }
        #endregion
    }
}
