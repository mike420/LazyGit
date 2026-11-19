using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LazyGit;

// Auto Shield: drawing a one-handed weapon equips a shield with it; a two-handed or ranged weapon puts the shield
// away. It watches what the player has in each hand and reacts when that changes.
internal static class ShieldKeeper
{
    // True while the mod itself changes the equipment, so that its own changes are not taken for the player's
    public static bool Busy;

    // What the hands held and whether the weapon was sheathed, the last time this looked
    private static ItemDrop.ItemData lastRight;
    private static bool wasSheathed;
    private static bool cameFromRangedOrTwoHanded;

    // Putting a weapon away with its hotbar key also empties the hand; throwing a spear does as well, and is not that
    private static float ignoreDeselectUntil;

    // Looking now and then as well, in case a change was missed
    private static float seenPlayerAt = -1f;
    private static float nextLook;
    private static int quickLooks;
    private static bool started;

    // Called every frame
    public static void Update()
    {
        if (!MainConfig.shieldEnabled.Value || Time.timeScale == 0f)
        {
            return;
        }

        Player me = Player.m_localPlayer;
        if (me == null || me.IsDead())
        {
            seenPlayerAt = -1f;
            started = false;
            quickLooks = 0;
            return;
        }

        float now = Time.unscaledTime;
        if (seenPlayerAt < 0f)
        {
            seenPlayerAt = now;
            return;
        }
        if (now < seenPlayerAt + 1.5f)
        {
            return;   // (the game is still setting the player up)
        }

        if (!started)
        {
            started = true;
            Remember(me);
            cameFromRangedOrTwoHanded = IsRangedOrTwoHanded(me.GetRightItem());
            Apply();
            quickLooks = 3;
            nextLook = now + 0.25f;
        }
        else if (now >= nextLook)
        {
            nextLook = now + (quickLooks > 0 ? 0.25f : 0.75f);
            quickLooks = Mathf.Max(0, quickLooks - 1);
            Look(me);
        }
    }

    // The player's equipment changed
    public static void OnEquipmentChanged(Humanoid humanoid)
    {
        if (!Busy && MainConfig.shieldEnabled.Value && IsMe(humanoid))
        {
            Look((Player)humanoid);
        }
    }

    // The hands were emptied by sheathing the weapon
    public static void OnHandsHidden(Humanoid humanoid, bool onlyRightHand)
    {
        if (!onlyRightHand && MainConfig.shieldEnabled.Value && MainConfig.shieldUnequipOnSheath.Value && IsMe(humanoid))
        {
            PutShieldAway(humanoid);
        }
    }

    // The sheathed weapon is about to be drawn again
    public static void BeforeHandsShown(Humanoid humanoid, bool onlyRightHand)
    {
        if (!onlyRightHand && MainConfig.shieldEnabled.Value && MainConfig.shieldUnequipOnSheath.Value && IsMe(humanoid) && IsShield(humanoid.m_hiddenLeftItem))
        {
            humanoid.m_hiddenLeftItem = null;   // the shield was put away with the sheath: it is not drawn
        }
    }

    public static void OnHandsShown(Humanoid humanoid, bool onlyRightHand)
    {
        if (!onlyRightHand && MainConfig.shieldEnabled.Value && IsMe(humanoid))
        {
            ignoreDeselectUntil = Time.unscaledTime + 0.35f;
            Remember((Player)humanoid);
            Apply();
        }
    }

    // A weapon is about to leave the hand: if it is being thrown, the empty hand is not a hotbar deselect
    public static void BeforeUnequip(Humanoid humanoid, ItemDrop.ItemData item)
    {
        if (Busy || !MainConfig.shieldEnabled.Value || !IsMe(humanoid) || !IsThrowable(item))
        {
            return;
        }
        if (humanoid.InAttack() || (humanoid.m_currentAttack != null && humanoid.m_currentAttack.m_weapon == item))
        {
            ignoreDeselectUntil = Time.unscaledTime + 1f;
        }
    }

    // An attack that sends a projectile out of a one-handed weapon is a throw
    public static void OnAttackTrigger(Attack attack)
    {
        if (attack != null && MainConfig.shieldEnabled.Value && attack.m_attackProjectile != null && IsMe(attack.m_character)
            && attack.m_weapon?.m_shared != null && attack.m_weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon)
        {
            ignoreDeselectUntil = Time.unscaledTime + 1f;
        }
    }

