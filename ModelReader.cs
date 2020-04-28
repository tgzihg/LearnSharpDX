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
                    new SharpDX.Vector3(float.Parse(strs[3]), float.Parse(strs[4]), float.Parse(strs[5]))
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
    }
}
