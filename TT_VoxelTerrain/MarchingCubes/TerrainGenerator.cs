using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Threading.Tasks;

public class TerrainGenerator : MonoBehaviour
{
    public static LayerMask TerrainOnlyLayer = LayerMask.GetMask(LayerMask.LayerToName(Globals.inst.layerTerrain));
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

    static Material sharedMaterial;

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

    void CreateChunkWithData(Vector3 offset)
    {
        var _tile = CheckAndGenerateChunk(offset + transform.position);
        if (_tile != null)
        {
            _tile.transform.parent = transform;
            _tile.transform.position = offset + transform.position;
        }
    }

    static Material mat = new Material(Shader.Find("Standard")) { color = new Color(.8f, .5f, .2f) };

void GenerateTerrain()
    {
        _terrain = gameObject.GetComponent<Terrain>();
        _terrainData = _terrain.terrainData;

        if (!sharedMaterial) sharedMaterial = mat;
        if (ChunkSize == 0) ChunkSize = Mathf.RoundToInt(_size) / subCount;

        var b = gameObject.GetComponent<TerrainCollider>().bounds;

        for (int z = 0; z < subCount; z++)
            for (int y = 0; y < 2; y++)//for (int y = Mathf.FloorToInt(b.min.y / ChunkSize); y < Mathf.CeilToInt(b.max.y / ChunkSize); y++) //Change to use buffer of tile, creating chunks where needed
                for (int x = 0; x < subCount; x++)
                {
                    var offset = new Vector3(x, y, z) * ChunkSize;
                    CreateChunkWithData(offset);
                }
        gameObject.GetComponent<TerrainCollider>().enabled = false;
        _terrain.enabled = false;
    }

    internal static VoxTerrain CheckAndGenerateChunk(Vector3 position)
    {
        var Tiles = Physics.OverlapSphere(position + Vector3.one * (ChunkSize / 2), ChunkSize / 4, TerrainOnlyLayer, QueryTriggerInteraction.Collide);
        foreach (var Tile in Tiles)
        {
            var vox = Tile.GetComponent<VoxTerrain>();
            if (vox) return null;//vox;
        }
        return GenerateChunk();
    }

    internal static VoxTerrain GenerateChunk()
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

        var bc = go.AddComponent<BoxCollider>();
        bc.size = Vector3.one * ChunkSize;
        bc.center = Vector3.one * (ChunkSize/2);
        bc.isTrigger = true;

        var vox = go.AddComponent<VoxTerrain>();
        vox.mc = mc;
        vox.mf = mf;
        vox.mr = mr;
        vox.Dirty = true;

