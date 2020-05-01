using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deleteSharpDX {
    class ModelReader {
        MeshData outMeshData;
        public MeshData meshData { get { return outMeshData; } }
        int vertexCount;
        int indexCount;
        public ModelReader(string filePath) {
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(filePath);
            line = file.ReadLine();
            string[] strs;
            strs = line.Split(' ');
            vertexCount = int.Parse(strs[1]);
            List<MyVertex> tempVertex = new List<MyVertex>(vertexCount);
            line = file.ReadLine();
            strs = line.Split(' ');
            indexCount = int.Parse(strs[1]);
            List<int> tempIndex = new List<int>(indexCount);
            line = file.ReadLine();
            line = file.ReadLine();
            while ((line = file.ReadLine()) != null) {
                strs = line.Split(' ');
                tempVertex.Add(new MyVertex(
                    new SharpDX.Vector3(float.Parse(strs[0]), float.Parse(strs[1]), float.Parse(strs[2])),
                    new SharpDX.Vector3(float.Parse(strs[3]), float.Parse(strs[4]), float.Parse(strs[5])),
                    new SharpDX.Vector2(0,0)
                    ));
                if (tempVertex.Count == vertexCount) {
                    break;
                }
            }
            line = file.ReadLine();
            line = file.ReadLine();
            line = file.ReadLine();
            while ((line = file.ReadLine()) != null) {
                strs = line.Split(' ');
                tempIndex.Add(int.Parse(strs[0]));
                tempIndex.Add(int.Parse(strs[1]));
                tempIndex.Add(int.Parse(strs[2]));
                if (tempIndex.Count == indexCount * 3) {
                    break;
                }
            }
            file.Close();
            this.outMeshData.Vertices = tempVertex.ToArray();
            this.outMeshData.Indices = tempIndex.ToArray();
        }
        public static MeshData ReadObj(string filePath) {
            //按顺序存放所有 顶点坐标
            List<Vector3> tempVertexVector = new List<Vector3>();
            //按顺序存放所有 顶点法线
            List<Vector3> tempNormalVector = new List<Vector3>();
            //字典类型，key是顶点 位置序号 ，value是顶点对应的 法向量序号
            var mapVerNum2NorNum = new Dictionary<int, int>();
            //按顺序存放所有 法线索引
            List<int> vertexIndice = new List<int>();
            //MeshData顶点索引
            string line;
            string[] strs;
            string[] temp;
            System.IO.StreamReader file = new System.IO.StreamReader(filePath);
            while ((line = file.ReadLine()) != null) {
                strs = line.Split(' ');
                //按顺序添加顶点位置->按顺序添加顶点法向量->按顺序添加三角形片面，并完成位置<->法向量映射表
                if (strs[0] == "v") {
                    tempVertexVector.Add(new Vector3(float.Parse(strs[1]), float.Parse(strs[2]), float.Parse(strs[3])));
                } else if (strs[0] == "vn") {
                    tempNormalVector.Add(new Vector3(float.Parse(strs[1]), float.Parse(strs[2]), float.Parse(strs[3])));
                } else if (strs[0] == "f") {
                    //处理一个3顶点片面
                    if (strs.Length == 4) {
                        temp = strs[1].Split('/');
                        mapVerNum2NorNum[int.Parse(temp[0])] = int.Parse(temp[2]);
                        vertexIndice.Add(int.Parse(temp[0]));
                        temp = strs[2].Split('/');
                        mapVerNum2NorNum[int.Parse(temp[0])] = int.Parse(temp[2]);
                        vertexIndice.Add(int.Parse(temp[0]));
                        temp = strs[3].Split('/');
                        mapVerNum2NorNum[int.Parse(temp[0])] = int.Parse(temp[2]);
                        vertexIndice.Add(int.Parse(temp[0]));
                    } else {
                        //处理一个四顶点片面
                        temp = strs[1].Split('/');
                        mapVerNum2NorNum[int.Parse(temp[0])] = int.Parse(temp[2]);
                        vertexIndice.Add(int.Parse(temp[0]));
                        temp = strs[2].Split('/');
                        mapVerNum2NorNum[int.Parse(temp[0])] = int.Parse(temp[2]);
                        vertexIndice.Add(int.Parse(temp[0]));
                        temp = strs[3].Split('/');
                        mapVerNum2NorNum[int.Parse(temp[0])] = int.Parse(temp[2]);
                        vertexIndice.Add(int.Parse(temp[0]));
                        temp = strs[4].Split('/');
                        mapVerNum2NorNum[int.Parse(temp[0])] = int.Parse(temp[2]);
                        vertexIndice.Add(vertexIndice[vertexIndice.Count-1]);
                        vertexIndice.Add(int.Parse(temp[0]));
                        vertexIndice.Add(vertexIndice[vertexIndice.Count-5]);
                    }
                }
            }
            List<MyVertex> outMyVertex = new List<MyVertex>(tempVertexVector.Count);
            foreach (int key in mapVerNum2NorNum.Keys) {
                outMyVertex.Add(new MyVertex() {
                    Position = tempVertexVector[key-1],
                    Normal = tempNormalVector[mapVerNum2NorNum[key]-1],
                });
            }
            MeshData meshData = new MeshData(outMyVertex.Count, vertexIndice.Count);
            meshData.Vertices = outMyVertex.ToArray();
            meshData.Indices = vertexIndice.ToArray();
            return meshData;
        }
    }
}
