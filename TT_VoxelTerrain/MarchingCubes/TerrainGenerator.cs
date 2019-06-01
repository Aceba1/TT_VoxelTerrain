using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections;

public class TerrainGenerator : MonoBehaviour
{
    //internal static List<VoxTerrain> ListOfAllActiveChunks = new List<VoxTerrain>();

    public const ObjectTypes ObjectTypeVoxelChunk = (ObjectTypes)ObjectTypes.Scenery; //Crate cannot be impacted or drilled, Scenery causes implementation problems (PATCH:EnforceNotActuallyScenery), out-of-enum causes null problems
    internal static Transform Prefab;
    public WorldTile worldTile;
    public static LayerMask VoxelTerrainOnlyLayer = LayerMask.GetMask(LayerMask.LayerToName(Globals.inst.layerTerrain));
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
    bool Dirty = true;

    internal static Material sharedMaterial = new Material(Shader.Find("Diffuse"));

    void LateUpdate()
    {
        if (Dirty)
        {
            Dirty = false;
            GenerateTerrain();//StartCoroutine("GenerateTerrain");
        }
    }

    /*IEnumerator*/void GenerateTerrain()
    {
        _terrain = gameObject.GetComponent<Terrain>();
        _terrainData = _terrain.terrainData;
        
        if (ChunkSize == 0) ChunkSize = Mathf.RoundToInt(_size) / subCount;

        var b = gameObject.GetComponent<TerrainCollider>().bounds;

        for (int z = 0; z < subCount; z++)
            for (int y = 0; y < 2; y++)//for (int y = Mathf.FloorToInt(b.min.y / ChunkSize); y < Mathf.CeilToInt(b.max.y / ChunkSize); y++) //Change to use buffer of tile, creating chunks where needed
                for (int x = 0; x < subCount; x++)
                {
                    var offset = new Vector3(x, y, z) * ChunkSize;
                    var t = CheckAndGenerateChunk(offset + transform.position).transform;
                    t.position = offset + transform.position;
                    //yield return null;// new WaitForEndOfFrame();
                }
        gameObject.GetComponent<TerrainCollider>().enabled = false;
        _terrain.enabled = false;
        Destroy(this);
    }

    internal static VoxTerrain CheckAndGenerateChunk(Vector3 position)
    {
        var Tiles = Physics.OverlapSphere(position + Vector3.one * (ChunkSize / 2), ChunkSize / 4, VoxelTerrainOnlyLayer, QueryTriggerInteraction.Collide);
        foreach (var Tile in Tiles)
        {
            var vox = Tile.GetComponent<VoxTerrain>();
            if (vox) return vox;
        }
        return GenerateChunk(position);//(worldTile);
    }

    static System.Reflection.PropertyInfo Visible_damageable;
    static System.Reflection.FieldInfo Visible_m_VisibleComponent;

    internal static VoxTerrain GenerateChunk(Vector3 pos)
    {
        if (Prefab == null)
        {
            Prefab = GeneratePrefab();
            //var b = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            //var toT = typeof(TerrainObject);
            //toT.GetField("m_PersistentObjectGUID", b).SetValue(Prefab as TerrainObject, TT_VoxelTerrain.Class1.VoxTerrainGUID);
            //(typeof(TerrainObjectTable).GetField("m_GUIDToPrefabLookup", b).GetValue(typeof(ManSpawn).GetField("m_TerrainObjectTable", b).GetValue(ManSpawn.inst) as TerrainObjectTable) as Dictionary<string, TerrainObject>).Add(TT_VoxelTerrain.Class1.VoxTerrainGUID, Prefab);
            //TerrainObject_AddToTileData = toT.GetMethod("AddToTileData", b);
        }

        var vinst = Prefab.Spawn(pos, Quaternion.identity);
        //TerrainObject_AddToTileData.Invoke(vinst, null);
        vinst.position = pos;
        var vox = vinst.GetComponent<VoxTerrain>();
        //ListOfAllActiveChunks.Add(vox);
        return vox;
    }

