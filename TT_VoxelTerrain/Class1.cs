using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using UnityEngine;
using System.Reflection;

namespace TT_VoxelTerrain
{
    public class Class1
    {
        public static void Init()
        {
            HarmonyInstance.Create("aceba1.betterterrain").PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            new GameObject().AddComponent<MassShifter>();
        }

        private static void SingtonStarted()
        {
            var b = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(CameraManager).GetField("m_UnderGroundTolerance", b).SetValue(CameraManager.inst, 100000f);
            TankCamera.inst.groundClearance = -100000f;
            ManSaveGame.k_RestoreOrder.Insert(0, -8);
        }

        public static void AddVoxTerrain(WorldTile tile)
        {
            tile.Terrain.gameObject.AddComponent<TerrainGenerator>().worldTile = tile;
        }

        internal class MassShifter : MonoBehaviour
        {
            byte BrushMat = 0xFF;
            int brushSize = 6;
            void Update()
            {
                if (!Input.GetKey(KeyCode.LeftAlt) && (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.RightBracket))) brushSize++;
                if (!Input.GetKey(KeyCode.LeftAlt) && (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.LeftBracket))) brushSize = Math.Max(brushSize - 1,1);
                if (Input.GetKey(KeyCode.LeftAlt) && (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.RightBracket))) BrushMat++;
                if (Input.GetKey(KeyCode.LeftAlt) && (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.LeftBracket))) BrushMat--;

                if (Physics.Raycast(Singleton.camera.ScreenPointToRay(Input.mousePosition), out var raycastHit, 10000, TerrainGenerator.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore))
                {
                    TerrainGenerator.VoxTerrain vox = raycastHit.transform.gameObject.GetComponentInParent<TerrainGenerator.VoxTerrain>();
                    if (vox != null)
                    {
                        if (Input.GetKey(KeyCode.Equals))
                        {
                            vox.BleedBrushModifyBuffer(raycastHit.point, brushSize / TerrainGenerator.voxelSize, 1f, raycastHit.normal, BrushMat);
                        }
                        if (Input.GetKey(KeyCode.Minus))
                        {
                            vox.BleedBrushModifyBuffer(raycastHit.point, brushSize / TerrainGenerator.voxelSize, -1f, raycastHit.normal, 0x00);
                        }
                        if (Input.GetKeyDown(KeyCode.Backspace))
                        {
                            Console.WriteLine("Vox ID "+vox.GetComponent<Visible>().ID);
                        }
                    }
                }
            }
        }

        private static class Patches
        {
            [HarmonyPatch(typeof(WorldTile), "AddVisible")]
            private static class EnforceNotActuallyScenery
            {
                private static bool Prefix(Visible visible)
                {
                    return visible.name != "VoxTerrainChunk";
                }
            }

            [HarmonyPatch(typeof(ManSaveGame.StoredTile), "SetSceneryAwake")]
            private static class NoResdispBecauseNotActuallyScenery
            {
                private static bool Prefix(Dictionary<int, Visible>[] visibles, bool awake)
                {
                    foreach (Visible visible in visibles[3].Values)
                    {
                        if (visible.resdisp != null)
                            visible.resdisp.SetAwake(awake);
                    }
                    return false;
                }
            }


            [HarmonyPatch(typeof(Visible), "OnPool")]
            private static class VisibleIsBeingStubborn
            {
                private static void Prefix(ref Visible __instance)
                {
                    if (__instance.name == "VoxTerrainChunk")
                    {
                        __instance.m_ItemType = new ItemTypeInfo(TerrainGenerator.ObjectTypeVoxelChunk, 0);
                    }
                }
            }

            //[HarmonyPatch(typeof(Visible), "OnSpawn")]
            //private static class VisibleIsBeingReallyStubborn
            //{
            //    private static void Prefix(ref Visible __instance)
            //    {
            //        if (__instance.name == "VoxTerrainChunk")
            //        {
            //            __instance.m_ItemType = new ItemTypeInfo(TerrainGenerator.ObjectTypeVoxelChunk, 0);
            //        }
            //    }
            //}

            [HarmonyPatch(typeof(TileManager), "SetTileCache")]
            private static class PleaseStopRemovingMyChunks
            {
                private static bool Prefix(Visible visible, WorldTile newTile, ref bool __result)
                {
                    __result = false;
                    if (visible.name == "VoxTerrainChunk" && newTile != null)
                    {
                        newTile.AddVisible(visible);
                        visible.tileCache.tile = newTile;
                        return false;
                    }
                    return true;
                }
            }

            [HarmonyPatch(typeof(ManSaveGame.StoredTile),"StoreScenery")]
            private static class PleaseSaveMyChunks
            {
                private static void Postfix(ref ManSaveGame.StoredTile __instance, Dictionary<int, Visible>[] visibles)
                {
                    ManSaveGame.Storing = true;
                    int i = 0;
                    foreach (Visible visible in visibles[(int)TerrainGenerator.ObjectTypeVoxelChunk].Values)
                    {
                        if (visible.name == "VoxTerrainChunk")
                        {
                            var store = new TerrainGenerator.VoxTerrain.VoxelSaveData();
                            store.Store(visible);
                            if (store.Cloud64 == null) continue;
                            if (!__instance.m_StoredVisibles.ContainsKey(-8))
                            {
                                __instance.m_StoredVisibles.Add(-8, new List<ManSaveGame.StoredVisible>(100));
                            }
                            __instance.m_StoredVisibles[-8].Add(store);
                            i++;
                        }
                    }
                    Console.WriteLine($"{i} unique vox terrain saved");
                    ManSaveGame.Storing = false;
                }
            }

            [HarmonyPatch(typeof(ManSaveGame.StoredTile), "RestoreVisibles")]
            private static class PleaseLoadMyChunks
            {
                private static void Prefix(ManSaveGame.StoredTile __instance)
                {
                    if (__instance.m_StoredVisibles.TryGetValue(-8, out List<ManSaveGame.StoredVisible> voxlist))
                    {
                    //    for (int j = 0; j < voxlist.Count; j++)
                    //    {
                    //        ManSaveGame.StoredVisible storedVisible = voxlist[j];
                    //        ManSaveGame.RestoreOrDeferLoadingVisible(storedVisible, __instance.coord);
                    //    }
                        if (!ManSaveGame.k_RestoreOrder.Contains(-8))
                        {
                            ManSaveGame.k_RestoreOrder.Insert(0, -8);
                            if (!ManSaveGame.k_RestoreOrder.Contains(-8))
                            {
                                Console.WriteLine("gues i'l die");
                            }
                            }
                    }
                }
            }

            //[HarmonyPatch(typeof(ManSaveGame), "CreateStoredVisible")]
            //private static class SaveVoxChunks
            //{
            //    private static bool Prefix(ref ManSaveGame.StoredVisible __result, Visible visible)
            //    {
            //        if (visible.name == "VoxTerrainChunk")
            //        {
            //            var result = new TerrainGenerator.VoxTerrain.VoxelSaveData();
            //            result.Store(visible);
            //            __result = result;
            //            //Console.WriteLine(result.Cloud64);
            //            return false;
            //        }
            //        return true;
            //    }
            //}

            [HarmonyPatch(typeof(Visible), "get_rbody")]
            private static class SupressPhysics
            {
                private static bool Prefix(Visible __instance)
                {
                    return __instance.name != "VoxTerrainChunk";
                }
            }

            [HarmonyPatch(typeof(TileManager), "Init")]
            private static class AttachVoxTerrain
            {
                private static void Postfix()
                {
                    ManWorld.inst.TileManager.TilePopulatedEvent.Subscribe(AddVoxTerrain);
                }
            }

            [HarmonyPatch(typeof(TileManager), "GetTerrainHeightAtPosition")]
            private static class ReplaceHeightGet
            {
                private static bool Prefix(ref float __result, Vector3 scenePos, /*out bool onTile,*/ bool forceCalculate)
                {
                    //onTile = true;
                    //int layerMask = hitScenery ? (Globals.inst.layerScenery.mask | Globals.inst.layerTerrain.mask) : (int)TerrainGenerator.TerrainOnlyLayer;
                    if (Physics.Raycast(scenePos, Vector3.down, out RaycastHit raycasthit, 8192, TerrainGenerator.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
                    {
                        __result = raycasthit.point.y;
                        return false;
                    }
                    if (Physics.Raycast(scenePos + Vector3.up * TerrainGenerator.voxelSize, Vector3.down, out raycasthit, 8192, TerrainGenerator.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
                    {
                        __result = raycasthit.point.y;
                        return false;
                    }
                    if (Physics.Raycast(scenePos + Vector3.up * 4096 + Vector3.one * 0.001f, Vector3.down, out raycasthit, 8192, TerrainGenerator.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
                    {
                        __result = raycasthit.point.y;
                        return false;
                    }
                    return true;
                }
            }

            [HarmonyPatch(typeof(TankCamera), "GroundProjectAllowingForGameMode")]
            private static class ReplaceTankCameraGroundProject
            {
                private static bool Prefix(ref Vector3 __result, Vector3 position)
                {
                    __result = Singleton.Manager<ManWorld>.inst.ProjectToGround(position, false) + Vector3.down * 10000;
                    return false;
                }
            }

            //[HarmonyPatch(typeof(Tank), "HandleCollision")]
            //private static class TerrainCollisionBypassPatch
            //{
            //    private static void Prefix(Tank __instance, Collision collisionData, bool stay, ref Visible __state)
            //    {
            //        var go = collisionData.GetContact(0).thisCollider;
            //        if (go.transform.parent.name == "VoxTerrainChunk")
            //        {
            //            var V = go.GetComponentInParent<Visible>();
            //            if (V)
            //            {
            //                V.m_ItemType = new ItemTypeInfo(ObjectTypes.Scenery, 0);
            //                __state = V;
            //                Console.WriteLine("oh yea vox");
            //                return;
            //            }
            //        }
            //        __state = null;
            //    }
            //    private static void Postfix(ref Visible __state)
            //    {
            //        if (__state)
            //        {
            //            __state.m_ItemType = new ItemTypeInfo(TerrainGenerator.ObjectTypeVoxelChunk, 0);
            //            Console.WriteLine("vox yea oh");
            //        }
            //    }
            //}
        }
    }
}