    // The settings that decide which shield is carried changed: start again with the right one
    public static void Reapply()
    {
        Player me = Player.m_localPlayer;
        if (me == null || me.IsDead())
        {
            return;
        }
        ItemDrop.ItemData left = me.GetLeftItem();
        if (IsShield(left))
        {
            Unequip(me, left);
        }
        Apply();
    }

    private static void Look(Player me)
    {
        ItemDrop.ItemData right = me.GetRightItem();
        ItemDrop.ItemData left = me.GetLeftItem();
        bool sheathed = IsSheathed(me);

        // Sheathed with the shield still showing, when it should be put away too
        if (MainConfig.shieldUnequipOnSheath.Value && sheathed && ShieldStillShowing(me))
        {
            Remember(me);
            Apply();
            return;
        }

        if (right == lastRight && sheathed == wasSheathed)
        {
            return;   // nothing changed
        }

        // The weapon was put away with its hotbar key: the shield goes too
        if (MainConfig.shieldUnequipOnDeselect.Value && Time.unscaledTime >= ignoreDeselectUntil && right == null && !sheathed
            && lastRight != null && IsOneHanded(lastRight) && IsShield(left))
        {
            Remember(me);
            Unequip(me, left);
            return;
        }

        if (IsRangedOrTwoHanded(right))
        {
            cameFromRangedOrTwoHanded = true;
        }
        Remember(me);
        Apply();
    }

    private static void Remember(Player me)
    {
        lastRight = me.GetRightItem();
        wasSheathed = IsSheathed(me);
    }

    // Does what is wanted for the weapon in the right hand now
    private static void Apply()
    {
        Player me = Player.m_localPlayer;
        if (Busy || !MainConfig.shieldEnabled.Value || me == null || me.IsDead())
        {
            return;
        }

        try
        {
            ApplyTo(me);
        }
        catch (Exception e)
        {
            Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Auto Shield could not change the shield: {e.Message}");
        }
    }

    private static void ApplyTo(Player me)
    {
        ItemDrop.ItemData right = me.GetRightItem();
        ItemDrop.ItemData left = me.GetLeftItem();
        bool cameFromRanged = cameFromRangedOrTwoHanded;
        cameFromRangedOrTwoHanded = IsRangedOrTwoHanded(right);

        if (IsSheathed(me))
        {
            if (MainConfig.shieldUnequipOnSheath.Value)
            {
                PutShieldAway(me);
            }
            return;
        }
        if (right == null)
        {
            return;
        }

        if (IsRangedOrTwoHanded(right))
        {
            if (MainConfig.shieldUnequipOnTwoHanded.Value && IsShield(left))
            {
                Unequip(me, left);
            }
            return;
        }
        if (!IsOneHanded(right) || !WeaponAllowed(right))
        {
            return;
        }

        // Tools that happen to be one-handed weapons: no shield for chopping trees or skinning, unless asked for
        bool noShield = (!MainConfig.shieldWithAxe.Value && right.m_shared.m_skillType == Skills.SkillType.Axes)
            || (!MainConfig.shieldWithButcherKnife.Value && PrefabName(right).Equals("KnifeButcher", StringComparison.OrdinalIgnoreCase));
        if (noShield)
        {
            if (IsShield(left))
            {
                Unequip(me, left);
            }
            return;
        }

        bool wanted = MainConfig.shieldAutoEquip.Value || (MainConfig.shieldAfterRanged.Value && cameFromRanged);
        if (wanted && left == null)
        {
            ItemDrop.ItemData shield = ChooseShield(me);
            if (shield != null)
            {
                Equip(me, shield);
            }
        }
    }

    // Takes the shield off, and out of sight on the back too (a sheathed weapon is shown on the back with its shield)
    private static void PutShieldAway(Humanoid humanoid)
    {
        bool changed = false;
        ItemDrop.ItemData left = humanoid.GetLeftItem();
        if (IsShield(left))
        {
            Unequip(humanoid, left);
            changed = true;
        }
        if (IsShield(humanoid.m_hiddenLeftItem))
        {
            humanoid.m_hiddenLeftItem = null;
            changed = true;
        }

        VisEquipment look = humanoid.m_visEquipment;
        if (look == null)
        {
            return;
        }
        bool onBack = look.m_leftBackItemInstance != null || look.m_currentLeftBackItemHash != 0;
        if (changed || onBack)
        {
            look.SetLeftBackItem(0, 0, 0);
            if (look.m_leftBackItemInstance != null)
            {
                UnityEngine.Object.Destroy(look.m_leftBackItemInstance);
                look.m_leftBackItemInstance = null;
            }
            look.m_currentLeftBackItemHash = 0;
            look.m_currentLeftBackItemVariant = 0;
            look.m_currentLeftBackItemQuality = 0;
            look.UpdateEquipmentVisuals();
        }
    }

