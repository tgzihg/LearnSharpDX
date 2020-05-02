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
using System.Runtime.InteropServices;
using SharpDX.WIC;

namespace deleteSharpDX {
    class Program {
        static void Main(string[] args) {
            //Console.WriteLine("说明：");
            //Console.WriteLine("-移动位置： W A S D Q E");
            //Console.WriteLine("-旋转视角：鼠标");
            //Console.WriteLine("-模型旋转：R键开启或关闭，默认开启");
            //Console.WriteLine("默认窗口显示，建议按下*F5*切换到全屏模式进行更舒服地explorer");
            //Console.WriteLine("阅读完毕后，按下回车展示模型");

            //Console.ReadKey();
            using (var demo = new MySharpDXForm()) {
            }
        }
    }
    /// <summary>
    /// 自定义顶点结构
    /// </summary>
    struct MyVertex {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 Tex;
        public MyVertex(Vector3 pos, Vector3 nor, Vector2 tex) {
            Position = pos;
            Normal = nor;
            Tex = tex;
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
    #region 光照
    /// <summary>
    /// 材质
    /// </summary>
    struct Material {
        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 Specular;
        public Vector4 Reflect;

        public Material(Vector4 ambient, Vector4 diffuse, Vector4 specular, Vector4 reflect) {
            Ambient = ambient;
            Diffuse = diffuse;
            Specular = specular;
            Reflect = reflect;
        }
    }
    /// <summary>
    /// 平行光
    /// </summary>
    struct DirectionalLight {
        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 Specular;
        public Vector3 Direction;
        public float Pad;
        public DirectionalLight(Vector4 ambient, Vector4 diffuse, Vector4 specular, Vector3 direction, float pad) {
            Ambient = ambient;
            Diffuse = diffuse;
            Specular = specular;
            Direction = direction;
            Pad = pad;
        }
    }
    /// <summary>
    /// 点光源
    /// </summary>
    struct PointLight {
        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 Specular;
        // 包装成4D向量
        public Vector3 Position;
        public float Range;
        // 包装成4D向量
        public Vector3 Att;
        public float Pad;

        public PointLight(Vector4 ambient, Vector4 diffuse, Vector4 specular, Vector3 position, float range, Vector3 att, float pad) {
            Ambient = ambient;
            Diffuse = diffuse;
            Specular = specular;
            Position = position;
            Range = range;
            Att = att;
            Pad = pad;
        }
    }
    /// <summary>
    /// 聚光灯
    /// </summary>
    struct SpotLight {
        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 Specular;
        // 包装成4D向量
        public Vector3 Position;
        public float Range;
        // 包装成4D向量
        public Vector3 Direction;
        public float Spot;
        // 包装成4D向量
        public Vector3 Att;
        public float Pad;
    }
    #endregion
    class MySharpDXForm : IDisposable {
        private D3D11.Device _d3DDevice;
        private D3D11.DeviceContext _d3DDeviceContext;
        private SwapChain _swapChain;
        private InputLayout _inputLayout;
        private ShaderSignature _inputShaderSignature;
        private EffectMatrixVariable mfxWorldViewProj;

        private D3D11.InputElement[] _inputElementsForMesh = new D3D11.InputElement[] {
                new D3D11.InputElement("POSITION", 0, Format.R32G32B32_Float, 0),
                new D3D11.InputElement("NORMAL", 0, Format.R32G32B32_Float, 0),
                new D3D11.InputElement("TEXTURE", 0, Format.R32G32_Float, 0),
        };

        private Matrix proj;
        private Matrix view;
        private Matrix[] world;
        private Matrix worldViewProj;

        // for camera
        Vector3 camRight = new Vector3(1, 0, 0);
        Vector3 camUp = new Vector3(0, 1, 0);
        Vector3 camLook = new Vector3(0, 0, 1);
        Vector3 camPos = new Vector3(0, 0, -5);

        private System.Drawing.Point _lastMousePoint;
        private bool _resized;
        private EffectVariable mfxDirLight;
        private EffectVariable mfxPointLight;
        private EffectVariable mfxSpotLight;
        private EffectVariable mfxEyePosW;
        private EffectVariable mfxMaterial;
        private EffectPass mfxPassW;
        private EffectPass mfxPassS;
        private EffectPass mfxPass;
        private EffectMatrixVariable mfxWorld;
        private EffectMatrixVariable mfxWorldTranInv;
        private EffectShaderResourceVariable mfxShaderRSVar;
        private EffectMatrixVariable mfxTexTransform;
        private RenderForm _renderForm;

        private MeshData mMeshData;
        private MeshData[] mSubMeshData;
        int[] vexNum;
        int[] vexOff;
        int[] indNum;
        int[] indOff;

        DirectionalLight mDirLight;
        PointLight mPointLight;
        SpotLight mSpotLight;
        Material[] mMatArray = new Material[1];
        //光照
        private byte[] _dirLightArray = new byte[Marshal.SizeOf(typeof(DirectionalLight)) * 1];
        private byte[] _pointLightArray = new byte[Marshal.SizeOf(typeof(PointLight)) * 1];
        private byte[] _spotLightArray = new byte[Marshal.SizeOf(typeof(SpotLight)) * 1];
        //材料
        private byte[] _matArray = new byte[Marshal.SizeOf(typeof(Material)) * 1];

        //贴图
        private ShaderResourceView ShaderRSV;

        /// <summary>
        /// 初始化
        /// </summary>
        public MySharpDXForm() {
            mSubMeshData = new MeshData[1];
            //纹理贴图——画正方体
            Tools.GenerateBox(1f, 1f, 1f, out mSubMeshData[0]);
            Tools.PackMeshDataInOne(mSubMeshData, out mMeshData, out vexNum, out vexOff, out indNum, out indOff);

            //初始化光源
            mDirLight = new DirectionalLight();
            mDirLight.Ambient = new Vector4(0.2f, 0.2f, 0.2f, 1.0f);
            mDirLight.Diffuse = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            mDirLight.Specular = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            mDirLight.Direction = new Vector3(0.57735f, -0.57735f, 0.57735f);

            mPointLight = new PointLight();
            mPointLight.Ambient = new Vector4(0.3f, 0.3f, 0.3f, 1.0f);
            mPointLight.Diffuse = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);
            mPointLight.Specular = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);
            mPointLight.Att = new Vector3(0.0f, 0.01f, 0.0f);
            mPointLight.Range = 15.0f;
            mPointLight.Position = new Vector3(0.0f, 10f, 0.0f);

            mSpotLight = new SpotLight();
            mSpotLight.Ambient = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
            mSpotLight.Diffuse = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
            mSpotLight.Specular = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            mSpotLight.Att = new Vector3(1.0f, 0.0f, 0.0f);
            mSpotLight.Spot = 96.0f;
            mSpotLight.Range = 10000.0f;
            //mSpotLight.Position = camPos;
            //mSpotLight.Direction = Vector3.Normalize(camTo - camPos);

            //材质
            mMatArray[0].Ambient = new Vector4(0.8f, 0.8f, 0.8f, 1.0f);
            mMatArray[0].Diffuse = new Vector4(0.6f, 0.7f, 0.4f, 1.0f);
            mMatArray[0].Specular = new Vector4(0.6f, 0.6f, 0.6f, 1.0f);

            //[*]常规代码
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
                var technique = effect.GetTechniqueByName("LightTech");

                //光照
                mfxDirLight = effect.GetVariableByName("gDirLight");
                mfxPointLight = effect.GetVariableByName("gPointLight");
                mfxSpotLight = effect.GetVariableByName("gSpotLight");

                mfxEyePosW = effect.GetVariableByName("gEyePosW");

                //材质
                mfxMaterial = effect.GetVariableByName("gMaterial");

                //纹理
                mfxShaderRSVar = effect.GetVariableByName("gTexture").AsShaderResource();
                mfxTexTransform = effect.GetVariableByName("gTexTransform").AsMatrix();

                //pass
                mfxPassW = technique.GetPassByName("P0");
                mfxPassS = technique.GetPassByName("P1");
                mfxPass = mfxPassS;

                mfxWorld = effect.GetVariableByName("gWorld").AsMatrix();
                mfxWorldTranInv = effect.GetVariableByName("gWorldInvTranspose").AsMatrix();
                mfxWorldViewProj = effect.GetVariableByName("gWorldViewProj").AsMatrix();


                var passSignature = mfxPassW.Description.Signature;
                _inputShaderSignature = ShaderSignature.GetInputSignature(passSignature);
            }
            _inputLayout = new D3D11.InputLayout(_d3DDevice, _inputShaderSignature, _inputElementsForMesh);
            var VertexBuffer = D3D11.Buffer.Create<MyVertex>(_d3DDevice, BindFlags.VertexBuffer, mMeshData.Vertices.ToArray());
            var IndexBuffer = D3D11.Buffer.Create<int>(_d3DDevice, BindFlags.IndexBuffer, mMeshData.Indices.ToArray());

