using SharpDX.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX.DXGI;
using SharpDX.D3DCompiler;
using D3D11 = SharpDX.Direct3D11;
using SharpDX.Direct3D11;
using SharpDX;
using SharpDX.Mathematics.Interop;
using System.Diagnostics;

using System.Runtime.InteropServices;

namespace deleteSharpDX {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("---网格生成与显示工具---");
            Console.WriteLine("----------说明---------");
            Console.WriteLine("-输入参数：生成的二维网格数");
            Console.WriteLine("-位置移动：深度方向W、S键，横向A，D键，纵向Q，E键");
            Console.WriteLine("-视角移动：鼠标按下->拖拽鼠标->松开鼠标");
            Console.WriteLine("-显示方式：试着按下P(Point)、L(Line)、T(Triangle)切换吧！");
            Console.WriteLine("-如何关闭：默认是窗口显示，ESC键退出");
            Console.WriteLine("---------------------");
            Console.WriteLine("请先读完上述！");
            Console.WriteLine("---------------------");
            Console.WriteLine("生成的网格数之 X轴：");
            var xnum = Console.ReadLine();
            int XNUM;
            var result = int.TryParse(xnum, out XNUM);
            if (!result) {
                Console.WriteLine("输入错误");
                return;
            }
            Console.WriteLine("生成的网格数之 Y轴：");
            int ZNUM;
            var znum = Console.ReadLine();
            result = int.TryParse(znum, out ZNUM);
            if (!result) {
                Console.WriteLine("输入错误");
                return;
            }
            using (var temp = new MySharpDXForm(XNUM + 1, ZNUM + 1)) {
            }
            Console.WriteLine("感谢使用，任意键退出...");
            Console.ReadKey();
        }
    }
    class MySharpDXForm : IDisposable {
        private RenderForm _renderForm;
        private int viewX = 0;
        private int viewY = 0;
        private int viewWidth = 800;
        private int viewHeight = 600;
        private Vector3 target;
        private Matrix proj;
        private Matrix view;
        private Matrix world;
        private Vector3 targetViewDir;   //摄像机目标向量
        private bool _resized;
        private D3D11.Device _d3DDevice;
        private D3D11.DeviceContext _d3DDeviceContext;
        private SwapChain _swapChain;
        private InputLayout _inputLayout;
        private ShaderSignature _inputShaderSignature;
        private VertexShader _vertexShader;
        private PixelShader _pixelShader;
        RasterizerStateDescription rasterizerStateDescWire;
        RasterizerStateDescription rasterizerStateDescSolid;

        //摄像机相关参数

        Vector3 camPos = new Vector3(0.0f, 2.0f, -5.0f);
        Vector3 camTo = new Vector3(2f, 0f, 0f);
        Vector3 camUp = new Vector3(0f, 1f, 0f);

        MeshData meshData;
        /// <summary>
        /// 初始化
        /// </summary>
        public MySharpDXForm(int xNUM, int zNUM) {
            //GenerateFXY(5f, 5f, 10, 10);
            //GenerateMesh(10, 10);
            GenerateFXY(5f, 5f, xNUM, zNUM);
            GenerateMesh(xNUM, zNUM);
            _renderForm = new RenderForm();
            //窗体事件
            _renderForm.KeyDown += _renderForm_KeyDown;
            _renderForm.ClientSize = new System.Drawing.Size(_renderForm.ClientSize.Width, _renderForm.ClientSize.Height);
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
            var factory = _swapChain.GetParent<Factory>();
            factory.MakeWindowAssociation(_renderForm.Handle, WindowAssociationFlags.IgnoreAll);
            #region 栅格化显示线框/固体state的初始化
            rasterizerStateDescWire = new RasterizerStateDescription() {
                FillMode = FillMode.Wireframe,
                CullMode = CullMode.None,
            };
            rasterizerStateDescSolid = new RasterizerStateDescription() {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
            };
            #endregion
            _d3DDeviceContext.Rasterizer.State = new RasterizerState(_d3DDevice, rasterizerStateDescWire);
            using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile("../../MyShader.fx", "VS", "vs_4_0")) {
                _inputShaderSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                _vertexShader = new D3D11.VertexShader(_d3DDevice, vertexShaderByteCode);
            }
            using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("../../MyShader.fx", "PS", "ps_4_0")) {
                _pixelShader = new D3D11.PixelShader(_d3DDevice, pixelShaderByteCode);
            }
            _inputLayout = new D3D11.InputLayout(_d3DDevice, _inputShaderSignature, _inputElementsForVertex2);
            var FxyVertexBuffer = D3D11.Buffer.Create<deleteSharpDX.MyFuncVertex>(_d3DDevice, BindFlags.VertexBuffer, meshData.Vertices);
            var FxyConstBuffer = new D3D11.Buffer(_d3DDevice, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            var indexBufferDesc = new BufferDescription(Utilities.SizeOf<int>() * meshData.Indices.Length, ResourceUsage.Default, BindFlags.IndexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            var FxyIndexBuffer = D3D11.Buffer.Create<int>(_d3DDevice, meshData.Indices, indexBufferDesc);
            _d3DDeviceContext.InputAssembler.InputLayout = _inputLayout;
            _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            _d3DDeviceContext.InputAssembler.SetVertexBuffers(0,
                new VertexBufferBinding(FxyVertexBuffer, Utilities.SizeOf<MyFuncVertex>(), 0));
            _d3DDeviceContext.InputAssembler.SetIndexBuffer(FxyIndexBuffer, Format.R32_UInt, 0);
            _d3DDeviceContext.VertexShader.SetConstantBuffer(0, FxyConstBuffer);
            _d3DDeviceContext.VertexShader.Set(_vertexShader);
            _d3DDeviceContext.PixelShader.Set(_pixelShader);

            //投影透视矩阵在resize的时候需要更新
            proj = Matrix.Identity;
            view = Matrix.LookAtLH(camPos, camTo, camUp);
            world = Matrix.Identity;

            //targetViewDir = new Vector3(targetX, targetY, targetZ);

            _resized = true;
            Texture2D backBuffer = null;
            RenderTargetView renderView = null;
            Texture2D depthBuffer = null;
            DepthStencilView depthView = null;
            var clock = new Stopwatch();
            clock.Start();
            RenderLoop.Run(_renderForm, () => {
                targetViewDir = new Vector3(targetX, targetY, targetZ);
                //接收鼠标消息，改变摄像机位置
                //view = Matrix.LookAtLH(new Vector3(camera_x, camera_y, camera_z), new Vector3(1f, 1f, 0.1f), Vector3.UnitY);
                //view = Matrix.LookAtLH(camPos, camTo, camUp);

                //从view到world
                //view = Matrix.LookAtLH(camPos, camTo, camUp);
                view = Matrix.LookAtLH(camPos, camPos + targetViewDir, camUp);
                //view = Matrix.LookAtLH(camPos, camPos + targetViewDir, camUp);

                //view = new Matrix(
                //1, 0, 0, 0,
                //0, 1, 0, 0,
                //0, 0, 1, 0,
                //0, 1, clock.ElapsedTicks / 10000000f, 1);
                //求逆：从world到view，world space中的坐标点乘以该矩阵，得到view space中的坐标显示在屏幕上
                //view = Matrix.Invert(view);

                //初始化或者改变窗口大小
                if (_resized) {
                    //清空之前的资源
                    Utilities.Dispose(ref backBuffer);
                    Utilities.Dispose(ref renderView);
                    Utilities.Dispose(ref depthBuffer);
                    Utilities.Dispose(ref depthView);
                    _swapChain.ResizeBuffers(swapChainDesc.BufferCount, _renderForm.ClientSize.Width, _renderForm.ClientSize.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
                    //也可以这个样子从交换链中获得后台缓冲区：
                    //backBuffer = _swapChain.GetBackBuffer<Texture2D>(0);
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
                    // pi/4:视距角
                    proj = Matrix.PerspectiveFovLH((float)Math.PI / 4f, _renderForm.ClientSize.Width / (float)_renderForm.ClientSize.Height, 0.1f, 100f);
                    _resized = false;
                }
                _d3DDeviceContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                _d3DDeviceContext.ClearRenderTargetView(renderView, SharpDX.Color.Black);
                //_renderForm.Cursor.
                //var viewPort = new Viewport(viewX, viewY, viewWidth, viewHeight);
                var viewPort = new Viewport(0, 0, _renderForm.ClientSize.Width, _renderForm.ClientSize.Height);
                _d3DDeviceContext.Rasterizer.SetViewport(viewPort);
                var viewProj = Matrix.Multiply(view, proj);
                //var world = Matrix.RotationY(time);
                worldViewProj = world * viewProj;
                worldViewProj.Transpose();

                _d3DDeviceContext.UpdateSubresource(ref worldViewProj, FxyConstBuffer);
                _d3DDeviceContext.DrawIndexed(meshData.Indices.Length, 0, 0);
                //_d3DDeviceContext.Draw(meshData.Vertices.Length, 0);
                _swapChain.Present(0, PresentFlags.None);
            });
        }
        private void _renderForm_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e) {
            pitchFlag = false;
            preMouse = e.Location;
        }

        void Picth(float rad) {
            view = Matrix.LookAtLH(camPos, camTo, camUp);
            //camUp = Vector3.TransformNormal(camUp, R);
            //camTo = Vector3.TransformNormal(camTo, R);
        }

        private void _renderForm_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e) {
            pitchFlag = true;
            preMouse = e.Location;
        }

        private void _renderForm_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (pitchFlag) {
                dx = e.Location.X - preMouse.X;
                dy = e.Location.Y - preMouse.Y;
                targetX -= dx / 10000f;
                targetY += dy / 10000f;
            }
            
            //camTo = new Vector3((float)e.X / 200f, (float)e.Y / 150f, 0f);
            //SetCursorPos(300, 300);

            //dx = e.Location.X - preMouse.X;
            //dy = e.Location.Y - preMouse.Y;
            //tx += 0.1f * dx;
            //ty -= 0.1f * dy;
            //camTo = new Vector3(tx, +ty, 1f);
            //preMouse = e.Location;

            //view = Matrix.LookAtLH(new Vector3(camera_x, camera_y, camera_z), target, Vector3.UnitY);

            //SetCursorPos(100, 100);
            //worldViewProj

            //Vector2 mouse = new Vector2(
            //    ((float)e.X - (float)_renderForm.Width / 2) / ((float)_renderForm.Width / 2),
            //    ((float)e.Y - (float)_renderForm.Height / 2) / ((float)_renderForm.Height / 2));
            //Vector3 orig = new Vector3(mouse.X, mouse.Y, 0.0f);
            //Vector3 far = new Vector3(mouse.X, mouse.Y, 1.0f);
            //var mvpInvers = Matrix.Invert(worldViewProj);
            //Vector3 origin = Vector3.TransformCoordinate(orig, mvpInvers);
            //Vector3 posfar = Vector3.TransformCoordinate(far, mvpInvers);

            //Console.WriteLine($"Far: {posfar.X} {posfar.Y} {posfar.Z}");
            //view = Matrix.LookAtLH(new Vector3(camera_x, camera_y, camera_z), new Vector3(0, 0, 0), Vector3.UnitY);
        }

        public void Dispose() {
            _swapChain.Dispose();
            _d3DDevice.Dispose();
            _d3DDeviceContext.Dispose();
            _renderForm.Dispose();
        }
        #region Some Unimmportant Methods
        static Vector4 SysColorTofloat4(System.Drawing.Color color) {
            const float n = 255f;
            return new Vector4(color.R / n, color.G / n, color.B / n, color.A / n);
        }
        private void _renderForm_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) {
            switch (e.KeyCode) {
                //按键设置显示方式：S--Solid; W--Wireframe
                case System.Windows.Forms.Keys.Z:
                    _d3DDeviceContext.Rasterizer.State = new RasterizerState(_d3DDevice, rasterizerStateDescWire);
                    break;
                case System.Windows.Forms.Keys.X:
                    _d3DDeviceContext.Rasterizer.State = new RasterizerState(_d3DDevice, rasterizerStateDescSolid);
                    break;
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
                default:
                    break;
            }
        }
        #endregion
        #region 立方体：自定义顶点:结构，数据，索引，输入布局
        public struct MyVertex {
            Vector3 VertexPos;
            Vector4 VertexColor;
            public MyVertex(Vector3 ver, Vector4 col) {
                this.VertexPos = ver;
                this.VertexColor = col;
            }
        }
        public MyVertex[] _myVertex = new[] {
            new MyVertex(new Vector3(-0.5f,0.5f,1), SysColorTofloat4(System.Drawing.Color.Red)),
            new MyVertex(new Vector3(0.5f,0.5f,1), SysColorTofloat4(System.Drawing.Color.Green)),
            new MyVertex(new Vector3(0.5f,-0.5f,1), SysColorTofloat4(System.Drawing.Color.Blue)),
            new MyVertex(new Vector3(-0.5f,-0.5f,1), SysColorTofloat4(System.Drawing.Color.Red)),
            new MyVertex(new Vector3(-0.3f,0.3f,0.5f), SysColorTofloat4(System.Drawing.Color.Green)),
            new MyVertex(new Vector3(0.3f,0.3f,0.5f), SysColorTofloat4(System.Drawing.Color.Blue)),
            new MyVertex(new Vector3(0.3f,-0.3f,0.5f), SysColorTofloat4(System.Drawing.Color.Red)),
            new MyVertex(new Vector3(-0.3f,-0.3f,0.5f), SysColorTofloat4(System.Drawing.Color.Green)),
            };
        int[] _indices = new int[]{
                0,1,2,3,4,5,6,7
            };
        private D3D11.InputElement[] _inputElementsForVertex2 = new D3D11.InputElement[] {
                new D3D11.InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0),
                new D3D11.InputElement("NORMAL", 0, Format.R32G32B32_Float, 0),
                new D3D11.InputElement("TANGENTU", 0, Format.R32G32B32_Float, 0),
                new D3D11.InputElement("TEXC", 0, Format.R32G32_Float, 0),
            };
        private Matrix worldViewProj;
        private System.Drawing.Point preMouse;
        private int dx;
        private int dy;
        private float tx;
        private float ty;
        private float targetX = 0f;
        private float targetY = 0f;
        private float targetZ = 5f;
        private bool pitchFlag;
        #endregion
        #region 三维函数图
        int[] fxyIndices = new int[] {};
        private int xNUM;
        private int zNUM;

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
            meshData = new MeshData(xNum, zNum);
            var dx = xlength / xNum;
            var dz = zlength / zNum;
            // for texturing 纹理坐标
            var du = 1f / xNum;
            var dv = 1f / zNum;
            for (int xIndex = 0; xIndex < xNum; xIndex++) {
                for (int zIndex = 0; zIndex < zNum; zIndex++) {
                    // 位置坐标
                    meshData.Vertices[xIndex * zNum + zIndex].Position  = new Vector3(xIndex, 0f, zIndex);
                    // 光照
                    meshData.Vertices[xIndex * zNum + zIndex].Normal    = new Vector3(0f, 1f, 0f);
                    meshData.Vertices[xIndex * zNum + zIndex].TangentU  = new Vector3(1f, 0f, 0f);
                    // 纹理坐标                
                    meshData.Vertices[xIndex * zNum + zIndex].TexC.X    = xIndex * du;
                    meshData.Vertices[xIndex * zNum + zIndex].TexC.Y    = zIndex * dv;
                }
            }
        }
        #endregion

        [DllImport("user32.dll")]
        private static extern int SetCursorPos(int x, int y);
    }
    struct MyFuncVertex {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 TangentU;
        public Vector2 TexC;
        public MyFuncVertex(Vector3 pos, Vector3 nor, Vector3 tan, Vector2 tex) {
            Position = pos;
            Normal = nor;
            TangentU = tan;
            TexC = tex;
        }
    }
    struct MeshData {
        public MyFuncVertex[] Vertices;
        public int[] Indices;
        public MeshData(int xNum, int zNum) {
            Vertices = new MyFuncVertex[xNum * zNum];
            Indices = new int[(xNum-1) * (zNum-1) * 6];
        }
    }
}
