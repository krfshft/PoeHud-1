﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PoeHUD.Controllers;
using PoeHUD.Framework;
using PoeHUD.Hud.MaxRolls;
using PoeHUD.Hud.UI;
using PoeHUD.Models.Enums;
using PoeHUD.Poe;
using PoeHUD.Poe.Components;
using PoeHUD.Poe.Elements;
using PoeHUD.Poe.FilesInMemory;
using PoeHUD.Poe.RemoteMemoryObjects;
using PoeHUD.Poe.UI;

using SharpDX;
using SharpDX.Direct3D9;

namespace PoeHUD.Hud.Dps
{
    public sealed class WeaponDpsPlugin : Plugin<WeaponDpsSettings>
    {
        private Entity itemEntity;
        private List<RollValue> mods = new List<RollValue>();

        public WeaponDpsPlugin(GameController gameController, Graphics graphics, WeaponDpsSettings settings)
            : base(gameController, graphics, settings) {}

        public override void Render(Dictionary<UiMountPoint, Vector2> mountPoints)
        {
            if (!Settings.Enable)
            return;

            Element uiHover = this.GameController.Game.IngameState.UIHover;

            Element tooltip = uiHover.AsObject<InventoryItemIcon>().Tooltip;
            if (tooltip == null)
                return;
            Element childAtIndex1 = tooltip.GetChildAtIndex(0);
            if (childAtIndex1 == null)
                return;
            Element childAtIndex2 = childAtIndex1.GetChildAtIndex(1);
            if (childAtIndex2 == null)
                return;
            var clientRect = childAtIndex2.GetClientRect();

            Entity poeEntity = uiHover.AsObject<InventoryItemIcon>().Item;
            if (poeEntity.Address == 0 || !poeEntity.IsValid)
                return;
            if (this.itemEntity == null || this.itemEntity.Id != poeEntity.Id)
            {
                this.mods = new List<RollValue>();
                List<ItemMod> expMods = poeEntity.GetComponent<Mods>().ItemMods;
                int ilvl = poeEntity.GetComponent<Mods>().ItemLevel;
                foreach (ItemMod item in expMods)
                    this.mods.Add(new RollValue(item, GameController.Files, ilvl));
                this.itemEntity = poeEntity;
            }

            if (poeEntity.HasComponent<Weapon>())
            {
                RenderWeaponStats(clientRect);
            }
        }

        private static readonly Color[] eleCols = new[] { Color.White, HudSkin.DmgFireColor, HudSkin.DmgColdColor, HudSkin.DmgLightingColor, HudSkin.DmgChaosColor };

        private void RenderWeaponStats(RectangleF clientRect)
        {
            Weapon weapon = itemEntity.GetComponent<Weapon>();
            float aSpd = ((float)1000) / weapon.AttackTime;
            int cntDamages = Enum.GetValues(typeof(DamageType)).Length;
            float[] doubleDpsPerStat = new float[cntDamages];
            float physDmgMultiplier = 1;
            doubleDpsPerStat[(int)DamageType.Physical] = weapon.DamageMax + weapon.DamageMin;
            foreach (RollValue roll in mods)
            {
                for (int iStat = 0; iStat < 4; iStat++)
                {
                    IntRange range = roll.TheMod.StatRange[iStat];
                    if (range.Min == 0 && range.Max == 0)
                        continue;

                    StatsDat.StatRecord theStat = roll.TheMod.StatNames[iStat];
                    int val = roll.StatValue[iStat];
                    switch (theStat.Key)
                    {
                        case "physical_damage_+%":
                        case "local_physical_damage_+%":
                            physDmgMultiplier += val / 100f;
                            break;
                        case "local_attack_speed_+%":
                            aSpd *= (100f + val) / 100;
                            break;
                        case "local_minimum_added_physical_damage":
                        case "local_maximum_added_physical_damage":
                            doubleDpsPerStat[(int)DamageType.Physical] += val;
                            break;
                        case "local_minimum_added_fire_damage":
                        case "local_maximum_added_fire_damage":
                        case "unique_local_minimum_added_fire_damage_when_in_main_hand":
                        case "unique_local_maximum_added_fire_damage_when_in_main_hand":
                            doubleDpsPerStat[(int)DamageType.Fire] += val;
                            break;
                        case "local_minimum_added_cold_damage":
                        case "local_maximum_added_cold_damage":
                        case "unique_local_minimum_added_cold_damage_when_in_off_hand":
                        case "unique_local_maximum_added_cold_damage_when_in_off_hand":
                            doubleDpsPerStat[(int)DamageType.Cold] += val;
                            break;
                        case "local_minimum_added_lightning_damage":
                        case "local_maximum_added_lightning_damage":
                            doubleDpsPerStat[(int)DamageType.Lightning] += val;
                            break;
                        case "unique_local_minimum_added_chaos_damage_when_in_off_hand":
                        case "unique_local_maximum_added_chaos_damage_when_in_off_hand":
                        case "local_minimum_added_chaos_damage":
                        case "local_maximum_added_chaos_damage":
                            doubleDpsPerStat[(int)DamageType.Chaos] += val;
                            break;

                    }
                }
            }

            doubleDpsPerStat[(int)DamageType.Physical] *= physDmgMultiplier;
            var quality = itemEntity.GetComponent<Quality>().ItemQuality;
            if (quality > 0)
                doubleDpsPerStat[(int)DamageType.Physical] += (weapon.DamageMax + weapon.DamageMin) * quality / 100f;
            float pDps = doubleDpsPerStat[(int)DamageType.Physical] / 2 * aSpd;

            float eDps = 0;
            int firstEmg = 0;
            Color eDpsColor = Color.White;

            for (int i = 1; i < cntDamages; i++)
            {
                eDps += doubleDpsPerStat[i] / 2 * aSpd;
                if (doubleDpsPerStat[i] > 0)
                {
                    if (firstEmg == 0)
                    {
                        firstEmg = i;
                        eDpsColor = eleCols[i];
                    }
                    else
                    {
                        eDpsColor = Color.DarkViolet;
                    }
                }
            }

            Size2 sz = new Size2();
            if (pDps > 0)
                sz = Graphics.DrawText(pDps.ToString("#.#"), Settings.DpsTextSize, new Vector2(clientRect.X + clientRect.Width - Settings.OffsetInnerX, clientRect.Y + Settings.OffsetInnerY), Color.White, FontDrawFlags.Right);
            Size2 sz2 = new Size2();
            if (eDps > 0)
                sz2 = Graphics.DrawText(eDps.ToString("#.#"), Settings.DpsTextSize, new Vector2(clientRect.X + clientRect.Width - Settings.OffsetInnerX, clientRect.Y + Settings.OffsetInnerY + sz.Height), eDpsColor, FontDrawFlags.Right);
            Graphics.DrawText("DPS", Settings.DpsNameTextSize, new Vector2(clientRect.X + clientRect.Width - Settings.OffsetInnerX, clientRect.Y + Settings.OffsetInnerY + sz.Height + sz2.Height), Color.White, FontDrawFlags.Right);
        }
    }
}