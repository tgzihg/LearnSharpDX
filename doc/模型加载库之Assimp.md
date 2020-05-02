# Assimp

[原文连接](https://learnopengl-cn.github.io/03%20Model%20Loading/01%20Assimp/#assimp)

## 简介

3D建模工具如Blender, 3DS Max, Maya可以让艺术家创建复杂的形状，并使用UV映射来应用贴图。建模工具会在导出到模型文件的时候自动生成所有的*顶点坐标*、*顶点法线*以及*纹理坐标*。

模型的文件格式有很多种，每一种都会以它们自己的方式来导出模型数据。像是Wavefront的.obj这样的模型格式，只包含了**模型数据**以及**材质信息**，像是模型**颜色**和**漫反射/镜面光**贴图。而以XML为基础的Collada文件格式则非常的丰富，包含**模型、光照、多种材质、动画数据、摄像机、完整的场景信息**等等。

总而言之，不同种类的文件格式有很多，它们之间通常并没有一个通用的结构。所以如果我们想从这些文件格式中导入模型的话，我们必须要去自己对每一种需要导入的文件格式写一个导入器。很幸运的是，正好有一个库专门处理这个问题——Assimp(Open Asset Import Library)。

Assimp能够导入很多种不同的模型文件格式（并也能够导出部分的格式），它会将所有的模型数据加载至Assimp的通用数据结构中。当Assimp加载完模型之后，我们就能够从Assimp的数据结构中提取我们所需的所有数据了。由于Assimp的数据结构保持不变，不论导入的是什么种类的文件格式，它都能够将我们从这些不同的文件格式中抽象出来，用同一种方式访问我们需要的数据。

Assimp的数据结构：

![Assimp数据结构](assimp_structure.png "Assimp数据结构")

## 使用
应首先在nuget中安装AssimpNet.如下是一个简单的函数：
```csharp
public static MeshData[] LoadModelFromFile(string filename) {
    var importer = new AssimpContext();
    if (!importer.IsImportFormatSupported(System.IO.Path.GetExtension(filename))) {
        throw new ArgumentException($"Model format {System.IO.Path.GetExtension(filename)} is not supported. Cannot load {filename}.", nameof(filename));
    }
    var postProcessFlags = PostProcessSteps.GenerateSmoothNormals | PostProcessSteps.CalculateTangentSpace;
    var model = importer.ImportFile(filename, postProcessFlags);
    List<MeshData> meshDatas = new List<MeshData>(model.MeshCount);
    MeshData meshData = new MeshData();
    foreach (var mesh in model.Meshes) {
        List<MyVertex> myVertices = new List<MyVertex>(mesh.VertexCount);
        for (int i = 0; i < mesh.VertexCount; i++) {
            var pos = mesh.HasVertices ? mesh.Vertices[i].ToVector3() : new Vector3();

            var norm = mesh.HasNormals ? mesh.Normals[i] : new Vector3D();
            var texC = mesh.HasTextureCoords(0) ? mesh.TextureCoordinateChannels[0][i] : new Vector3D(1, 1, 0);
            var v = new MyVertex(pos, norm.ToVector3(), texC.ToVector2());
            myVertices.Add(v);
        }
        var indices = mesh.GetIndices().ToList();
        meshData.Vertices = myVertices.ToArray();
        meshData.Indices = indices.ToArray();
        meshDatas.Add(meshData);
    }
    return meshDatas.ToArray();
}
```