            ShaderRSV = Tools.CreateShaderResourceViewFromFile(_d3DDevice, System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../model/ydmy.jpg"));

            _d3DDeviceContext.InputAssembler.InputLayout = _inputLayout;
            _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            _d3DDeviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(VertexBuffer, Utilities.SizeOf<MyVertex>(), 0));
            _d3DDeviceContext.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R32_UInt, 0);
            proj = Matrix.Identity;
            world = new Matrix[1];
            world[0] = Matrix.Identity;
            _resized = true;
            Texture2D backBuffer = null;
            RenderTargetView renderView = null;
            Texture2D depthBuffer = null;
            DepthStencilView depthView = null;
            long lastTime = 0;
            var clock = new System.Diagnostics.Stopwatch();
            clock.Start();
            int fpsCounter = 0;
            byte[] d;
            RenderLoop.Run(_renderForm, () => {
                view.Row1 = new Vector4(camRight, 0);
                view.Row2 = new Vector4(camUp, 0);
                view.Row3 = new Vector4(camLook, 0);
                view.Row4 = new Vector4(camPos, 1);
                view = Matrix.Invert(view);

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
                    proj = Matrix.PerspectiveFovLH((float)Math.PI / 4f, _renderForm.ClientSize.Width / (float)_renderForm.ClientSize.Height, 0.1f, 1000f);
                    _resized = false;
                }
                _d3DDeviceContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                _d3DDeviceContext.ClearRenderTargetView(renderView, SharpDX.Color.Black);
                var viewProj = Matrix.Multiply(view, proj);

                //设置平行光
                d = Tools.StructureToBytes(mDirLight);
                Array.Copy(d, 0, _dirLightArray, 0, Marshal.SizeOf(typeof(DirectionalLight)));
                using (var dataStream = DataStream.Create(_dirLightArray, false, false)) {
                    mfxDirLight.SetRawValue(dataStream, _dirLightArray.Length);
                }

                //纹理贴图：画正方体
                world[0] = Matrix.RotationAxis(Vector3.UnitY, clock.ElapsedMilliseconds / 1000f);
                worldViewProj = world[0] * viewProj;
                //像素着色器计算需要的变量
                mfxWorld.SetMatrix(world[0]);
                mfxWorldTranInv.SetMatrix(Tools.InverseTranspose(world[0]));
                mfxWorldViewProj.SetMatrix(worldViewProj);
                //设置材质
                d = Tools.StructureToBytes(mMatArray[0]);
                Array.Copy(d, 0, _matArray, 0, Marshal.SizeOf(typeof(Material))); // 结构体大小
                using (var dataStream = DataStream.Create(_matArray, false, false)) {
                    mfxMaterial.SetRawValue(dataStream, _matArray.Length);
                }
                //设置纹理
                mfxShaderRSVar.SetResource(ShaderRSV);
                mfxTexTransform.SetMatrix(Matrix.Identity);

                mfxPass.Apply(_d3DDeviceContext);
                _d3DDeviceContext.DrawIndexed(mSubMeshData[0].Indices.Length, indOff[0], vexOff[0]);

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
        #region Some Unimmportant Methods
        private void _renderForm_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e) {
            var c = sender as Control;
            if (c != null) {
                c.Capture = false;
            }
        }
        private void _renderForm_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e) {
            _lastMousePoint = e.Location;
            var c = sender as Control;
            if (c != null) {
                c.Capture = true;
            }
        }
        private void _renderForm_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                var dx = MathUtil.DegreesToRadians(0.1f * (e.X - _lastMousePoint.X));
                var dy = MathUtil.DegreesToRadians(0.1f * (e.Y - _lastMousePoint.Y));
                //pitch
                var r = Matrix.RotationAxis(camRight, dy);
                camUp = Vector3.TransformNormal(camUp, r);
                camLook = Vector3.TransformNormal(camLook, r);
                //yaw
                r = Matrix.RotationY(dx);
                camRight = Vector3.TransformNormal(camRight, r);
                camUp = Vector3.TransformNormal(camUp, r);
                camLook = Vector3.TransformNormal(camLook, r);
            }
            _lastMousePoint = e.Location;
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
                    camPos += camLook * 0.1f;
                    break;
                case System.Windows.Forms.Keys.S:
                    camPos -= camLook * 0.1f;
                    break;
                case System.Windows.Forms.Keys.A:
                    camPos -= camRight * 0.1f;
                    break;
                case System.Windows.Forms.Keys.D:
                    camPos += camRight * 0.1f;
                    break;
                case System.Windows.Forms.Keys.Space:
                    camPos.Y += 0.1f;
                    break;
                case System.Windows.Forms.Keys.Z:
                    camPos.Y -= 0.1f;
                    break;
                case System.Windows.Forms.Keys.Escape:
                    _renderForm.Close();
                    break;
                // 切换pass
                case System.Windows.Forms.Keys.F1:
                    mfxPass = mfxPassW;
                    break;
                case System.Windows.Forms.Keys.F2:
                    mfxPass = mfxPassS;
                    break;
                default:
                    break;
            }
        }
        #endregion
    }
    static class Tools {
        //纹理用
        private static SharpDX.WIC.ImagingFactory _factory;
        static Tools() {
            _factory = new SharpDX.WIC.ImagingFactory();
        }
        /// <summary>
        /// 计算三个点组成的平面的 单位法线向量
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="v3"></param>
        /// <returns></returns>
        public static Vector3 ComputeNormal(Vector3 v1, Vector3 v2, Vector3 v3) {
            Vector3 temp;
            Vector3 u = v2 - v1;
            Vector3 v = v3 - v1;
            temp = Vector3.Cross(u, v);
            temp = Vector3.Normalize(temp);
            return temp;
        }
        /// <summary>
        /// 计算一组三角形网格中每个顶点的Normal值
        /// 1.对每个三角形片元分析，根据*三个顶点*的坐标位置计算出该片元的法向矢量
        /// 2.将法向矢量赋值给三个顶点
        /// 3.对有片元进行处理完毕后，每个顶点的normal值是所有*与之相邻的片元法向矢量的和*
        /// 4.最后遍历每个顶点，对Norma值归一化即可
        /// </summary>
        /// <param name="inputVex"></param>
        /// <param name="inputInd"></param>
        public static void ComputeVertexNormal(ref MyVertex[] inputVex, int[] inputInd) {
            // 遍历每个三角形片元
            for (int i = 0; i < inputInd.Length / 3; i++) {
                // 三角形对应的三个顶点索引
                int i0 = inputInd[i * 3 + 0];
                int i1 = inputInd[i * 3 + 1];
                int i2 = inputInd[i * 3 + 2];
                // 三角形三个顶点
                MyVertex v0 = inputVex[i0];
                MyVertex v1 = inputVex[i1];
                MyVertex v2 = inputVex[i2];
                // 计算平面法向矢量
                Vector3 e0 = v1.Position - v0.Position;
                Vector3 e1 = v2.Position - v0.Position;
                Vector3 faceNormal = Vector3.Cross(e0, e1);
                // 因为这个三角形占用了这三个顶点
                // 因此这三个顶点的normal值应该添加该平面三角形的法线向量
                inputVex[i0].Normal += faceNormal;
                inputVex[i1].Normal += faceNormal;
                inputVex[i2].Normal += faceNormal;
            }
            // 遍历每个顶点，将其Normal值归一化
            for (int i = 0; i < inputVex.Length; i++) {
                inputVex[i].Normal = Vector3.Normalize(inputVex[i].Normal);
            }
        }
        /// <summary>
        /// 计算逆转置矩阵
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public static Matrix InverseTranspose(Matrix m) {
            return Matrix.Transpose(Matrix.Invert(m));
        }
        /// <summary>
        /// 将<T>结构体转化为字节码
        /// 用于SetRawValue
        /// </summary>
        /// <typeparam name="T">结构体类型</typeparam>
        /// <param name="obj">转化对象</param>
        /// <returns></returns>
        public static byte[] StructureToBytes<T>(T obj) where T : struct {
            var len = Marshal.SizeOf(obj);
            var buf = new byte[len];
            var ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, buf, 0, len);
            Marshal.FreeHGlobal(ptr);
            return buf;
        }
        #region 生成基本几何体
        /// <summary>
        /// 生成球体
        /// </summary>
        /// <param name="rad">球体半径</param>
        /// <param name="sliceNum">切片数</param>
        /// <param name="stackNum">断层数</param>
        /// <param name="sphereMesh">输出的MeshData网格结构</param>
        public static void GenerateSphere(int rad, int sliceNum, int stackNum, out MeshData sphereMesh) {
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
                Normal = new Vector3(0, -1, 0),
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
                    myVertex.Normal = new Vector3(0, 1f, 0);
                    tempVertex.Add(myVertex);
                }
                layerColor += 0.1f;
            }
            //[3]顶部
            tempVertex.Add(new MyVertex() {
                Position = new Vector3(0, rad, 0),
                Normal = new Vector3(0, 1, 0),
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
        public static void GenerateCylinder(float topRadius, float bottomRadius, float height, int sliceCount, int stackCount, out MeshData outMesh) {
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
                    float dr = bottomRadius - topRadius;
                    Vector3 bitangent = new Vector3(dr * c, -height, dr * s);
                    myVertex.Normal = Vector3.Normalize(Vector3.Cross(new Vector3(-s, 0f, c), bitangent));
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
                myVertex.Normal = new Vector3(0, 1, 0);

                tempVertex.Add(myVertex);
            }
            // 顶点中心数据
            var myVertexCenter = new MyVertex();
            myVertexCenter.Position = new Vector3(0, 0.5f * height, 0);
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
                myVertex.Normal = new Vector3(0, -1, 0);

                tempVertex.Add(myVertex);
            }
            // 顶点中心数据
            myVertexCenter = new MyVertex();
            myVertexCenter.Position = new Vector3(0, -0.5f * height, 0);
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
        /// 生成平面
        /// </summary>
        /// <param name="length">平面长度 X方向</param>
        /// <param name="width">平面宽度 Z方向</param>
        /// <param name="lenNum">X方向切片数</param>
        /// <param name="widNum">Z方向切片数</param>
        /// <param name="outMesh">输出数据</param>
        public static void GeneratePlane(float length, float width, int lenNum, int widNum, out MeshData outMesh) {
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
        /// <summary>
        /// 生成贴图正方体
        /// </summary>
        /// <param name="length"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="outMesh"></param>
        public static void GenerateBox(float length, float width, float height, out MeshData outMesh) {
            outMesh = new MeshData(24, 0);
            float lHalf = 0.5f * length;
            float hHalf = 0.5f * height;
            float wHalf = 0.5f * width;
            //FrontFace
            outMesh.Vertices[0] = new MyVertex(new Vector3(-lHalf, -hHalf, -wHalf), new Vector3(0, 0, -1), new Vector2(0f, 1f));
            outMesh.Vertices[1] = new MyVertex(new Vector3(-lHalf, +hHalf, -wHalf), new Vector3(0, 0, -1), new Vector2(0f, 0f));
            outMesh.Vertices[2] = new MyVertex(new Vector3(+lHalf, +hHalf, -wHalf), new Vector3(0, 0, -1), new Vector2(1f, 0f));
            outMesh.Vertices[3] = new MyVertex(new Vector3(+lHalf, -hHalf, -wHalf), new Vector3(0, 0, -1), new Vector2(1f, 1f));

            //BackFace
            outMesh.Vertices[4] = new MyVertex(new Vector3(-lHalf, -hHalf, +wHalf), new Vector3(0, 0, 1), new Vector2(0f, 1f));
            outMesh.Vertices[5] = new MyVertex(new Vector3(-lHalf, +hHalf, +wHalf), new Vector3(0, 0, 1), new Vector2(0f, 0f));
            outMesh.Vertices[6] = new MyVertex(new Vector3(+lHalf, +hHalf, +wHalf), new Vector3(0, 0, 1), new Vector2(1f, 0f));
            outMesh.Vertices[7] = new MyVertex(new Vector3(+lHalf, -hHalf, +wHalf), new Vector3(0, 0, 1), new Vector2(1f, 1f));

            //UpFace
            outMesh.Vertices[8]     = new MyVertex(new Vector3(-lHalf, +hHalf, -wHalf), new Vector3(0, 1, 0), new Vector2(0f, 0f));
            outMesh.Vertices[9]     = new MyVertex(new Vector3(-lHalf, +hHalf, +wHalf), new Vector3(0, 1, 0), new Vector2(1f, 0f));
            outMesh.Vertices[10]    = new MyVertex(new Vector3(+lHalf, +hHalf, +wHalf), new Vector3(0, 1, 0), new Vector2(1f, 1f));
            outMesh.Vertices[11]    = new MyVertex(new Vector3(+lHalf, +hHalf, -wHalf), new Vector3(0, 1, 0), new Vector2(0f, 1f));

            //downFace
            outMesh.Vertices[12]    = new MyVertex(new Vector3(-lHalf, -hHalf, +wHalf), new Vector3(0, -1, 0), new Vector2(0f, 1f));
            outMesh.Vertices[13]    = new MyVertex(new Vector3(-lHalf, -hHalf, -wHalf), new Vector3(0, -1, 0), new Vector2(0f, 0f));
            outMesh.Vertices[14]    = new MyVertex(new Vector3(+lHalf, -hHalf, -wHalf), new Vector3(0, -1, 0), new Vector2(1f, 0f));
            outMesh.Vertices[15]    = new MyVertex(new Vector3(+lHalf, -hHalf, +wHalf), new Vector3(0, -1, 0), new Vector2(1f, 1f));

            //rightFace
            outMesh.Vertices[16] = new MyVertex(new Vector3(+lHalf, -hHalf, -wHalf), new Vector3(1, 0, 0), new Vector2(0f, 1f));
            outMesh.Vertices[17] = new MyVertex(new Vector3(+lHalf, +hHalf, -wHalf), new Vector3(1, 0, 0), new Vector2(0f, 0f));
            outMesh.Vertices[18] = new MyVertex(new Vector3(+lHalf, +hHalf, +wHalf), new Vector3(1, 0, 0), new Vector2(1f, 0f));
            outMesh.Vertices[19] = new MyVertex(new Vector3(+lHalf, -hHalf, +wHalf), new Vector3(1, 0, 0), new Vector2(1f, 1f));

            //leftFace
            outMesh.Vertices[20] = new MyVertex(new Vector3(-lHalf, -hHalf, +wHalf), new Vector3(-1, 0, 0), new Vector2(0f, 1f));
            outMesh.Vertices[21] = new MyVertex(new Vector3(-lHalf, +hHalf, +wHalf), new Vector3(-1, 0, 0), new Vector2(0f, 0f));
            outMesh.Vertices[22] = new MyVertex(new Vector3(-lHalf, +hHalf, -wHalf), new Vector3(-1, 0, 0), new Vector2(1f, 0f));
            outMesh.Vertices[23] = new MyVertex(new Vector3(-lHalf, -hHalf, -wHalf), new Vector3(-1, 0, 0), new Vector2(1f, 1f));

            outMesh.Indices = new int[] {
                0, 1, 2, 2, 3, 0,       // front
                4, 5, 6, 6, 7, 4,       // back
                8, 9, 10, 10, 11, 8,    // up
                12,13,14,14,15,12,      // down
                16,17,18,18,19,16,      // right
                20,21,22,22,23,20,      // left
            };
        }
        #endregion
        /// <summary>
        /// 将输入的结构打包成一个，显示出来
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        public static void PackMeshDataInOne(MeshData[] source, out MeshData dest,
        out int[] vexNum, out int[] vexOffset,
        out int[] indNum, out int[] indOffset) {
            vexNum = new int[source.Length];
            indNum = new int[source.Length];
            vexOffset = new int[source.Length];
            indOffset = new int[source.Length];
            vexOffset[0] = 0;
            indOffset[0] = 0;
            for (int i = 0; i < source.Length; i++) {
                vexNum[i] = source[i].Vertices.Length;
                indNum[i] = source[i].Indices.Length;
                if (i + 1 >= vexOffset.Length) {
                    break;
                }
                vexOffset[i + 1] += vexOffset[i] + vexNum[i];
                indOffset[i + 1] += indOffset[i] + indNum[i];
            }
            var totalVexNum = vexNum.Sum();
            var totalIndNum = indNum.Sum();
            var vertexsInOne = new List<MyVertex>(totalVexNum);
            var indicesInOne = new List<int>(totalIndNum);
            foreach (var subModel in source) {
                for (int i = 0; i < subModel.Vertices.Length; i++) {
                    vertexsInOne.Add(subModel.Vertices[i]);
                }
                for (int i = 0; i < subModel.Indices.Length; i++) {
                    indicesInOne.Add(subModel.Indices[i]);
                }
            }
            dest = new MeshData() { Vertices = vertexsInOne.ToArray(), Indices = indicesInOne.ToArray() };
        }
        /// <summary>
        /// 参考自:https://github.com/sharpdx/SharpDX-Samples/blob/master/StoreApp/OldSamplesToBeBackPorted/CommonDX/TextureLoader.cs 和noire项目
        /// </summary>
        /// <param name="device">设备对象</param>
        /// <param name="bitmapSource"></param>
        /// <returns></returns>
        public static SharpDX.Direct3D11.ShaderResourceView CreateShaderResourceViewFromFile(SharpDX.Direct3D11.Device device, string fileName) {
            Texture2D tempTex;
            using (var bitmapSource = LoadBitmapSourceFromFile(_factory, fileName)) {
                // Allocate DataStream to receive the WIC image pixels
                int stride = bitmapSource.Size.Width * 4;
                using (var buffer = new SharpDX.DataStream(bitmapSource.Size.Height * stride, true, true)) {
                    // Copy the content of the WIC to the buffer
                    bitmapSource.CopyPixels(stride, buffer);
                    tempTex = new SharpDX.Direct3D11.Texture2D(
                        device,
                        new SharpDX.Direct3D11.Texture2DDescription() {
                            Width = bitmapSource.Size.Width,
                            Height = bitmapSource.Size.Height,
                            ArraySize = 1,
                            BindFlags = SharpDX.Direct3D11.BindFlags.ShaderResource,
                            Usage = SharpDX.Direct3D11.ResourceUsage.Immutable,
                            CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.None,
                            Format = SharpDX.DXGI.Format.R8G8B8A8_UNorm,
                            MipLevels = 1,
                            OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None,
                            SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                        },
                        new SharpDX.DataRectangle(buffer.DataPointer, stride));
                    bitmapSource.Dispose();
                }
            }
            return new ShaderResourceView(device, tempTex);
        }
        public static BitmapSource LoadBitmapSourceFromFile(ImagingFactory factory, string filename) {
            using (var bitmapDecoder = new BitmapDecoder(factory, filename, DecodeOptions.CacheOnDemand)) {
                var result = new FormatConverter(factory);
                using (var bitmapFrameDecode = bitmapDecoder.GetFrame(0)) {
                    result.Initialize(bitmapFrameDecode, PixelFormat.Format32bppPRGBA, BitmapDitherType.None, null, 0, BitmapPaletteType.Custom);
                }
                return result;
            }
        }
        public static void Dispose() {
            Utilities.Dispose(ref _factory);
        }
    }
}
