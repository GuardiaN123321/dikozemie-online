using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Localization;

namespace Dikozemie.Online
{
    /// <summary>
    /// A data-only mirror of the installed campaign character-creation flow.
    /// IDs, localization keys, rewards, occupations, ages and animation names
    /// are intentionally kept here so all seven culture paths can be audited.
    /// </summary>
    internal static class DikozemieNativeCreationCatalog
    {
        private static readonly Dictionary<string, DikozemieHeroBackgroundChoice[]> Parents =
            new Dictionary<string, DikozemieHeroBackgroundChoice[]>(StringComparer.Ordinal)
            {
                ["empire"] = new[]
                {
                    P("empire_lanlord_option", "{=InN5ZZt3}A landlord's retainers", "{=ivKl4mV2}", "vigor", "Riding", "Polearm", "retainer", "side_to_side_1"),
                    P("empire_merchant_option", "{=651FhzdR}Urban merchants", "{=FQntPChs}", "social", "Trade", "Charm", "merchant_urban", "mother_front"),
                    P("empire_farmer_option", "{=sb4gg8Ak}Freeholders", "{=09z8Q08f}", "endurance", "Athletics", "Polearm", "farmer", "father_sitting"),
                    P("empire_artisan_option", "{=v48N6h1t}Urban artisans", "{=ueCm5y1C}", "intelligence", "Crafting", "Crossbow", "artisan_urban", "side_to_side_2"),
                    P("empire_hunter_option", "{=7eWmU2mF}Foresters", "{=yRFSzSDZ}", "control", "Scouting", "Bow", "hunter", "side_to_side_3"),
                    P("empire_vagabond_option", "{=aEke8dSb}Urban vagabonds", "{=Jvf6K7TZ}", "cunning", "Roguery", "Throwing", "vagabond_urban", "hugging"),
                    PN("empire_small_boat_fisherman_option", "{=e1aebAAL}Small Boat Fisherman", "{=nBr0jL3X}", "control", "Boatswain", "Throwing", "seafarer", "empire_smallboatfisherman", fatherLeft: "hanging_fishes", fatherRight: "hanging_fishes"),
                    PN("empire_imperial_fleet_option", "{=LdCQfaUi}Imperial Fleet", "{=N6o7Gnpz}", "social", "Trade", "Shipmaster", "shipmaster_urban", "imperial_fleet", fatherRight: "book_right_hand")
                },
                ["vlandia"] = new[]
                {
                    P("vlandia_retainer_option", "{=2TptWc4m}A baron's retainers", "{=0Suu1Q9q}", "social", "Riding", "Polearm", "retainer", "side_to_side_1"),
                    P("vlandia_merchant_option", "{=651FhzdR}Urban merchants", "{=qNZFkxJb}", "intelligence", "Trade", "Charm", "merchant_urban", "mother_front"),
                    P("vlandia_farmer_option", "{=RDfXuVxT}Yeomen", "{=BLZ4mdhb}", "endurance", "Polearm", "Crossbow", "farmer", "father_sitting"),
                    P("vlandia_blacksmith_option", "{=p2KIhGbE}Urban blacksmith", "{=btsMpRcA}", "vigor", "Crafting", "TwoHanded", "artisan_urban", "side_to_side_2"),
                    P("vlandia_hunter_option", "{=YcnK0Thk}Hunters", "{=yRFSzSDZ}", "control", "Scouting", "Crossbow", "hunter", "side_to_side_3"),
                    P("vlandia_mercenary_option", "{=ipQP6aVi}Mercenaries", "{=yYhX6JQC}", "cunning", "Roguery", "Crossbow", "merchant_urban", "hugging"),
                    PN("vlandia_coastal_fisherman_option", "{=MPaZbhRc}Coastal fisherman", "{=VBy8WxVw}", "endurance", "Boatswain", "Scouting", "seafarer", "vlandia_fisherman", motherLeft: "fishnet_char_creation", fatherRight: "fishing_rod_s"),
                    PN("vlandia_dockers_option", "{=rsUCF3H8}Dockers", "{=OyIKF2r6}", "vigor", "Shipmaster", "Athletics", "shipmaster_urban", "vlandia_dockers", motherRight: "sack_s", fatherRight: "sack")
                },
                ["sturgia"] = new[]
                {
                    P("sturgia_companion_option", "{=mc78FEbA}A boyar's companions", "{=hob3WVkU}", "social", "Riding", "TwoHanded", "retainer", "side_to_side_1"),
                    P("sturgia_trader_option", "{=HqzVBfpl}Urban traders", "{=bjVMtW3W}", "cunning", "Trade", "Tactics", "merchant_urban", "mother_front"),
                    P("sturgia_farmer_option", "{=zrpqSWSh}Free farmers", "{=Mcd3ZyKq}", "endurance", "Athletics", "Polearm", "farmer", "father_sitting"),
                    P("sturgia_artisan_option", "{=v48N6h1t}Urban artisans", "{=ueCm5y1C}", "intelligence", "Crafting", "OneHanded", "artisan_urban", "side_to_side_2"),
                    P("sturgia_hunter_option", "{=YcnK0Thk}Hunters", "{=WyZ2UtFF}", "vigor", "Scouting", "Bow", "hunter", "side_to_side_3"),
                    P("sturgia_vagabond_option", "{=TPoK3GSj}Vagabonds", "{=2SDWhGmQ}", "control", "Roguery", "Throwing", "vagabond_urban", "hugging"),
                    PN("sturgia_river_fisherman_option", "{=iuAi8rZ4}River Fisherman", "{=gpNBMzW8}", "control", "Boatswain", "Throwing", "seafarer", "sturgia_riverfisherman", fatherLeft: "fishnet"),
                    PN("sturgia_shipbuilders_option", "{=V0GSUvaU}Shipbuilders", "{=9XmQrI23}", "intelligence", "Shipmaster", "Engineering", "shipmaster_urban", "sturgia_shipbuilder", fatherLeft: "blacksmith_hammer")
                },
                ["aserai"] = new[]
                {
                    P("aserai_kinsfolk_option", "{=Sw8OxnNr}Kinsfolk of an emir", "{=MFrIHJZM}", "social", "Riding", "Throwing", "retainer", "side_to_side_1"),
                    P("aserai_slave_option", "{=ngFVgwDD}Warrior-slaves", "{=GsPC2MgU}", "vigor", "Riding", "Polearm", "mercenary_urban", "mother_front"),
                    P("aserai_physician_option", "{=bgy8LVvY}Physician", "{=BhQlmQoj}", "intelligence", "Medicine", "Charm", "physician_urban", "father_sitting"),
                    P("aserai_farmer_option", "{=g31pXuqi}Oasis farmers", "{=5P0KqBAw}", "endurance", "Athletics", "OneHanded", "farmer", "side_to_side_2"),
                    P("aserai_herder_option", "{=EEedqolz}Bedouin", "{=PKhcPbBX}", "cunning", "Scouting", "Bow", "herder", "side_to_side_3"),
                    P("aserai_artisan_option", "{=tRIrbTvv}Urban back-alley thugs", "{=6bUSbsKC}", "control", "Roguery", "Polearm", "artisan_urban", "hugging"),
                    PN("aserai_ferryman_option", "{=PaXaNLrb}Ferryman", "{=LtOCnEC8}", "cunning", "Boatswain", "Trade", "seafarer", "aserai_ferryman", fatherRight: "shovel_right_hand"),
                    PN("aserai_corsair_traders_option", "{=V0IGaFFn}Corsair Traders", "{=Gl5CFpEM}", "vigor", "Boatswain", "Mariner", "shipmaster_urban", "aserai_corsair_trader")
                },
                ["battania"] = new[]
                {
                    P("battania_retainer_option", "{=GeNKQlHR}Members of the chieftain's hearthguard", "{=LpH8SYFL}", "vigor", "TwoHanded", "Bow", "retainer_urban", "side_to_side_1"),
                    P("battania_healer_option", "{=AeBzTj6w}Healers", "{=j6py5Rv5}", "intelligence", "Medicine", "Charm", "healer", "mother_front"),
                    P("battania_farmer_option", "{=tGEStbxb}Tribespeople", "{=WchH8bS2}", "control", "Athletics", "Throwing", "farmer", "father_sitting"),
                    P("battania_artisan_option", "{=BCU6RezA}Smiths", "{=kg9YtrOg}", "endurance", "Crafting", "TwoHanded", "artisan_urban", "side_to_side_2"),
                    P("battania_hunter_option", "{=7eWmU2mF}Foresters", "{=7jBroUUQ}", "cunning", "Scouting", "Tactics", "hunter", "side_to_side_3"),
                    P("battania_bard_option", "{=SpJqhEEh}Bards", "{=aVzcyhhy}", "social", "Roguery", "Charm", "bard_urban", "hugging"),
                    PN("battania_currach_sailors_option", "{=4zNU0J1S}Currach Sailors", "{=bnrmJHc6}", "control", "Boatswain", "Bow", "seafarer", "battania_currach_sailors", motherLeft: "bow", fatherRight: "battle_axe"),
                    PN("battania_guardian_of_the_lake_option", "{=o7BFw2WW}Guardian of the Lake", "{=ydyaMa6E}", "vigor", "Mariner", "Polearm", "shipmaster_urban", "battania_guardian_of_the_lake", motherRight: "javelin_a", fatherLeft: "heater_shield", fatherRight: "blacksmith_sword")
                },
                ["khuzait"] = new[]
                {
                    P("khuzait_retainer_option", "{=FVaRDe2a}A noyan's kinsfolk", "{=jAs3kDXh}", "endurance", "Riding", "Polearm", "retainer_urban", "side_to_side_1"),
                    P("khuzait_merhant_option", "{=TkgLEDRM}Merchants", "{=qPg3IDiq}", "social", "Trade", "Charm", "merchant_urban", "mother_front"),
                    P("khuzait_mercenary_option", "{=tGEStbxb}Tribespeople", "{=URgZ4ai4}", "control", "Bow", "Riding", "herder", "father_sitting"),
                    P("khuzait_farmer_option", "{=gQ2tAvCz}Farmers", "{=5QSGoRFj}", "vigor", "Polearm", "Throwing", "farmer", "side_to_side_2"),
                    P("khuzait_healer_option", "{=vfhVveLW}Shamans", "{=WOKNhaG2}", "intelligence", "Medicine", "Charm", "healer_urban", "side_to_side_3"),
                    P("khuzait_herder_option", "{=Xqba1Obq}Nomads", "{=9aoQYpZs}", "cunning", "Scouting", "Riding", "herder", "hugging"),
                    PN("khuzait_river_foragers_option", "{=fEIzJtSF}River Foragers", "{=2rNqqZnm}", "endurance", "Boatswain", "Shipmaster", "seafarer", "khuzait_river_foragers", fatherLeft: "fish_stick"),
                    // The installed NavalDLC intentionally reuses the river-forager
                    // parent poses for this second Khuzait option.
                    PN("khuzait_river_traders_option", "{=DQQogYtq}River Traders", "{=enS8isiB}", "cunning", "Bow", "Mariner", "shipmaster_urban", "khuzait_river_foragers", fatherLeft: "stick")
                },
                ["nords"] = new[]
                {
                    P("nord_hersir_option", "{=DRC5bTE5}Hersir", "{=w3AI4lwM}", "vigor", "Steward", "OneHanded", "retainer", "side_to_side_1"),
                    P("nord_market_trader_option", "{=uqpHfuZV}Peddler", "{=DvgmjoCE}", "social", "Trade", "Charm", "merchant_urban", "side_to_side_2"),
                    P("nord_skald_option", "{=1lX8eks5}Travelling skalds", "{=KtucaHqd}", "intelligence", "Scouting", "Charm", "bard", "father_sitting"),
                    P("nord_blacksmith_option", "{=v48N6h1t}Urban artisans", "{=AAHhp1ly}", "intelligence", "Crafting", "Engineering", "artisan_urban", "mother_front"),
                    P("nord_hunter_option", "{=izTHRXo5}Hunters", "{=rdRamFhv}", "control", "Bow", "Medicine", "hunter", "side_to_side_3"),
                    P("nord_vagabonds_option", "{=TPoK3GSj}Vagabonds", "{=nrtrMbLx}", "cunning", "Throwing", "Roguery", "vagabond_urban", "hugging"),
                    PN("nord_sailors_option", "{=6aKaV4ua}Sailors", "{=BbOM3F8H}", "social", "Trade", "Boatswain", "seafarer", "nord_sailors", motherRight: "fish_basket", fatherLeft: "fish_left_hand"),
                    PN("nord_shipwrights_option", "{=WYS68dRq}Shipwrights", "{=qUwVnncn}", "endurance", "Engineering", "Shipmaster", "shipmaster_urban", "nord_shipwrights", fatherRight: "blacksmith_hammer")
                }
            };

