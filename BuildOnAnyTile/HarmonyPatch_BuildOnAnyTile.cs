﻿using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using System;
using xTile.Dimensions;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace BuildOnAnyTile
{
    /// <summary>A Harmony patch that makes <see cref="BuildableGameLocation"/> allow buildings to be placed on any tile (depending on <see cref="ModConfig"/> settings).</summary>
    public static class HarmonyPatch_BuildOnAnyTile
    {
        public static void ApplyPatch(Harmony harmony)
        {
            ModEntry.Instance.Monitor.Log($"Applying Harmony patch \"{nameof(HarmonyPatch_BuildOnAnyTile)}\": prefixing SDV method \"BuildableGameLocation.isBuildable(Vector2)\".", LogLevel.Trace);
            harmony.Patch(
                original: AccessTools.Method(typeof(BuildableGameLocation), nameof(BuildableGameLocation.isBuildable), new[] { typeof(Vector2) }),
                prefix: new HarmonyMethod(typeof(HarmonyPatch_BuildOnAnyTile), nameof(IsBuildable))
            );
        }

        /// <summary>A Harmony prefix patch that causes <see cref="BuildableGameLocation.isBuildable(Vector2)"/> to return true under more conditions.</summary>
        /// <param name="__instance">The buildable location on which this method is called.</param>
        /// <param name="tileLocation">The tile being checked.</param>
        /// <param name="__result">True if this tile should allow <see cref="Building"/> placement.</param>
        /// <returns>False if the original method (and any other patches) should be skipped.</returns>
        public static bool IsBuildable(BuildableGameLocation __instance, Vector2 tileLocation, ref bool __result)
        {
            try
            {
                if (ModEntry.Config.EverythingEnabled()) //if every feature is enabled
                {
                    __result = true; //this tile is buildable
                    return false; //skip the original method
                }

                if (ModEntry.Config.EverythingDisabled()) //if every feature is disabled
                    return true; //run the original method (do nothing)

                if (ModEntry.Config.BuildOnAllTerrainFeatures == false) //if most terrain features should prevent building (based on the original method's behavior)
                {
                    Rectangle tileLocationRect = new Rectangle((int)tileLocation.X * 64, (int)tileLocation.Y * 64, 64, 64); //get a rectangle representing this tile

                    if (__instance.terrainFeatures.TryGetValue(tileLocation, out TerrainFeature feature) //if this tile has a terrain feature
                        && tileLocationRect.Intersects(feature.getBoundingBox(tileLocation))) //AND the feature's box overlaps with the tile (note: copied from GameLocation.isOccupiedForPlacement)
                    {
                        if (!__instance.terrainFeatures[tileLocation].isPassable() //if the feature is impassable
                            || (feature is HoeDirt dirt && dirt.crop != null)) //OR the feature is a crop
                        {
                            __result = false; //this tile is NOT buildable
                            return false; //skip the original method
                        }
                    }
                        
                }

                if (ModEntry.Config.BuildOnOtherBuildings == false) //if collision with other buildings should prevent building
                {
                    foreach (Building building in __instance.buildings) //for each existing building
                    {
                        if (building.isTileOccupiedForPlacement(tileLocation, null)) //if this building occupies this tile
                        {
                            __result = false; //this tile is NOT buildable
                            return false; //skip the original method
                        }
                    }
                }

                if (ModEntry.Config.BuildOnWater == false) //if water should prevent building
                {
                    if (__instance.doesTileHaveProperty((int)tileLocation.X, (int)tileLocation.Y, "Water", "Back") != null //if this tile is water
                        && __instance.doesTileHaveProperty((int)tileLocation.X, (int)tileLocation.Y, "Passable", "Buildings") == null) //AND this tile does NOT specifically allow buildings
                    {
                        __result = false; //this tile is NOT buildable
                        return false; //skip the original method
                    }
                }

                if (ModEntry.Config.BuildOnImpassableTiles == false) //if impassable tiles should prevent building
                {
                    if (ModEntry.Config.BuildOnWater == false || __instance.isOpenWater((int)tileLocation.X, (int)tileLocation.Y) == false) //if this tile is NOT specifically allowed by the water setting
                    {
                        if (__instance.isTileOccupiedForPlacement(tileLocation) //if this tile is occupied
                            || __instance.isTilePassable(new Location((int)tileLocation.X, (int)tileLocation.Y), Game1.viewport) == false) //OR if this tile is NOT passable
                        {
                            __result = false; //this tile is NOT buildable
                            return false; //skip the original method
                        }
                    }
                }

                if (ModEntry.Config.BuildOnNoFurnitureTiles == false) //if "no furniture" tiles should prevent building
                {
                    if (__instance.doesTileHaveProperty((int)tileLocation.X, (int)tileLocation.Y, "NoFurniture", "Back") != null) //if this tile has a "NoFurniture" property
                    {
                        __result = false; //this tile is NOT buildable
                        return false; //skip the original method
                    }
                }

                if (ModEntry.Config.BuildOnCaveAndShippingZones == false) //if "no build" zones should prevent building
                {
                    //NOTE: as of SDV 1.5.5, the static preset rectangle "zones" no longer exist, and farm maps use the tile property "Buildable" "f" instead;
                    //that tile property may exist elsewhere too, but checking relevant properties here should be understandable enough

                    string buildableValue = __instance.doesTileHavePropertyNoNull((int)tileLocation.X, (int)tileLocation.Y, "Buildable", "Back"); //get the value of this tile's "Buildable" property ("" if null)
                    if (buildableValue.Equals("f", StringComparison.OrdinalIgnoreCase) || buildableValue.Equals("false", StringComparison.OrdinalIgnoreCase)) //if the value is false
                    {
                        __result = false; //this tile is NOT buildable
                        return false; //skip the original method
                    }
                }

                //all checks have been successful

                __result = true; //this tile is buildable
                return false; //skip the original method
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.LogOnce($"Encountered an error in Harmony patch \"{nameof(HarmonyPatch_BuildOnAnyTile)}\". The default building rules will be used instead. Full error message:\n-----\n{ex.ToString()}", LogLevel.Error);
                return true; //run the original method
            }
        }
    }
}
