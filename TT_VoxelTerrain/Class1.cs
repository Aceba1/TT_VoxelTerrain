using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using UnityEngine;

namespace TT_VoxelTerrain
{
    public class Class1
    {
        public static void Init()
        {
            HarmonyInstance.Create("aceba1.betterterrain").PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            new GameObject().AddComponent<MassShifter>();
            Singleton.DoOnceAfterStart(SingtonStarted);
        }

        private static void SingtonStarted()
        {
            var b = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(CameraManager).GetField("m_UnderGroundTolerance", b).SetValue(CameraManager.inst, 10000f);
            TankCamera.inst.groundClearance = -10000f;
        }

        internal class MassShifter : MonoBehaviour
        {

            int brushSize = 6;
            void Update()
            {
                if (Input.GetKeyDown(KeyCode.KeypadPlus)) brushSize++;
                if (Input.GetKeyDown(KeyCode.KeypadMinus)) brushSize = Math.Max(brushSize - 1,1);

                if (Physics.Raycast(Singleton.camera.ScreenPointToRay(Input.mousePosition), out var raycastHit, 10000, TerrainGenerator.TerrainOnlyLayer, QueryTriggerInteraction.Ignore))
                {
                    TerrainGenerator.VoxTerrain vox = raycastHit.transform.gameObject.GetComponentInParent<TerrainGenerator.VoxTerrain>();
                    if (vox != null)
                    {
                        if (Input.GetKey(KeyCode.Equals))
                        {
                            vox.BleedBrushModifyBuffer(raycastHit.point, brushSize / TerrainGenerator.voxelSize, .05f);
                        }
                        if (Input.GetKey(KeyCode.Minus))
                        {
                            vox.BleedBrushModifyBuffer(raycastHit.point, brushSize / TerrainGenerator.voxelSize, -.05f);
                        }
                    }
                }
            }
        }

        private static class Patches
        {
            [HarmonyPatch(typeof(TileManager), "CreateTile")]
            private static class ReplaceTile
            {
                private static void Postfix(WorldTile tile)
                {
                    tile.Terrain.gameObject.AddComponent<TerrainGenerator>();//.worldTile = tile;
                }
            }

            [HarmonyPatch(typeof(TileManager), "GetTerrainHeightAtPosition")]
            private static class ReplaceHeightGet
            {
                private static bool Prefix(ref float __result, Vector3 scenePos, bool forceCalculate)
                {
                    if (/*!forceCalculate && */Physics.Raycast(scenePos, Vector3.down, out RaycastHit raycasthit, 8192, TerrainGenerator.TerrainOnlyLayer, QueryTriggerInteraction.Ignore) && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>())
                    {
                        __result = raycasthit.point.y;
                        return false;
                    }
                    return true;
                }
            }
        }
    }
}