        private static readonly DikozemieHeroBackgroundChoice[] Childhood =
        {
            C("childhood_leadership_option", "{=kmM68Qx4}your leadership skills.", "{=FfNwXtii}", "cunning", "Leadership", "Tactics", "act_childhood_leader", 7),
            C("childhood_brawn_option", "{=5HXS8HEY}your brawn.", "{=YKzuGc54}", "vigor", "TwoHanded", "Throwing", "act_childhood_athlete", 7),
            C("childhood_detail_option", "{=QrYjPUEf}your attention to detail.", "{=JUSHAPnu}", "control", "Athletics", "Bow", "act_childhood_memory", 7),
            C("childhood_smart_option", "{=Y3UcaX74}your aptitude for numbers.", "{=DFidSjIf}", "intelligence", "Engineering", "Trade", "act_childhood_numbers", 7),
            C("childhood_leader_option", "{=GEYzLuwb}your way with people.", "{=w2TEQq26}", "social", "Charm", "Leadership", "act_childhood_manners", 7),
            C("childhood_horse_option", "{=MEgLE2kj}your skill with horses.", "{=ngazFofr}", "endurance", "Riding", "Medicine", "act_childhood_animals", 7),
            C("childhood_predict_weather_option", "{=cYIB0838}your uncanny ability to predict the weather.", "{=w77I1ijB}", "intelligence", "Boatswain", "Scouting", "act_childhood_memory", 7)
        };

