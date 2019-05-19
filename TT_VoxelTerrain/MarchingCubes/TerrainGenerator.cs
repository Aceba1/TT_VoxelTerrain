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

    /// <summary>
    /// Size of each voxel
    /// </summary>
    public const float voxelSize = 4.0f;

    /// <summary>
    /// Size of tile
    /// </summary>
    public float _size => _terrainData.size.x;

    /// <summary>
    /// Number of chunks to break the terrain in to
    /// </summary>
    const int subCount = 8;

    /// <summary>
    /// Size of a chunk in world units
    /// </summary>
    public static int ChunkSize;
    bool Dirty;

    Material sharedMaterial;

    void Start()
    {
        Dirty = true;
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

        sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(.7f, .4f, .1f) };
        //_terrain.height
        ChunkSize = Mathf.RoundToInt(_size) / subCount;

        for (int z = 0; z < subCount; z++)
            for (int y = 0; y < subCount; y++)
                for (int x = 0; x < subCount; x++)
                {
                    var offset = new Vector3(x, y, z) * ChunkSize;
                    _tile = GenerateChunk();
                    _tile.transform.parent = transform;
                    _tile.transform.position = offset + transform.position;
                }
        gameObject.GetComponent<TerrainCollider>().enabled = false;
        //_terrain.enabled = false;
    }

    GameObject GenerateChunk()
    {
        var go = new GameObject("TerrainChunk");
        go.layer = Globals.inst.layerTerrain;
        go.AddComponent<ChunkBounds>();

        var mf = go.AddComponent<MeshFilter>();

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = sharedMaterial;

        var mc = go.AddComponent<MeshCollider>();
        mc.convex = false;
        mc.sharedMaterial = new PhysicMaterial();

        var vox = go.AddComponent<VoxTerrain>();
        vox.mc = mc;
        vox.mf = mf;
        vox.Dirty = true;

        return go;
    }

    internal class VoxTerrain : MonoBehaviour
    {
        public static float Sample(Vector3 pos)
        {
            //Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float Height);
            return /*Height + */25 - pos.y;
        }
        public float[,,] Buffer = null;
        public bool Dirty = false, DoneBaking = false, Processing = false;
        public MeshFilter mf;
        public MeshCollider mc;
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        MarchingCubes mcubes = new MarchingCubes() {interpolate = true, sampleProc = Sample };
        //List<Vector3> normalcache;

        void Update()
        {
            if (DoneBaking && Dirty)
            {
                DoneBaking = false;
                Processing = false;
                Dirty = false;
                Mesh mesh = new Mesh();
                mesh.vertices = mcubes.GetVertices();
                mesh.triangles = mcubes.GetIndices();
                mesh.uv = new Vector2[mesh.vertices.Length];
                mesh.RecalculateBounds();
                //mesh.SetNormals(normalcache);
                //normalcache.Clear();

                mc.sharedMesh = mesh;
                mf.sharedMesh = mesh;

                mcubes.Reset();

                stopwatch.Stop();
                Debug.LogFormat("Generation took {0} seconds", stopwatch.Elapsed.TotalSeconds);
            }

            if (Dirty && !Processing)
            {
                stopwatch.Restart();
                DoneBaking = false;
                Processing = true;
                new Task(delegate
                {
                    Buffer = mcubes.MarchChunk(transform.position, Mathf.RoundToInt(ChunkSize/voxelSize), voxelSize, Buffer);
                    //normalcache = NormalSolver.RecalculateNormals(mcubes.GetIndices(), mcubes.GetVertices(), 50);
                    DoneBaking = true;
                }).Start();
            }
        }

        public void ModifyBufferPoint(Vector3 WorldPos, float Radius, float Change)
        {
            Dirty = true;
            var LocalPos = (WorldPos - transform.position) / voxelSize;
            for (int z = Math.Max(0, Mathf.FloorToInt(LocalPos.z - Radius)); z < Mathf.CeilToInt(LocalPos.z - Radius); z++)
                for (int y = Math.Max(0, Mathf.FloorToInt(LocalPos.y - Radius)); y < Mathf.CeilToInt(LocalPos.y - Radius); y++)
                    for (int x = Math.Max(0, Mathf.FloorToInt(LocalPos.x - Radius)); x < Mathf.CeilToInt(LocalPos.x - Radius); x++)
                    {
                        var pre = Buffer[x, y, z];
                        var post = Mathf.Clamp01(pre + Change /*Mathf.Max(0, Radius-Vector3.Distance(new Vector3(x,y,z), LocalPos) * Change)*/);
                        Console.Write("(" + (post - pre) + ") ");
                        Buffer[x, y, z] = post;
                    }
        }
    }
}