# 14章 第一人称视角照相机
## 14.1 回顾观察变换矩阵
坐标系转换矩阵的构成，以局部坐标系->全局坐标系为例。局部坐标系在全局坐标系中可以由以下四个构成：
- 坐标原点位置：点，四元数中w为1
- 坐标x轴正向向量位置：向量，w为0
- 坐标y轴正向向量位置
- 坐标z轴正向向量位置

构建矩阵，求其逆，即可作为观察变换矩阵，最后传入到常量缓冲区中。世界坐标通过计算即可转换到观察坐标系。

在此次commit中，手动输入观察变换矩阵的四个元素。尽管D3D提供了专门生成观察变换矩阵的`LookAt()`函数。

## 14.2 SharpDX中的实现
用四个变量定义相机三个朝向和位置。
```csharp
Vector3 camRight = new Vector3(1, 0, 0);
Vector3 camUp = new Vector3(0, 1, 0);
Vector3 camLook = new Vector3(0, 0, 1);
Vector3 camPos = new Vector3(0, 0, -5);
```
直接从这三个向量创建观察矩阵`view`:
```csharp
view.Row1 = new Vector4(camRight, 0);
view.Row2 = new Vector4(camUp, 0);
view.Row3 = new Vector4(camLook, 0);
view.Row4 = new Vector4(camPos, 1);
view = Matrix.Invert(view);
```

对观察矩阵的相关操作有：
- 鼠标拖拽：改变相机朝向(camRight,camUp,camLook)
  - 产生的横向偏移`dx`->`yaw`:改变`camRight`和`camLook`
  - 产生的纵向偏移`dy`->`pitch`：改变`camUp`和`camLook`
```csharp
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
```
- 键盘：WASD作用在摄像机位置`camPos`上，在其上加减前后分量`camLook`或者横向分量`camRight`。
```csharp
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
```