        private static readonly DikozemieHeroBackgroundChoice[] RuralEducation =
        {
            WithProps(C("education_herder_option", "{=RKVNvimC}herded the sheep.", "{=KfaqPpbK}", "control", "Athletics", "Throwing", "act_childhood_streets", 12), right: "carry_bostaff_rogue1"),
            WithProps(C("education_smith_option", "{=bTKiN0hr}worked in the village smithy.", "{=y6j1bJTH}", "vigor", "TwoHanded", "Crafting", "act_childhood_militia", 12), right: "peasant_hammer_1_t1"),
            WithProps(C("education_engineer_option", "{=tI8ZLtoA}repaired projects.", "{=6LFj919J}", "intelligence", "Crafting", "Engineering", "act_childhood_grit", 12), right: "carry_hammer"),
            WithProps(C("education_doctor_option", "{=TRwgSLD2}gathered herbs in the wild.", "{=9ks4u5cH}", "endurance", "Medicine", "Scouting", "act_childhood_peddlers", 12), right: "_to_carry_bd_basket_a"),
            WithProps(C("education_hunter_option", "{=T7m7ReTq}hunted small game.", "{=RuvSk3QT}", "cunning", "Bow", "Tactics", "act_childhood_sharp", 12), right: "composite_bow"),
            WithProps(C("education_merchant_option", "{=qAbMagWq}sold product at the market.", "{=DIgsfYfz}", "social", "Trade", "Charm", "act_childhood_peddlers_2", 12), right: "_to_carry_bd_fabric_c"),
            C("education_fishing_boat", "{=MHXeREoc}worked as a deckhand on a fishing boat.", "{=3H4sk6zN}", "endurance", "Boatswain", "Athletics", "act_childhood_athlete", 12)
        };