    private static Transform GeneratePrefab()
    {
        if (Visible_damageable == null)
        {
            Type vis = typeof(Visible);
            var bind = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            Visible_damageable = vis.GetProperty("damageable", bind);
            Visible_m_VisibleComponent = vis.GetField("m_VisibleComponent", bind);
        }

        //if (Prefab == null)
        //{
        //    Prefab = (typeof(ManSpawn).GetField("spawnableScenery", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ManSpawn.inst) as List<Visible>)[0].GetComponent<TerrainObject>();
        //}
        //var so = Prefab.SpawnFromPrefab(ParentTile, Vector3.zero, Quaternion.identity);
        //var go = so.gameObject;

        var go = new GameObject("VoxTerrainChunk");
        

        //go.AddComponent<ChunkBounds>();
        go.layer = Globals.inst.layerTerrain;
        go.tag = "_V";

        var bc = go.AddComponent<BoxCollider>();
        bc.size = Vector3.one * ChunkSize;
        bc.center = Vector3.one * (ChunkSize / 2);
        bc.isTrigger = true;

        var cgo = new GameObject("Terrain");
        cgo.layer = Globals.inst.layerTerrain;
        cgo.transform.parent = go.transform;
        cgo.transform.localPosition = Vector3.zero;

        var mf = cgo.AddComponent<MeshFilter>();

        var mr = cgo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = sharedMaterial;

        var mc = cgo.AddComponent<MeshCollider>();
        mc.convex = false;
        mc.sharedMaterial = new PhysicMaterial();

        //var voxdisp = go.AddComponent<VoxDispenser>();

        var vox = go.AddComponent<VoxTerrain>();

        var d = go.AddComponent<Damageable>();
        d.destroyOnDeath = false;
        d.SetMaxHealth(1000);
        d.InitHealth(1000);

        var v = go.AddComponent<Visible>();
        //v.m_ItemType = new ItemTypeInfo(ObjectTypeVoxelChunk, 8192);
        v.tag = "_V";

        go.SetActive(false);

        return go.transform;
    }

    internal class VoxTerrain : MonoBehaviour, IWorldTreadmill
    {
        public WorldTile parent;

        //private void PrePool()
        //{
        //    Visible v = GetComponent<Visible>();
        //    v.m_ItemType = new ItemTypeInfo(ObjectTypes.Scenery, 9283);
        //    mr = GetComponentInChildren<MeshRenderer>();
        //    mf = GetComponentInChildren<MeshFilter>();
        //    mc = GetComponentInChildren<MeshCollider>();
        //    d = GetComponent<Damageable>();
        //    d.rejectDamageEvent += RejectDamageEvent;
        //    vd = GetComponent<VoxDispenser>();
        //}

        private void OnPool()
        {
            Visible v = GetComponent<Visible>();
            Visible_m_VisibleComponent.SetValue(v, this);
            d = GetComponent<Damageable>();
            d.rejectDamageEvent += RejectDamageEvent;
            Visible_damageable.SetValue(v, d, null);
            voxFriendLookup = new Dictionary<IntVector3, VoxTerrain>();
            mr = GetComponentInChildren<MeshRenderer>();
            mf = GetComponentInChildren<MeshFilter>();
            mc = GetComponentInChildren<MeshCollider>();

            mcubes = new MarchingCubes() { interpolate = true, sampleProc = Sample };
            PendingBleedBrushEffects = new List<BrushEffect>();
            PendingBleedBrush = new List<ManDamage.DamageInfo>();
        }

        private void OnSpawn()
        {
            Singleton.Manager<ManWorldTreadmill>.inst.AddListener(this);
            Dirty = true;
            Buffer = null;
            Modified = false;
            voxFriendLookup.Clear();
            voxFriendLookup.Add(IntVector3.zero, this);
            parent = Singleton.Manager<ManWorld>.inst.TileManager.LookupTile(transform.position, false);
            LocalPos = Vector3.zero;
            if (parent != null)
            {
                transform.parent = parent.StaticParent;
                LocalPos = transform.localPosition;
            }
            enabled = true;
            gameObject.SetActive(true);
            PendingBleedBrush.Clear();
            PendingBleedBrushEffects.Clear();
        }

