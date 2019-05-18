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
        }

        private class MassShifter : MonoBehaviour
        {
            void Update()
            {

                if (Physics.Raycast(Singleton.camera.ScreenPointToRay(Input.mousePosition), out var raycastHit, 100, Globals.inst.layerTerrainOnly, QueryTriggerInteraction.Ignore))
                {
                    TerrainGenerator.VoxTerrain vox = raycastHit.collider.GetComponent<TerrainGenerator.VoxTerrain>();
                    if (vox != null)
                    {
                        if (Input.GetKey(KeyCode.Equals))
                        {
                            vox.ModifyBufferPoint(raycastHit.point, 0.1f);
                        }
                        if (Input.GetKey(KeyCode.Minus))
                        {
                            vox.ModifyBufferPoint(raycastHit.point, -0.1f);
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
                    tile.Terrain.gameObject.AddComponent<TerrainGenerator>();
                }
            }
        }
    }
}