        private static readonly DikozemieHeroBackgroundChoice[] UrbanEducation =
        {
            C("education_watcher_option", "{=go7Yu7KS}watched the militia training.", "{=qnqdEJOv}", "control", "Polearm", "Tactics", "act_childhood_fox", 12),
            C("education_ganger_option", "{=gAjvAGTa}hung out with the gangs in the alleys.", "{=1SUTcF0J}", "cunning", "Roguery", "OneHanded", "act_childhood_athlete", 12),
            WithProps(C("education_docker_option", "{=QVVCgajg}helped at building sites.", "{=bhdkegZ4}", "vigor", "Athletics", "Crafting", "act_childhood_peddlers", 12), right: "_to_carry_bd_basket_a"),
            C("education_marketer_option", "{=JTsv6PFe}worked in the markets and caravanserais.", "{=rmMcwSn8}", "social", "Trade", "Charm", "act_childhood_manners", 12),
            WithProps(C("education_tutor_option", "{=EMVojYzW}studied with your private tutor.", "{=hXl25avg}", "intelligence", "Engineering", "Leadership", "act_childhood_book", 12), left: "character_creation_notebook"),
            WithProps(C("education_horser_option", "{=hin3iA2D}cared for the horses.", "{=Ghz90npw}", "endurance", "Riding", "Steward", "act_childhood_peddlers_2", 12), right: "_to_carry_bd_fabric_c"),
            C("education_docks", "{=eTXb0QYP}worked at the docks.", "{=EDwrct2r}", "social", "Shipmaster", "Trade", "act_childhood_tough", 12)
        };

        private static readonly Dictionary<string, DikozemieHeroBackgroundChoice[]> Youth =
            new Dictionary<string, DikozemieHeroBackgroundChoice[]>(StringComparer.Ordinal)
            {
                ["empire"] = new[] { Staff1(), GuardEmpire(), RiderHigh(), Infantry(), Skirmisher(), Envoy1(), Galley() },
                ["vlandia"] = new[]
                {
                    Y("youth_groom_option", "{=bhE2i6OU}served as a baron's groom.", "{=i3k7YtA8}", "social", "Charm", "Tactics", "retainer", "act_childhood_sharp"),
                    Y("youth_cavalry_option", "{=h2KnarLL}trained with the cavalry.", "{=7cHsIMLP}", "endurance", "Riding", "Polearm", "mercenary", "act_childhood_apprentice"),
                    Y("youth_guard_high_register_option", "{=aTncHUfL}stood guard with the garrisons.", "{=63TAYbkx}", "intelligence", "Crossbow", "Engineering", "guard", "act_childhood_vibrant"),
                    Infantry(), Skirmisher(), Camp(), Galley()
                },
                ["sturgia"] = new[] { Hearth(), GuardLow(), RiderLow(), Infantry(), Skirmisher(), Camp(), RiverTrader() },
                ["aserai"] = new[] { Staff2(), GuardGarrison(), RiderLow(), Infantry(), Skirmisher(), Envoy2(), Corsair() },
                ["battania"] = new[] { Servant2(), Hearth(), GuardGarrison(), Infantry(), Kern(), Envoy2(), CoastalDefender() },
                ["khuzait"] = new[] { Servant1(), GuardGarrison(), RiderHigh(), Infantry(), Skirmisher(), Envoy1(), RiverRaider() },
                ["nords"] = new[]
                {
                    Y("youth_nord_guard_option", "{=I23UbK4E}served as a shieldbearer to a huscarl.", "{=Rffyscuk}", "social", "Charm", "Scouting", "guard", "act_character_creation_nord_served_as_a_shieldbearer"),
                    Y("youth_nord_skirmisher_option", "{=8c7mwLQQ}joined the raiders as a lookout.", "{=6X2hZY6z}", "cunning", "Throwing", "Tactics", "skirmisher", "act_childhood_fox"),
                    Y("youth_nord_vagabond_option", "{=T7B4KmHz}drafted to war as a thrall.", "{=lilGmaCg}", "endurance", "OneHanded", "Polearm", "vagabond", "act_drafted_to_war_pose"),
                    Y("youth_nord_artisan_option", "{=qJweXkmJ}stood sentry at the Walls of the Hold.", "{=XpiyI865}", "intelligence", "Engineering", "Bow", "artisan", "act_childhood_decisive"),
                    Y("youth_nord_infantry_option", "{=ZfaBIuFL}scavenged the battlefields for scraps.", "{=unIV7bqB}", "cunning", "Roguery", "Medicine", "infantry", "act_character_creation_nord_served_as_a_shieldbearer"),
                    Y("youth_nord_mercenary_option", "{=On8SIR0J}became a warchild of the North.", "{=La4V8zQn}", "control", "Throwing", "OneHanded", "mercenary", "act_childhood_decisive"),
                    Y("youth_serve_raider_ship_option", "{=8GnOKv5r}went serving on a raider ship.", "{=Xclr9fU3}", "vigor", "Mariner", "OneHanded", "seafarer", "act_childhood_athlete")
                }
            };

        private static readonly DikozemieHeroBackgroundChoice[] CommonAdulthood =
        {
            A("adulthood_defeated_enemy_option", "{=8bwpVpgy}you defeated an enemy in battle.", "{=1IEroJKs}", "vigor", "OneHanded", "TwoHanded", "act_childhood_athlete", 20, "Valor", 1),
            A("adulthood_nice_person_option", "{=Yqm0Dics}you treated people well.", "{=dDmcqTzb}", "social", "Charm", "Steward", "act_childhood_manners", 5, "Mercy", 1, "Generosity", 1, "Honor", 1)
        };

        private static readonly DikozemieHeroBackgroundChoice[] RuralAdulthood =
        {
            A("adulthood_manhunt_option", "{=mP3uFbcq}you led a successful manhunt.", "{=4f5xwzX0}", "cunning", "Tactics", "Leadership", "act_battania_mp_clan_warrior_shieldperk_idle", 10, "Calculating", 1),
            A("adulthood_investor_option", "{=xKXcqRJI}you invested some money in land.", "{=cbF9jdQo}", "intelligence", "Trade", "Crafting", "act_childhood_decisive", 10, "Calculating", 1),
            A("adulthood_hunter_option", "{=TbNRtUjb}you hunted a dangerous animal.", "{=I3PcdaaL}", "control", "Polearm", "Athletics", "act_childhood_tough", 5, "Valor", 1),
            A("adulthood_escapade_high_register_option", "{=kNXet6Um}you had a famous escapade in town.", "{=DjeAJtix}", "endurance", "Athletics", "Roguery", "act_childhood_clever", 5, "Valor", 1)
        };

