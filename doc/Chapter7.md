# 第七章 光照
## 7.1 光和材质作用
材质经过光照，用方程计算出结果，赋给颜色。
### 7.2.1 计算平面法线向量
取平面上不在同一直线上的三点，根据三点计算出两个向量，向量叉乘获得法线向量。
```csharp
public Vector3 ComputeNormal(Vector3 v1, Vector3 v2, Vector3 v3) {
    Vector3 temp;
    Vector3 u = v2 - v1;
    Vector3 v = v3 - v1;
    temp = Vector3.Cross(u, v);
    temp = Vector3.Normalize(temp);
    return temp;
}
```
平面上任一顶点的法线向量是由共享这个点的面法线向量计算得到的。这个平均的过程可以根据面积及逆行加权。

实现思路：
1. 对每个三角形片元分析，根据*三个顶点*的坐标位置计算出该片元的法向矢量
2. 将法向矢量赋值给三个顶点
3. 对有片元进行处理完毕后，每个顶点的Normal值是所有*与之相邻的片元法向矢量的和*
4. 最后遍历每个顶点，对Norma值归一化即可