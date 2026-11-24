// Windows Script Host audit: installation requires no Python or Node.js.
// Same checks as audit_arena_prices.py (the release gate runs both on one
// fixture set). Policy historical-penny-v2: one coin is one silver penny,
// prices follow the items' own numbers, 0 means "not sold".
(function () {
    var policy = "historical-penny-v2";
    var fso = new ActiveXObject("Scripting.FileSystemObject");
    // [cheaper, dearer, why]: orderings anyone at a medieval market knows.
    var orderings = [
        ["mp_western_pitchfork_wood", "mp_small_flat_heater_shield", "a shield is harder to make than a pitchfork"],
        ["mp_western_spear", "mp_vlandian_arming_sword", "a sword costs more than a spear"],
        ["mp_hood", "mp_battania_light_armor_e", "a hood is cheaper than a woollen robe"],
        ["sumpter_horse", "mp_vlandia_horse", "a pack horse is cheaper than a riding horse"],
        ["mp_vlandia_horse", "war_horse", "a riding horse is cheaper than a war horse"],
        ["war_horse", "noble_horse", "a war horse is cheaper than a noble horse"],
        ["mp_hunting_bow", "mp_strong_recurve_bow", "a self bow is cheaper than a composite bow"]
    ];
    // [item, lowest, highest]
    var bands = [
        ["fish", 1, 50, "trade goods keep the penny scale"],
        ["mp_battania_light_armor_e", 1, 60, "a robe without armour stays cheap"],
        ["mp_throwing_stone", 1, 5, "stones are picked up for free"],
        ["mp_vlandia_horse", 120, 2400, "a horse has to be earned"],
        ["mp_long_bow", 6, 120, "a self bow cost about a shilling"]
    ];
    var withdrawn = ["ballista_c_projectile_stone", "mp_test_sword"];

    function fail(message) {
        WScript.Echo("[ERROR] Arena price audit: " + message);
        WScript.Quit(1);
    }

    function readLines(file) {
        var stream = new ActiveXObject("ADODB.Stream");
        try {
            stream.Type = 2;
            stream.Charset = "utf-8";
            stream.Open();
            stream.LoadFromFile(file);
            return stream.ReadText(-1).replace(/^\uFEFF/, "").split(/\r\n|\n|\r/);
        } finally {
            if (stream.State) { stream.Close(); }
        }
    }

    function trim(value) { return value.replace(/^\s+|\s+$/g, ""); }

    if (WScript.Arguments.length !== 1) {
        fail("expected the generated singleplayer_catalog directory");
    }
    var catalog = String(WScript.Arguments.Item(0));
    var manifestFile = fso.BuildPath(catalog, "catalog-info.txt");
    var pricesFile = fso.BuildPath(catalog, "arena-prices.tsv");
    if (!fso.FileExists(manifestFile) || !fso.FileExists(pricesFile)) {
        fail("catalogue files are missing in " + catalog);
    }

    try {
        var manifest = {};
        var lines = readLines(manifestFile);
        var index;
        for (index = 0; index < lines.length; index++) {
            var equal = lines[index].indexOf("=");
            if (equal >= 0) {
                manifest["$" + trim(lines[index].slice(0, equal))] =
                    trim(lines[index].slice(equal + 1));
            }
        }
        var prices = {};
        var distinct = {};
        var distinctCount = 0;
        var rowCount = 0;
        var sold = 0;
        lines = readLines(pricesFile);
        for (index = 0; index < lines.length; index++) {
            var line = lines[index];
            if (!line || line.charAt(0) === "#") { continue; }
            var columns = line.split("\t");
            if (columns.length !== 2) { throw new Error("malformed row " + (index + 1)); }
            var itemId = columns[0];
            var rawPrice = trim(columns[1]);
            if (!/^[+-]?[0-9]+(?:_[0-9]+)*$/.test(rawPrice)) {
                throw new Error("invalid integer price at row " + (index + 1));
            }
            var price = Number(rawPrice.replace(/_/g, ""));
            if (!isFinite(price) || Math.abs(price) > 9007199254740991) {
                throw new Error("integer price is out of range at row " + (index + 1));
            }
            if (price < 0) { throw new Error("negative price at row " + (index + 1)); }
            var key = "$" + itemId;
            if (typeof prices[key] !== "undefined" && prices[key] !== price) {
                throw new Error("conflicting duplicate price for " + itemId);
            }
            if (typeof prices[key] === "undefined" && price > 0) { sold++; }
            prices[key] = price;
            if (price > 0 && !distinct["$" + price]) {
                distinct["$" + price] = true;
                distinctCount++;
            }
            rowCount++;
        }

        if (manifest.$HistoricalPricePolicy !== policy) {
            fail("historical penny policy marker is missing");
        }
        if (rowCount < 2200) { fail("only " + rowCount + " price rows were generated"); }
        if (distinctCount < 60) { fail("price curve collapsed into too few distinct values"); }
        for (index = 0; index < bands.length; index++) {
            var band = bands[index];
            var value = prices["$" + band[0]];
            if (typeof value === "undefined" || value < band[1] || value > band[2]) {
                fail(band[0] + " costs " + value + ", expected " + band[1] + ".." +
                    band[2] + " (" + band[3] + ")");
            }
        }
        for (index = 0; index < orderings.length; index++) {
            var pair = orderings[index];
            var cheaper = prices["$" + pair[0]];
            var dearer = prices["$" + pair[1]];
            if (!(cheaper > 0) || !(dearer > 0) || cheaper >= dearer) {
                fail(pair[0] + "=" + cheaper + " is not cheaper than " + pair[1] +
                    "=" + dearer + " (" + pair[2] + ")");
            }
        }
        for (index = 0; index < withdrawn.length; index++) {
            var gone = prices["$" + withdrawn[index]];
            if (typeof gone !== "undefined" && gone !== 0) {
                fail(withdrawn[index] + " must not be sold, costs " + gone);
            }
        }
        WScript.Echo("[OK] Arena price audit: policy=" + policy + ", rows=" + rowCount +
            ", sold=" + sold + ", distinct=" + distinctCount + ", fish=" + prices.$fish +
            ", pitchfork=" + prices.$mp_western_pitchfork_wood +
            ", riding_horse=" + prices.$mp_vlandia_horse + ".");
    } catch (error) {
        fail(error.message || String(error));
    }
}());