        private static readonly DikozemieHeroBackgroundChoice SavedVillage =
            A("adulthood_saved_village_option", "{=x1HTX5hq}you saved your village from a flood.", "{=bWlmGDf3}", "cunning", "Tactics", "Leadership", "act_drafted_to_war_pose", 10, "Valor", 1);

        private static readonly DikozemieHeroBackgroundChoice[] UrbanAdulthood =
        {
            A("adulthood_caravan_leader_option", "{=wfbtS71d}you led a caravan.", "{=joRHKCkm}", "cunning", "Trade", "Leadership", "act_childhood_ready_handshield", 10, "Calculating", 1),
            A("adulthood_workshop_option", "{=xORjDTal}you invested some money in a workshop.", "{=PyVqDLBu}", "intelligence", "Trade", "Crafting", "act_childhood_decisive", 10, "Calculating", 1),
            A("adulthood_siege_survivor_option", "{=WbHfGCbd}you survived a siege.", "{=FhZPjhli}", "control", "Bow", "Crossbow", "act_childhood_tough", 5),
            A("adulthood_escapade_low_register_option", "{=qlOuiKXj}you had a famous escapade.", "{=lD5Ob3R4}", "endurance", "Athletics", "Roguery", "act_childhood_clever", 5, "Valor", 1)
        };

        private static readonly DikozemieHeroBackgroundChoice SavedCity =
            A("adulthood_saved_city_option", "{=s8PNllPN}you saved your city quarter from a fire.", "{=ZAGR6PYc}", "cunning", "Tactics", "Leadership", "act_childhood_vibrant", 10, "Calculating", 1);

        private static readonly DikozemieHeroBackgroundChoice[] Ages =
        {
            Age("age_selection_young_adult_option", "{=!}20", "{=2k7adlh7}", 20, 2, 1, "act_childhood_focus"),
            Age("age_selection_adult_option", "{=!}30", "{=NUlVFRtK}", 30, 4, 2, "act_childhood_athlete"),
            Age("age_selection_middle_age_option", "{=!}40", "{=5MxTYApM}", 40, 6, 3, "act_childhood_sharp"),
            Age("age_selection_elder_option", "{=!}50", "{=ePD5Afvy}", 50, 8, 4, "act_childhood_tough")
        };

        internal static IReadOnlyList<DikozemieHeroBackgroundChoice> GetChoices(
            int step,
            string cultureId,
            string parentOccupation)
        {
            string culture = NormalizeCulture(cultureId);
            switch (Math.Max(0, Math.Min(5, step)))
            {
                case 0:
                    return Parents[culture];
                case 1:
                    return Childhood;
                case 2:
                    return IsUrbanOccupation(parentOccupation)
                        ? UrbanEducation
                        : RuralEducation;
                case 3:
                    return Youth[culture];
                case 4:
                    return GetAdulthood(culture, parentOccupation);
                default:
                    return Ages;
            }
        }

