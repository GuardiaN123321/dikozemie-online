using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TaleWorlds.Core;

namespace Dikozemie.Online
{
    /// <summary>
    /// Developer tool, off for players: with DIKOZEMIE_DUMP_ITEM_STATS set to
    /// a file path, the lobby writes the real statistics of every market item
    /// (the numbers its tooltip shows) to that file. Tools/
    /// build_arena_price_table.py turns them into the market prices, so a
    /// price follows what an item really gives (customer 2026-11-05: a toga
    /// without armour cost 840). Crafted weapons get their damage from parts
    /// only at runtime, so the XML alone cannot tell this.
    /// Reads only TaleWorlds.Core data, like DikozemieItemTooltipVM, and never
    /// ItemObject.Tier: in multiplayer that dereferences the absent campaign
    /// model and kills the client.
    /// </summary>
    internal static class DikozemieItemStatsDump
    {
        private const string EnvironmentVariable = "DIKOZEMIE_DUMP_ITEM_STATS";
        private const int ItemsPerTick = 40;

        private static string _path;
        private static bool _checked;
        private static bool _finished;
        private static float _delay;
        private static List<ItemObject> _items;
        private static int _next;
        private static StringBuilder _rows;
        private static int _failed;

        internal static void Tick(float dt)
        {
            if (_finished)
            {
                return;
            }
            if (!_checked)
            {
                _checked = true;
                _path = Environment.GetEnvironmentVariable(EnvironmentVariable);
                if (string.IsNullOrWhiteSpace(_path))
                {
                    _finished = true;
                    return;
                }
            }

            // Let the lobby settle first: items, cultures and the prepared
            // price table are all loaded by then.
            _delay += Math.Max(0f, dt);
            if (_delay < 12f)
            {
                return;
            }

            if (_items == null)
            {
                _items = DikozemieItemStore.GetCatalog().ToList();
                _rows = new StringBuilder();
                _rows.AppendLine(string.Join("\t", Columns));
                DikozemieHeroMenuController.TraceLifecycle(
                    "item stats dump started count=" + _items.Count +
                    " path=" + _path);
            }

            int end = Math.Min(_items.Count, _next + ItemsPerTick);
            for (; _next < end; _next++)
            {
                try
                {
                    _rows.AppendLine(Describe(_items[_next]));
                }
                catch (Exception error)
                {
                    _failed++;
                    DikozemieHeroMenuController.TraceLifecycle(
                        "item stats dump skipped " + _items[_next]?.StringId +
                        ": " + error.Message);
                }
            }
            if (_next < _items.Count)
            {
                return;
            }

            _finished = true;
            try
            {
                File.WriteAllText(_path, _rows.ToString(), new UTF8Encoding(false));
                DikozemieHeroMenuController.TraceLifecycle(
                    "item stats dump written rows=" + (_items.Count - _failed) +
                    " failed=" + _failed + " path=" + _path);
            }
            catch (Exception error)
            {
                DikozemieHeroMenuController.TraceLifecycle(
                    "item stats dump not written: " + error.Message);
            }
        }

        private static readonly string[] Columns =
        {
            "id", "name", "type", "culture", "price", "weight", "appearance",
            "difficulty", "skill", "civilian", "category", "material",
            "head", "body", "arm", "leg",
            "weaponClass", "swing", "swingType", "thrust", "thrustType",
            "missile", "swingSpeed", "thrustSpeed", "missileSpeed",
            "handling", "length", "accuracy", "maxData", "shieldArmor",
            "usages", "weaponFlags", "template", "pieceTiers",
            "horseSpeed", "horseManeuver", "horseCharge", "horseHp",
            "monster", "packAnimal"
        };

