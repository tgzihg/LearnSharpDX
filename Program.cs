using SharpDX.Windows;
using System;
using SharpDX.DXGI;
using SharpDX.D3DCompiler;
using D3D11 = SharpDX.Direct3D11;
using SharpDX.Direct3D11;
using SharpDX;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

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
        struct MyVertex {
            public Vector3 Position;
            public Vector4 Color;
            public Vector3 Normal;
            public Vector3 TangentU;
            public Vector2 TexC;
            public MyVertex(Vector3 pos, Vector4 color, Vector3 nor, Vector3 tan, Vector2 tex) {
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
            public MyVertex[] Vertices;
            public int[] Indices;
            public MeshData(int count) {
                Vertices = new MyVertex[count];
                Indices = new int[count];
            }
            public MeshData(int vertexCount, int indexCount) {
                Vertices = new MyVertex[vertexCount];
                Indices = new int[indexCount];
            }
            public void Resize2DQuad(int xNum, int zNum) {
                Vertices = new MyVertex[xNum * zNum];
                Indices = new int[(xNum - 1) * (zNum - 1) * 6];
            }
        }
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
            MeshData cylinderMesh;
            int sliceNum = 100;
            int stackNum = 10;
            int topRad = 10;
            int botRad = 10;
            int height = 20;
            GenerateCylinder(topRad, botRad, height, sliceNum, stackNum, out cylinderMesh);
            GenerateCyMeshIndex(sliceNum, stackNum, ref cylinderMesh);
            GenerateTopFace(topRad, botRad, height, sliceNum, ref cylinderMesh);
            GenerateBotFace(topRad, botRad, height, sliceNum, ref cylinderMesh);
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
            var CylVertexBuffer = D3D11.Buffer.Create<MyVertex>(_d3DDevice, BindFlags.VertexBuffer, cylinderMesh.Vertices);
            var CylIndexBuffer = D3D11.Buffer.Create<int>(_d3DDevice, BindFlags.IndexBuffer, cylinderMesh.Indices);
            _d3DDeviceContext.InputAssembler.InputLayout = _inputLayout;
            _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            _d3DDeviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(CylVertexBuffer, Utilities.SizeOf<MyVertex>(), 0));
            _d3DDeviceContext.InputAssembler.SetIndexBuffer(CylIndexBuffer, Format.R32_UInt, 0);
            proj = Matrix.Identity;
            view = Matrix.LookAtLH(camPos, camTo, camUp);
            world = Matrix.Identity;
            _resized = true;
            Texture2D backBuffer = null;
            RenderTargetView renderView = null;
            Texture2D depthBuffer = null;
            DepthStencilView depthView = null;
            long lastTime = 0;
            // 帧数钟
            var clock = new System.Diagnostics.Stopwatch();
            clock.Start();
            int fpsCounter = 0;
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
                var viewPort = new Viewport(0, 0, _renderForm.ClientSize.Width, _renderForm.ClientSize.Height,0,100f);
                _d3DDeviceContext.Rasterizer.SetViewport(viewPort);
                var viewProj = Matrix.Multiply(view, proj);
                worldViewProj = world * viewProj;
                mfxWorldViewProj.SetMatrix(worldViewProj);
                mfxPass.Apply(_d3DDeviceContext);
                _d3DDeviceContext.DrawIndexed(cylinderMesh.Indices.Length, 0, 0);
                _swapChain.Present(0, PresentFlags.None);
                fpsCounter++;
                if (clock.ElapsedMilliseconds - lastTime >= 1000) {
                    _renderForm.Text = "FPS:" + fpsCounter.ToString();
                    fpsCounter = 0;
                    lastTime = clock.ElapsedMilliseconds;
                }
            });
        }

        

        public void Dispose() {
            _swapChain.Dispose();
            _d3DDevice.Dispose();
            _d3DDeviceContext.Dispose();
        }

        #region 几何网格
        void GenerateCylinder(float topRadius, float bottomRadius, float height, int sliceCount, int stackCount, out MeshData outMesh) {
            // init meshdata size
            var vertexsNum = (stackCount + 1) * (sliceCount + 1);
            var indexNum = stackCount * sliceCount * 6;
            outMesh = new MeshData(vertexsNum, indexNum);
            // Stacks
            float stackHeight = height / stackCount;
            // radius increment
            float radiusStep = (topRadius - bottomRadius) / stackCount;
            // ring count
            var ringCount = stackCount + 1;
            int vetexNumber = 0;
            float dTheta = 2.0f * (float)Math.PI / sliceCount;
            // 层层建模 侧面顶点数据
            for (int i = 0; i < ringCount; i++) {
                // 建模中心 y 在中心高度处，从低往高建
                float y = -0.5f * height + i * stackHeight;
                float r = bottomRadius + i * radiusStep;
                // vertices
                // 起始顶点和最终顶点只是位置一样，但顶点其它分量不同
                for (int j = 0; j <= sliceCount; j++) {
                    float c = (float)Math.Cos(j * dTheta);
                    float s = (float)Math.Sin(j * dTheta);
                    MyVertex myVertex = new MyVertex();
                    myVertex.Position = new Vector3(r * c, y, r * s);
                    myVertex.TexC.X = (float)j / sliceCount;
                    myVertex.TexC.Y = 1.0f - (float)i / stackCount;
                    myVertex.TangentU = new Vector3(-s, 0f, c);
                    myVertex.Color = new Vector4(1, 1, 1, 1);
                    float dr = bottomRadius - topRadius;
                    Vector3 bitangent = new Vector3(dr * c, -height, dr * s);
                    myVertex.Normal = Vector3.Normalize(Vector3.Cross(myVertex.TangentU, bitangent));
                    outMesh.Vertices[vetexNumber] = myVertex;
                    vetexNumber++;
                }
            }
        }
        void GenerateCyMeshIndex(int sliceCount, int stackCount, ref MeshData refMesh) {
            int number = 0;
            int numsPerRing = sliceCount + 1;
            for (int i = 0; i < stackCount; i++) {
                for (int j = 0; j < sliceCount; j++) {
                    refMesh.Indices[number + 0] = i * numsPerRing + j;
                    refMesh.Indices[number + 1] = (i + 1) * numsPerRing + j;
                    refMesh.Indices[number + 2] = (i + 1) * numsPerRing + j + 1;
                    refMesh.Indices[number + 3] = (i + 1) * numsPerRing + j + 1;
                    refMesh.Indices[number + 4] = i * numsPerRing + j + 1;
                    refMesh.Indices[number + 5] = i * numsPerRing + j;
                    number += 6;
                }
            }
        }
        void GenerateTopFace(float topRadius, float bottomRadius, float height, int sliceCount, ref MeshData outMesh) {
            // 顶面顶点数据
            float dTheta = 2.0f * (float)Math.PI / sliceCount;
            List<MyVertex> tempVertex = outMesh.Vertices.ToList();
            for (int i = 0; i <= sliceCount; i++) {
                float x = (float)(topRadius * Math.Cos(i * dTheta));
                float z = (float)(topRadius * Math.Sin(i * dTheta));
                MyVertex myVertex = new MyVertex();
                myVertex.Position = new Vector3(x, 0.5f * height, z);
                myVertex.TexC.X = x / height + 0.5f;
                myVertex.TexC.Y = z / height + 0.5f;
                myVertex.TangentU = new Vector3(1, 0, 0);
                myVertex.Color = new Vector4(1, 1, 0, 1);
                myVertex.Normal = new Vector3(0, 1, 0);

                tempVertex.Add(myVertex);
            }
            // 顶点中心数据
            var myVertexCenter = new MyVertex();
            myVertexCenter.Position = new Vector3(0, 0.5f * height, 0);
            myVertexCenter.TexC.X = 0.5f;
            myVertexCenter.TexC.Y = 0.5f;
            myVertexCenter.TangentU = new Vector3(1, 0, 0);
            myVertexCenter.Color = new Vector4(1, 1, 0, 1);
            myVertexCenter.Normal = new Vector3(0, 1, 0);

            tempVertex.Add(myVertexCenter);
            outMesh.Vertices = tempVertex.ToArray();

            // 顶面索引
            List<int> tempIndices = outMesh.Indices.ToList();
            for (int i = 0; i <= sliceCount; i++) {
                tempIndices.Add(outMesh.Vertices.Length - 1);
                tempIndices.Add(outMesh.Vertices.Length - 2 - i);
                tempIndices.Add(outMesh.Vertices.Length - 1 - i);
            }
            outMesh.Indices = tempIndices.ToArray();
        }

        private void GenerateBotFace(float topRadius, float bottomRadius, float height, int sliceCount, ref MeshData outMesh) {
            // 底面顶点数据
            float dTheta = 2.0f * (float)Math.PI / sliceCount;
            List<MyVertex> tempVertex = outMesh.Vertices.ToList();
            for (int i = 0; i <= sliceCount; i++) {
                float x = (float)(topRadius * Math.Cos(i * dTheta));
                float z = (float)(topRadius * Math.Sin(i * dTheta));
                MyVertex myVertex = new MyVertex();
                myVertex.Position = new Vector3(x, -0.5f * height, z);
                myVertex.TexC.X = x / height + 0.5f;
                myVertex.TexC.Y = z / height + 0.5f;
                myVertex.TangentU = new Vector3(1, 0, 0);
                myVertex.Color = new Vector4(1, 0, 1, 1);
                myVertex.Normal = new Vector3(0, -1, 0);

                tempVertex.Add(myVertex);
            }
            // 顶点中心数据
            var myVertexCenter = new MyVertex();
            myVertexCenter.Position = new Vector3(0, -0.5f * height, 0);
            myVertexCenter.TexC.X = 0.5f;
            myVertexCenter.TexC.Y = 0.5f;
            myVertexCenter.TangentU = new Vector3(1, 0, 0);
            myVertexCenter.Color = new Vector4(1, 0, 1, 1);
            myVertexCenter.Normal = new Vector3(0, -1, 0);

            tempVertex.Add(myVertexCenter);
            outMesh.Vertices = tempVertex.ToArray();

            // 顶面索引
            List<int> tempIndices = outMesh.Indices.ToList();
            for (int i = 0; i <= sliceCount; i++) {
                tempIndices.Add(outMesh.Vertices.Length - 1);
                tempIndices.Add(outMesh.Vertices.Length - 2 - i);
                tempIndices.Add(outMesh.Vertices.Length - 1 - i);
            }
            outMesh.Indices = tempIndices.ToArray();
        }
        #endregion
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
                    camPos.Z += 1f;
                    break;
                case System.Windows.Forms.Keys.S:
                    camPos.Z -= 1f;
                    break;
                case System.Windows.Forms.Keys.A:
                    camPos.X -= 1f;
                    break;
                case System.Windows.Forms.Keys.D:
                    camPos.X += 1f;
                    break;
                case System.Windows.Forms.Keys.Q:
                    camPos.Y += 1f;
                    break;
                case System.Windows.Forms.Keys.E:
                    camPos.Y -= 1f;
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
       
    }
}