    private static bool ShieldStillShowing(Humanoid humanoid)
    {
        if (IsShield(humanoid.GetLeftItem()) || IsShield(humanoid.m_hiddenLeftItem))
        {
            return true;
        }
        VisEquipment look = humanoid.m_visEquipment;
        return look != null && (look.m_leftBackItemInstance != null || look.m_currentLeftBackItemHash != 0);
    }

    // The shield to equip, by the chosen way of picking: the first in the inventory, the one that blocks best, or a
    // named one (the best if that is not carried). Shields on the never list are not used.
    private static ItemDrop.ItemData ChooseShield(Player me)
    {
        HashSet<string> never = new HashSet<string>(Split(MainConfig.shieldNever.Value), StringComparer.OrdinalIgnoreCase);
        List<ItemDrop.ItemData> shields = me.GetInventory().GetAllItems().Where(i => IsShield(i) && !never.Contains(PrefabName(i))).ToList();

        string mode = (MainConfig.shieldMode.Value ?? "First").Trim();
        if (mode.Equals("First", StringComparison.OrdinalIgnoreCase))
        {
            return shields.FirstOrDefault();
        }

        string named = (MainConfig.shieldNamed.Value ?? "").Trim();
        if (mode.Equals("Preferred", StringComparison.OrdinalIgnoreCase) && named.Length > 0)
        {
            ItemDrop.ItemData found = shields.FirstOrDefault(s => PrefabName(s).Equals(named, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                return found;
            }
        }
        return shields.OrderByDescending(s => s.m_shared.m_blockPower + s.m_shared.m_timedBlockBonus).FirstOrDefault();
    }

    // The weapon types, skills or names the shield is used with (empty: every one-handed weapon)
    private static bool WeaponAllowed(ItemDrop.ItemData weapon)
    {
        List<string> allowed = Split(MainConfig.shieldWeapons.Value);
        if (allowed.Count == 0)
        {
            return true;
        }
        return allowed.Any(a => a.Equals(weapon.m_shared.m_itemType.ToString(), StringComparison.OrdinalIgnoreCase)
            || a.Equals(weapon.m_shared.m_skillType.ToString(), StringComparison.OrdinalIgnoreCase)
            || a.Equals(PrefabName(weapon), StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> Split(string text)
    {
        return (text ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
    }

    private static void Equip(Humanoid humanoid, ItemDrop.ItemData item)
    {
        Busy = true;
        try
        {
            humanoid.EquipItem(item, false);
        }
        finally
        {
            Busy = false;
        }
    }

    private static void Unequip(Humanoid humanoid, ItemDrop.ItemData item)
    {
        Busy = true;
        try
        {
            humanoid.UnequipItem(item, false);
        }
        finally
        {
            Busy = false;
        }
    }

    private static bool IsMe(Humanoid humanoid)
    {
        return humanoid != null && Player.m_localPlayer != null && humanoid == Player.m_localPlayer;
    }

    // Weapon sheathed: nothing in the right hand, but the weapon is carried on the back
    private static bool IsSheathed(Humanoid humanoid)
    {
        return humanoid.GetRightItem() == null && humanoid.m_hiddenRightItem != null;
    }

    private static bool IsShield(ItemDrop.ItemData item)
    {
        return item?.m_shared != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield;
    }

    private static bool IsOneHanded(ItemDrop.ItemData item)
    {
        return item?.m_shared != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon;
    }

    private static bool IsRangedOrTwoHanded(ItemDrop.ItemData item)
    {
        if (item?.m_shared == null)
        {
            return false;
        }
        ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
        return type == ItemDrop.ItemData.ItemType.Bow || type == ItemDrop.ItemData.ItemType.TwoHandedWeapon
            || type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || type == ItemDrop.ItemData.ItemType.Attach_Atgeir;
    }

    // A one-handed weapon that can be thrown: a spear, or anything that sends a projectile
    private static bool IsThrowable(ItemDrop.ItemData item)
    {
        if (!IsOneHanded(item))
        {
            return false;
        }
        return item.m_shared.m_skillType == Skills.SkillType.Spears
            || item.m_shared.m_attack?.m_attackProjectile != null || item.m_shared.m_secondaryAttack?.m_attackProjectile != null;
    }

    private static string PrefabName(ItemDrop.ItemData item)
    {
        return item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name ?? "";
    }
}