        internal static DikozemieHeroBackgroundChoice FindChoice(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }
            foreach (string culture in Parents.Keys)
            {
                DikozemieHeroBackgroundChoice found = Parents[culture]
                    .Concat(Childhood)
                    .Concat(RuralEducation)
                    .Concat(UrbanEducation)
                    .Concat(Youth[culture])
                    .Concat(GetAdulthood(culture, string.Empty))
                    .Concat(GetAdulthood(culture, "merchant_urban"))
                    .Concat(Ages)
                    .FirstOrDefault(choice => string.Equals(choice.Id, id, StringComparison.Ordinal));
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        internal static string GetQuestion(
            int step,
            string parentOccupation,
            bool isFemale)
        {
            switch (Math.Max(0, Math.Min(5, step)))
            {
                case 0: return L("{=XgFU1pCx}You were born into a family of...");
                case 1: return L("{=character_creation_content_16}As a child you were noted for...");
                case 2: return L("{=WYvnWcXQ}Like all village children you helped out in the fields. You also...");
                case 3:
                    return isFemale
                        ? L("{=5kbeAC7k}In wartorn Calradia, especially in frontier or tribal areas, some women as well as men learn to fight from an early age. You...")
                        : L("{=F7OO5SAa}As a youngster growing up in Calradia, war was never too far away. You...");
                case 4: return L("{=4WYY0X59}Before you set out for a life of adventure, your biggest achievement was...");
                default: return L("{=VlOGrGSn}Your character started off on the adventuring path at the age of...");
            }
        }

        internal static string GetStageTitle(int step)
        {
            switch (Math.Max(0, Math.Min(5, step)))
            {
                case 0: return L("{=b4lDDcli}Family");
                case 1: return L("{=8Yiwt1z6}Early Childhood");
                case 2: return L("{=rcoueCmk}Adolescence");
                case 3: return L("{=ok8lSW6M}Youth");
                case 4: return L("{=MafIe9yI}Young Adulthood");
                default: return L("{=HDFEAYDk}Starting Age");
            }
        }

        internal static int GetPreviewAge(int step, DikozemieHeroBackgroundChoice choice)
        {
            if (choice != null && choice.PreviewAge > 0)
            {
                return choice.PreviewAge;
            }
            int[] ages = { 33, 7, 12, 17, 20, 20 };
            return ages[Math.Max(0, Math.Min(ages.Length - 1, step))];
        }

        internal static string GetEquipmentCultureId(string cultureId)
        {
            return string.Equals(cultureId, "nords", StringComparison.OrdinalIgnoreCase)
                ? "nord"
                : NormalizeCulture(cultureId);
        }

        internal static bool IsUrbanOccupation(string occupation)
        {
            return (occupation ?? string.Empty).EndsWith("_urban", StringComparison.Ordinal);
        }

        internal static string GetEquipmentKey(string occupation)
        {
            switch (occupation ?? string.Empty)
            {
                case "retainer_urban": return "retainer";
                case "merchant_urban": return "merchant";
                case "mercenary_urban": return "mercenary";
                case "vagabond_urban": return "vagabond";
                case "artisan_urban": return "artisan";
                case "physician_urban": return "physician";
                case "healer_urban": return "healer";
                case "bard_urban": return "bard";
                case "shipmaster_urban": return "shipmaster";
                default: return string.IsNullOrWhiteSpace(occupation) ? "none" : occupation;
            }
        }

        internal static string ResolvePreviewEquipmentId(
            int step,
            bool isFemale,
            string cultureId,
            string parentOccupation,
            string selectedTitleType,
            DikozemieHeroBackgroundChoice choice)
        {
            string culture = GetEquipmentCultureId(cultureId);
            string gender = isFemale ? "f" : "m";
            if (step == 0)
            {
                string occupation = choice?.ParentOccupation ?? parentOccupation;
                return (isFemale ? "mother" : "father") + "_char_creation_" +
                    GetEquipmentKey(occupation) + "_" + culture;
            }
            if (step == 1)
            {
                return "player_char_creation_childhood_age_" + culture + "_" +
                    GetEquipmentKey(parentOccupation) + "_" + gender;
            }
            if (step == 2)
            {
                return "player_char_creation_education_age_" + culture + "_" +
                    GetEquipmentKey(parentOccupation) + "_" + gender;
            }
            string titleType = choice?.SelectedTitleType;
            if (string.IsNullOrWhiteSpace(titleType))
            {
                titleType = selectedTitleType;
            }
            if (string.IsNullOrWhiteSpace(titleType))
            {
                titleType = "guard";
            }
            return "player_char_creation_" + culture + "_" + titleType + "_" + gender;
        }

        internal const string MissingTextError = ": missing text for ";

        internal static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            foreach (string culture in Parents.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                ValidateCount(errors, culture, 0, Parents[culture], 8);
                ValidateCount(errors, culture, 1, Childhood, 7);
                ValidateCount(errors, culture + "/rural", 2, RuralEducation, 7);
                ValidateCount(errors, culture + "/urban", 2, UrbanEducation, 7);
                ValidateCount(errors, culture, 3, Youth[culture], 7);
                ValidateCount(errors, culture + "/rural", 4, GetAdulthood(culture, string.Empty), 6);
                ValidateCount(errors, culture + "/urban", 4, GetAdulthood(culture, "merchant_urban"), 6);
                ValidateCount(errors, culture, 5, Ages, 4);

                foreach (DikozemieHeroBackgroundChoice parent in Parents[culture])
                {
                    string branch = culture + "/" + parent.ParentOccupation;
                    ValidateCount(errors, branch, 2,
                        GetChoices(2, culture, parent.ParentOccupation), 7);
                    ValidateCount(errors, branch, 4,
                        GetChoices(4, culture, parent.ParentOccupation), 6);
                    foreach (bool isFemale in new[] { false, true })
                    {
                        ValidateEquipmentId(errors, branch, 0, isFemale,
                            culture, parent.ParentOccupation, "guard", parent);
                        ValidateEquipmentId(errors, branch, 1, isFemale,
                            culture, parent.ParentOccupation, "guard", null);
                        ValidateEquipmentId(errors, branch, 2, isFemale,
                            culture, parent.ParentOccupation, "guard", null);
                        foreach (DikozemieHeroBackgroundChoice youth in Youth[culture])
                        {
                            ValidateEquipmentId(errors, branch, 3, isFemale,
                                culture, parent.ParentOccupation,
                                youth.SelectedTitleType, youth);
                        }
                    }
                }
            }
            return errors;
        }

        internal static int CompleteNarrativePathCount =>
            Parents.Count * 8 * 7 * 7 * 7 * 6 * 4;

        private static void ValidateEquipmentId(
            ICollection<string> errors,
            string branch,
            int step,
            bool isFemale,
            string culture,
            string parentOccupation,
            string selectedTitleType,
            DikozemieHeroBackgroundChoice choice)
        {
            string id = ResolvePreviewEquipmentId(step, isFemale, culture,
                parentOccupation, selectedTitleType, choice);
            if (string.IsNullOrWhiteSpace(id) || !id.Contains("char_creation_"))
            {
                errors.Add(branch + ": invalid preview equipment at step " +
                    step + " (" + (isFemale ? "f" : "m") + ")");
            }
        }

        private static IReadOnlyList<DikozemieHeroBackgroundChoice> GetAdulthood(
            string culture,
            string parentOccupation)
        {
            var result = new List<DikozemieHeroBackgroundChoice>
            {
                CommonAdulthood[0]
            };
            if (IsUrbanOccupation(parentOccupation))
            {
                if (culture == "battania")
                {
                    result.Add(SavedCity);
                }
                else
                {
                    result.Add(UrbanAdulthood[0]);
                }
                result.AddRange(UrbanAdulthood.Skip(1));
            }
            else
            {
                if (culture == "sturgia" || culture == "nords")
                {
                    result.Add(SavedVillage);
                }
                else
                {
                    result.Add(RuralAdulthood[0]);
                }
                result.AddRange(RuralAdulthood.Skip(1));
            }
            result.Add(CommonAdulthood[1]);
            return result;
        }

