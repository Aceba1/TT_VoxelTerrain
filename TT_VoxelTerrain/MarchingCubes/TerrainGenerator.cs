using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Threading.Tasks;

public class TerrainGenerator : MonoBehaviour
{
    public GameObject _tile;
    public Terrain _terrain;
    public TerrainData _terrainData;
    public static MarchingCubes _mcubes;
    const float voxelSize = 4.0f;
    public float _size;
    const int subCount = 4;
    public static int chunkSize;
    bool Dirty;

    Material sharedMaterial;

    public float fSample(IntVector3 position)
    {
        return (_terrainData.GetHeight(position.x, position.z) + _terrain.transform.position.y - position.y * voxelSize) / voxelSize;
    }

    void Start()
    {
        Dirty = true;
        if (_mcubes == null) _mcubes = new MarchingCubes();
        //_mcubes.sampleProc = fSample;
        _mcubes.interpolate = true;
    }

    void LateUpdate()
    {
        if (Dirty)
        {
            Dirty = false;
            GenerateTerrain();
        }
    }

    void GenerateTerrain()
    {
        _terrain = gameObject.GetComponent<Terrain>();
        _terrainData = _terrain.terrainData;
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(.7f, .4f, .1f) };

        chunkSize = Mathf.RoundToInt(_terrainData.size.x) / subCount;

        for (int z = 0; z < subCount; z++)
            for (int y = 0; y < subCount; y++)
                for (int x = 0; x < subCount; x++)
                {
                    _size = _terrainData.size.x;
                    var offset = new Vector3(x, y, z) * chunkSize + transform.position;
                    _tile = GenerateChunk(offset, chunkSize);
                    _tile.transform.parent = transform;
                    _tile.transform.position = offset;
                    _tile.AddComponent<ChunkBounds>();
                }
        sw.Stop();
        Debug.LogFormat("Generation took {0} seconds", sw.Elapsed.TotalSeconds);
    }

    GameObject GenerateChunk(Vector3 origin, int size)
    {
        _mcubes.Reset();

        var Buffer = _mcubes.MarchChunk(origin / voxelSize, size, voxelSize);
        Mesh mesh = new Mesh();
        mesh.vertices = _mcubes.GetVertices();
        mesh.triangles = _mcubes.GetIndices();
        mesh.uv = new Vector2[mesh.vertices.Length];
        mesh.RecalculateBounds();
        //mesh.RecalculateNormals(90);

        var go = new GameObject("TerrainChunk");
        go.layer = Globals.inst.layerTerrain;
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        var mc = go.AddComponent<MeshCollider>();
        mc.convex = false;
        mc.sharedMesh = mesh;
        mc.sharedMaterial = new PhysicMaterial();
        mf.sharedMesh = mesh;
        mr.sharedMaterial = sharedMaterial;
        var vox = go.AddComponent<VoxTerrain>();
        vox.mr = mr;
        vox.mc = mc;
        vox.mf = mf;
        vox.Buffer = Buffer;
        return go;
    }

    internal class VoxTerrain : MonoBehaviour
    {
        public float[,,] Buffer;
        bool Dirty = false;
        public MeshRenderer mr;
        public MeshFilter mf;
        public MeshCollider mc;

        void Update()
        {
            if (Dirty)
            {
                Dirty = false;

                var Buffer = _mcubes.MarchChunk(transform.position / voxelSize, chunkSize, voxelSize);
                Mesh mesh = new Mesh();
                mesh.vertices = _mcubes.GetVertices();
                mesh.triangles = _mcubes.GetIndices();
                mesh.uv = new Vector2[mesh.vertices.Length];
                mesh.RecalculateBounds();
                //mesh.RecalculateNormals(90);
                
                mc.sharedMesh = mesh;
                mf.sharedMesh = mesh;
            }
        }

        public float ModifyBufferPoint(Vector3 WorldPos, float Change)
        {
            var LocalPos = (WorldPos - transform.position) / voxelSize;
            int x = Mathf.RoundToInt(LocalPos.x), y = Mathf.RoundToInt(LocalPos.y), z = Mathf.RoundToInt(LocalPos.z);
            var pre = Buffer[x, y, z];
            var post = Mathf.Clamp01(pre + Change);
            Buffer[x, y, z] = post;
            Dirty |= pre != post;
            return pre + Change - post;
        }
    }
}