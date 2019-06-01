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
                if (!Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.KeypadPlus)) brushSize++;
                if (!Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.KeypadMinus)) brushSize = Math.Max(brushSize - 1,1);
                if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.KeypadPlus)) BrushMat++;
                if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.KeypadMinus)) BrushMat--;

                if (Physics.Raycast(Singleton.camera.ScreenPointToRay(Input.mousePosition), out var raycastHit, 10000, TerrainGenerator.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore))
                {
                    TerrainGenerator.VoxTerrain vox = raycastHit.transform.gameObject.GetComponentInParent<TerrainGenerator.VoxTerrain>();
                    if (vox != null)
                    {
                        if (Input.GetKey(KeyCode.Equals))
                        {
                            vox.BleedBrushModifyBuffer(raycastHit.point, brushSize / TerrainGenerator.voxelSize, .5f, BrushMat);
                        }
                        if (Input.GetKey(KeyCode.Minus))
                        {
                            vox.BleedBrushModifyBuffer(raycastHit.point, brushSize / TerrainGenerator.voxelSize, -.5f, 0x00);
                        }
                        if (Input.GetKey(KeyCode.Backslash))
                        {
                            Console.WriteLine(vox.WriteBoolState());
                        }
                    }
                }
            }
        }

        private static class Patches
        {
            [HarmonyPatch(typeof(Visible), "OnPool")]
            private static class VisibleIsBeingStubborn
            {
                private static void Prefix(ref Visible __instance)
                {
                    if (__instance.gameObject.name == "VoxTerrainChunk")
                    {
                        __instance.m_ItemType = new ItemTypeInfo(TerrainGenerator.ObjectTypeVoxelChunk, 8192);
                    }
                }
            }

            [HarmonyPatch(typeof(TileManager), "SetTileCache")]
            private static class PleaseStopRemovingMyChunks
            {
                private static bool Prefix(Visible visible, WorldTile newTile, ref bool __result)
                {
                        __result = false;
                        if (visible.name == "VoxTerrainChunk")
                        {
                            newTile.AddVisible(visible);
                            visible.tileCache.tile = newTile;
                            return false;
                        }
                        return true;
                }
            }

            [HarmonyPatch(typeof(ManSaveGame), "CreateStoredVisible")]
            private static class SaveVoxChunks
            {
                private static bool Prefix(ref ManSaveGame.StoredVisible __result, Visible visible)
                {
                    if (visible.gameObject.name == "VoxTerrainChunk")
                    {
                        var result = new TerrainGenerator.VoxTerrain.VoxelSaveData();
                        result.Store(visible);
                        __result = result;
                        //Console.WriteLine(result.Cloud64);
                        return false;
                    }
                    return true;
                }
            }

            [HarmonyPatch(typeof(Visible), "get_rbody")]
            private static class SupressPhysics
            {
                private static bool Prefix(Visible __instance)
                {
                    return __instance.gameObject.name != "VoxTerrainChunk";
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
            //[HarmonyPatch(typeof(ManSpawn), "GetTerrainObjectPrefabFromGUID")]
            //private static class BruteForceVoxTerrainData
            //{
            //    private static void Prefix(ref TerrainObject __result, ref string guid)
            //    {
            //        if (!guid.StartsWith(VoxTerrainGUID))
            //            return;
            //        TerrainGenerator.VoxTerrain.NextBuffer = guid.Substring(VoxTerrainGUID.Length);
            //        guid = VoxTerrainGUID;
            //    }
            //}

            //[HarmonyPatch(typeof(TerrainObject), "get_PrefabGUID")]
            //private static class BruteForceVoxTerrainGUID
            //{
            //    private static bool Prefix(ref TerrainObject __instance, ref string __result)
            //    {
            //        if (__instance is TerrainGenerator.VoxTerrain)
            //        {
            //            __result = VoxTerrainGUID + (__instance as TerrainGenerator.VoxTerrain).GetCloudString();
            //            Console.WriteLine("RESGLT " + __result);
            //            return false;
            //        }
            //        return true;
            //    }
            //}

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
                    if (Physics.Raycast(scenePos + Vector3.one * 0.001f, Vector3.down, out raycasthit, 8192, TerrainGenerator.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
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

            [HarmonyPatch(typeof(Tank), "OnCollisionStay")]
            private static class TerrainCollisionBypassPatch
            {
                private static void Prefix(Tank __instance, Collision collision)
                {
                    var go = collision.GetContact(0).thisCollider.gameObject;
                    if (go.IsTerrain())
                    {
                        var ci = new Tank.CollisionInfo();
                        ci.Init(collision);
                        __instance.CollisionEvent.Send(ci, Tank.CollisionInfo.Event.Stay);
                    }
                }
            }
        }
    }
}