        private static void ValidateCount(
            ICollection<string> errors,
            string culture,
            int step,
            IReadOnlyList<DikozemieHeroBackgroundChoice> choices,
            int expected)
        {
            if (choices.Count != expected)
            {
                errors.Add(culture + ": step " + step + " has " + choices.Count +
                    " choices, expected " + expected);
            }
            foreach (DikozemieHeroBackgroundChoice choice in choices)
            {
                if (string.IsNullOrWhiteSpace(choice.Id) ||
                    choices.Count(item => item.Id == choice.Id) != 1)
                {
                    errors.Add(culture + ": duplicate/empty id at step " + step);
                }
                if (string.IsNullOrWhiteSpace(choice.Title) ||
                    string.IsNullOrWhiteSpace(choice.Description))
                {
                    errors.Add(culture + MissingTextError + choice.Id);
                }
                if (step < 5 && (choice.Skills.Count != 2 ||
                    string.IsNullOrWhiteSpace(choice.Attribute)))
                {
                    errors.Add(culture + ": incomplete rewards for " + choice.Id);
                }
                if (step == 0 && (string.IsNullOrWhiteSpace(choice.ParentOccupation) ||
                    string.IsNullOrWhiteSpace(choice.MotherAnimationId) ||
                    string.IsNullOrWhiteSpace(choice.FatherAnimationId)))
                {
                    errors.Add(culture + ": incomplete parent preview for " + choice.Id);
                }
                if (step > 0 && string.IsNullOrWhiteSpace(choice.AnimationId))
                {
                    errors.Add(culture + ": missing animation for " + choice.Id);
                }
            }
        }

        private static DikozemieHeroBackgroundChoice P(
            string id, string title, string description, string attribute,
            string skill1, string skill2, string occupation, string pose)
        {
            var choice = C(id, title, description, attribute, skill1, skill2,
                string.Empty, 33);
            choice.ParentOccupation = occupation;
            choice.MotherAnimationId = "act_character_creation_female_default_" + pose;
            choice.FatherAnimationId = "act_character_creation_male_default_" + pose;
            return choice;
        }

        private static DikozemieHeroBackgroundChoice PN(
            string id, string title, string description, string attribute,
            string skill1, string skill2, string occupation, string pose,
            string motherLeft = "", string motherRight = "",
            string fatherLeft = "", string fatherRight = "")
        {
            var choice = C(id, title, description, attribute, skill1, skill2,
                string.Empty, 33);
            choice.ParentOccupation = occupation;
            choice.MotherAnimationId = "act_character_creation_" + pose + "_mother";
            choice.FatherAnimationId = "act_character_creation_" + pose + "_father";
            choice.MotherLeftPropId = motherLeft;
            choice.MotherRightPropId = motherRight;
            choice.FatherLeftPropId = fatherLeft;
            choice.FatherRightPropId = fatherRight;
            return choice;
        }

        private static DikozemieHeroBackgroundChoice C(
            string id, string title, string description, string attribute,
            string skill1, string skill2, string animation, int age)
        {
            return new DikozemieHeroBackgroundChoice(
                id, title, description, InferTrack(skill1, skill2), attribute,
                skill1, skill2)
            {
                AnimationId = animation,
                PreviewAge = age
            };
        }

        private static DikozemieHeroBackgroundChoice Y(
            string id, string title, string description, string attribute,
            string skill1, string skill2, string titleType, string animation)
        {
            DikozemieHeroBackgroundChoice choice = C(
                id, title, description, attribute, skill1, skill2, animation, 17);
            choice.SelectedTitleType = titleType;
            return choice;
        }

        private static DikozemieHeroBackgroundChoice WithProps(
            DikozemieHeroBackgroundChoice choice,
            string left = "",
            string right = "")
        {
            choice.LeftPropId = left;
            choice.RightPropId = right;
            return choice;
        }

        private static DikozemieHeroBackgroundChoice A(
            string id, string title, string description, string attribute,
            string skill1, string skill2, string animation, int renown,
            params object[] traits)
        {
            DikozemieHeroBackgroundChoice choice = C(
                id, title, description, attribute, skill1, skill2, animation, 20);
            choice.Renown = renown;
            var values = new List<KeyValuePair<string, int>>();
            for (int index = 0; index + 1 < traits.Length; index += 2)
            {
                values.Add(new KeyValuePair<string, int>(
                    Convert.ToString(traits[index]),
                    Convert.ToInt32(traits[index + 1])));
            }
            choice.Traits = values;
            return choice;
        }

        private static DikozemieHeroBackgroundChoice Age(
            string id, string title, string description, int age,
            int focus, int attributes, string animation)
        {
            return new DikozemieHeroBackgroundChoice(
                id, title, description, "guard", string.Empty)
            {
                PreviewAge = age,
                UnspentFocusPoints = focus,
                UnspentAttributePoints = attributes,
                AnimationId = animation
            };
        }

        private static string InferTrack(string first, string second)
        {
            string[] skills = { first, second };
            if (skills.Contains("Bow") || skills.Contains("Crossbow")) return "archer";
            if (skills.Contains("Riding")) return "rider";
            if (skills.Contains("Throwing")) return "skirmish";
            if (skills.Contains("TwoHanded")) return "shock";
            return "guard";
        }

        private static string NormalizeCulture(string cultureId)
        {
            string value = (cultureId ?? string.Empty).Trim().ToLowerInvariant();
            return Parents.ContainsKey(value) ? value : "empire";
        }

        private static string L(string source) => DikozemieCreationText.Localize(source);

