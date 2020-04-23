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
using System.Threading;

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
            public MeshData(int a = 1, int b = 1) {
                Vertices = new MyVertex[a];
                Indices = new int[b];
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
        Vector3 camPos = new Vector3(0.0f, 1.0f, -55.0f);
        Vector3 camTo = new Vector3(2f, 0f, 0f);
        Vector3 camUp = new Vector3(0f, 1f, 0f);
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

        MeshData sphereMesh;
        MeshData cylinderMesh;
        MeshData boxMesh;
        MeshData planeMesh;
        MeshData renderMesh;

        /// <summary>
        /// 初始化
        /// </summary>
        public MySharpDXForm() {
            //[0]生成两个几何体的MeshData
            GenerateSphere(20, 10, 10, out sphereMesh);
            GenerateCylinder(10, 10, 20, 100, 10, out cylinderMesh);
            GenerateBox(10, 5, 2, 10, 5, 20, out boxMesh);
            GeneratePlane(10, 5, 20, 10, out planeMesh);
            renderMesh = sphereMesh;
            //[1]常规代码
            DoCommonThings();
        }
        /// <summary>
        /// 三大块：初始化设备、设置缓冲和管线等、渲染循环
        /// </summary>
        private void DoCommonThings() {
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
            using (var effectByteCode = ShaderBytecode.CompileFromFile("../../MyShader.fx", "fx_5_0", ShaderFlags.Debug | ShaderFlags.SkipOptimization)) {
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
            var CylVertexBuffer = D3D11.Buffer.Create<MyVertex>(_d3DDevice, BindFlags.VertexBuffer, renderMesh.Vertices);
            var CylIndexBuffer = D3D11.Buffer.Create<int>(_d3DDevice, BindFlags.IndexBuffer, renderMesh.Indices);
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
                var viewProj = Matrix.Multiply(view, proj);
                worldViewProj = world * viewProj;
                mfxWorldViewProj.SetMatrix(worldViewProj);
                mfxPass.Apply(_d3DDeviceContext);
                //索引画图
                _d3DDeviceContext.DrawIndexed(renderMesh.Indices.Length, 0, 0);
                //顶点画图
                //_d3DDeviceContext.Draw(renderMesh.Vertices.Length, 0);
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
        #region 生成基本几何体
        /// <summary>
        /// 生成球体
        /// </summary>
        /// <param name="rad">球体半径</param>
        /// <param name="sliceNum">切片数</param>
        /// <param name="stackNum">断层数</param>
        /// <param name="sphereMesh">输出的MeshData网格结构</param>
        void GenerateSphere(int rad, int sliceNum, int stackNum, out MeshData sphereMesh) {
            //[0]侧面
            sphereMesh = new MeshData(1, 1);
            float dTheta = 2.0f * (float)Math.PI / sliceNum;
            float dPhi = (float)Math.PI / stackNum;
            float Phi = 0;
            var tempVertex = sphereMesh.Vertices.ToList();
            tempVertex.Clear();
            //[1]底部顶点
            tempVertex.Add(new MyVertex() {
                Position = new Vector3(0, -rad, 0),
                Color = new Vector4(1, 0, 1, 1),
                Normal = new Vector3(0, -1, 0),
                TangentU = new Vector3(0, 0, 0),
                TexC = new Vector2(0, 0),
            });
            float layerColor = 0f;
            //[2]层层建模 侧面顶点数据
            for (int i = 1; i < stackNum; i++) {
                // 建模中心 y 在中心高度处，从低往高建
                float y = -rad * (float)Math.Cos(Phi + i * dPhi);
                float r = rad * (float)Math.Sin(Phi + i * dPhi);
                // 起始顶点和最终顶点只是位置一样，但顶点其它分量不同
                for (int j = 0; j <= sliceNum; j++) {
                    float c = (float)Math.Cos(j * dTheta);
                    float s = (float)Math.Sin(j * dTheta);
                    MyVertex myVertex = new MyVertex();
                    myVertex.Position = new Vector3(r * c, y, r * s);
                    myVertex.TexC.X = (float)j / sliceNum;
                    myVertex.TexC.Y = 1.0f - (float)i / stackNum;
                    myVertex.TangentU = new Vector3(-s, 0f, c);
                    myVertex.Color = new Vector4(1, layerColor, 0.5f * layerColor, 1);
                    myVertex.Normal = new Vector3(1f, 0, 0);
                    tempVertex.Add(myVertex);
                }
                layerColor += 0.1f;
            }
            //[3]顶部
            tempVertex.Add(new MyVertex() {
                Position = new Vector3(0, rad, 0),
                Color = new Vector4(1, 1, 0, 1),
                Normal = new Vector3(0, 1, 0),
                TangentU = new Vector3(0, 0, 0),
                TexC = new Vector2(0, 0),
            });
            sphereMesh.Vertices = tempVertex.ToArray();

            //[4]索引部分
            int numsPerRing = sliceNum + 1;
            List<int> tempIndice = sphereMesh.Indices.ToList();
            tempIndice.Clear();
            int number = 0;
            for (int i = 0; i < sliceNum; i++) {
                tempIndice.Add(0);
                tempIndice.Add(number + 1);
                tempIndice.Add(number + 2);
                number += 1;
            }
            for (int i = 0; i < stackNum - 2; i++) {
                for (int j = 1; j <= sliceNum; j++) {
                    tempIndice.Add(i * numsPerRing + j);
                    tempIndice.Add((i + 1) * numsPerRing + j);
                    tempIndice.Add((i + 1) * numsPerRing + j + 1);
                    tempIndice.Add((i + 1) * numsPerRing + j + 1);
                    tempIndice.Add(i * numsPerRing + j + 1);
                    tempIndice.Add(i * numsPerRing + j);
                }
            }
            number = 0;
            for (int i = 0; i < sliceNum; i++) {
                tempIndice.Add(sphereMesh.Vertices.Length - 1);
                tempIndice.Add(sphereMesh.Vertices.Length - 2 - number);
                tempIndice.Add(sphereMesh.Vertices.Length - 3 - number);
                number += 1;
            }
            sphereMesh.Indices = tempIndice.ToArray();
        }
        /// <summary>
        /// 生成柱体
        /// </summary>
        /// <param name="topRadius">顶部半径</param>
        /// <param name="bottomRadius">底部半径</param>
        /// <param name="height">高度</param>
        /// <param name="sliceCount">切片数</param>
        /// <param name="stackCount">断层数</param>
        /// <param name="outMesh">输出的MeshData网格结构</param>
        void GenerateCylinder(float topRadius, float bottomRadius, float height, int sliceCount, int stackCount, out MeshData outMesh) {
            //[0] 侧面数据
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

            //[1] 修改索引
            int number = 0;
            int numsPerRing = sliceCount + 1;
            for (int i = 0; i < stackCount; i++) {
                for (int j = 0; j < sliceCount; j++) {
                    outMesh.Indices[number + 0] = i * numsPerRing + j;
                    outMesh.Indices[number + 1] = (i + 1) * numsPerRing + j;
                    outMesh.Indices[number + 2] = (i + 1) * numsPerRing + j + 1;
                    outMesh.Indices[number + 3] = (i + 1) * numsPerRing + j + 1;
                    outMesh.Indices[number + 4] = i * numsPerRing + j + 1;
                    outMesh.Indices[number + 5] = i * numsPerRing + j;
                    number += 6;
                }
            }

            //[2] 顶面数据和索引
            // 顶面顶点数据
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

            //[3] 底面数据和索引
            // 底面顶点数据
            tempVertex = outMesh.Vertices.ToList();
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
            myVertexCenter = new MyVertex();
            myVertexCenter.Position = new Vector3(0, -0.5f * height, 0);
            myVertexCenter.TexC.X = 0.5f;
            myVertexCenter.TexC.Y = 0.5f;
            myVertexCenter.TangentU = new Vector3(1, 0, 0);
            myVertexCenter.Color = new Vector4(1, 0, 1, 1);
            myVertexCenter.Normal = new Vector3(0, -1, 0);

            tempVertex.Add(myVertexCenter);
            outMesh.Vertices = tempVertex.ToArray();

            // 顶面索引
            tempIndices = outMesh.Indices.ToList();
            for (int i = 0; i <= sliceCount; i++) {
                tempIndices.Add(outMesh.Vertices.Length - 1);
                tempIndices.Add(outMesh.Vertices.Length - 2 - i);
                tempIndices.Add(outMesh.Vertices.Length - 1 - i);
            }
            outMesh.Indices = tempIndices.ToArray();
        }
        /// <summary>
        /// 生成长方形实体，索引暂未写
        /// </summary>
        /// <param name="length">长度</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="lenNum">长度方向切片数</param>
        /// <param name="widNum">宽度方向切片数</param>
        /// <param name="heiNum">高度方向切片数</param>
        /// <param name="outMesh">输出长方体数据</param>
        void GenerateBox(float length, float width, float height, int lenNum, int widNum, int heiNum, out MeshData outMesh) {
            //[0]顶点数据
            List<MyVertex> tempVertice = new List<MyVertex>();
            MyVertex tempVex;
            float dl = length / lenNum;
            float dw = width / widNum;
            float dh = height / heiNum;
            for (int i = 0; i < heiNum; i++) {
                //第i层数据(从下往上)
                float y = -height / 2 + i * dh;
                for (int j = 0; j < lenNum; j++) {
                    //第j行
                    float x = -length / 2 + j * dl;
                    for (int k = 0; k < widNum; k++) {
                        //第k列
                        float z = -width / 2 + k * dw;
                        tempVex = new MyVertex();
                        tempVex.Position = new Vector3(x, y, z);
                        tempVex.Color = new Vector4(1, 1, 1, 1);
                        tempVex.Normal = new Vector3(0, 1, 0);
                        tempVertice.Add(tempVex);
                    }
                }
            }
            //[1]索引数据
            List<int> tempIndex = new List<int>();
            tempIndex.Add(0);

            outMesh.Indices = tempIndex.ToArray();
            outMesh.Vertices = tempVertice.ToArray();
        }
        /// <summary>
        /// 生成平面
        /// </summary>
        /// <param name="length">平面长度 X方向</param>
        /// <param name="width">平面宽度 Z方向</param>
        /// <param name="lenNum">X方向切片数</param>
        /// <param name="widNum">Z方向切片数</param>
        /// <param name="outMesh">输出数据</param>
        void GeneratePlane(float length, float width, int lenNum, int widNum, out MeshData outMesh) {
            //[0]顶点数据
            List<MyVertex> tempVertice = new List<MyVertex>();
            MyVertex tempVex;
            lenNum += 1;
            widNum += 1;
            float dl = length / lenNum;
            float dw = width / widNum;
            float y = 0f;
            for (int i = 0; i < lenNum; i++) {
                float x = -length / 2 + i * dl;
                //第i列(从前往后)
                for (int j = 0; j < widNum; j++) {
                    //第j行(从左往右)
                    float z = -width / 2 + j * dw;
                    tempVex = new MyVertex();
                    tempVex.Position = new Vector3(x, y, z);
                    tempVex.Color = new Vector4(1, 1, 1, 1);
                    tempVex.Normal = new Vector3(0, 1, 0);
                    tempVertice.Add(tempVex);
                }
            }
            //[1]索引数据
            List<int> tempIndex = new List<int>();
            for (int i = 0; i < lenNum - 1; i++) {
                //第i列(从前往后)
                for (int j = 0; j < widNum - 1; j++) {
                    //第j行(从左往右)
                    tempIndex.Add(i * widNum + j);
                    tempIndex.Add(i * widNum + j + 1);
                    tempIndex.Add((i + 1) * widNum + j + 1);
                    tempIndex.Add((i + 1) * widNum + j + 1);
                    tempIndex.Add((i + 1) * widNum + j);
                    tempIndex.Add(i * widNum + j);
                }
            }

            outMesh.Indices = tempIndex.ToArray();
            outMesh.Vertices = tempVertice.ToArray();
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