        private static string Describe(ItemObject item)
        {
            var cells = new List<string>
            {
                item.StringId,
                item.Name?.ToString() ?? string.Empty,
                item.ItemType.ToString(),
                item.Culture?.StringId ?? string.Empty,
                Int(DikozemieItemStore.GetBuyPrice(item)),
                Float(item.Weight),
                Float(item.Appearance),
                Int(item.Difficulty),
                item.RelevantSkill?.StringId ?? string.Empty,
                item.IsCivilian ? "1" : "0",
                item.ItemCategory?.StringId ?? string.Empty
            };

            ArmorComponent armor = item.HasArmorComponent ? item.ArmorComponent : null;
            cells.Add(armor?.MaterialType.ToString() ?? string.Empty);
            cells.Add(Int(armor?.HeadArmor ?? 0));
            cells.Add(Int(armor?.BodyArmor ?? 0));
            cells.Add(Int(armor?.ArmArmor ?? 0));
            cells.Add(Int(armor?.LegArmor ?? 0));

            WeaponComponentData weapon = item.HasWeaponComponent ? item.PrimaryWeapon : null;
            cells.Add(weapon?.WeaponClass.ToString() ?? string.Empty);
            cells.Add(Int(weapon?.SwingDamage ?? 0));
            cells.Add(weapon != null && weapon.SwingDamage > 0
                ? weapon.SwingDamageType.ToString()
                : string.Empty);
            cells.Add(Int(weapon?.ThrustDamage ?? 0));
            cells.Add(weapon != null && weapon.ThrustDamage > 0
                ? weapon.ThrustDamageType.ToString()
                : string.Empty);
            cells.Add(Int(weapon?.MissileDamage ?? 0));
            cells.Add(Int(weapon?.SwingSpeed ?? 0));
            cells.Add(Int(weapon?.ThrustSpeed ?? 0));
            cells.Add(Int(weapon?.MissileSpeed ?? 0));
            cells.Add(Int(weapon?.Handling ?? 0));
            cells.Add(Int(weapon?.WeaponLength ?? 0));
            cells.Add(Int(weapon?.Accuracy ?? 0));
            cells.Add(Int(weapon?.MaxDataValue ?? 0));
            cells.Add(Int(weapon?.BodyArmor ?? 0));
            cells.Add(Int(item.Weapons?.Count ?? 0));
            cells.Add(weapon?.WeaponFlags.ToString().Replace(", ", "|") ?? string.Empty);

            string template = string.Empty;
            string pieceTiers = string.Empty;
            try
            {
                WeaponDesign design = item.WeaponDesign;
                if (design != null)
                {
                    template = design.Template?.StringId ?? string.Empty;
                    pieceTiers = string.Join(
                        "|",
                        (design.UsedPieces ?? new WeaponDesignElement[0])
                            .Where(piece => piece?.CraftingPiece != null &&
                                !piece.CraftingPiece.IsEmptyPiece)
                            .Select(piece => piece.CraftingPiece.PieceType + ":" +
                                piece.CraftingPiece.PieceTier));
                }
            }
            catch (Exception)
            {
                // A design that cannot be read leaves the weapon numbers above.
            }
            cells.Add(template);
            cells.Add(pieceTiers);

            HorseComponent horse = item.HasHorseComponent ? item.HorseComponent : null;
            if (horse != null)
            {
                EquipmentElement noHarness = default;
                var element = new EquipmentElement(item);
                cells.Add(Int(element.GetModifiedMountSpeed(in noHarness)));
                cells.Add(Int(element.GetModifiedMountManeuver(in noHarness)));
                cells.Add(Int(element.GetModifiedMountCharge(in noHarness)));
                cells.Add(Int(element.GetModifiedMountHitPoints()));
                cells.Add(horse.Monster?.StringId ?? string.Empty);
                cells.Add(horse.IsPackAnimal ? "1" : "0");
            }
            else
            {
                cells.AddRange(new[] { "0", "0", "0", "0", string.Empty, "0" });
            }

            return string.Join("\t", cells.Select(Clean));
        }

        private static string Int(int value) =>
            value.ToString(CultureInfo.InvariantCulture);

        private static string Float(float value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Clean(string value) =>
            (value ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }
}