        private static DikozemieHeroBackgroundChoice Staff1() => Y("youth_staff_first_option", "{=CITG915d}joined a commander's staff.", "{=wNHqFlDL}", "cunning", "Steward", "Tactics", "retainer", "act_childhood_decisive");
        private static DikozemieHeroBackgroundChoice Staff2() => Y("youth_staff_second_option", "{=CITG915d}joined a commander's staff.", "{=ANbNblaH}", "cunning", "Steward", "Tactics", "retainer", "act_childhood_decisive");
        private static DikozemieHeroBackgroundChoice GuardEmpire() => Y("youth_guard_empire_register_option", "{=aTncHUfL}stood guard with the garrisons.", "{=oR58iNDz}", "intelligence", "Crossbow", "Engineering", "guard", "act_childhood_sharp");
        private static DikozemieHeroBackgroundChoice GuardLow() => Y("youth_guard_low_register_option", "{=aTncHUfL}stood guard with the garrisons.", "{=oR58iNDz}", "intelligence", "Bow", "Engineering", "guard", "act_childhood_sharp");
        private static DikozemieHeroBackgroundChoice GuardGarrison() => Y("youth_guard_garrisons_register_option", "{=aTncHUfL}stood guard with the garrisons.", "{=e6lINjFg}", "intelligence", "Bow", "Engineering", "guard", "act_childhood_sharp");
        private static DikozemieHeroBackgroundChoice RiderHigh() => Y("youth_rider_high_register_option", "{=VlXOgIX6}rode with the scouts.", "{=888lmJqs}", "endurance", "Riding", "Bow", "hunter", "act_sturgia_mp_warrior_axe");
        private static DikozemieHeroBackgroundChoice RiderLow() => Y("youth_rider_low_register_option", "{=VlXOgIX6}rode with the scouts.", "{=sYuN6hPD}", "endurance", "Riding", "Bow", "hunter", "act_sturgia_mp_huskarl_idle");
        private static DikozemieHeroBackgroundChoice Infantry() => Y("youth_infantry_option", "{=a8arFSra}trained with the infantry.", "{=afH90aNs}", "vigor", "Polearm", "OneHanded", "infantry", "act_childhood_fierce");
        private static DikozemieHeroBackgroundChoice Skirmisher() => Y("youth_skirmisher_option", "{=oMbOIPc9}joined the skirmishers.", "{=bXAg5w19}", "control", "Throwing", "OneHanded", "skirmisher", "act_childhood_fox");
        private static DikozemieHeroBackgroundChoice Envoy1() => Y("youth_envoys_guard_first_option", "{=YmPlLGXb}served in an envoy's entourage", "{=qPamcCkA}", "social", "Charm", "Scouting", "guard", "act_childhood_sharp");
        private static DikozemieHeroBackgroundChoice Envoy2() => Y("youth_envoys_guard_second_option", "{=YmPlLGXb}served in an envoy's entourage", "{=VYU1nEHP}", "social", "Charm", "Scouting", "guard", "act_childhood_sharp");
        private static DikozemieHeroBackgroundChoice Camp() => Y("youth_camp_option", "{=GFUggps8}marched with the camp followers.", "{=64rWqBLN}", "cunning", "Roguery", "Throwing", "bard", "act_childhood_militia");
        private static DikozemieHeroBackgroundChoice Hearth() => Y("youth_hearth_option", "{=zsC2t5Hb}trained with the hearth guard.", "{=RmbWW6Bm}", "endurance", "Riding", "Polearm", "mercenary", "act_childhood_athlete");
        private static DikozemieHeroBackgroundChoice Kern() => Y("youth_kern_option", "{=cDWbwBwI}joined the kern.", "{=tTb28jyU}", "control", "Throwing", "OneHanded", "kern", "act_childhood_apprentice");
        private static DikozemieHeroBackgroundChoice Servant1() => Y("youth_servant_first_option", "{=F2bgujPo}were a chieftain's servant.", "{=AXWO4C69}", "cunning", "Steward", "Tactics", "retainer", "act_childhood_ready");
        private static DikozemieHeroBackgroundChoice Servant2() => Y("youth_servant_second_option", "{=F2bgujPo}were a chieftain's servant.", "{=neMCgMZM}", "cunning", "Steward", "Tactics", "retainer", "act_childhood_ready");
        private static DikozemieHeroBackgroundChoice Galley() => Y("youth_crewed_a_galley_option", "{=Hhkt6gtQ}crewed a galley in the coastal raids.", "{=KWI2QOAO}", "vigor", "Mariner", "OneHanded", "seafarer", "act_childhood_athlete");
        private static DikozemieHeroBackgroundChoice RiverTrader() => Y("youth_rowed_river_trader_option", "{=BRcMIDYK}rowed on a river trader.", "{=urpdbYXl}", "endurance", "Boatswain", "TwoHanded", "seafarer", "act_childhood_athlete");
        private static DikozemieHeroBackgroundChoice Corsair() => Y("youth_deckhand_corsair_option", "{=h0h4abww}served as a deckhand on a corsair.", "{=LVxRFT5b}", "cunning", "Mariner", "OneHanded", "seafarer", "act_childhood_athlete");
        private static DikozemieHeroBackgroundChoice CoastalDefender() => Y("youth_coastal_defender_option", "{=OMnJGBCR}served as a coastal defender.", "{=gvp4AsMQ}", "vigor", "Bow", "Boatswain", "seafarer", "act_childhood_athlete");
        private static DikozemieHeroBackgroundChoice RiverRaider() => Y("youth_raided_river_traffic_option", "{=C04DgO2S}raided river traffic.", "{=lHd0H3jg}", "social", "Mariner", "Charm", "seafarer", "act_childhood_athlete");
    }
}