        private void OnRecycle()
        {
            Singleton.Manager<ManWorldTreadmill>.inst.RemoveListener(this);
            transform.parent = null;
            //Console.WriteLine("Recycling VoxTerrain:"); Console.WriteLine(new System.Diagnostics.StackTrace().ToString());
        }

        public void OnMoveWorldOrigin(IntVector3 amountToMove)
        {
            transform.localPosition = LocalPos;
        }

        #region Base64 save conversion

        static byte[] GetBytes(MarchingCubes.CloudPair[,,] value)
        {
            int sizep1 = Mathf.RoundToInt(ChunkSize / voxelSize) + 1;
            int size = sizep1 * sizep1 * sizep1 * 2;
            byte[] array = new byte[size];

            int c = 0;
            for (int i = 0; i < sizep1; i++)
            {
                for (int j = 0; j < sizep1; j++)
                {
                    for (int k = 0; k < sizep1; k++)
                    {
                        var item = value[i, j, k];
                        array[c++] = (byte)item.Density;
                        array[c++] = item.Terrain;
                    }
                }
            }

            return array;
        }
        MarchingCubes.CloudPair[,,] FromBytes(byte[] array)
        { 
            int sizep1 = Mathf.RoundToInt(ChunkSize / voxelSize) + 1;
            int size = sizep1 * sizep1 * sizep1 * 2;
            MarchingCubes.CloudPair[,,] value = new MarchingCubes.CloudPair[sizep1, sizep1, sizep1];
            
            int c = 0;
            for (int i = 0; i < sizep1; i++)
            {
                for (int j = 0; j < sizep1; j++)
                {
                    for (int k = 0; k < sizep1; k++)
                    {
                        value[i, j, k] = new MarchingCubes.CloudPair((sbyte)array[c++], array[c++]);
                    }
                }
            }

            return value;
        }

        private byte[] BufferToByteArray()
        {
            return GetBytes(Buffer);
        }

        public override string ToString()
        {
            return BufferToString();
        }

        private string BufferToString()
        {
            if (Buffer != null && Buffer.Length > 7)
            return System.Convert.ToBase64String(BufferToByteArray());
            return "";
        }

        private void StringToBuffer(string base64buffer)
        {
            Modified = true;
            Dirty = true;
            Buffer = FromBytes(System.Convert.FromBase64String(base64buffer));
        }
        #endregion

        private static float GetHeightAtPos(Vector2 scenePos)
        {
            //if (Physics.Raycast(scenePos.ToVector3XZ().SetY(512), Vector3.down, out RaycastHit y, 1024, Globals.inst.layerTerrainOnly, QueryTriggerInteraction.Ignore))
            //if (ManWorld.inst.GetTerrainHeight(scenePos.ToVector3XZ(), out float y))
            //    return y.point.y;
            return 0;
        }
        public static MarchingCubes.ReadPair Sample(Vector2 pos)
        {
            //var bc = ManWorld.inst.GetBiomeWeightsAtScenePosition(pos.ToVector3XZ());
            //Biome highestB = null;
            //float h = 0f;
            //for (int i = 0; i < bc.NumWeights; i++)
            //{
            //    Biome biome = bc.Biome(i);
            //    float weight = bc.Weight(i);
            //    if (biome != null && weight > h)
            //    {
            //        h = weight;
            //        highestB = biome;
            //    }
            //}
            return new MarchingCubes.ReadPair(GetHeightAtPos(pos),0x08, 0x09);// (byte)(((byte)highestB.BiomeType)*2), (byte)(((byte)highestB.BiomeType)*2+1));//(-10 + Mathf.Round(Mathf.Sin((pos.x + ManWorld.inst.SceneToGameWorld.x)*0.01f))*20f + Mathf.Sin((pos.y + ManWorld.inst.SceneToGameWorld.z) * 0.004f) * 20f, 0x00, 0x01);
        }

