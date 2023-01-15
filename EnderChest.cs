using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace EnderChest
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class EnderChest : BaseUnityPlugin
    {
        const string GUID = "kg.enderchest";
        const string NAME = "EnderChest";
        const string VERSION = "1.0.0";

        private static GameObject EnderChest_Prefab;
        private static ConfigEntry<int> Width;
        private static ConfigEntry<int> Height;
        private static ConfigEntry<string> BlockedItems;

        private readonly ConfigSync configSync = new(GUID) { DisplayName = NAME };
        
        private void Awake()
        {
            Width = config("EnderChest", "Width", 8, "Width of the EnderChest");
            Height = config("EnderChest", "Height", 4, "Height of the EnderChest");
            BlockedItems = config("EnderChest", "Blocked Items", "Coins,","Blocked items for chest transfer ");
            EnderChest_Prefab = GetAssetBundle("enderchest").LoadAsset<GameObject>("kg_EnderChest");
            new Harmony(GUID).PatchAll();
        }
        
        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true) =>
            config(group, name, value, new ConfigDescription(description), synchronizedSetting);

        private static AssetBundle GetAssetBundle(string filename)
        {
            Assembly execAssembly = Assembly.GetExecutingAssembly();
            string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(filename));
            using Stream stream = execAssembly.GetManifestResourceStream(resourceName);
            return AssetBundle.LoadFromStream(stream);
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        static class ZNetScene_Awake_Patch
        {
            static void Postfix(ZNetScene __instance)
            {
                __instance.m_namedPrefabs[EnderChest_Prefab.name.GetStableHashCode()] = EnderChest_Prefab;
                var hammer = __instance.GetPrefab("Hammer").GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces
                    .m_pieces;
                if (!hammer.Contains(EnderChest_Prefab)) hammer.Add(EnderChest_Prefab);


                List<Piece.Requirement> reqs = new();
                reqs.Add(new Piece.Requirement()
                {
                    m_resItem = __instance.GetPrefab("Thunderstone").GetComponent<ItemDrop>(),
                    m_amount = 1,
                    m_recover = true
                });
                reqs.Add(new Piece.Requirement()
                {
                    m_resItem = __instance.GetPrefab("SurtlingCore").GetComponent<ItemDrop>(),
                    m_amount = 10,
                    m_recover = true
                });
                reqs.Add(new Piece.Requirement()
                {  
                    m_resItem = __instance.GetPrefab("Bronze").GetComponent<ItemDrop>(),
                    m_amount = 5,
                    m_recover = true
                });
                reqs.Add(new Piece.Requirement()
                {
                    m_resItem = __instance.GetPrefab("Stone").GetComponent<ItemDrop>(),
                    m_amount = 100,
                    m_recover = true
                });
                reqs.Add(new Piece.Requirement()
                {
                    m_resItem = __instance.GetPrefab("DragonTear").GetComponent<ItemDrop>(),
                    m_amount = 10,
                    m_recover = true
                });
                EnderChest_Prefab.GetComponent<Piece>().m_resources = reqs.ToArray();
                
            }
        }


        [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
        static class Container_Awake_Patch
        {
            static void Prefix(Container __instance)
            {
                if (Utils.GetPrefabName(__instance.gameObject) == EnderChest_Prefab.name)
                {
                    __instance.m_width = Width.Value;
                    __instance.m_height = Height.Value;
                }
            }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.Save))]
        static class Container_Save_Patch
        {
            static bool Prefix(Container __instance)
            {
                if (Utils.GetPrefabName(__instance.gameObject) == EnderChest_Prefab.name)
                {
                    Player p = Player.m_localPlayer;
                    if (!p) return false;
                    ZPackage zpackage = new ZPackage();
                    __instance.m_inventory.Save(zpackage);
                    p.m_customData["kg_EnderChestData"] = zpackage.GetBase64();
                    __instance.m_lastDataString = p.m_customData["kg_EnderChestData"];
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.Load))]
        static class Container_Load_Patch
        {
            static bool Prefix(Container __instance)
            {
                if (Utils.GetPrefabName(__instance.gameObject) == EnderChest_Prefab.name)
                {
                    Player p = Player.m_localPlayer;
                    if (!p) return false;
                    if (p.m_customData.TryGetValue("kg_EnderChestData", out var data))
                    {
                        if (__instance.m_lastDataString == data) return false;
                        __instance.m_lastDataString = data;
                        ZPackage pkg = new ZPackage(data);
                        __instance.m_loading = true;
                        __instance.m_inventory.Load(pkg);
                        __instance.m_loading = false;
                    }

                    return false;
                }

                return true;
            }
        }


        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
        static class InventoryGui_OnSelectedItem_Patch
        {
            static bool Prefix(InventoryGui __instance, ItemDrop.ItemData item)
            {
                if (item == null || __instance.m_currentContainer is not { } container || Utils.GetPrefabName(container.gameObject) != EnderChest_Prefab.name) return true;
                if (BlockedItems.Value.Split(',').Contains(item.m_dropPrefab?.name))
                {
                    return false;
                }

                return true;
            }
        }
    }
}