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
        public MyVertex(Vector3 pos, Vector3 nor) {
            Position = pos;
            Normal = nor;
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
        };

        private Matrix proj;
        private Matrix view;
        private Matrix world;
        private Matrix worldViewProj;

        private Vector3 targetViewDir;
        Vector3 camPos = new Vector3(0f, 3f, -13.0f);
        Vector3 camTo = new Vector3(0f, 0f, 0f);
        Vector3 camUp = new Vector3(0f, 1f, 0f);
        private int dx;
        private int dy;
        private float targetX = 0f;
        private float targetY = 0f;
        private float targetZ = 5f;
        private System.Drawing.Point preMouse;
        private bool _resized;
        private bool rotateFlag;
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
        private RenderForm _renderForm;

        private MeshData skullMeshData;

        DirectionalLight mDirLight;
        PointLight mPointLight;
        SpotLight mSpotLight;
        Material mSkullMat;
        //光照
        private byte[] _dirLightArray   = new byte[Marshal.SizeOf(typeof(DirectionalLight)) * 1];
        private byte[] _pointLightArray = new byte[Marshal.SizeOf(typeof(PointLight)) * 1];
        private byte[] _spotLightArray  = new byte[Marshal.SizeOf(typeof(SpotLight)) * 1];
        //材料
        private byte[] _matArray = new byte[Marshal.SizeOf(typeof(Material)) * 1];

        /// <summary>
        /// 初始化
        /// </summary>
        public MySharpDXForm() {
            //读取模型
            ModelReader modelReader = new ModelReader("../../model/skull.txt");
            skullMeshData = modelReader.meshData;
            //初始化光源
            mDirLight = new DirectionalLight();
            mDirLight.Ambient   = new Vector4(0.2f, 0.2f, 0.2f, 1.0f);
            mDirLight.Diffuse   = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            mDirLight.Specular  = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            mDirLight.Direction = new Vector3(0.57735f, -0.57735f, 0.57735f);

            mPointLight = new PointLight();
            mPointLight.Ambient = new Vector4(0.3f, 0.3f, 0.3f, 1.0f);
            mPointLight.Diffuse = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);
            mPointLight.Specular= new Vector4(0.7f, 0.7f, 0.7f, 1.0f);
            mPointLight.Att = new Vector3(0.0f, 0.1f, 0.0f);
            mPointLight.Range = 25.0f;
            mPointLight.Position = new Vector3(0.0f, 10f, 0.0f);

            mSpotLight = new SpotLight();
            mSpotLight.Ambient = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
            mSpotLight.Diffuse = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
            mSpotLight.Specular= new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            mSpotLight.Att = new Vector3(1.0f, 0.0f, 0.0f);
            mSpotLight.Spot = 96.0f;
            mSpotLight.Range = 10000.0f;
            mSpotLight.Position = camPos;
            mSpotLight.Direction = Vector3.Normalize(camTo-camPos);

            //材质
            mSkullMat = new Material();
            mSkullMat.Ambient = new Vector4(0.48f, 0.77f, 0.46f, 1.0f);
            mSkullMat.Diffuse = new Vector4(0.48f, 0.77f, 0.46f, 1.0f);
            mSkullMat.Specular = new Vector4(0.2f, 0.2f, 0.2f, 16.0f);

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
                mfxDirLight     = effect.GetVariableByName("gDirLight");
                mfxPointLight   = effect.GetVariableByName("gPointLight");
                mfxSpotLight    = effect.GetVariableByName("gSpotLight");
                
                mfxEyePosW      = effect.GetVariableByName("gEyePosW");

                //材质
                mfxMaterial = effect.GetVariableByName("gMaterial");

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
            var VertexBuffer = D3D11.Buffer.Create<MyVertex>(_d3DDevice, BindFlags.VertexBuffer, skullMeshData.Vertices.ToArray());
            var IndexBuffer = D3D11.Buffer.Create<int>(_d3DDevice, BindFlags.IndexBuffer, skullMeshData.Indices.ToArray());
            _d3DDeviceContext.InputAssembler.InputLayout = _inputLayout;
            _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            _d3DDeviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(VertexBuffer, Utilities.SizeOf<MyVertex>(), 0));
            _d3DDeviceContext.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R32_UInt, 0);
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
                    proj = Matrix.PerspectiveFovLH((float)Math.PI / 4f, _renderForm.ClientSize.Width / (float)_renderForm.ClientSize.Height, 0.1f, 1000f);
                    _resized = false;
                }
                _d3DDeviceContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                _d3DDeviceContext.ClearRenderTargetView(renderView, SharpDX.Color.Blue);
                var viewProj = Matrix.Multiply(view, proj);
                world = Matrix.RotationAxis(Vector3.UnitY, clock.ElapsedMilliseconds / 1000f);
                worldViewProj = world * viewProj;

                //像素着色器计算需要的变量
                mfxWorld.SetMatrix(world);
                mfxWorldTranInv.SetMatrix(Tools.InverseTranspose(world));
                mfxWorldViewProj.SetMatrix(worldViewProj);

                //设置光
                var d = Tools.StructureToBytes(mDirLight);
                Array.Copy(d, 0, _dirLightArray, 0, Marshal.SizeOf(typeof(DirectionalLight)));
                using (var dataStream = DataStream.Create(_dirLightArray, false, false)) {
                    mfxDirLight.SetRawValue(dataStream, _dirLightArray.Length);
                }
                d = Tools.StructureToBytes(mPointLight);
                Array.Copy(d, 0, _pointLightArray, 0, Marshal.SizeOf(typeof(PointLight)));
                using (var dataStream = DataStream.Create(_pointLightArray, false, false)) {
                    mfxPointLight.SetRawValue(dataStream, _pointLightArray.Length);
                }
                d = Tools.StructureToBytes(mSpotLight);
                Array.Copy(d, 0, _spotLightArray, 0, Marshal.SizeOf(typeof(SpotLight)));
                using (var dataStream = DataStream.Create(_spotLightArray, false, false)) {
                    mfxSpotLight.SetRawValue(dataStream, _spotLightArray.Length);
                }

                //设置材质
                d = Tools.StructureToBytes(mSkullMat);
                Array.Copy(d, 0, _matArray, 0, Marshal.SizeOf(typeof(Material))); // 结构体大小
                using (var dataStream = DataStream.Create(_matArray, false, false)) {
                    mfxMaterial.SetRawValue(dataStream, _matArray.Length);
                }

                mfxPass.Apply(_d3DDeviceContext);
                _d3DDeviceContext.DrawIndexed(skullMeshData.Indices.Length, 0, 0);
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
                // 切换pass
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
    static class Tools {
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
    }
}