        public Dictionary<IntVector3, VoxTerrain> voxFriendLookup;
        private static int _BleedWrap = -1;
        public static int BleedWrap { get { if (_BleedWrap == -1) { _BleedWrap = Mathf.RoundToInt(ChunkSize / voxelSize); } return _BleedWrap; } }

        public MarchingCubes.CloudPair[,,] Buffer = null;
        public bool Dirty = false, DoneBaking = false, Processing = false;
        //public VoxDispenser vd;
        public MeshFilter mf;
        public MeshRenderer mr;
        public MeshCollider mc;
        public Damageable d;
        //System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        MarchingCubes mcubes;
        List<Vector3> normalcache;
        List<BrushEffect> PendingBleedBrushEffects;
        List<ManDamage.DamageInfo> PendingBleedBrush;

        Vector3 LocalPos;

        private static Dictionary<byte, Material> _matcache = new Dictionary<byte, Material>();
        private bool Modified;

        private static Material GetMaterialFromBiome(byte ID)
        {
            try
            {
                Material result;
                if (!_matcache.TryGetValue(ID, out result))
                {
                    Biome b = ManWorld.inst.CurrentBiomeMap.LookupBiome((byte)(ID / 2));
                    var bmat = (ID % 2 == 0 ? b.MainMaterialLayer : b.AltMaterialLayer);
                    result = new Material(sharedMaterial);
                    result.SetTexture("_MainTex", bmat.diffuseTexture);
                    result.SetTexture("_BumpMap", bmat.normalMapTexture);
                    _matcache.Add(ID, result);
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        public string WriteBoolState()
        {
            return $"Enabled?{enabled}, GameObjectEnabled?{gameObject.activeInHierarchy}{gameObject.activeSelf}, Dirty?{Dirty}, Processing?{Processing}, DoneBaking?{DoneBaking}, BufferExists?{(Buffer != null ? Buffer.Length.ToString() : "false")}, Parent?{parent != null}";
        }

        void LateUpdate()
        {
            if (parent == null)
            {
                parent = Singleton.Manager<ManWorld>.inst.TileManager.LookupTile(transform.position, false);
                if (parent == null) return;
                transform.parent = parent.Terrain.transform;
                LocalPos = transform.localPosition;
            }
            if (DoneBaking)
            {
                DoneBaking = false;
                Mesh mesh = new Mesh();
                mesh.vertices = mcubes.GetVertices();
                int i= 0;

                Material[] mats = new Material[mcubes._indices.Count];

                mesh.subMeshCount = mcubes._indices.Count;
                foreach (var pair in mcubes._indices)
                {
                    mesh.SetTriangles(pair.Value, i);
                    mats[i] = GetMaterialFromBiome(pair.Key);
                    i++;
                }
                mesh.SetUVs(0, mcubes._uvs);//new Vector2[mesh.vertices.Length];

                mr.sharedMaterials = mats;

                //for (int i = 0; i < mesh.vertices.Length; i++)
                //mesh.uv[i] = new Vector2(mesh.vertices[i].x, mesh.vertices[i].y);

                mesh.RecalculateBounds();

                mesh.SetNormals(normalcache);
                normalcache.Clear();

                mc.sharedMesh = mesh;
                mf.sharedMesh = mesh;

                mcubes.Reset();

                Processing = false;

                //stopwatch.Stop();
                //Debug.LogFormat("Generation took {0} seconds", stopwatch.Elapsed.TotalSeconds);
            }

            if (!Processing)
            {
                if (PendingBleedBrushEffects.Count != 0)
                {
                    while (0 < PendingBleedBrushEffects.Count)
                    {
                        if (Buffer == null) break;
                        BrushModifyBuffer(PendingBleedBrushEffects[0]);
                        PendingBleedBrushEffects.RemoveAt(0);
                    }
                }

                if (Dirty)
                {
                    //stopwatch.Restart();
                    Dirty = false;
                    DoneBaking = false;
                    Processing = true;
                    if (Buffer == null)
                        Buffer = mcubes.CreateBuffer(transform.position, Mathf.RoundToInt(ChunkSize / voxelSize), voxelSize);
                    new Task(delegate
                    {
                        mcubes.MarchChunk(transform.position, Mathf.RoundToInt(ChunkSize / voxelSize), voxelSize, Buffer);

                        normalcache = NormalSolver.RecalculateNormals(mcubes.GetIndices(), mcubes.GetVertices(), 70);

                        DoneBaking = true;
                    }).Start();
                }
            }
        }

        void FixedUpdate()
        {
            if (!Processing)
            {
                if (PendingBleedBrush.Count != 0)
                {
                    while (0 < PendingBleedBrush.Count)
                    {
                        if (Buffer == null) break;
                        ProcessDamageEvent(PendingBleedBrush[0], 0x00);
                        PendingBleedBrush.RemoveAt(0);
                    }
                }
            }
        }

        public void PointModifyBuffer(int x, int y, int z, float Change, byte Terrain)
        {
            var pre = Buffer[x, y, z];
            MarchingCubes.CloudPair post;
            if (Change <= 0f)
                post = pre.AddDensity(Change);
            else
                post = pre.AddDensityAndSeepTerrain(Change, Terrain);
            Buffer[x, y, z] = post;
            Dirty |= pre.Density != post.Density;
        }

        private struct BrushEffect
        {
            public Vector3 WorldPos;
            public float Radius;
            public float Change;
            public byte Terrain;

            public BrushEffect(Vector3 WorldPos, float Radius, float Change, byte Terrain)
            {
                this.WorldPos = WorldPos;
                this.Radius = Radius;
                this.Change = Change;
                this.Terrain = Terrain;
            }
        }

        public void BleedBrushModifyBuffer(Vector3 WorldPos, float Radius, float Change, byte Terrain = 0xFF)
        {
            var LocalPos = (WorldPos - transform.position) / voxelSize;
            var ContactTerrain = Terrain == 0xFF ? Buffer[Mathf.RoundToInt(LocalPos.x), Mathf.RoundToInt(LocalPos.y), Mathf.RoundToInt(LocalPos.z)].Terrain : Terrain;
            int xmax = Mathf.CeilToInt((LocalPos.x + Radius + 2) / BleedWrap),
                ymax = Mathf.CeilToInt((LocalPos.y + Radius + 2) / BleedWrap),
                zmax = Mathf.CeilToInt((LocalPos.z + Radius + 2) / BleedWrap);
            for (int x = Mathf.FloorToInt((LocalPos.x - Radius - 2) / BleedWrap); x <= xmax; x++)
                for (int y = Mathf.FloorToInt((LocalPos.y - Radius - 2) / BleedWrap); y <= ymax; y++)
                    for (int z = Mathf.FloorToInt((LocalPos.z - Radius - 2) / BleedWrap); z <= zmax; z++)
                        FindFriend(new Vector3(x, y, z)).BBMB_internal(WorldPos, Radius, Change, ContactTerrain);
        }

        internal void BBMB_internal(Vector3 WorldPos, float Radius, float Change, byte Terrain)
        {
            if (Processing || Buffer == null)
                PendingBleedBrushEffects.Add(new BrushEffect(WorldPos, Radius, Change, Terrain));
            else
                BrushModifyBuffer(WorldPos, Radius, Change, Terrain);
        }

        private void BrushModifyBuffer(BrushEffect b)
        {
            BrushModifyBuffer(b.WorldPos, b.Radius, b.Change, b.Terrain);
        }

        private static float PointInterpolate(float Radius, float Distance)
        {
            return Mathf.Min(Mathf.Max(Radius - Distance, 0), voxelSize) / voxelSize;
        }

        public void BrushModifyBuffer(Vector3 WorldPos, float Radius, float Change, byte Terrain)
        {
            var LocalPos = (WorldPos - transform.position) / voxelSize;
            int zmax = Mathf.Min(Mathf.CeilToInt(LocalPos.z + Radius), BleedWrap + 1),
                ymax = Mathf.Min(Mathf.CeilToInt(LocalPos.y + Radius), BleedWrap + 1),
                xmax = Mathf.Min(Mathf.CeilToInt(LocalPos.x + Radius), BleedWrap + 1);
            for (int z = Mathf.Max(0,Mathf.FloorToInt(LocalPos.z - Radius)); z < zmax; z++)
                for (int y = Mathf.Max(0,Mathf.FloorToInt(LocalPos.y - Radius)); y < ymax; y++)
                    for (int x = Mathf.Max(0,Mathf.FloorToInt(LocalPos.x - Radius)); x < xmax; x++)
                        PointModifyBuffer(x, y, z, PointInterpolate(Radius, Vector3.Distance(new Vector3(x, y, z), LocalPos)) * Change, Terrain);
            Modified |= Dirty;
        }

        public VoxTerrain FindFriend(Vector3 Direction)
        {
            VoxTerrain fVox;
            if (voxFriendLookup.TryGetValue(Direction, out fVox))
            {
                if (fVox != null && fVox.enabled && fVox.transform.position - transform.position == Direction * ChunkSize)
                {
                    return fVox;
                }
                voxFriendLookup.Remove(Direction);
            }
            var Tiles = Physics.OverlapSphere(transform.position + Direction * ChunkSize + Vector3.one * (ChunkSize / 2), ChunkSize / 4, VoxelTerrainOnlyLayer, QueryTriggerInteraction.Collide);
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
            var pos = transform.position + Direction * ChunkSize;
            VoxTerrain newVox = GenerateChunk(pos);
            int Size = Mathf.RoundToInt(ChunkSize / voxelSize) + 1;
            return newVox;
        }

        private void ProcessDamageEvent(ManDamage.DamageInfo arg, byte Terrain)
        {
            float Radius, Strength;
            float dmg = arg.Damage * 0.01f;
            switch (arg.DamageType)
            {
                case ManDamage.DamageType.Cutting:
                case ManDamage.DamageType.Standard:
                    Radius = voxelSize * 0.2f * Mathf.Pow(dmg * 0.1f, 0.25f);
                    Strength = -.5f;//-0.01f - dmg * 0.0001f;
                    break;
                case ManDamage.DamageType.Explosive:
                    Radius = voxelSize * 0.15f + Mathf.Pow(dmg * 0.05f, 0.5f);
                    Strength = -.5f;//-0.01f - dmg * 0.001f;
                    break;
                case ManDamage.DamageType.Impact:
                    Radius = voxelSize * 0.25f + Mathf.Pow(dmg * 0.1f, 0.5f);
                    Strength = -.5f;//-0.01f - dmg * 0.0001f;
                    break;
                default:
                    return;
            }
            BleedBrushModifyBuffer(arg.HitPosition, Radius, Strength, Terrain);
        }

        internal bool RejectDamageEvent(ManDamage.DamageInfo arg)
        {
            PendingBleedBrush.Add(arg);
            return true;
        }

        static System.Reflection.MethodInfo StoredTile_AddStoredVisibleToTile = typeof(ManSaveGame.StoredTile).GetMethod("AddStoredVisibleToTile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        
        internal class VoxelSaveData : ManSaveGame.StoredVisible
        {
            public override bool CanRestore()
            {
                return !string.IsNullOrEmpty(Cloud64);
            }

            public override Visible SpawnAndRestore()
            {
                
                var vox = CheckAndGenerateChunk(GetBackwardsCompatiblePosition());
                if (!string.IsNullOrEmpty(Cloud64))
                {
                    vox.StringToBuffer(Cloud64);
                }
                return vox.GetComponent<Visible>();
            }

            public override void Store(Visible visible)
            {
                visible.SaveForStorage(this);
                Store(visible.GetComponent<VoxTerrain>());
            }

            private void Store(VoxTerrain vox)
            {
                if (vox.Modified)
                {
                    Cloud64 = vox.BufferToString();
                }
                else
                {
                    Cloud64 = null;
                }
            }
            
            public string Cloud64;
        }
    }


}