        return vox;
    }

    internal class VoxTerrain : MonoBehaviour
    {
        // Right, Left, Up, Down, Forward, Backward
        public VoxTerrain voxLeft, voxRight, voxDown, voxUp, voxBack, voxFront;
        private static int _BleedWrap = -1;
        public static int BleedWrap
        {
            get
            {
                if (_BleedWrap == -1)
                {
                    _BleedWrap = Mathf.RoundToInt(ChunkSize / voxelSize);
                }
                return _BleedWrap;
            }
        }

        public static float Sample(Vector3 pos)
        {
            return Mathf.Clamp(-3-pos.y, -1, 1);
        }

        public float[,,] Buffer = null;
        public bool Dirty = false, DoneBaking = false, Processing = false;
        public MeshFilter mf;
        public MeshRenderer mr;
        public MeshCollider mc;
        //System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        MarchingCubes mcubes = new MarchingCubes() {interpolate = true, sampleProc = Sample };
        List<Vector3> normalcache;

        void Update()
        {
            if (DoneBaking)
            {
                DoneBaking = false;
                Processing = false;
                Mesh mesh = new Mesh();
                mesh.vertices = mcubes.GetVertices();
                mesh.triangles = mcubes.GetIndices();
                mesh.uv = new Vector2[mesh.vertices.Length];
                for (int i = 0; i < mesh.vertices.Length; i++)
//                    mesh.uv[i] = new Vector2(mesh.vertices[i].x, mesh.vertices[i].y);

                mesh.RecalculateBounds();

                mesh.SetNormals(normalcache);
                normalcache.Clear();

                mc.sharedMesh = mesh;
                mf.sharedMesh = mesh;

                mcubes.Reset();

                //stopwatch.Stop();
                //Debug.LogFormat("Generation took {0} seconds", stopwatch.Elapsed.TotalSeconds);
            }

            if (Dirty && !Processing)
            {
                //stopwatch.Restart();
                Dirty = false;
                DoneBaking = false;
                Processing = true;
                new Task(delegate
                {
                    Buffer = mcubes.MarchChunk(transform.position, Mathf.RoundToInt(ChunkSize/voxelSize), voxelSize, Buffer);

                    normalcache = NormalSolver.RecalculateNormals(mcubes.GetIndices(), mcubes.GetVertices(), 70);

                    DoneBaking = true;
                }).Start();
            }
        }

        public void PointModifyBuffer(int x, int y, int z, float Change)
        {
            Buffer[x, y, z] = Mathf.Clamp(Buffer[x, y, z] + Change, -1, 1);
            Dirty = true;
        }

        public void BleedBrushModifyBuffer(Vector3 WorldPos, float Radius, float Change)
        {
            var LocalPos = (WorldPos - transform.position) / voxelSize;
            for (int x = Mathf.FloorToInt((LocalPos.x - Radius) / BleedWrap); x < Mathf.CeilToInt((LocalPos.x + Radius) / BleedWrap); x++)
                for (int y = Mathf.FloorToInt((LocalPos.y - Radius) / BleedWrap); y < Mathf.CeilToInt((LocalPos.y + Radius) / BleedWrap); y++)
                    for (int z = Mathf.FloorToInt((LocalPos.z - Radius) / BleedWrap); z < Mathf.CeilToInt((LocalPos.z + Radius) / BleedWrap); z++)
                        FindFriend(new Vector3(x, y, z)).BrushModifyBuffer(WorldPos, Radius, Change);
        }

        public void BrushModifyBuffer(Vector3 WorldPos, float Radius, float Change)
        {
            var LocalPos = (WorldPos - transform.position) / voxelSize;
            for (int z = Mathf.Max(0,Mathf.FloorToInt(LocalPos.z - Radius)); z < Mathf.Min(Mathf.CeilToInt(LocalPos.z + Radius), BleedWrap + 1); z++)
                for (int y = Mathf.Max(0,Mathf.FloorToInt(LocalPos.y - Radius)); y < Mathf.Min(Mathf.CeilToInt(LocalPos.y + Radius), BleedWrap + 1); y++)
                    for (int x = Mathf.Max(0,Mathf.FloorToInt(LocalPos.x - Radius)); x < Mathf.Min(Mathf.CeilToInt(LocalPos.x + Radius), BleedWrap + 1); x++)
                        PointModifyBuffer(x, y, z, Mathf.Max(0, Radius - Vector3.Distance(new Vector3(x, y, z), LocalPos)) * Change);
        }

        public VoxTerrain FindFriend(Vector3 Direction)
        {
            var Tiles = Physics.OverlapSphere(transform.position + Direction * ChunkSize + Vector3.one * (ChunkSize / 2), ChunkSize / 4, TerrainOnlyLayer, QueryTriggerInteraction.Collide);
            foreach (var Tile in Tiles)
            {
                var vox = Tile.GetComponent<VoxTerrain>();
                if (vox) return vox;
            }
            return CreateFriend(Direction, Direction == Vector3.down ? 1f : -1f);
        }

        public VoxTerrain CreateFriend(IntVector3 Direction, float Fill)
        {
            VoxTerrain newVox = GenerateChunk();
            int Size = Mathf.RoundToInt(ChunkSize / voxelSize) + 1;
            newVox.transform.parent = transform.parent;
            newVox.transform.position = transform.position + Direction * ChunkSize;
            return newVox;
        }
    }
}