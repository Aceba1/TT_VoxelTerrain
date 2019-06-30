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
    public const float voxelSize = 6; //3.0f; //! Half of a terrain vertex scale

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
        if (Dirty && worldTile.m_LoadStep >= WorldTile.LoadStep.PopulatingScenery)
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
        int size = Mathf.RoundToInt(ChunkSize / voxelSize);

        for (int z = 0; z < subCount; z++)
            for (int x = 0; x < subCount; x++)
            {
                float chWorld;
                if (ManGameMode.inst.GetCurrentGameType() == ManGameMode.GameType.RaD)
                {
                    chWorld = worldTile.GetTerrainheight(_terrain.transform.position + Vector3.one);
                    MarchingCubes.DefaultSampleHeight = chWorld;
                }
                else
                {
                    chWorld = _terrainData.GetHeight((int)((x + 0.5f) * size), (int)((z + 0.5f) * size));
                }
                int centerHeight =(int)(chWorld / ChunkSize);
                //for (int y = minY; y < Mathf.CeilToInt(centerHeight / ChunkSize + voxelSize); y++)
                ////for (int y = Mathf.FloorToInt(b.min.y / ChunkSize); y < Mathf.CeilToInt(b.max.y / ChunkSize); y++) //Change to use buffer of tile, creating chunks where needed
                //{
                    var offset = new Vector3(x, centerHeight, z) * ChunkSize;
                    var t = CheckAndGenerateChunk(offset + transform.position);
                    t.parent = worldTile;
                    t.transform.parent = worldTile.StaticParent;
                    t.transform.position = offset + transform.position;
                    //t.Buffer = MarchingCubes.CreateBufferFromTerrain(worldTile, offset, size, voxelSize);
                    //yield return null;// new WaitForEndOfFrame();
                //}
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
        
        //vinst.position = pos;
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
        v.m_ItemType = new ItemTypeInfo(ObjectTypeVoxelChunk, 0);
        v.tag = "_V";

        go.SetActive(false);

        return go.transform;
    }

    internal class VoxTerrain : MonoBehaviour, IWorldTreadmill
    {
        public WorldTile parent;

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

            mcubes = new MarchingCubes() { interpolate = true };//, sampleProc = Sample };
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
            if (parent == null)
            {
                parent = Singleton.Manager<ManWorld>.inst.TileManager.LookupTile(transform.position, false);
                transform.parent = parent.StaticParent;
            }
            LocalPos = transform.localPosition;
            var V = GetComponent<Visible>();
            try
            {
                parent.Visibles[(int)V.type].Add(V.ID, V);
            }
            catch
            {
                //Console.WriteLine($"{V.type}-ItemType {V.ID}-ID {V.name}-name - FAILED");
            }
            enabled = true;
            mr.enabled = false;
            mc.enabled = false;
            PendingBleedBrush.Clear();
            PendingBleedBrushEffects.Clear();
        }

        private void OnRecycle()
        {
            if (Modified && !Saved)
            {
                Console.WriteLine("Chunk is being removed, but did not save!");
                //if (parent == null || parent.SaveData == null)
                //{
                //    Console.WriteLine("The owning tile is null!");
                //}
                //else
                //{ 
                //    var store = new VoxelSaveData();
                //    store.Store(GetComponent<Visible>());
                //    var savedata = parent.SaveData;
                //    if (!savedata.m_StoredVisibles.ContainsKey(-8))
                //    {
                //        savedata.m_StoredVisibles.Add(-8, new List<ManSaveGame.StoredVisible>(100));
                //    }
                //    savedata.m_StoredVisibles[-8].Add(store);
                //}
            }
            Singleton.Manager<ManWorldTreadmill>.inst.RemoveListener(this);
            transform.parent = null;
            parent = null;
            Buffer = null;
            //Console.WriteLine("Recycling VoxTerrain:"); Console.WriteLine(new System.Diagnostics.StackTrace().ToString());
        }



        public void OnMoveWorldOrigin(IntVector3 amountToMove)
        {
            transform.localPosition = LocalPos;
        }

        #region Base64 save conversion

        static byte[] GetBytes(CloudPair[,,] value)
        {
            int sizep1 = Mathf.RoundToInt(ChunkSize / voxelSize) + 1;

            int size = sizep1 * sizep1 * sizep1 * 2;
            byte[] array = new byte[size];

            int c = 0;
            for (int j = 0; j < sizep1; j++)
            {
                for (int k = 0; k < sizep1; k++)
                {
                    for (int i = 0; i < sizep1; i++)
                    {
                        var item = value[i, j, k];
                        array[c++] = (byte)item.Density;
                        array[c++] = item.Terrain;
                    }
                }
            }
            return array;
            //return OcTree.GetByteArrayFromBuffer(value, sizep1);
        }
        CloudPair[,,] FromBytes(byte[] array)
        { 
            int sizep1 = Mathf.RoundToInt(ChunkSize / voxelSize) + 1;

            int size = sizep1 * sizep1 * sizep1 * 2;
            CloudPair[,,] value = new CloudPair[sizep1, sizep1, sizep1];
            
            int c = 0;
            for (int j = 0; j < sizep1; j++)
            {
                for (int k = 0; k < sizep1; k++)
                {
                    for (int i = 0; i < sizep1; i++)
                    {
                        value[i, j, k] = new CloudPair((sbyte)array[c++], array[c++]);
                    }
                }
            }
            return value;

            //return OcTree.GetBufferFromByteArray(array, sizep1);
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

        public Dictionary<IntVector3, VoxTerrain> voxFriendLookup;
        private static int _BleedWrap = -1;
        public static int BleedWrap { get { if (_BleedWrap == -1) { _BleedWrap = Mathf.RoundToInt(ChunkSize / voxelSize); } return _BleedWrap; } }

        public CloudPair[,,] Buffer = null;
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
        private bool _modified;
        private bool Modified { get => _modified; set { Saved = Saved && !value; _modified = value; } }
        private bool Saved;

        private static Material GetMaterialFromBiome(byte ID)
        {
            Material result = null;
            try
            {
                if (!_matcache.TryGetValue(ID, out result))
                {
                    result = new Material(sharedMaterial);
                    _matcache.Add(ID, result);
                    Biome b = ManWorld.inst.CurrentBiomeMap.LookupBiome((byte)(ID / 2));
                    var bmat = (ID % 2 == 0 ? b.MainMaterialLayer : b.AltMaterialLayer);
                    result.SetTexture("_MainTex", bmat.diffuseTexture);
                    result.SetTexture("_BumpMap", bmat.normalMapTexture);
                }
                return result;
            }
            catch
            {
                var nultx = new Texture2D(1000, 1000);
                for (int y = 0; y < 1000; y++)
                {
                    for (int x = 0; x < 1000; x++)
                    {
                        nultx.SetPixel(x, y, new Color(Mathf.PerlinNoise(x / 1000f, y / 1000f), Mathf.PerlinNoise(x / 1000f + 1, y / 1000f), Mathf.PerlinNoise(x / 1000f + 2, y / 1000f)));
                    }
                }

                // Copy the pixel data to the texture and load it into the GPU.
                result.SetTexture("_BumpMap", nultx);
                return result;
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
                transform.parent = parent.StaticParent;
                LocalPos = transform.localPosition;
                parent.AddVisible(GetComponent<Visible>());
                Console.WriteLine("VoxTerrain was adopted...");
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
                mc.contactOffset = 0.001f;
                mc.enabled = true;
                mf.sharedMesh = mesh;
                mr.enabled = true;

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
                    {
                        if (Singleton.Manager<ManGameMode>.inst.GetCurrentGameType() == ManGameMode.GameType.RaD)
                        {
                            Buffer = mcubes.CreateBuffer(transform.localPosition, Mathf.RoundToInt(ChunkSize / voxelSize), voxelSize);
                        }
                        else
                        {
                            Buffer = MarchingCubes.CreateBufferFromTerrain(parent, transform.localPosition, Mathf.RoundToInt(ChunkSize / voxelSize), voxelSize, out int CountBelow, out int CountAbove);
                            if (CountBelow != 0)
                            {
                                //Console.WriteLine($"<{CountBelow}");
                                for (int i = -1; i >= CountBelow; i--)
                                {
                                    FindFriend(Vector3.up * i);
                                }
                            }
                            if (CountAbove != 0)
                            {
                                //Console.WriteLine($"{CountAbove}->");
                                for (int i = 1; i <= CountAbove; i++)
                                {
                                    FindFriend(Vector3.up * i);
                                }
                            }
                        }
                    }
                    //Buffer = mcubes.CreateBuffer(transform.position, Mathf.RoundToInt(ChunkSize / voxelSize), voxelSize);
                    new Task(delegate
                    {
                        mcubes.MarchChunk(transform.position, Mathf.RoundToInt(ChunkSize / voxelSize), voxelSize, Buffer);

                        normalcache = NormalSolver.RecalculateNormals(mcubes.GetIndices(), mcubes.GetVertices(), 0);//70);

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

        public int PointModifyBuffer(int x, int y, int z, float Change, byte Terrain)
        {
            int result = 0;
            var pre = Buffer[x, y, z];
            CloudPair post;
            if (Change <= 0f) // Remove
            {
                post = pre.AddDensity(Change);
                if (pre.Density > 0 && post.Density <= 0) SpawnChunk(new Vector3(x, y, z) * voxelSize + transform.position);
                //result = pre.Density > 0 ? (post.Density > 0 ? 0 : -1) : 0;
            }
            else // Add
            {
                post = pre.AddDensityAndSeepTerrain(Change, Terrain);
                //result = pre.Density > 0 ? 0 : (post.Density > 0 ? 1 : 0);
            }
            Buffer[x, y, z] = post;
            Dirty |= pre.Density != post.Density;
            return result;
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

        private Vector3 DigNormal;

        public void BleedBrushModifyBuffer(Vector3 WorldPos, float Radius, float Change, Vector3 DigNormal, byte Terrain = 0xFF)
        {
            this.DigNormal = DigNormal;
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

        public float BrushModifyBuffer(Vector3 WorldPos, float Radius, float Change, byte Terrain)
        {
            int Result = 0;
            var LocalPos = (WorldPos - transform.position) / voxelSize;
            int zmax = Mathf.Min(Mathf.CeilToInt(LocalPos.z + Radius), BleedWrap + 1),
                ymax = Mathf.Min(Mathf.CeilToInt(LocalPos.y + Radius), BleedWrap + 1),
                xmax = Mathf.Min(Mathf.CeilToInt(LocalPos.x + Radius), BleedWrap + 1);
            for (int z = Mathf.Max(0,Mathf.FloorToInt(LocalPos.z - Radius)); z < zmax; z++)
                for (int y = Mathf.Max(0,Mathf.FloorToInt(LocalPos.y - Radius)); y < ymax; y++)
                    for (int x = Mathf.Max(0,Mathf.FloorToInt(LocalPos.x - Radius)); x < xmax; x++)
                        Result += PointModifyBuffer(x, y, z, PointInterpolate(Radius, Vector3.Distance(new Vector3(x, y, z), LocalPos)) * Change, Terrain);
            Modified |= Dirty;
            return Result;
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
                    Radius = voxelSize * 0.4f;//0.75f * Mathf.Pow(dmg * 0.2f, 1f);
                    Strength = -.5f;//.5f;//-0.01f - dmg * 0.0001f;
                    break;
                case ManDamage.DamageType.Explosive:
                    Radius = voxelSize * 0.7f + Mathf.Pow(dmg * 0.4f, 1f);
                    Strength = -.5f;//-0.01f - dmg * 0.001f;
                    break;
                case ManDamage.DamageType.Impact:
                    Radius = voxelSize * 0.5f + Mathf.Pow(dmg * 0.25f, 1f);
                    Strength = -.5f;//-0.01f - dmg * 0.0001f;
                    break;
                default:
                    return;
            }
            Radius = Radius / voxelSize;
            BleedBrushModifyBuffer(arg.HitPosition, Radius, Strength, -arg.DamageDirection, Terrain);
        }

        private int SpawnChunk(Vector3 position)
        {
            return 0;
            if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer())
            {
                return 0;
            }
            ChunkTypes chunkTypes = ChunkTypes.SenseOre;
            Vector3 velocity = Vector3.zero;
            velocity = DigNormal * 40f;

            Visible visible = Singleton.Manager<ManSpawn>.inst.SpawnItem(new ItemTypeInfo(ObjectTypes.Chunk, (int)chunkTypes), position, Quaternion.identity, false, false, false, true);
            if (visible)
            {
                velocity += (2f/*Random speed*/) * UnityEngine.Random.insideUnitSphere;
                Vector3 angularVelocity = (2f /*Random spinny speed*/) * UnityEngine.Random.insideUnitSphere;
                visible.pickup.InitNew(velocity, angularVelocity);
                visible.trans.SetParent(Singleton.dynamicContainer);
                visible.SetCollidersEnabled(true);
                return 1;
            }
            Console.WriteLine(new object[]
            {
                    string.Concat(new string[]
                    {
                        "VoxTerrain.SpawnChunk - '",
                        base.name,
                        "' Failed to spawn resource: ",
                        chunkTypes.ToString(),
                        " ...check ResourceTable"
                    })
            });
            return 0;
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
                return true;//!string.IsNullOrEmpty(Cloud64);
            }

            public override Visible SpawnAndRestore()
            {
                //Console.WriteLine("Loading unique VOX...");
                
                var vox = CheckAndGenerateChunk(GetBackwardsCompatiblePosition());
                if (!string.IsNullOrEmpty(Cloud64))
                {
                    vox.StringToBuffer(Cloud64);
                }
                return vox.GetComponent<Visible>();
            }

            public override void Store(Visible visible)
            {
                if (false)
                {
                    visible.SaveForStorage(this);
                }
                else
                {
                    m_WorldPosition = WorldPosition.FromScenePosition(visible.centrePosition);
                    if (visible.centrePosition.IsNaN())
                    {
                        Debug.LogError("Saving Visible " + visible.name + " for storage - CentrePos is NaN. Using transform pos instead");
                        m_WorldPosition = WorldPosition.FromScenePosition(visible.trans.position);
                    }
                    m_ID = visible.ID;
                }
                Store(visible.GetComponent<VoxTerrain>());
            }

            private void Store(VoxTerrain vox)
            {
                if (vox.Modified)
                {
                    vox.Saved = true;
                    Cloud64 = vox.BufferToString();
                    //Console.WriteLine("Stored unique VOX...");
                }
                else
                {
                    Cloud64 = null;
                }
            }
            public string Cloud64;
        }

        internal static class OcTree
        {
            static Vector3Int GetExtents(int Extents, int Corner, Vector3Int CurrentExtents)
            {
                int xMin = (Corner % 2) * Extents;
                int yMin = ((Corner / 2) % 2) * Extents;
                int zMin = ((Corner / 4) % 2) * Extents;
                return new Vector3Int(xMin + CurrentExtents.x, yMin + CurrentExtents.y, zMin + CurrentExtents.z);
            }

            static CloudPair[,,] CopyBufferFromCorner(int Extents, int Corner, CloudPair[,,] Source)
            {
                var Buffer = new CloudPair[Extents, Extents, Extents];
                int xMin = (Corner % 2) * Extents;
                int yMin = ((Corner / 2) % 2) * Extents;
                int zMin = ((Corner / 4) % 2) * Extents;
                for (int j = 0; j < Extents; j++)
                    for (int k = 0; k < Extents; k++)
                        for (int i = 0; i < Extents; i++)
                            Buffer[i, j, k] = Source[i + xMin, j + yMin, k + zMin];
                return Buffer;
            }

            static void WriteValueToBufferArea(int Extents, Vector3Int MinExtents, ref CloudPair[,,] Buffer, CloudPair Value)
            {
                for (int j = MinExtents.x; j < MinExtents.x + Extents; j++)
                    for (int k = MinExtents.x; k < MinExtents.x + Extents; k++)
                        for (int i = MinExtents.x; i < MinExtents.x + Extents; i++)
                            Buffer[i, j, k] = Value;
            }

            static bool SplitCondition(CloudPair[,,] Buffer)
            {
                var control = Buffer[0, 0, 0];
                foreach(var pair in Buffer)
                {
                    if (!control.Equals(pair)) return true;
                }
                return false;
            }

            static void Split(ref List<byte> Out, CloudPair[,,] Buffer, int Extents)
            {
                if (SplitCondition(Buffer))
                {
                    Console.Write("#");
                    Out.Add(255); // Mark as has children (Terrain should not normally be 0xFF)
                    for (int i = 0; i < 8; i++)
                    {
                        var nExtents = Extents / 2;
                        var nBuffer = CopyBufferFromCorner(nExtents, i, Buffer);
                        Split(ref Out, nBuffer, nExtents);
                    }
                    return;
                }
                Console.Write("+");
                Out.Add(Buffer[0, 0, 0].Terrain); //Terrain first
                Out.Add((byte)Buffer[0, 0, 0].Density); //Density second
            }

            static void Join(ref CloudPair[,,] Buffer, int Extents, Vector3Int MinCorner, byte[] bytes, ref int CurrentStep)
            {
                if (bytes[CurrentStep] == 255)
                {
                    Console.Write("#");
                    CurrentStep++;
                    for (int i = 0; i < 8; i++)
                    {
                        var nExtents = Extents / 2;
                        var nBuffer = CopyBufferFromCorner(nExtents, i, Buffer);
                        Join(ref Buffer, nExtents, GetExtents(nExtents, i, MinCorner), bytes, ref CurrentStep);
                    }
                    return;
                }
                Console.Write("+");
                WriteValueToBufferArea(Extents, MinCorner, ref Buffer, new CloudPair((sbyte)bytes[CurrentStep + 1], bytes[CurrentStep]));
                CurrentStep+=2;
            }

            public static byte[] GetByteArrayFromBuffer(CloudPair[,,] buffer, int ArraySize)
            {
                var bytes = new List<Byte>();
                Split(ref bytes, buffer, ArraySize);
                return bytes.ToArray();
            }

            public static CloudPair[,,] GetBufferFromByteArray(byte[] bytes, int DimensionSize)
            {
                var result = new CloudPair[DimensionSize, DimensionSize, DimensionSize];
                int iterator = 0;
                Join(ref result, DimensionSize, Vector3Int.zero, bytes, ref iterator);
                return result;
            }
        }
    }
}
