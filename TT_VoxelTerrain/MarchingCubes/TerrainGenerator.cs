﻿using System;
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
        go.AddComponent<ChunkBounds>();
        go.layer = Globals.inst.layerTerrain;
        go.tag = "_V";
        var mf = go.AddComponent<MeshFilter>();

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = sharedMaterial;

        var cgo = new GameObject("Collider");
        cgo.layer = Globals.inst.layerTerrain;
        cgo.transform.parent = go.transform;
        cgo.transform.localPosition = Vector3.zero;

        var mc = cgo.AddComponent<MeshCollider>();
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
        vox.voxFriendLookup.Add(Vector3.zero, vox);
        var d = go.AddComponent<Damageable>();
        d.destroyOnDeath = false;
        d.InitHealth(0);
        d.damageEvent.Subscribe(vox.DamageEvent);

        vox.d = d;

        var v = go.AddComponent<Visible>();
        v.m_ItemType = new ItemTypeInfo(ObjectTypes.Scenery, 1905);

        vox.Dirty = true;

        return vox;
    }

    internal class VoxTerrain : MonoBehaviour
    {
        public static MarchingCubes.ReadPair Sample(Vector2 pos)
        {
            return new MarchingCubes.ReadPair(-10 + Mathf.Sin((pos.x + ManWorld.inst.SceneToGameWorld.x)*0.01f)*20f + Mathf.Sin((pos.y + ManWorld.inst.SceneToGameWorld.z) * 0.004f) * 20f, 0x00, 0x01);
        }

        public void DamageEvent(ManDamage.DamageInfo damageInfo)
        {
            float Radius, Strength;
            switch (damageInfo.DamageType)
            {
                case ManDamage.DamageType.Cutting:
                    Radius = 1.75f;
                    Strength = damageInfo.Damage * 0.1f;
                    break;
                case ManDamage.DamageType.Explosive:
                    Radius = damageInfo.Damage / (10f * voxelSize);
                    Strength = damageInfo.Damage * 0.05f;
                    break;
                case ManDamage.DamageType.Impact:
                    Radius = 1.25f;
                    Strength = damageInfo.Damage * 0.5f;
                    break;
                default:
                    return;
            }
            BleedBrushModifyBuffer(damageInfo.HitPosition, Radius, Strength);
        }

        // Right, Left, Up, Down, Forward, Backward
        public Dictionary<IntVector3, VoxTerrain> voxFriendLookup = new Dictionary<IntVector3, VoxTerrain>();
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

        public MarchingCubes.CloudPair[,,] Buffer = null;
        public bool Dirty = false, DoneBaking = false, Processing = false;
        public MeshFilter mf;
        public MeshRenderer mr;
        public MeshCollider mc;
        public Damageable d;
        //System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        MarchingCubes mcubes = new MarchingCubes() {interpolate = true, sampleProc = Sample };
        List<Vector3> normalcache;
        List<BrushEffect> PendingBleedBrushEffects = new List<BrushEffect>();



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
                //for (int i = 0; i < mesh.vertices.Length; i++)
                    //mesh.uv[i] = new Vector2(mesh.vertices[i].x, mesh.vertices[i].y);

                mesh.RecalculateBounds();

                mesh.SetNormals(normalcache);
                normalcache.Clear();

                mc.sharedMesh = mesh;
                mf.sharedMesh = mesh;

                mcubes.Reset();

                //stopwatch.Stop();
                //Debug.LogFormat("Generation took {0} seconds", stopwatch.Elapsed.TotalSeconds);
            }

            if (!Processing)
            {
                if (PendingBleedBrushEffects.Count != 0)
                {
                    for (int i = 0; i < PendingBleedBrushEffects.Count; i++)
                        BrushModifyBuffer(PendingBleedBrushEffects[i]);
                    PendingBleedBrushEffects.Clear();
                }
                if (Dirty)
                {
                    //stopwatch.Restart();
                    Dirty = false;
                    DoneBaking = false;
                    Processing = true;
                    new Task(delegate
                    {
                        mcubes.MarchChunk(transform.position, Mathf.RoundToInt(ChunkSize / voxelSize), voxelSize, ref Buffer);

                        normalcache = NormalSolver.RecalculateNormals(mcubes.GetIndices(), mcubes.GetVertices(), 70);

                        DoneBaking = true;
                    }).Start();
                }
            }
        }

        public void PointModifyBuffer(int x, int y, int z, float Change)
        {
            var pre = Buffer[x, y, z];
            var post = pre.AddDensity(Change);
            Buffer[x, y, z] = post;
            Dirty |= pre.Density != post.Density;
        }

        private struct BrushEffect
        {
            public Vector3 WorldPos;
            public float Radius;
            public float Change;

            public BrushEffect(Vector3 WorldPos, float Radius, float Change)
            {
                this.WorldPos = WorldPos;
                this.Radius = Radius;
                this.Change = Change;
            }
        }

        public void BleedBrushModifyBuffer(Vector3 WorldPos, float Radius, float Change)
        {
            var LocalPos = (Brush.WorldPos - transform.position) / voxelSize;
            int xmax = Mathf.CeilToInt((LocalPos.x + Brush.Radius) / BleedWrap),
                ymax = Mathf.CeilToInt((LocalPos.y + Brush.Radius) / BleedWrap),
                zmax = Mathf.CeilToInt((LocalPos.z + Brush.Radius) / BleedWrap);
            for (int x = Mathf.FloorToInt((LocalPos.x - Brush.Radius) / BleedWrap); x < xmax; x++)
                for (int y = Mathf.FloorToInt((LocalPos.y - Brush.Radius) / BleedWrap); y < ymax; y++)
                    for (int z = Mathf.FloorToInt((LocalPos.z - Brush.Radius) / BleedWrap); z < zmax; z++)
                        FindFriend(new Vector3(x, y, z)).BBMB_internal(Brush.WorldPos, Brush.Radius, Brush.Change);
        }

        internal void BBMB_internal(Vector3 WorldPos, float Radius, float Change)
        {
            if (Processing)
                PendingBleedBrushEffects.Add(new BrushEffect(WorldPos, Radius, Change));
            else
                BrushModifyBuffer(WorldPos, Radius, Change);
        }

        private void BrushModifyBuffer(BrushEffect b)
        {
            BrushModifyBuffer(b.WorldPos, b.Radius, b.Change)
        }

        public void BrushModifyBuffer(Vector3 WorldPos, float Radius, float Change)
        {
            var LocalPos = (WorldPos - transform.position) / voxelSize;
            int zmax = Mathf.Min(Mathf.CeilToInt(LocalPos.z + Radius), BleedWrap + 1),
                ymax = Mathf.Min(Mathf.CeilToInt(LocalPos.y + Radius), BleedWrap + 1),
                xmax = Mathf.Min(Mathf.CeilToInt(LocalPos.x + Radius), BleedWrap + 1);
            for (int z = Mathf.Max(0,Mathf.FloorToInt(LocalPos.z - Radius)); z < zmax; z++)
                for (int y = Mathf.Max(0,Mathf.FloorToInt(LocalPos.y - Radius)); y < ymax; y++)
                    for (int x = Mathf.Max(0,Mathf.FloorToInt(LocalPos.x - Radius)); x < xmax; x++)
                        PointModifyBuffer(x, y, z, Mathf.Max(0, Radius - Vector3.Distance(new Vector3(x, y, z), LocalPos)) * Change);
        }

        public VoxTerrain FindFriend(Vector3 Direction)
        {
            VoxTerrain fVox;
            if (voxFriendLookup.TryGetValue(Direction, out fVox)) return fVox;
            var Tiles = Physics.OverlapSphere(transform.position + Direction * ChunkSize + Vector3.one * (ChunkSize / 2), ChunkSize / 4, TerrainOnlyLayer, QueryTriggerInteraction.Collide);
            foreach (var Tile in Tiles)
            {
                fVox = Tile.GetComponent<VoxTerrain>();
                if (!fVox) continue;
                voxFriendLookup.Add(Direction, fVox);
                return fVox;
            }
            fVox = CreateFriend(Direction, Direction == Vector3.down ? 1f : -1f);
            voxFriendLookup.Add(Direction, fVox);
            return fVox;
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
