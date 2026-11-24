// Prepares multiplayer-safe copies of the installed single-player catalogue.
// Bannerlord normally never loads Native/mpitems and SandBoxCore/items in the
// same game type. Eleven siege objects share ids, so remove only those known
// duplicates and leave the player's official game data otherwise untouched.
(function () {
    var args = WScript.Arguments;
    if (args.length !== 3 && !(args.length === 4 && String(args.Item(3)) === "server")) {
        WScript.Echo("[ERROR] Expected: items-source crafting-pieces-source destination [server]");
        WScript.Quit(2);
    }
    // The dedicated battle servers run without War Sails. There every War
    // Sails crafted weapon is built from pieces the engine calls "not a valid
    // valid anymore" (coh_axe_head_1, naval_axe_head_08_2, ...), and the first
    // one throws inside ItemObject.Deserialize: MBObjectManager stops loading
    // the merged Items document there, so shields, shoulder armour, tournament
    // weapons and every weapon of weapons.xml were unknown to the servers and
    // heroes carrying them were refused (found 2026-11-10). Since 1.5.7 the
    // client fails the same way (War Sails adds its pieces to the crafting
    // templates only for campaign and custom games), and server and client
    // must register the very same items anyway: network messages name items
    // by their registration order. Every catalogue keeps War Sails clothing
    // and drops its crafted weapons; "server" only marks the manifest.
    var serverCatalog = args.length === 4;

    var fso = new ActiveXObject("Scripting.FileSystemObject");
    // The callers pass absolute paths. Keeping them verbatim also avoids an
    // old Windows Script Host quirk that can prefix the current drive twice.
    var itemsSource = String(args.Item(0));
    var craftingSource = String(args.Item(1));
    var destination = String(args.Item(2));
    var itemsDestination = fso.BuildPath(destination, "items");
    var modulesRoot = fso.GetParentFolderName(
        fso.GetParentFolderName(
            fso.GetParentFolderName(itemsSource)));
    var nativeMpItems = fso.BuildPath(
        fso.BuildPath(
            fso.BuildPath(modulesRoot, "Native"),
            "ModuleData"),
        "mpitems.xml");
    var nativeMpCraftingPieces = fso.BuildPath(
        fso.GetParentFolderName(nativeMpItems),
        "mp_crafting_pieces.xml");
    var nativeMpCharacters = fso.BuildPath(
        fso.GetParentFolderName(nativeMpItems),
        "mpcharacters.xml");
    var sandboxEquipmentSets = fso.BuildPath(
        fso.BuildPath(fso.BuildPath(modulesRoot, "SandBox"), "ModuleData"),
        "sandbox_equipment_sets.xml");
    var sandboxCoreEquipmentSets = fso.BuildPath(
        fso.BuildPath(fso.BuildPath(modulesRoot, "SandBoxCore"), "ModuleData"),
        "sandboxcore_equipment_sets.xml");
    var sandboxBodyProperties = fso.BuildPath(
        fso.BuildPath(fso.BuildPath(modulesRoot, "SandBox"), "ModuleData"),
        "sandbox_bodyproperties.xml");
    var navalModuleData = fso.BuildPath(
        fso.BuildPath(modulesRoot, "NavalDLC"),
        "ModuleData");
    var navalEquipmentSets = fso.BuildPath(
        navalModuleData,
        "naval_equipment_sets.xml");
    var navalBodyProperties = fso.BuildPath(
        navalModuleData,
        "naval_bodyproperties.xml");
    var navalCraftingSource = fso.BuildPath(
        navalModuleData,
        "naval_crafting_pieces.xml");
    var moduleDataDestination = fso.GetParentFolderName(destination);
    var languagesDestination = fso.BuildPath(
        moduleDataDestination,
        "Languages");
    var russianLanguagesDestination = fso.BuildPath(
        languagesDestination,
        "RU");
    var sandboxLanguages = fso.BuildPath(
        fso.BuildPath(fso.BuildPath(modulesRoot, "SandBox"), "ModuleData"),
        "Languages");
    var navalLanguages = fso.BuildPath(navalModuleData, "Languages");
    var duplicateIds = {
        "ballista_projectile": true,
        "ballista_projectile_burning": true,
        "boulder": true,
        "boulder_projectile": true,
        "grapeshot_fire_projectile": true,
        "grapeshot_fire_stack": true,
        "grapeshot_projectile": true,
        "grapeshot_stack": true,
        "pot": true,
        "pot_projectile": true,
        "push_fork": true
    };
    // SubModule.xml must name every item file of the catalogue, one by one
    // (a folder is read in directory order, which differs between Windows
    // and the Linux battle servers). Checked when the module's SubModule.xml
    // is next to the catalogue (installation, release stage).
    var itemFilesListed = "not-checked";
    function checkItemFilesListedInSubModule() {
        var moduleFolder = fso.GetParentFolderName(
            fso.GetParentFolderName(destination));
        var subModulePath = fso.BuildPath(moduleFolder, "SubModule.xml");
        if (!fso.FileExists(subModulePath)) {
            return;
        }
        var subModule = new ActiveXObject("Msxml2.DOMDocument.6.0");
        subModule.async = false;
        if (!subModule.load(subModulePath)) {
            fail("Cannot parse " + subModulePath + ": " +
                subModule.parseError.reason, 17);
        }
        var listed = {};
        var listedCount = 0;
        var names = subModule.selectNodes(
            "/Module/Xmls/XmlNode/XmlName[@id='Items']");
        for (var nameIndex = 0; nameIndex < names.length; nameIndex++) {
            var itemPath = String(names.item(nameIndex).getAttribute("path") || "");
            if (itemPath === "singleplayer_catalog/items") {
                fail("SubModule.xml loads the whole item folder: list every file instead", 17);
            }
            if (itemPath.indexOf("singleplayer_catalog/items/") === 0) {
                listed[itemPath.substring("singleplayer_catalog/items/".length).toLowerCase()] = true;
                listedCount++;
            }
        }
        var files = new Enumerator(fso.GetFolder(itemsDestination).Files);
        var fileCount = 0;
        for (; !files.atEnd(); files.moveNext()) {
            var fileName = String(files.item().Name);
            if (!/\.xml$/i.test(fileName)) {
                continue;
            }
            fileCount++;
            var stem = fileName.substring(0, fileName.length - 4).toLowerCase();
            if (!listed[stem]) {
                fail("SubModule.xml does not list the item file " + fileName, 17);
            }
        }
        if (fileCount !== listedCount) {
            fail("SubModule.xml lists " + listedCount + " item files, the catalogue has " + fileCount, 17);
        }
        itemFilesListed = String(fileCount);
    }
    var requiredItemFiles = [
        "arm_armors.xml",
        "banners.xml",
        "body_armors.xml",
        "head_armors.xml",
        "horses_and_others.xml",
        "leg_armors.xml",
        "shields.xml",
        "shoulder_armors.xml",
        "tournament_weapons.xml",
        "weapons.xml"
    ];
    // Market prices (customer 2026-11-05: by the item's own numbers, historically):
    // one Arena coin is one silver penny, and each item is priced by what it
    // really gives. Tools/build_arena_price_table.py builds that table from
    // the game's own item numbers; 0 there means "not sold" (siege ammo,
    // TaleWorlds test items). The old campaign curve below only prices an
    // item the table does not know yet (a newer game version), scaled down
    // to pennies.
    var pricePolicy = "historical-penny-v2";
    var historicalPricesPath = fso.BuildPath(
        fso.GetParentFolderName(destination),
        "arena_price_table.tsv");
    var historicalPrices = {};
    var historicalRows = 0;
    var historicalHits = 0;
    var fallbackPriced = 0;
    var priceRows = [];
    var priceById = {};
    var copiedItemIds = {};
    var craftingPieceTiers = {};

    function fail(message, code) {
        WScript.Echo("[ERROR] " + message);
        WScript.Quit(code);
    }

    function ensureFolder(path) {
        if (fso.FolderExists(path)) {
            return;
        }
        var parent = fso.GetParentFolderName(path);
        if (parent && !fso.FolderExists(parent)) {
            ensureFolder(parent);
        }
        fso.CreateFolder(path);
    }

    function writeLanguageData(path, languageId, files) {
        var document = new ActiveXObject("Msxml2.DOMDocument.6.0");
        document.async = false;
        document.preserveWhiteSpace = true;
        document.appendChild(document.createProcessingInstruction(
            "xml", "version=\"1.0\" encoding=\"utf-8\""));
        var root = document.createElement("LanguageData");
        root.setAttribute("id", languageId);
        document.appendChild(root);
        for (var index = 0; index < files.length; index++) {
            var languageFile = document.createElement("LanguageFile");
            languageFile.setAttribute("xml_path", files[index]);
            root.appendChild(languageFile);
        }
        document.save(path);
    }

    function numberAttribute(node, name) {
        if (!node) {
            return 0;
        }
        var raw = node.getAttribute(name);
        if (raw === null || raw === "") {
            return 0;
        }
        var value = parseFloat(raw);
        return isNaN(value) ? 0 : value;
    }

    function maxDescendantAttribute(node, name) {
        var result = 0;
        var descendants = node.selectNodes(".//*[@" + name + "]");
        for (var index = 0; index < descendants.length; index++) {
            result = Math.max(result, numberAttribute(descendants.item(index), name));
        }
        return result;
    }

    function loadCraftingPieceTiers(path) {
        if (!fso.FileExists(path)) {
            return 0;
        }
        var document = new ActiveXObject("Msxml2.DOMDocument.6.0");
        document.async = false;
        document.preserveWhiteSpace = false;
        if (!document.load(path)) {
            fail("Cannot parse crafting pieces for arena prices: " + path +
                ": " + document.parseError.reason, 13);
        }
        var nodes = document.selectNodes("//CraftingPiece");
        for (var index = 0; index < nodes.length; index++) {
            var node = nodes.item(index);
            var id = String(node.getAttribute("id") || "");
            var tier = numberAttribute(node, "tier");
            if (id && tier > 0) {
                craftingPieceTiers[id] = Math.max(
                    craftingPieceTiers[id] || 0,
                    tier);
            }
        }
        return nodes.length;
    }

    function resolvedCraftingPieceTier(id) {
        return craftingPieceTiers[String(id || "")] || 0;
    }

    function normalizedItemType(node) {
        var type = String(node.getAttribute("Type") ||
            node.getAttribute("type") || "").toLowerCase();
        if (type) {
            return type;
        }

        // CraftedItem gets its ItemType from a crafting template at runtime,
        // but the safe XML preparation runs before Bannerlord resolves those
        // templates. Recover the category without touching native objects.
        var template = String(
            node.getAttribute("crafting_template") || "").toLowerCase();
        if (template.indexOf("javelin") >= 0 ||
                template.indexOf("throwing") >= 0) {
            return "thrown";
        }
        if (template.indexOf("polearm") >= 0 ||
                template.indexOf("spear") >= 0 ||
                template.indexOf("pike") >= 0 ||
                template.indexOf("lance") >= 0) {
            return "polearm";
        }
        if (template.indexOf("twohanded") >= 0) {
            return "twohandedweapon";
        }
        if (template.indexOf("onehanded") >= 0 ||
                template.indexOf("sword") >= 0 ||
                template.indexOf("axe") >= 0 ||
                template.indexOf("mace") >= 0 ||
                template.indexOf("dagger") >= 0) {
            return "onehandedweapon";
        }
        return "";
    }

    function tierHint(node) {
        var result = numberAttribute(node, "tier");
        var idNodes = node.selectNodes(".//*[@id]");
        var ids = [String(node.getAttribute("id") || "")];
        for (var index = 0; index < idNodes.length; index++) {
            ids.push(String(idNodes.item(index).getAttribute("id") || ""));
        }
        for (var idIndex = 0; idIndex < ids.length; idIndex++) {
            result = Math.max(
                result,
                resolvedCraftingPieceTier(ids[idIndex]));
            var match = /(?:^|_)t([0-9])(?:_|$)/i.exec(ids[idIndex]);
            if (match) {
                result = Math.max(result, parseInt(match[1], 10));
            }
        }
        // The XML item tiers stop at six while the campaign economy curve for
        // the rarest equipment extends to roughly 7.5.
        return Math.min(7.5, result * 1.25);
    }

    function damageTypeFactor(value) {
        value = String(value || "").toLowerCase();
        if (value === "pierce") {
            return 1.15;
        }
        if (value === "blunt") {
            return 1.45;
        }
        return 1;
    }

    function estimateMeleeTier(node) {
        var thrustDamage = maxDescendantAttribute(node, "thrust_damage");
        var swingDamage = maxDescendantAttribute(node, "swing_damage");
        var thrustSpeed = maxDescendantAttribute(node, "thrust_speed");
        var swingSpeed = maxDescendantAttribute(node, "swing_speed");
        var length = maxDescendantAttribute(node, "weapon_length");
        var thrustTypeNode = node.selectSingleNode(".//*[@thrust_damage_type]");
        var swingTypeNode = node.selectSingleNode(".//*[@swing_damage_type]");
        var thrust = thrustDamage * damageTypeFactor(
            thrustTypeNode && thrustTypeNode.getAttribute("thrust_damage_type")) *
            Math.pow(Math.max(0, thrustSpeed) * 0.01, 1.5);
        var swing = swingDamage * damageTypeFactor(
            swingTypeNode && swingTypeNode.getAttribute("swing_damage_type")) *
            Math.pow(Math.max(0, swingSpeed) * 0.01, 1.5) * 1.1;
        var power = Math.max(thrust, swing);
        return Math.max(-1.5, Math.min(7.5,
            0.06 * power * (1 + Math.max(0, length) * 0.01) - 3.5));
    }

    function inferredTierFromDeclaredValue(declaredValue) {
        var ratio = Math.max(25, declaredValue) / 100;
        return Math.max(-1, Math.min(7.5,
            Math.log(ratio) / Math.log(2.75) * 1.75));
    }

    function roundStorePrice(price) {
        var step = price >= 100000
            ? 1000
            : price >= 10000
                ? 100
                : price >= 1000
                    ? 50
                    : price >= 100
                        ? 10
                        : 5;
        return Math.round(price / step) * step;
    }

    function priceIdentity(node) {
        var parts = [
            String(node.getAttribute("id") || ""),
            String(node.getAttribute("name") || ""),
            String(node.getAttribute("culture") || ""),
            String(node.getAttribute("crafting_template") || ""),
            String(node.getAttribute("item_category") || "")
        ];
        var idNodes = node.selectNodes(".//*[@id]");
        for (var index = 0; index < idNodes.length; index++) {
            parts.push(String(idNodes.item(index).getAttribute("id") || ""));
        }
        return parts.join(" ").toLowerCase();
    }

    function inferredCulture(identity) {
        if (/aserai/.test(identity)) return "aserai";
        if (/khuzait/.test(identity)) return "khuzait";
        if (/sturgia|sturgian/.test(identity)) return "sturgia";
        if (/nord|northern/.test(identity)) return "nord";
        if (/vlandia|vlandian/.test(identity)) return "vlandia";
        if (/battania|battanian/.test(identity)) return "battania";
        if (/empire|imperial/.test(identity)) return "empire";
        return "neutral";
    }

    function isWeaponType(type) {
        return type === "onehandedweapon" ||
            type === "twohandedweapon" || type === "polearm" ||
            type === "thrown" || type === "bow" || type === "crossbow";
    }

    // These factors model a single inter-cultural arena market, not modern
    // national price tags. A culture's common military goods receive a supply
    // discount, while imported materials and specialist workshop traditions
    // carry a premium. The Empire's three successor states share one material
    // culture in Bannerlord's item data and therefore one economic profile.
    function historicalCultureFactor(node, type, identity) {
        var culture = inferredCulture(identity);
        if (culture == "aserai") {
            if (isWeaponType(type)) return 1.35;
            if (type === "horse" || type === "animal") return 1.12;
            if (type.indexOf("armor") >= 0 || type === "horseharness") {
                return 1.08;
            }
        } else if (culture == "khuzait") {
            if (type === "horse" || type === "animal" || type === "bow" ||
                    type === "arrows") return 0.84;
            if (type === "polearm" || type === "thrown") return 0.92;
            if (type === "bodyarmor" && /plate/.test(identity)) return 1.18;
        } else if (culture == "sturgia") {
            if (/axe|spear|mail|fur/.test(identity) || type === "shield") {
                return 0.88;
            }
            if (type === "crossbow" || /plate/.test(identity)) return 1.15;
        } else if (culture == "nord") {
            if (/axe|spear|mail|fur|round_shield/.test(identity) ||
                    type === "shield") return 0.86;
            if (type === "horse" || type === "horseharness") return 1.18;
        } else if (culture == "vlandia") {
            if (type === "crossbow" || /lance|plate|mail|sword/.test(identity)) {
                return 0.91;
            }
            if (type === "bow" || type === "arrows") return 1.12;
        } else if (culture == "battania") {
            if (type === "bow" || type === "arrows" || type === "thrown" ||
                    /axe|javelin/.test(identity)) return 0.86;
            if (type === "horse" || type === "horseharness" ||
                    /plate/.test(identity)) return 1.18;
        } else if (culture == "empire") {
            if (type.indexOf("armor") >= 0 || type === "shield" ||
                    type === "crossbow" || /sword|pilum/.test(identity)) {
                return 0.94;
            }
        }
        return 1;
    }

    function historicalMaterialFactor(identity) {
        var factor = 1;
        if (/damascus|damask|wootz|fine_steel|tempered/.test(identity)) {
            factor *= 1.7;
        } else if (/steel/.test(identity)) {
            factor *= 1.28;
        } else if (/iron/.test(identity)) {
            factor *= 1.12;
        }
        if (/decorated|ornate|gilded|gold|silver|jewel/.test(identity)) {
            factor *= 1.32;
        }
        if (/wooden|wood_|practice|training|pitchfork|peasant|rusty/.test(
                identity)) {
            factor *= 0.64;
        } else if (/bamboo/.test(identity)) {
            factor *= 0.78;
        }
        return Math.max(0.55, Math.min(2.25, factor));
    }

    function historicalManufacturingFactor(type, identity) {
        if (/pike/.test(identity)) return 1.2;
        if (/lance/.test(identity)) return 1.15;
        if (type === "crossbow") return 1.25;
        if (/sword|sabre|scimitar|falx/.test(identity)) return 1.14;
        if (/plate/.test(identity)) return 1.24;
        if (/mail|lamellar|scale/.test(identity)) return 1.12;
        return 1;
    }

    function minimumHistoricalPrice(type, identity) {
        if (type === "onehandedweapon") return 250;
        if (type === "twohandedweapon") return 400;
        if (type === "polearm") {
            if (/pike/.test(identity)) return 500;
            if (/lance/.test(identity)) return 350;
            return 220;
        }
        if (type === "thrown") return 100;
        if (type === "bow") return 200;
        if (type === "crossbow") return 400;
        if (type === "shield") return 120;
        if (type === "arrows" || type === "bolts") return 60;
        if (type === "headarmor") return 100;
        if (type === "bodyarmor") return 160;
        if (type === "legarmor" || type === "handarmor") return 80;
        if (type === "cape") return 40;
        if (type === "horse" || type === "animal") return 400;
        if (type === "horseharness") return 200;
        return 10;
    }

    // Bake the campaign-style price into the multiplayer-safe copy. Opening
    // Inventory can then sort 1000+ objects by ItemObject.Value without asking
    // the native client to resolve every weapon component in a single frame.
    function calculateArenaPrice(node) {
        var declaredValue = Math.max(1, numberAttribute(node, "value"));
        var type = normalizedItemType(node);
        var identity = priceIdentity(node);
        var equipmentTypes = {
            "headarmor": true,
            "bodyarmor": true,
            "legarmor": true,
            "handarmor": true,
            "cape": true,
            "horseharness": true,
            "horse": true,
            "animal": true,
            "shield": true,
            "arrows": true,
            "bolts": true,
            "thrown": true,
            "onehandedweapon": true,
            "twohandedweapon": true,
            "polearm": true,
            "bow": true,
            "crossbow": true,
            "banner": true
        };
        if (!equipmentTypes[type]) {
            return Math.max(10, declaredValue);
        }

        var appearance = numberAttribute(node, "appearance");
        var weight = numberAttribute(node, "weight");
        var difficulty = numberAttribute(node, "difficulty");
        var armor = maxDescendantAttribute(node, "head_armor") +
            maxDescendantAttribute(node, "body_armor") +
            maxDescendantAttribute(node, "leg_armor") +
            maxDescendantAttribute(node, "arm_armor");
        var damage = Math.max(
            maxDescendantAttribute(node, "swing_damage"),
            maxDescendantAttribute(node, "thrust_damage"));
        var speed = Math.max(
            maxDescendantAttribute(node, "speed_rating"),
            maxDescendantAttribute(node, "missile_speed"),
            maxDescendantAttribute(node, "speed"));
        var length = maxDescendantAttribute(node, "weapon_length");
        var hitPoints = maxDescendantAttribute(node, "hit_points");
        var stack = maxDescendantAttribute(node, "stack_amount");
        var maneuver = maxDescendantAttribute(node, "maneuver");
        var charge = maxDescendantAttribute(node, "charge_damage");
        var estimatedValue;
        if (type.indexOf("armor") >= 0 || type === "cape" ||
                type === "horseharness") {
            estimatedValue = 40 + appearance * 120 + armor * armor * 0.7 +
                weight * 12;
        } else if (type === "horse" || type === "animal") {
            estimatedValue = 250 + appearance * 220 + speed * 8 +
                maneuver * 6 + charge * 15 + hitPoints * 1.5;
        } else if (type === "shield") {
            estimatedValue = 80 + appearance * 120 + hitPoints * 0.5 +
                damage * 4;
        } else if (type === "arrows" || type === "bolts" ||
                type === "thrown") {
            estimatedValue = 40 + appearance * 90 + stack * 8 +
                damage * 4 + speed;
        } else if (type === "onehandedweapon" ||
                type === "twohandedweapon" || type === "polearm" ||
                type === "bow" || type === "crossbow") {
            estimatedValue = 90 + appearance * 150 + damage * 8 +
                length * 1.5 + speed * 1.2;
        } else {
            estimatedValue = 250 + appearance * 200;
        }
        var tier = Math.max(
            tierHint(node),
            inferredTierFromDeclaredValue(Math.max(
                declaredValue,
                estimatedValue)));
        if (type === "onehandedweapon" || type === "twohandedweapon" ||
                type === "polearm" || type === "thrown") {
            tier = Math.max(tier, estimateMeleeTier(node));
        } else if (type === "bow" || type === "crossbow") {
            // Top campaign bows require 70 skill and sell around 50k.
            tier = Math.max(tier, Math.min(6.2, difficulty / 11.25));
        } else if (type === "shield") {
            tier = Math.max(tier, Math.min(5.25, hitPoints / 90));
        } else if (type === "horse" || type === "animal") {
            // The upper end of the campaign horse market is close to 20k.
            tier = Math.min(5.25, tier + 0.55);
        }

        var tierCap;
        var baseValue = 100;
        var multiplier = 1;
        var minimumPrice;
        var maximumPrice;
        if (type === "headarmor" || type === "cape") {
            tierCap = 7.5;
            minimumPrice = 50;
            maximumPrice = 850000;
            multiplier = 1 + 2.75 * Math.max(0, Math.min(1,
                (tier - 4.5) / 2.5));
        } else if (type === "bodyarmor" || type === "legarmor" ||
                type === "handarmor") {
            tierCap = 7.5;
            baseValue = 120;
            minimumPrice = 60;
            maximumPrice = 950000;
            multiplier = 1 + 2.75 * Math.max(0, Math.min(1,
                (tier - 4.5) / 2.5));
        } else if (type === "shield") {
            tierCap = 5.25;
            minimumPrice = 75;
            maximumPrice = 22000;
        } else if (type === "arrows" || type === "bolts") {
            tierCap = 4.5;
            minimumPrice = 40;
            maximumPrice = 10000;
        } else if (type === "thrown" || type === "onehandedweapon" ||
                type === "twohandedweapon" || type === "polearm") {
            tierCap = 5.5;
            minimumPrice = 75;
            maximumPrice = 30000;
        } else if (type === "bow" || type === "crossbow") {
            tierCap = 6.2;
            minimumPrice = 100;
            maximumPrice = 55000;
        } else if (type === "horse" || type === "animal") {
            tierCap = 5.25;
            minimumPrice = 250;
            maximumPrice = 20000;
        } else if (type === "horseharness") {
            tierCap = 6.5;
            minimumPrice = 100;
            maximumPrice = 90000;
        } else {
            tierCap = 6;
            minimumPrice = 250;
            maximumPrice = 45000;
        }

        tier = Math.max(-1, Math.min(tierCap, tier));
        var campaignValue = baseValue * Math.pow(2.75, tier) *
            (1 + 0.2 * (appearance - 1)) +
            100 * Math.max(0, appearance - 1);
        var historicalFactor = historicalCultureFactor(node, type, identity) *
            historicalMaterialFactor(identity) *
            historicalManufacturingFactor(type, identity);
        var price = roundStorePrice(
            campaignValue * multiplier * historicalFactor);
        price = Math.max(minimumPrice, Math.min(maximumPrice, price));
        price = Math.max(price, minimumHistoricalPrice(type, identity));

        // Rank words are real economy signals too: noble and lord equipment
        // is deliberately rare even when a decorative model has modest raw
        // armour numbers. Keep those pieces out of the bargain pages.
        var prestigeIdentity = identity;
        var isPrestigeItem = /noble|lord|royal|crown|emir|sultan|jewel/.test(
            prestigeIdentity);
        if (isPrestigeItem) {
            var minimumPrestigePrice = 0;
            if (type === "headarmor") {
                minimumPrestigePrice = 200000;
            } else if (type === "bodyarmor") {
                minimumPrestigePrice = 350000;
            } else if (type === "cape") {
                minimumPrestigePrice = 60000;
            } else if (type === "handarmor" || type === "legarmor") {
                minimumPrestigePrice = 75000;
            } else if (type === "onehandedweapon" ||
                    type === "twohandedweapon" || type === "polearm" ||
                    type === "bow" || type === "crossbow" ||
                    type === "thrown") {
                minimumPrestigePrice = 30000;
            }
            price = Math.max(price, minimumPrestigePrice);
        }
        return Math.min(maximumPrice, price);
    }

    function loadHistoricalPrices(path) {
        if (!fso.FileExists(path)) {
            fail("Arena price table is missing: " + path, 17);
        }
        var stream = new ActiveXObject("ADODB.Stream");
        var text;
        try {
            stream.Type = 2;
            stream.Charset = "utf-8";
            stream.Open();
            stream.LoadFromFile(path);
            text = stream.ReadText(-1);
        } finally {
            if (stream.State) {
                stream.Close();
            }
        }
        var lines = text.replace(/^\uFEFF/, "").split(/\r\n|\n|\r/);
        for (var lineIndex = 0; lineIndex < lines.length; lineIndex++) {
            var line = lines[lineIndex];
            if (!line || line.charAt(0) === "#") {
                continue;
            }
            var columns = line.split("\t");
            if (columns.length !== 2 || !columns[0] ||
                    !/^[0-9]+$/.test(columns[1])) {
                fail("Arena price table row " + (lineIndex + 1) +
                    " is malformed: " + line, 17);
            }
            historicalPrices["$" + columns[0]] = parseInt(columns[1], 10);
            historicalRows++;
        }
        if (historicalRows < 1000) {
            fail("Arena price table is too short: " + historicalRows, 17);
        }
    }

    // The table price when the table knows the item, otherwise the old
    // campaign curve converted to pennies (1/25) and kept below the dearest
    // armour of the table.
    function arenaPrice(node) {
        var key = "$" + String(node.getAttribute("id") || "");
        if (key !== "$" && historicalPrices.hasOwnProperty(key)) {
            historicalHits++;
            return historicalPrices[key];
        }
        fallbackPriced++;
        return Math.max(1, Math.min(4800,
            Math.round(calculateArenaPrice(node) / 25)));
    }

    function ensureMultiplayerValue(node) {
        // MultiplayerGame ignores campaign weapons (especially CraftedItem)
        // unless they are explicitly marked for the multiplayer object pass.
        // The catalogue is a private duplicate with Native id collisions
        // removed above, so every retained object is safe to expose here.
        node.setAttribute("multiplayer_item", "true");
        // SandBox keeps a number of perfectly usable weapons (including most
        // crafted blades) out of town markets with is_merchandise="false".
        // Arena owns its own complete catalogue, so make every imported copy
        // tradable instead of silently losing the whole weapon category.
        node.setAttribute("is_merchandise", "true");
        var price = arenaPrice(node);
        // ItemObject.Value sorts the inventory; a withdrawn item keeps 1.
        node.setAttribute("value", String(Math.max(1, price)));
        var id = String(node.getAttribute("id") || "");
        if (id) {
            priceRows.push(id + "\t" + price);
            priceById[id] = Math.max(priceById[id] || 0, price);
        }
        return true;
    }

    if (!fso.FolderExists(itemsSource)) {
        fail("SandBoxCore item directory was not found: " + itemsSource, 3);
    }
    if (!fso.FileExists(craftingSource)) {
        fail("Native crafting pieces were not found: " + craftingSource, 4);
    }
    if (!fso.FileExists(nativeMpItems)) {
        fail("Native multiplayer items were not found: " + nativeMpItems, 7);
    }
    if (!fso.FileExists(nativeMpCraftingPieces)) {
        fail("Native multiplayer crafting pieces were not found: " +
            nativeMpCraftingPieces, 13);
    }
    if (!fso.FileExists(nativeMpCharacters)) {
        fail("Native multiplayer characters were not found: " +
            nativeMpCharacters, 15);
    }

    var resolvedPieceCount = loadCraftingPieceTiers(craftingSource) +
        loadCraftingPieceTiers(nativeMpCraftingPieces) +
        loadCraftingPieceTiers(navalCraftingSource);
    loadHistoricalPrices(historicalPricesPath);

    if (fso.FolderExists(destination)) {
        fso.DeleteFolder(destination, true);
    }
    ensureFolder(itemsDestination);

    // The multiplayer launch does not activate SandBox or NavalDLC, so their
    // character-creation strings would otherwise be blank even though the
    // options and outfits are present. Load the player's installed official
    // English and Russian tables from this module as well. This also keeps the
    // wording in lockstep with the exact game version being tested.
    ensureFolder(languagesDestination);
    ensureFolder(russianLanguagesDestination);
    var localizationFiles = [
        {
            source: fso.BuildPath(sandboxLanguages,
                "std_TaleWorlds_CampaignSystem.xml"),
            target: fso.BuildPath(languagesDestination,
                "dikozemie_creation_campaign.xml")
        },
        {
            source: fso.BuildPath(navalLanguages, "std_NavalDLC.xml"),
            target: fso.BuildPath(languagesDestination,
                "dikozemie_creation_naval.xml")
        },
        {
            source: fso.BuildPath(navalLanguages,
                "std_naval_dialog_strings_xml.xml"),
            target: fso.BuildPath(languagesDestination,
                "dikozemie_creation_naval_dialog.xml")
        },
        {
            source: fso.BuildPath(fso.BuildPath(sandboxLanguages, "RU"),
                "std_TaleWorlds_CampaignSystem_rus.xml"),
            target: fso.BuildPath(russianLanguagesDestination,
                "dikozemie_creation_campaign_rus.xml")
        },
        {
            source: fso.BuildPath(fso.BuildPath(navalLanguages, "RU"),
                "std_NavalDLC_rus.xml"),
            target: fso.BuildPath(russianLanguagesDestination,
                "dikozemie_creation_naval_rus.xml")
        },
        {
            source: fso.BuildPath(fso.BuildPath(navalLanguages, "RU"),
                "std_naval_dialog_strings_xml_rus.xml"),
            target: fso.BuildPath(russianLanguagesDestination,
                "dikozemie_creation_naval_dialog_rus.xml")
        }
    ];
    var localizationCopied = 0;
    for (var localizationIndex = 0;
            localizationIndex < localizationFiles.length;
            localizationIndex++) {
        var localization = localizationFiles[localizationIndex];
        if (!fso.FileExists(localization.source)) {
            fail("Character-creation localization was not found: " +
                localization.source, 11);
        }
        fso.CopyFile(localization.source, localization.target, true);
        localizationCopied++;
    }
    writeLanguageData(
        fso.BuildPath(languagesDestination, "language_data.xml"),
        "English",
        [
            "dikozemie_creation_campaign.xml",
            "dikozemie_creation_naval.xml",
            "dikozemie_creation_naval_dialog.xml"
        ]);
    writeLanguageData(
        fso.BuildPath(russianLanguagesDestination, "language_data.xml"),
        "\u0420\u0443\u0441\u0441\u043a\u0438\u0439",
        [
            "RU/dikozemie_creation_campaign_rus.xml",
            "RU/dikozemie_creation_naval_rus.xml",
            "RU/dikozemie_creation_naval_dialog_rus.xml"
        ]);

    var files = new Enumerator(fso.GetFolder(itemsSource).Files);
    var copied = 0;
    var removed = 0;
    var priced = 0;
    for (; !files.atEnd(); files.moveNext()) {
        var sourceFile = files.item();
        if (fso.GetExtensionName(sourceFile.Name).toLowerCase() !== "xml") {
            continue;
        }

        var targetFile = fso.BuildPath(itemsDestination, sourceFile.Name);
        var document = new ActiveXObject("Msxml2.DOMDocument.6.0");
        document.async = false;
        document.preserveWhiteSpace = true;
        if (!document.load(sourceFile.Path)) {
            fail("Cannot parse " + sourceFile.Name + ": " +
                document.parseError.reason, 5);
        }
        var nodes = document.selectNodes("//Item | //CraftedItem");
        for (var index = nodes.length - 1; index >= 0; index--) {
            var node = nodes.item(index);
            var id = node.getAttribute("id");
            if (duplicateIds[id] || copiedItemIds[id]) {
                node.parentNode.removeChild(node);
                removed++;
                continue;
            }
            copiedItemIds[id] = true;
            if (ensureMultiplayerValue(node)) {
                priced++;
            }
        }
        document.save(targetFile);
        copied++;
    }

    // NavalDLC is not part of Bannerlord's multiplayer module set, while the
    // installed character creator adds Nord and maritime choices from it.
    // Import the two item catalogues needed by those exact preview outfits.
    var navalItemFiles = ["items.xml", "naval_weapons.xml"];
    var navalCopied = 0;
    var navalCraftedDropped = 0;
    if (fso.FolderExists(navalModuleData)) {
        for (var navalFileIndex = 0;
                navalFileIndex < navalItemFiles.length;
                navalFileIndex++) {
            var navalSource = fso.BuildPath(
                navalModuleData,
                navalItemFiles[navalFileIndex]);
            if (!fso.FileExists(navalSource)) {
                continue;
            }
            var navalDocument = new ActiveXObject("Msxml2.DOMDocument.6.0");
            navalDocument.async = false;
            navalDocument.preserveWhiteSpace = true;
            if (!navalDocument.load(navalSource)) {
                fail("Cannot parse NavalDLC/" + navalItemFiles[navalFileIndex] +
                    ": " + navalDocument.parseError.reason, 9);
            }
            var navalNodes = navalDocument.selectNodes("//Item | //CraftedItem");
            for (var navalNodeIndex = navalNodes.length - 1;
                    navalNodeIndex >= 0;
                    navalNodeIndex--) {
                var navalNode = navalNodes.item(navalNodeIndex);
                var navalId = String(navalNode.getAttribute("id") || "");
                if (!navalId || duplicateIds[navalId] || copiedItemIds[navalId]) {
                    navalNode.parentNode.removeChild(navalNode);
                    removed++;
                    continue;
                }
                if (navalNode.nodeName === "CraftedItem") {
                    navalNode.parentNode.removeChild(navalNode);
                    navalCraftedDropped++;
                    continue;
                }
                copiedItemIds[navalId] = true;
                if (ensureMultiplayerValue(navalNode)) {
                    priced++;
                }
            }
            navalDocument.save(fso.BuildPath(
                itemsDestination,
                "naval_" + navalItemFiles[navalFileIndex]));
            navalCopied++;
        }
    }

    for (var requiredIndex = 0;
            requiredIndex < requiredItemFiles.length;
            requiredIndex++) {
        if (!fso.FileExists(fso.BuildPath(
                itemsDestination,
                requiredItemFiles[requiredIndex]))) {
            fail("Required item file is missing: " +
                requiredItemFiles[requiredIndex], 6);
        }
    }
    checkItemFilesListedInSubModule();
    fso.CopyFile(
        craftingSource,
        fso.BuildPath(destination, "crafting_pieces.xml"),
        true);
    if (fso.FileExists(navalCraftingSource)) {
        fso.CopyFile(
            navalCraftingSource,
            fso.BuildPath(destination, "naval_crafting_pieces.xml"),
            true);
    }
    // War Sails creates a CustomGame, while Native exposes its multiplayer
    // pieces, items and fighters only to MultiplayerGame. Prepare exact
    // installed-version copies that our module declares for CustomGame. They
    // are loaded during NavalCustomGame initialization, before objects are
    // finalized; late MBObjectManager loading drops crafted mp_* weapons.
    fso.CopyFile(
        nativeMpCraftingPieces,
        fso.BuildPath(destination, "naval_arena_mp_crafting_pieces.xml"),
        true);

    // Campaign character-creation equipment rosters are excluded from the
    // MultiplayerGame type by SandBox/NavalDLC. Copy only those rosters, not
    // the rest of either campaign catalogue, so every preview resolves the
    // same civilian clothing without colliding with MP class equipment.
    var creationEquipmentDocument =
        new ActiveXObject("Msxml2.DOMDocument.6.0");
    creationEquipmentDocument.async = false;
    creationEquipmentDocument.preserveWhiteSpace = true;
    creationEquipmentDocument.appendChild(
        creationEquipmentDocument.createProcessingInstruction(
            "xml", "version=\"1.0\" encoding=\"utf-8\""));
    var creationEquipmentRoot =
        creationEquipmentDocument.createElement("EquipmentRosters");
    creationEquipmentDocument.appendChild(creationEquipmentRoot);
    var equipmentSources = [
        sandboxEquipmentSets,
        sandboxCoreEquipmentSets,
        navalEquipmentSets
    ];
    var creationEquipmentCount = 0;
    var creationEquipmentIds = {};
    for (var equipmentSourceIndex = 0;
            equipmentSourceIndex < equipmentSources.length;
            equipmentSourceIndex++) {
        var equipmentSource = equipmentSources[equipmentSourceIndex];
        if (!fso.FileExists(equipmentSource)) {
            continue;
        }
        var equipmentDocument = new ActiveXObject("Msxml2.DOMDocument.6.0");
        equipmentDocument.async = false;
        equipmentDocument.preserveWhiteSpace = true;
        if (!equipmentDocument.load(equipmentSource)) {
            fail("Cannot parse equipment roster source: " + equipmentSource,
                10);
        }
        var equipmentNodes = equipmentDocument.selectNodes(
            "//EquipmentRoster[contains(@id, 'char_creation')]");
        for (var equipmentIndex = 0;
                equipmentIndex < equipmentNodes.length;
                equipmentIndex++) {
            var equipmentNode = equipmentNodes.item(equipmentIndex);
            var equipmentId = String(equipmentNode.getAttribute("id") || "");
            if (!equipmentId || creationEquipmentIds[equipmentId]) {
                continue;
            }
            creationEquipmentIds[equipmentId] = true;
            creationEquipmentRoot.appendChild(equipmentNode.cloneNode(true));
            creationEquipmentCount++;
        }
    }
    creationEquipmentDocument.save(fso.BuildPath(
        destination,
        "creation_equipment_sets.xml"));

    // The campaign replaces the player's face template as soon as a culture
    // is selected. Multiplayer does not load SandBox/NavalDLC body-property
    // tables, so import only the seven official creation templates.
    var creationBodyDocument =
        new ActiveXObject("Msxml2.DOMDocument.6.0");
    creationBodyDocument.async = false;
    creationBodyDocument.preserveWhiteSpace = true;
    creationBodyDocument.appendChild(
        creationBodyDocument.createProcessingInstruction(
            "xml", "version=\"1.0\" encoding=\"utf-8\""));
    var creationBodyRoot = creationBodyDocument.createElement("BodyProperties");
    creationBodyDocument.appendChild(creationBodyRoot);
    var bodySources = [sandboxBodyProperties, navalBodyProperties];
    var requiredBodyIds = {
        "default_character_creation_body_property_empire": true,
        "default_character_creation_body_property_vlandia": true,
        "default_character_creation_body_property_sturgia": true,
        "default_character_creation_body_property_aserai": true,
        "default_character_creation_body_property_khuzait": true,
        "default_character_creation_body_property_battania": true,
        "default_character_creation_body_property_nord": true
    };
    var creationBodyCount = 0;
    for (var bodySourceIndex = 0;
            bodySourceIndex < bodySources.length;
            bodySourceIndex++) {
        if (!fso.FileExists(bodySources[bodySourceIndex])) {
            continue;
        }
        var bodyDocument = new ActiveXObject("Msxml2.DOMDocument.6.0");
        bodyDocument.async = false;
        bodyDocument.preserveWhiteSpace = true;
        if (!bodyDocument.load(bodySources[bodySourceIndex])) {
            fail("Cannot parse body-property source: " +
                bodySources[bodySourceIndex], 12);
        }
        var bodyNodes = bodyDocument.selectNodes("//BodyProperty");
        for (var bodyIndex = 0; bodyIndex < bodyNodes.length; bodyIndex++) {
            var bodyNode = bodyNodes.item(bodyIndex);
            var bodyId = String(bodyNode.getAttribute("id") || "");
            if (!requiredBodyIds[bodyId]) {
                continue;
            }
            creationBodyRoot.appendChild(bodyNode.cloneNode(true));
            delete requiredBodyIds[bodyId];
            creationBodyCount++;
        }
    }
    for (var missingBodyId in requiredBodyIds) {
        if (requiredBodyIds.hasOwnProperty(missingBodyId)) {
            fail("Character-creation body property is missing: " +
                missingBodyId, 12);
        }
    }
    creationBodyDocument.save(fso.BuildPath(
        destination,
        "creation_body_properties.xml"));

    // Native/mpitems.xml remains loaded by the game itself, but its equipment
    // also reports Value=1 in multiplayer. Add those mp_* ids to the same
    // sidecar table without copying or modifying the official file.
    var nativeDocument = new ActiveXObject("Msxml2.DOMDocument.6.0");
    nativeDocument.async = false;
    nativeDocument.preserveWhiteSpace = true;
    if (!nativeDocument.load(nativeMpItems)) {
        fail("Cannot parse Native mpitems.xml: " +
            nativeDocument.parseError.reason, 8);
    }
    var nativeNodes = nativeDocument.selectNodes("//Item | //CraftedItem");
    var nativePriced = 0;
    for (var nativeIndex = 0;
            nativeIndex < nativeNodes.length;
            nativeIndex++) {
        var nativeNode = nativeNodes.item(nativeIndex);
        var nativeId = String(nativeNode.getAttribute("id") || "");
        if (!nativeId || duplicateIds[nativeId]) {
            continue;
        }
        var nativePrice = arenaPrice(nativeNode);
        priceRows.push(nativeId + "\t" + nativePrice);
        priceById[nativeId] = Math.max(
            priceById[nativeId] || 0,
            nativePrice);
        nativePriced++;
    }

    // The CustomGame already receives SandBoxCore's siege items. Remove the
    // small known Native collision set from the copied MP catalogue and keep
    // every actual arena weapon/armour byte-for-byte from the installed game.
    for (var bridgeItemIndex = nativeNodes.length - 1;
            bridgeItemIndex >= 0;
            bridgeItemIndex--) {
        var bridgeItem = nativeNodes.item(bridgeItemIndex);
        var bridgeItemId = String(bridgeItem.getAttribute("id") || "");
        if (duplicateIds[bridgeItemId]) {
            bridgeItem.parentNode.removeChild(bridgeItem);
        }
    }
    nativeDocument.save(fso.BuildPath(
        destination,
        "naval_arena_mpitems.xml"));

    var navalArenaCharacters =
        new ActiveXObject("Msxml2.DOMDocument.6.0");
    navalArenaCharacters.async = false;
    navalArenaCharacters.preserveWhiteSpace = true;
    if (!navalArenaCharacters.load(nativeMpCharacters)) {
        fail("Cannot parse Native mpcharacters.xml: " +
            navalArenaCharacters.parseError.reason, 16);
    }
    var originalCharacterRoot = navalArenaCharacters.documentElement;
    var customCharacterDocument =
        new ActiveXObject("Msxml2.DOMDocument.6.0");
    customCharacterDocument.async = false;
    customCharacterDocument.preserveWhiteSpace = true;
    customCharacterDocument.appendChild(
        customCharacterDocument.createProcessingInstruction(
            "xml", "version=\"1.0\" encoding=\"utf-8\""));
    var customCharacterRoot =
        customCharacterDocument.createElement("NPCCharacters");
    customCharacterDocument.appendChild(customCharacterRoot);
    for (var characterIndex = 0;
            characterIndex < originalCharacterRoot.childNodes.length;
            characterIndex++) {
        customCharacterRoot.appendChild(
            originalCharacterRoot.childNodes.item(characterIndex)
                .cloneNode(true));
    }
    customCharacterDocument.save(fso.BuildPath(
        destination,
        "naval_arena_characters.xml"));

    var fishPrice = priceById.fish || 0;
    var aseraiPikePrice = priceById.mp_aserai_pike || 0;
    if (fishPrice <= 0 || aseraiPikePrice <= fishPrice ||
            historicalHits < 1000) {
        fail("Historical arena price audit failed: fish=" + fishPrice +
            " aseraiPike=" + aseraiPikePrice + " tablePrices=" +
            historicalHits, 14);
    }

    priceRows.sort();
    var priceTable = fso.CreateTextFile(
        fso.BuildPath(destination, "arena-prices.tsv"),
        true,
        false);
    priceTable.WriteLine("# item_id\tprice");
    for (var priceIndex = 0; priceIndex < priceRows.length; priceIndex++) {
        priceTable.WriteLine(priceRows[priceIndex]);
    }
    priceTable.Close();

    var manifest = fso.CreateTextFile(
        fso.BuildPath(destination, "catalog-info.txt"),
        true,
        false);
    manifest.WriteLine("Source=installed SandBoxCore/ModuleData/items");
    manifest.WriteLine("ItemFilesListedInSubModule=" + itemFilesListed);
    manifest.WriteLine("ItemXmlFiles=" + copied);
    manifest.WriteLine("RemovedNativeDuplicates=" + removed);
    manifest.WriteLine("EstimatedMultiplayerPrices=" + priced);
    manifest.WriteLine("PreparedNativePrices=" + nativePriced);
    manifest.WriteLine("NavalArenaNativeItems=" + nativeNodes.length);
    manifest.WriteLine("NavalArenaNativeCharacters=" +
        customCharacterRoot.selectNodes("NPCCharacter").length);
    manifest.WriteLine("PreparedPriceRows=" + priceRows.length);
    manifest.WriteLine("HistoricalPricePolicy=" + pricePolicy);
    manifest.WriteLine("HistoricalPriceTableRows=" + historicalRows);
    manifest.WriteLine("HistoricalPriceTableHits=" + historicalHits);
    manifest.WriteLine("FallbackPrices=" + fallbackPriced);
    manifest.WriteLine("ResolvedCraftingPieces=" + resolvedPieceCount);
    manifest.WriteLine("FishPrice=" + fishPrice);
    manifest.WriteLine("AseraiPikePrice=" + aseraiPikePrice);
    manifest.WriteLine("NavalItemXmlFiles=" + navalCopied);
    manifest.WriteLine("ServerCatalog=" + (serverCatalog ? "true" : "false"));
    manifest.WriteLine("NavalCraftedItemsDropped=" + navalCraftedDropped);
    manifest.WriteLine("CreationEquipmentRosters=" + creationEquipmentCount);
    manifest.WriteLine("CreationBodyProperties=" + creationBodyCount);
    manifest.WriteLine("CreationLocalizationFiles=" + localizationCopied);
    manifest.Close();
    WScript.Echo("[OK] Single-player catalogue prepared: " + copied +
        " item files, " + removed + " Native duplicates removed, " +
        priced + " campaign and " + nativePriced +
        " native multiplayer prices assigned; " + creationEquipmentCount +
        " native character-creation equipment rosters, " +
        creationBodyCount + " culture body templates and " +
        localizationCopied + " localization tables prepared.");
}());
