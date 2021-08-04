/*
 * Copyright (C) 2012-2020 CypherCore <http://github.com/CypherCore>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

namespace Framework.Database
{
    public class HotfixDatabase : MySqlBase<HotfixStatements>
    {
        public override void PreparedStatements()
        {
            // AnimationData.db2
            PrepareStatement(HotfixStatements.SEL_ANIMATION_DATA, "SELECT ID, BehaviorID, BehaviorTier, Fallback, Flags1, Flags2 FROM animation_data");

            // AnimKit.db2
            PrepareStatement(HotfixStatements.SEL_ANIM_KIT, "SELECT ID, OneShotDuration, OneShotStopAnimKitID, LowDefAnimKitID FROM anim_kit");

            // AreaGroupMember.db2
            PrepareStatement(HotfixStatements.SEL_AREA_GROUP_MEMBER, "SELECT ID, AreaID, AreaGroupID FROM area_group_member");

            // AreaTable.db2
            PrepareStatement(HotfixStatements.SEL_AREA_TABLE, "SELECT ID, ZoneName, AreaName, ContinentID, ParentAreaID, AreaBit, SoundProviderPref, " +
                "SoundProviderPrefUnderwater, AmbienceID, UwAmbience, ZoneMusic, UwZoneMusic, IntroSound, UwIntroSound, FactionGroupMask, AmbientMultiplier, " +
                "MountFlags, PvpCombatWorldStateID, WildBattlePetLevelMin, WildBattlePetLevelMax, WindSettingsID, ContentTuningID, Flags1, Flags2, " +
                "LiquidTypeID1, LiquidTypeID2, LiquidTypeID3, LiquidTypeID4 FROM area_table");
            PrepareStatement(HotfixStatements.SEL_AREA_TABLE_LOCALE, "SELECT ID, AreaName_lang FROM area_table_locale WHERE locale = ?");

            // AreaTrigger.db2
            PrepareStatement(HotfixStatements.SEL_AREA_TRIGGER, "SELECT PosX, PosY, PosZ, ID, ContinentID, PhaseUseFlags, PhaseID, PhaseGroupID, Radius, BoxLength, " +
                "BoxWidth, BoxHeight, BoxYaw, ShapeType, ShapeID, AreaTriggerActionSetID, Flags FROM area_trigger");

            // AuctionHouse.db2
            PrepareStatement(HotfixStatements.SEL_AUCTION_HOUSE, "SELECT ID, Name, FactionID, DepositRate, ConsignmentRate FROM auction_house");
            PrepareStatement(HotfixStatements.SEL_AUCTION_HOUSE_LOCALE, "SELECT ID, Name_lang FROM auction_house_locale WHERE locale = ?");

            // AzeriteEmpoweredItem.db2
            PrepareStatement(HotfixStatements.SEL_AZERITE_EMPOWERED_ITEM, "SELECT ID, ItemID, AzeriteTierUnlockSetID, AzeritePowerSetID FROM azerite_empowered_item");

            // BankBagSlotPrices.db2
            PrepareStatement(HotfixStatements.SEL_BANK_BAG_SLOT_PRICES, "SELECT ID, Cost FROM bank_bag_slot_prices");

            // BannedAddons.db2
            PrepareStatement(HotfixStatements.SEL_BANNED_ADDONS, "SELECT ID, Name, Version, Flags FROM banned_addons");

            // BattlemasterList.db2
            PrepareStatement(HotfixStatements.SEL_BATTLEMASTER_LIST, "SELECT ID, Name, GameType, ShortDescription, LongDescription, InstanceType, MinLevel, MaxLevel, " +
                "RatedPlayers, MinPlayers, MaxPlayers, GroupsAllowed, MaxGroupSize, HolidayWorldState, Flags, IconFileDataID, RequiredPlayerConditionID, " +
                "MapID1, MapID2, MapID3, MapID4, MapID5, MapID6, MapID7, MapID8, MapID9, MapID10, MapID11, MapID12, MapID13, MapID14, MapID15, MapID16" +
                " FROM battlemaster_list");
            PrepareStatement(HotfixStatements.SEL_BATTLEMASTER_LIST_LOCALE, "SELECT ID, Name_lang, GameType_lang, ShortDescription_lang, LongDescription_lang" +
                " FROM battlemaster_list_locale WHERE locale = ?");

            // CfgRegions.db2
            PrepareStatement(HotfixStatements.SEL_CFG_REGIONS, "SELECT ID, Tag, RegionID, Raidorigin, RegionGroupMask, ChallengeOrigin FROM cfg_regions");

            // CharTitles.db2
            PrepareStatement(HotfixStatements.SEL_CHAR_TITLES, "SELECT ID, Name, Name1, MaskID, Flags FROM char_titles");
            PrepareStatement(HotfixStatements.SEL_CHAR_TITLES_LOCALE, "SELECT ID, Name_lang, Name1_lang FROM char_titles_locale WHERE locale = ?");

            // CharacterLoadout.db2
            PrepareStatement(HotfixStatements.SEL_CHARACTER_LOADOUT, "SELECT RaceMask, ID, ChrClassID, Purpose FROM character_loadout");

            // CharacterLoadoutItem.db2
            PrepareStatement(HotfixStatements.SEL_CHARACTER_LOADOUT_ITEM, "SELECT ID, CharacterLoadoutID, ItemID FROM character_loadout_item");

            // ChatChannels.db2
            PrepareStatement(HotfixStatements.SEL_CHAT_CHANNELS, "SELECT Name, Shortcut, ID, Flags, FactionGroup, Ruleset FROM chat_channels");
            PrepareStatement(HotfixStatements.SEL_CHAT_CHANNELS_LOCALE, "SELECT ID, Name_lang, Shortcut_lang FROM chat_channels_locale WHERE locale = ?");

            // ChrClasses.db2
            PrepareStatement(HotfixStatements.SEL_CHR_CLASSES, "SELECT Name, Filename, NameMale, NameFemale, PetNameToken, Description, RoleInfoString, DisabledString, " +
                "HyphenatedNameMale, HyphenatedNameFemale, ID, CreateScreenFileDataID, SelectScreenFileDataID, IconFileDataID, LowResScreenFileDataID, Flags, " +
                "SpellTextureBlobFileDataID, RolesMask, ArmorTypeMask, CharStartKitUnknown901, MaleCharacterCreationVisualFallback, " +
                "MaleCharacterCreationIdleVisualFallback, FemaleCharacterCreationVisualFallback, FemaleCharacterCreationIdleVisualFallback, " +
                "CharacterCreationIdleGroundVisualFallback, CharacterCreationGroundVisualFallback, AlteredFormCharacterCreationIdleVisualFallback, " +
                "CharacterCreationAnimLoopWaitTimeMsFallback, CinematicSequenceID, DefaultSpec, PrimaryStatPriority, DisplayPower, " +
                "RangedAttackPowerPerAgility, AttackPowerPerAgility, AttackPowerPerStrength, SpellClassSet, ChatColorR, ChatColorG, ChatColorB FROM chr_classes");
            PrepareStatement(HotfixStatements.SEL_CHR_CLASSES_LOCALE, "SELECT ID, Name_lang, NameMale_lang, NameFemale_lang, Description_lang, RoleInfoString_lang, " +
                "DisabledString_lang, HyphenatedNameMale_lang, HyphenatedNameFemale_lang FROM chr_classes_locale WHERE locale = ?");

            // ChrClassesXPowerTypes.db2
            PrepareStatement(HotfixStatements.SEL_CHR_CLASSES_X_POWER_TYPES, "SELECT ID, PowerType, ClassID FROM chr_classes_x_power_types");

            // ChrCustomizationChoice.db2
            PrepareStatement(HotfixStatements.SEL_CHR_CUSTOMIZATION_CHOICE, "SELECT Name, ID, ChrCustomizationOptionID, ChrCustomizationReqID, SortOrder, SwatchColor1, " +
                "SwatchColor2, UiOrderIndex, Flags FROM chr_customization_choice");
            PrepareStatement(HotfixStatements.SEL_CHR_CUSTOMIZATION_CHOICE_LOCALE, "SELECT ID, Name_lang FROM chr_customization_choice_locale WHERE locale = ?");

            // ChrCustomizationElement.db2
            PrepareStatement(HotfixStatements.SEL_CHR_CUSTOMIZATION_ELEMENT, "SELECT ID, ChrCustomizationChoiceID, RelatedChrCustomizationChoiceID, " +
                "ChrCustomizationGeosetID, ChrCustomizationSkinnedModelID, ChrCustomizationMaterialID, ChrCustomizationBoneSetID, " +
                "ChrCustomizationCondModelID, ChrCustomizationDisplayInfoID, ChrCustItemGeoModifyID FROM chr_customization_element");

            // ChrCustomizationOption.db2
            PrepareStatement(HotfixStatements.SEL_CHR_CUSTOMIZATION_OPTION, "SELECT Name, ID, SecondaryID, Flags, ChrModelID, SortIndex, ChrCustomizationCategoryID, " +
                "OptionType, BarberShopCostModifier, ChrCustomizationID, ChrCustomizationReqID, UiOrderIndex FROM chr_customization_option");
            PrepareStatement(HotfixStatements.SEL_CHR_CUSTOMIZATION_OPTION_LOCALE, "SELECT ID, Name_lang FROM chr_customization_option_locale WHERE locale = ?");

            // ChrCustomizationReq.db2
            PrepareStatement(HotfixStatements.SEL_CHR_CUSTOMIZATION_REQ, "SELECT ID, Flags, ClassMask, AchievementID, OverrideArchive, ItemModifiedAppearanceID FROM chr_customization_req");

            // ChrModel.db2
            PrepareStatement(HotfixStatements.SEL_CHR_MODEL, "SELECT FaceCustomizationOffset1, FaceCustomizationOffset2, FaceCustomizationOffset3, CustomizeOffset1, " +
                "CustomizeOffset2, CustomizeOffset3, ID, Sex, DisplayID, CharComponentTextureLayoutID, Flags, SkeletonFileDataID, ModelFallbackChrModelID, " +
                "TextureFallbackChrModelID, HelmVisFallbackChrModelID, CustomizeScale, CustomizeFacing, CameraDistanceOffset, BarberShopCameraOffsetScale, " +
                "BarberShopCameraRotationOffset FROM chr_model");

            // ChrRaceXChrModel.db2
            PrepareStatement(HotfixStatements.SEL_CHR_RACE_X_CHR_MODEL, "SELECT ID, ChrRacesID, ChrModelID FROM chr_race_x_chr_model");

            // ChrRaces.db2
            PrepareStatement(HotfixStatements.SEL_CHR_RACES, "SELECT ClientPrefix, ClientFileString, Name, NameFemale, NameLowercase, NameFemaleLowercase, NameS, " +
                "NameFemaleS, NameLowercaseS, NameFemaleLowercaseS, RaceFantasyDescription, NameL, NameFemaleL, NameLowercaseL, NameFemaleLowercaseL, ID, " +
                "Flags, BaseLanguage, ResSicknessSpellID, SplashSoundID, CreateScreenFileDataID, SelectScreenFileDataID, LowResScreenFileDataID, " +
                "AlteredFormStartVisualKitID1, AlteredFormStartVisualKitID2, AlteredFormStartVisualKitID3, AlteredFormFinishVisualKitID1, " +
                "AlteredFormFinishVisualKitID2, AlteredFormFinishVisualKitID3, HeritageArmorAchievementID, StartingLevel, UiDisplayOrder, PlayableRaceBit, " +
                "HelmetAnimScalingRaceID, TransmogrifyDisabledSlotMask, AlteredFormCustomizeOffsetFallback1, AlteredFormCustomizeOffsetFallback2, " +
                "AlteredFormCustomizeOffsetFallback3, AlteredFormCustomizeRotationFallback, FactionID, CinematicSequenceID, CreatureType, Alliance, " +
                "RaceRelated, UnalteredVisualRaceID, DefaultClassID, NeutralRaceID, MaleModelFallbackRaceID, MaleModelFallbackSex, FemaleModelFallbackRaceID, " +
                "FemaleModelFallbackSex, MaleTextureFallbackRaceID, MaleTextureFallbackSex, FemaleTextureFallbackRaceID, FemaleTextureFallbackSex, " +
                "UnalteredVisualCustomizationRaceID FROM chr_races");
            PrepareStatement(HotfixStatements.SEL_CHR_RACES_LOCALE, "SELECT ID, Name_lang, NameFemale_lang, NameLowercase_lang, NameFemaleLowercase_lang, NameS_lang, " +
                "NameFemaleS_lang, NameLowercaseS_lang, NameFemaleLowercaseS_lang, RaceFantasyDescription_lang, NameL_lang, NameFemaleL_lang, " +
                "NameLowercaseL_lang, NameFemaleLowercaseL_lang FROM chr_races_locale WHERE locale = ?");

            // CinematicCamera.db2
            PrepareStatement(HotfixStatements.SEL_CINEMATIC_CAMERA, "SELECT ID, OriginX, OriginY, OriginZ, SoundID, OriginFacing, FileDataID FROM cinematic_camera");

            // CinematicSequences.db2
            PrepareStatement(HotfixStatements.SEL_CINEMATIC_SEQUENCES, "SELECT ID, SoundID, Camera1, Camera2, Camera3, Camera4, Camera5, Camera6, Camera7, Camera8 FROM cinematic_sequences");

            // ContentTuning.db2
            PrepareStatement(HotfixStatements.SEL_CONTENT_TUNING, "SELECT ID, Flags, ExpansionID, MinLevel, MaxLevel, MinLevelType, MaxLevelType, TargetLevelDelta, " +
                "TargetLevelMaxDelta, TargetLevelMin, TargetLevelMax, MinItemLevel FROM content_tuning");

            // CreatureDisplayInfo.db2
            PrepareStatement(HotfixStatements.SEL_CREATURE_DISPLAY_INFO, "SELECT ID, ModelID, SoundID, SizeClass, CreatureModelScale, CreatureModelAlpha, BloodID, " +
                "ExtendedDisplayInfoID, NPCSoundID, ParticleColorID, PortraitCreatureDisplayInfoID, PortraitTextureFileDataID, ObjectEffectPackageID, " +
                "AnimReplacementSetID, Flags, StateSpellVisualKitID, PlayerOverrideScale, PetInstanceScale, UnarmedWeaponType, MountPoofSpellVisualKitID, " +
                "DissolveEffectID, Gender, DissolveOutEffectID, CreatureModelMinLod, TextureVariationFileDataID1, TextureVariationFileDataID2, " +
                "TextureVariationFileDataID3 FROM creature_display_info");

            // CreatureDisplayInfoExtra.db2
            PrepareStatement(HotfixStatements.SEL_CREATURE_DISPLAY_INFO_EXTRA, "SELECT ID, DisplayRaceID, DisplaySexID, DisplayClassID, Flags, BakeMaterialResourcesID, " +
                "HDBakeMaterialResourcesID FROM creature_display_info_extra");

            // CreatureFamily.db2
            PrepareStatement(HotfixStatements.SEL_CREATURE_FAMILY, "SELECT ID, Name, MinScale, MinScaleLevel, MaxScale, MaxScaleLevel, PetFoodMask, PetTalentType, " +
                "IconFileID, SkillLine1, SkillLine2 FROM creature_family");
            PrepareStatement(HotfixStatements.SEL_CREATURE_FAMILY_LOCALE, "SELECT ID, Name_lang FROM creature_family_locale WHERE locale = ?");

            // CreatureModelData.db2
            PrepareStatement(HotfixStatements.SEL_CREATURE_MODEL_DATA, "SELECT ID, GeoBox1, GeoBox2, GeoBox3, GeoBox4, GeoBox5, GeoBox6, Flags, FileDataID, BloodID, " +
                "FootprintTextureID, FootprintTextureLength, FootprintTextureWidth, FootprintParticleScale, FoleyMaterialID, FootstepCameraEffectID, " +
                "DeathThudCameraEffectID, SoundID, SizeClass, CollisionWidth, CollisionHeight, WorldEffectScale, CreatureGeosetDataID, HoverHeight, " +
                "AttachedEffectScale, ModelScale, MissileCollisionRadius, MissileCollisionPush, MissileCollisionRaise, MountHeight, OverrideLootEffectScale, " +
                "OverrideNameScale, OverrideSelectionRadius, TamedPetBaseScale, Unknown820_1, Unknown820_2, Unknown820_31, Unknown820_32" +
                " FROM creature_model_data");

            // CreatureType.db2
            PrepareStatement(HotfixStatements.SEL_CREATURE_TYPE, "SELECT ID, Name, Flags FROM creature_type");
            PrepareStatement(HotfixStatements.SEL_CREATURE_TYPE_LOCALE, "SELECT ID, Name_lang FROM creature_type_locale WHERE locale = ?");

            // Criteria.db2
            PrepareStatement(HotfixStatements.SEL_CRITERIA, "SELECT ID, Type, Asset, ModifierTreeId, StartEvent, StartAsset, StartTimer, FailEvent, FailAsset, Flags, " +
                "EligibilityWorldStateID, EligibilityWorldStateValue FROM criteria");

            // CriteriaTree.db2
            PrepareStatement(HotfixStatements.SEL_CRITERIA_TREE, "SELECT ID, Description, Parent, Amount, Operator, CriteriaID, OrderIndex, Flags FROM criteria_tree");
            PrepareStatement(HotfixStatements.SEL_CRITERIA_TREE_LOCALE, "SELECT ID, Description_lang FROM criteria_tree_locale WHERE locale = ?");

            // CurrencyTypes.db2
            PrepareStatement(HotfixStatements.SEL_CURRENCY_TYPES, "SELECT ID, Name, Description, CategoryID, InventoryIconFileID, SpellWeight, SpellCategory, MaxQty, " +
                "MaxEarnablePerWeek, Quality, FactionID, ItemGroupSoundsID, XpQuestDifficulty, AwardConditionID, MaxQtyWorldStateID, Flags1, Flags2 FROM currency_types");
            PrepareStatement(HotfixStatements.SEL_CURRENCY_TYPES_LOCALE, "SELECT ID, Name_lang, Description_lang FROM currency_types_locale WHERE locale = ?");

            // Curve.db2
            PrepareStatement(HotfixStatements.SEL_CURVE, "SELECT ID, Type, Flags FROM curve");

            // CurvePoint.db2
            PrepareStatement(HotfixStatements.SEL_CURVE_POINT, "SELECT ID, PosX, PosY, PosPreSquishX, PosPreSquishY, CurveID, OrderIndex FROM curve_point");

            // Difficulty.db2
            PrepareStatement(HotfixStatements.SEL_DIFFICULTY, "SELECT ID, Name, InstanceType, OrderIndex, OldEnumValue, FallbackDifficultyID, MinPlayers, MaxPlayers, " +
                "Flags, ItemContext, ToggleDifficultyID, GroupSizeHealthCurveID, GroupSizeDmgCurveID, GroupSizeSpellPointsCurveID FROM difficulty");
            PrepareStatement(HotfixStatements.SEL_DIFFICULTY_LOCALE, "SELECT ID, Name_lang FROM difficulty_locale WHERE locale = ?");

            // DungeonEncounter.db2
            PrepareStatement(HotfixStatements.SEL_DUNGEON_ENCOUNTER, "SELECT Name, ID, MapID, DifficultyID, OrderIndex, CompleteWorldStateID, Bit, CreatureDisplayID, " +
                "Flags, SpellIconFileID, Faction FROM dungeon_encounter");
            PrepareStatement(HotfixStatements.SEL_DUNGEON_ENCOUNTER_LOCALE, "SELECT ID, Name_lang FROM dungeon_encounter_locale WHERE locale = ?");

            // DurabilityCosts.db2
            PrepareStatement(HotfixStatements.SEL_DURABILITY_COSTS, "SELECT ID, WeaponSubClassCost1, WeaponSubClassCost2, WeaponSubClassCost3, WeaponSubClassCost4, " +
                "WeaponSubClassCost5, WeaponSubClassCost6, WeaponSubClassCost7, WeaponSubClassCost8, WeaponSubClassCost9, WeaponSubClassCost10, " +
                "WeaponSubClassCost11, WeaponSubClassCost12, WeaponSubClassCost13, WeaponSubClassCost14, WeaponSubClassCost15, WeaponSubClassCost16, " +
                "WeaponSubClassCost17, WeaponSubClassCost18, WeaponSubClassCost19, WeaponSubClassCost20, WeaponSubClassCost21, ArmorSubClassCost1, " +
                "ArmorSubClassCost2, ArmorSubClassCost3, ArmorSubClassCost4, ArmorSubClassCost5, ArmorSubClassCost6, ArmorSubClassCost7, ArmorSubClassCost8" +
                " FROM durability_costs");

            // DurabilityQuality.db2
            PrepareStatement(HotfixStatements.SEL_DURABILITY_QUALITY, "SELECT ID, Data FROM durability_quality");

            // Emotes.db2
            PrepareStatement(HotfixStatements.SEL_EMOTES, "SELECT ID, RaceMask, EmoteSlashCommand, AnimID, EmoteFlags, EmoteSpecProc, EmoteSpecProcParam, EventSoundID, " +
                "SpellVisualKitID, ClassMask FROM emotes");

            // EmotesText.db2
            PrepareStatement(HotfixStatements.SEL_EMOTES_TEXT, "SELECT ID, Name, EmoteID FROM emotes_text");

            // EmotesTextSound.db2
            PrepareStatement(HotfixStatements.SEL_EMOTES_TEXT_SOUND, "SELECT ID, RaceID, ClassID, SexID, SoundID, EmotesTextID FROM emotes_text_sound");

            // Faction.db2
            PrepareStatement(HotfixStatements.SEL_FACTION, "SELECT ReputationRaceMask1, ReputationRaceMask2, ReputationRaceMask3, ReputationRaceMask4, Name, " +
                "Description, ID, ReputationIndex, ParentFactionID, Expansion, FriendshipRepID, Flags, ParagonFactionID, ReputationClassMask1, " +
                "ReputationClassMask2, ReputationClassMask3, ReputationClassMask4, ReputationFlags1, ReputationFlags2, ReputationFlags3, ReputationFlags4, " +
                "ReputationBase1, ReputationBase2, ReputationBase3, ReputationBase4, ReputationMax1, ReputationMax2, ReputationMax3, ReputationMax4, " +
                "ParentFactionMod1, ParentFactionMod2, ParentFactionCap1, ParentFactionCap2 FROM faction");
            PrepareStatement(HotfixStatements.SEL_FACTION_LOCALE, "SELECT ID, Name_lang, Description_lang FROM faction_locale WHERE locale = ?");

            // FactionTemplate.db2
            PrepareStatement(HotfixStatements.SEL_FACTION_TEMPLATE, "SELECT ID, Faction, Flags, FactionGroup, FriendGroup, EnemyGroup, Enemies1, Enemies2, Enemies3, " +
                "Enemies4, Friend1, Friend2, Friend3, Friend4 FROM faction_template");

            // GameobjectDisplayInfo.db2
            PrepareStatement(HotfixStatements.SEL_GAMEOBJECT_DISPLAY_INFO, "SELECT ID, GeoBoxMinX, GeoBoxMinY, GeoBoxMinZ, GeoBoxMaxX, GeoBoxMaxY, GeoBoxMaxZ, " +
                "FileDataID, ObjectEffectPackageID, OverrideLootEffectScale, OverrideNameScale FROM gameobject_display_info");

            // Gameobjects.db2
            PrepareStatement(HotfixStatements.SEL_GAMEOBJECTS, "SELECT Name, PosX, PosY, PosZ, Rot1, Rot2, Rot3, Rot4, ID, OwnerID, DisplayID, Scale, TypeID, " +
                "PhaseUseFlags, PhaseID, PhaseGroupID, PropValue1, PropValue2, PropValue3, PropValue4, PropValue5, PropValue6, PropValue7, PropValue8" +
                " FROM gameobjects");
            PrepareStatement(HotfixStatements.SEL_GAMEOBJECTS_LOCALE, "SELECT ID, Name_lang FROM gameobjects_locale WHERE locale = ?");

            // GemProperties.db2
            PrepareStatement(HotfixStatements.SEL_GEM_PROPERTIES, "SELECT ID, EnchantId, Type FROM gem_properties");

            // Holidays.db2
            PrepareStatement(HotfixStatements.SEL_HOLIDAYS, "SELECT ID, Region, Looping, HolidayNameID, HolidayDescriptionID, Priority, CalendarFilterType, Flags, " +
                "Duration1, Duration2, Duration3, Duration4, Duration5, Duration6, Duration7, Duration8, Duration9, Duration10, Date1, Date2, Date3, Date4, " +
                "Date5, Date6, Date7, Date8, Date9, Date10, Date11, Date12, Date13, Date14, Date15, Date16, Date17, Date18, Date19, Date20, Date21, Date22, " +
                "Date23, Date24, Date25, Date26, CalendarFlags1, CalendarFlags2, CalendarFlags3, CalendarFlags4, CalendarFlags5, CalendarFlags6, " +
                "CalendarFlags7, CalendarFlags8, CalendarFlags9, CalendarFlags10, TextureFileDataID1, TextureFileDataID2, TextureFileDataID3 FROM holidays");

            // ImportPriceArmor.db2
            PrepareStatement(HotfixStatements.SEL_IMPORT_PRICE_ARMOR, "SELECT ID, ClothModifier, LeatherModifier, ChainModifier, PlateModifier FROM import_price_armor");

            // ImportPriceQuality.db2
            PrepareStatement(HotfixStatements.SEL_IMPORT_PRICE_QUALITY, "SELECT ID, Data FROM import_price_quality");

            // ImportPriceShield.db2
            PrepareStatement(HotfixStatements.SEL_IMPORT_PRICE_SHIELD, "SELECT ID, Data FROM import_price_shield");

            // ImportPriceWeapon.db2
            PrepareStatement(HotfixStatements.SEL_IMPORT_PRICE_WEAPON, "SELECT ID, Data FROM import_price_weapon");

            // Item.db2
            PrepareStatement(HotfixStatements.SEL_ITEM, "SELECT ID, ClassID, SubclassID, Material, InventoryType, SheatheType, SoundOverrideSubclassID, IconFileDataID, " +
                "ItemGroupSoundsID, ModifiedCraftingReagentItemID FROM item");

            // ItemAppearance.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_APPEARANCE, "SELECT ID, DisplayType, ItemDisplayInfoID, DefaultIconFileDataID, UiOrder, PlayerConditionID FROM item_appearance");

            // ItemClass.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_CLASS, "SELECT ID, ClassName, ClassID, PriceModifier, Flags FROM item_class");
            PrepareStatement(HotfixStatements.SEL_ITEM_CLASS_LOCALE, "SELECT ID, ClassName_lang FROM item_class_locale WHERE locale = ?");

            // ItemCurrencyCost.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_CURRENCY_COST, "SELECT ID, ItemID FROM item_currency_cost");

            // ItemDamageAmmo.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_DAMAGE_AMMO, "SELECT ID, ItemLevel, Quality1, Quality2, Quality3, Quality4, Quality5, Quality6, Quality7" +
                " FROM item_damage_ammo");

            // ItemDamageOneHand.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_DAMAGE_ONE_HAND, "SELECT ID, ItemLevel, Quality1, Quality2, Quality3, Quality4, Quality5, Quality6, Quality7" +
                " FROM item_damage_one_hand");

            // ItemDamageOneHandCaster.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_DAMAGE_ONE_HAND_CASTER, "SELECT ID, ItemLevel, Quality1, Quality2, Quality3, Quality4, Quality5, Quality6, " +
                "Quality7 FROM item_damage_one_hand_caster");

            // ItemDamageTwoHand.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_DAMAGE_TWO_HAND, "SELECT ID, ItemLevel, Quality1, Quality2, Quality3, Quality4, Quality5, Quality6, Quality7" +
                " FROM item_damage_two_hand");

            // ItemDamageTwoHandCaster.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_DAMAGE_TWO_HAND_CASTER, "SELECT ID, ItemLevel, Quality1, Quality2, Quality3, Quality4, Quality5, Quality6, " +
                "Quality7 FROM item_damage_two_hand_caster");

            // ItemDisenchantLoot.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_DISENCHANT_LOOT, "SELECT ID, Subclass, Quality, MinLevel, MaxLevel, SkillRequired, ExpansionID, Class" +
                " FROM item_disenchant_loot");

            // ItemEffect.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_EFFECT, "SELECT ID, LegacySlotIndex, TriggerType, Charges, CoolDownMSec, CategoryCoolDownMSec, SpellCategoryID, " +
                "SpellID, ChrSpecializationID, ParentItemID FROM item_effect");

            // ItemExtendedCost.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_EXTENDED_COST, "SELECT ID, RequiredArenaRating, ArenaBracket, Flags, MinFactionID, MinReputation, " +
                "RequiredAchievement, ItemID1, ItemID2, ItemID3, ItemID4, ItemID5, ItemCount1, ItemCount2, ItemCount3, ItemCount4, ItemCount5, CurrencyID1, " +
                "CurrencyID2, CurrencyID3, CurrencyID4, CurrencyID5, CurrencyCount1, CurrencyCount2, CurrencyCount3, CurrencyCount4, CurrencyCount5" +
                " FROM item_extended_cost");

            // ItemLimitCategory.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_LIMIT_CATEGORY, "SELECT ID, Name, Quantity, Flags FROM item_limit_category");
            PrepareStatement(HotfixStatements.SEL_ITEM_LIMIT_CATEGORY_LOCALE, "SELECT ID, Name_lang FROM item_limit_category_locale WHERE locale = ?");

            // ItemModifiedAppearance.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_MODIFIED_APPEARANCE, "SELECT ID, ItemID, ItemAppearanceModifierID, ItemAppearanceID, OrderIndex, " +
                "TransmogSourceTypeEnum FROM item_modified_appearance");

            // ItemPriceBase.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_PRICE_BASE, "SELECT ID, ItemLevel, Armor, Weapon FROM item_price_base");

            // ItemSet.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_SET, "SELECT ID, Name, SetFlags, RequiredSkill, RequiredSkillRank, ItemID1, ItemID2, ItemID3, ItemID4, ItemID5, " +
                "ItemID6, ItemID7, ItemID8, ItemID9, ItemID10, ItemID11, ItemID12, ItemID13, ItemID14, ItemID15, ItemID16, ItemID17 FROM item_set");
            PrepareStatement(HotfixStatements.SEL_ITEM_SET_LOCALE, "SELECT ID, Name_lang FROM item_set_locale WHERE locale = ?");

            // ItemSetSpell.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_SET_SPELL, "SELECT ID, ChrSpecID, SpellID, Threshold, ItemSetID FROM item_set_spell");

            // ItemSparse.db2
            PrepareStatement(HotfixStatements.SEL_ITEM_SPARSE, "SELECT ID, AllowableRace, Description, Display3, Display2, Display1, Display, DmgVariance, " +
                "DurationInInventory, QualityModifier, BagFamily, ItemRange, StatPercentageOfSocket1, StatPercentageOfSocket2, StatPercentageOfSocket3, " +
                "StatPercentageOfSocket4, StatPercentageOfSocket5, StatPercentageOfSocket6, StatPercentageOfSocket7, StatPercentageOfSocket8, " +
                "StatPercentageOfSocket9, StatPercentageOfSocket10, StatPercentEditor1, StatPercentEditor2, StatPercentEditor3, StatPercentEditor4, " +
                "StatPercentEditor5, StatPercentEditor6, StatPercentEditor7, StatPercentEditor8, StatPercentEditor9, StatPercentEditor10, Stackable, " +
                "MaxCount, RequiredAbility, SellPrice, BuyPrice, VendorStackCount, PriceVariance, PriceRandomValue, Flags1, Flags2, Flags3, Flags4, " +
                "FactionRelated, ModifiedCraftingReagentItemID, ContentTuningID, PlayerLevelToItemLevelCurveID, ItemNameDescriptionID, " +
                "RequiredTransmogHoliday, RequiredHoliday, LimitCategory, GemProperties, SocketMatchEnchantmentId, TotemCategoryID, InstanceBound, " +
                "ZoneBound1, ZoneBound2, ItemSet, LockID, StartQuestID, PageID, ItemDelay, MinFactionID, RequiredSkillRank, RequiredSkill, ItemLevel, " +
                "AllowableClass, ExpansionID, ArtifactID, SpellWeight, SpellWeightCategory, SocketType1, SocketType2, SocketType3, SheatheType, Material, " +
                "PageMaterialID, LanguageID, Bonding, DamageDamageType, StatModifierBonusStat1, StatModifierBonusStat2, StatModifierBonusStat3, " +
                "StatModifierBonusStat4, StatModifierBonusStat5, StatModifierBonusStat6, StatModifierBonusStat7, StatModifierBonusStat8, " +
                "StatModifierBonusStat9, StatModifierBonusStat10, ContainerSlots, MinReputation, RequiredPVPMedal, RequiredPVPRank, RequiredLevel, " +
                "InventoryType, OverallQualityID FROM item_sparse");
            PrepareStatement(HotfixStatements.SEL_ITEM_SPARSE_LOCALE, "SELECT ID, Description_lang, Display3_lang, Display2_lang, Display1_lang, Display_lang" +
                " FROM item_sparse_locale WHERE locale = ?");

            // Keychain.db2
            PrepareStatement(HotfixStatements.SEL_KEYCHAIN, "SELECT ID, Key1, Key2, Key3, Key4, Key5, Key6, Key7, Key8, Key9, Key10, Key11, Key12, Key13, Key14, Key15, " +
                "Key16, Key17, Key18, Key19, Key20, Key21, Key22, Key23, Key24, Key25, Key26, Key27, Key28, Key29, Key30, Key31, Key32 FROM keychain");

            // LanguageWords.db2
            PrepareStatement(HotfixStatements.SEL_LANGUAGE_WORDS, "SELECT ID, Word, LanguageID FROM language_words");

            // Languages.db2
            PrepareStatement(HotfixStatements.SEL_LANGUAGES, "SELECT Name, ID FROM languages");
            PrepareStatement(HotfixStatements.SEL_LANGUAGES_LOCALE, "SELECT ID, Name_lang FROM languages_locale WHERE locale = ?");

            // LfgDungeons.db2
            PrepareStatement(HotfixStatements.SEL_LFG_DUNGEONS, "SELECT ID, Name, Description, TypeID, Subtype, Faction, IconTextureFileID, RewardsBgTextureFileID, " +
                "PopupBgTextureFileID, ExpansionLevel, MapID, DifficultyID, MinGear, GroupID, OrderIndex, RequiredPlayerConditionId, RandomID, ScenarioID, " +
                "FinalEncounterID, CountTank, CountHealer, CountDamage, MinCountTank, MinCountHealer, MinCountDamage, BonusReputationAmount, MentorItemLevel, " +
                "MentorCharLevel, ContentTuningID, Flags1, Flags2 FROM lfg_dungeons");
            PrepareStatement(HotfixStatements.SEL_LFG_DUNGEONS_LOCALE, "SELECT ID, Name_lang, Description_lang FROM lfg_dungeons_locale WHERE locale = ?");

            // Light.db2
            PrepareStatement(HotfixStatements.SEL_LIGHT, "SELECT ID, GameCoordsX, GameCoordsY, GameCoordsZ, GameFalloffStart, GameFalloffEnd, ContinentID, " +
                "LightParamsID1, LightParamsID2, LightParamsID3, LightParamsID4, LightParamsID5, LightParamsID6, LightParamsID7, LightParamsID8 FROM light");

            // LiquidType.db2
            PrepareStatement(HotfixStatements.SEL_LIQUID_TYPE, "SELECT ID, Name, Texture1, Texture2, Texture3, Texture4, Texture5, Texture6, Flags, SoundBank, SoundID, " +
                "SpellID, MaxDarkenDepth, FogDarkenIntensity, AmbDarkenIntensity, DirDarkenIntensity, LightID, ParticleScale, ParticleMovement, " +
                "ParticleTexSlots, MaterialID, MinimapStaticCol, FrameCountTexture1, FrameCountTexture2, FrameCountTexture3, FrameCountTexture4, " +
                "FrameCountTexture5, FrameCountTexture6, Color1, Color2, Float1, Float2, Float3, `Float4`, Float5, Float6, Float7, `Float8`, Float9, Float10, " +
                "Float11, Float12, Float13, Float14, Float15, Float16, Float17, Float18, `Int1`, `Int2`, `Int3`, `Int4`, Coefficient1, Coefficient2, " +
                "Coefficient3, Coefficient4 FROM liquid_type");

            // Lock.db2
            PrepareStatement(HotfixStatements.SEL_LOCK, "SELECT ID, Flags, Index1, Index2, Index3, Index4, Index5, Index6, Index7, Index8, Skill1, Skill2, Skill3, Skill4, " +
                "Skill5, Skill6, Skill7, Skill8, Type1, Type2, Type3, Type4, Type5, Type6, Type7, Type8, Action1, Action2, Action3, Action4, Action5, " +
                "Action6, Action7, Action8 FROM `lock`");

            // MailTemplate.db2
            PrepareStatement(HotfixStatements.SEL_MAIL_TEMPLATE, "SELECT ID, Body FROM mail_template");
            PrepareStatement(HotfixStatements.SEL_MAIL_TEMPLATE_LOCALE, "SELECT ID, Body_lang FROM mail_template_locale WHERE locale = ?");

            // Map.db2
            PrepareStatement(HotfixStatements.SEL_MAP, "SELECT ID, Directory, MapName, InternalName, MapDescription0, MapDescription1, PvpShortDescription, " +
                "PvpLongDescription, CorpseX, CorpseY, MapType, InstanceType, ExpansionID, AreaTableID, LoadingScreenID, TimeOfDayOverride, ParentMapID, " +
                "CosmeticParentMapID, TimeOffset, MinimapIconScale, CorpseMapID, MaxPlayers, WindSettingsID, ZmpFileDataID, WdtFileDataID, Flags1, Flags2 FROM map");
            PrepareStatement(HotfixStatements.SEL_MAP_LOCALE, "SELECT ID, MapName_lang, MapDescription0_lang, MapDescription1_lang, PvpShortDescription_lang, " +
                "PvpLongDescription_lang FROM map_locale WHERE locale = ?");

            // MapDifficulty.db2
            PrepareStatement(HotfixStatements.SEL_MAP_DIFFICULTY, "SELECT ID, Message, DifficultyID, LockID, ResetInterval, MaxPlayers, ItemContext, " +
                "ItemContextPickerID, Flags, ContentTuningID, MapID FROM map_difficulty");
            PrepareStatement(HotfixStatements.SEL_MAP_DIFFICULTY_LOCALE, "SELECT ID, Message_lang FROM map_difficulty_locale WHERE locale = ?");

            // MapDifficultyXCondition.db2
            PrepareStatement(HotfixStatements.SEL_MAP_DIFFICULTY_X_CONDITION, "SELECT ID, FailureDescription, PlayerConditionID, OrderIndex, MapDifficultyID" +
                " FROM map_difficulty_x_condition");
            PrepareStatement(HotfixStatements.SEL_MAP_DIFFICULTY_X_CONDITION_LOCALE, "SELECT ID, FailureDescription_lang FROM map_difficulty_x_condition_locale WHERE locale = ?");

            // ModifierTree.db2
            PrepareStatement(HotfixStatements.SEL_MODIFIER_TREE, "SELECT ID, Parent, Operator, Amount, Type, Asset, SecondaryAsset, TertiaryAsset FROM modifier_tree");

            // Movie.db2
            PrepareStatement(HotfixStatements.SEL_MOVIE, "SELECT ID, Volume, KeyID, AudioFileDataID, SubtitleFileDataID FROM movie");

            // NameGen.db2
            PrepareStatement(HotfixStatements.SEL_NAME_GEN, "SELECT ID, Name, RaceID, Sex FROM name_gen");

            // NamesProfanity.db2
            PrepareStatement(HotfixStatements.SEL_NAMES_PROFANITY, "SELECT ID, Name, Language FROM names_profanity");

            // NamesReserved.db2
            PrepareStatement(HotfixStatements.SEL_NAMES_RESERVED, "SELECT ID, Name FROM names_reserved");

            // NamesReservedLocale.db2
            PrepareStatement(HotfixStatements.SEL_NAMES_RESERVED_LOCALE, "SELECT ID, Name, LocaleMask FROM names_reserved_locale");

            // Phase.db2
            PrepareStatement(HotfixStatements.SEL_PHASE, "SELECT ID, Flags FROM phase");

            // PhaseXPhaseGroup.db2
            PrepareStatement(HotfixStatements.SEL_PHASE_X_PHASE_GROUP, "SELECT ID, PhaseID, PhaseGroupID FROM phase_x_phase_group");

            // PlayerCondition.db2
            PrepareStatement(HotfixStatements.SEL_PLAYER_CONDITION, "SELECT RaceMask, FailureDescription, ID, ClassMask, SkillLogic, LanguageID, MinLanguage, " +
                "MaxLanguage, MaxFactionID, MaxReputation, ReputationLogic, CurrentPvpFaction, PvpMedal, PrevQuestLogic, CurrQuestLogic, " +
                "CurrentCompletedQuestLogic, SpellLogic, ItemLogic, ItemFlags, AuraSpellLogic, WorldStateExpressionID, WeatherID, PartyStatus, " +
                "LifetimeMaxPVPRank, AchievementLogic, Gender, NativeGender, AreaLogic, LfgLogic, CurrencyLogic, QuestKillID, QuestKillLogic, " +
                "MinExpansionLevel, MaxExpansionLevel, MinAvgItemLevel, MaxAvgItemLevel, MinAvgEquippedItemLevel, MaxAvgEquippedItemLevel, PhaseUseFlags, " +
                "PhaseID, PhaseGroupID, Flags, ChrSpecializationIndex, ChrSpecializationRole, ModifierTreeID, PowerType, PowerTypeComp, PowerTypeValue, " +
                "WeaponSubclassMask, MaxGuildLevel, MinGuildLevel, MaxExpansionTier, MinExpansionTier, MinPVPRank, MaxPVPRank, ContentTuningID, CovenantID, " +
                "SkillID1, SkillID2, SkillID3, SkillID4, MinSkill1, MinSkill2, MinSkill3, MinSkill4, MaxSkill1, MaxSkill2, MaxSkill3, MaxSkill4, " +
                "MinFactionID1, MinFactionID2, MinFactionID3, MinReputation1, MinReputation2, MinReputation3, PrevQuestID1, PrevQuestID2, PrevQuestID3, " +
                "PrevQuestID4, CurrQuestID1, CurrQuestID2, CurrQuestID3, CurrQuestID4, CurrentCompletedQuestID1, CurrentCompletedQuestID2, " +
                "CurrentCompletedQuestID3, CurrentCompletedQuestID4, SpellID1, SpellID2, SpellID3, SpellID4, ItemID1, ItemID2, ItemID3, ItemID4, ItemCount1, " +
                "ItemCount2, ItemCount3, ItemCount4, Explored1, Explored2, Time1, Time2, AuraSpellID1, AuraSpellID2, AuraSpellID3, AuraSpellID4, AuraStacks1, " +
                "AuraStacks2, AuraStacks3, AuraStacks4, Achievement1, Achievement2, Achievement3, Achievement4, AreaID1, AreaID2, AreaID3, AreaID4, " +
                "LfgStatus1, LfgStatus2, LfgStatus3, LfgStatus4, LfgCompare1, LfgCompare2, LfgCompare3, LfgCompare4, LfgValue1, LfgValue2, LfgValue3, " +
                "LfgValue4, CurrencyID1, CurrencyID2, CurrencyID3, CurrencyID4, CurrencyCount1, CurrencyCount2, CurrencyCount3, CurrencyCount4, " +
                "QuestKillMonster1, QuestKillMonster2, QuestKillMonster3, QuestKillMonster4, QuestKillMonster5, QuestKillMonster6, MovementFlags1, " +
                "MovementFlags2 FROM player_condition");
            PrepareStatement(HotfixStatements.SEL_PLAYER_CONDITION_LOCALE, "SELECT ID, FailureDescription_lang FROM player_condition_locale WHERE locale = ?");

            // PowerType.db2
            PrepareStatement(HotfixStatements.SEL_POWER_TYPE, "SELECT NameGlobalStringTag, CostGlobalStringTag, ID, PowerTypeEnum, MinPower, MaxBasePower, CenterPower, " +
                "DefaultPower, DisplayModifier, RegenInterruptTimeMS, RegenPeace, RegenCombat, Flags FROM power_type");

            // PvpDifficulty.db2
            PrepareStatement(HotfixStatements.SEL_PVP_DIFFICULTY, "SELECT ID, RangeIndex, MinLevel, MaxLevel, MapID FROM pvp_difficulty");

            // QuestFactionReward.db2
            PrepareStatement(HotfixStatements.SEL_QUEST_FACTION_REWARD, "SELECT ID, Difficulty1, Difficulty2, Difficulty3, Difficulty4, Difficulty5, Difficulty6, " +
                "Difficulty7, Difficulty8, Difficulty9, Difficulty10 FROM quest_faction_reward");

            // QuestInfo.db2
            PrepareStatement(HotfixStatements.SEL_QUEST_INFO, "SELECT ID, InfoName, Type, Modifiers, Profession FROM quest_info");
            PrepareStatement(HotfixStatements.SEL_QUEST_INFO_LOCALE, "SELECT ID, InfoName_lang FROM quest_info_locale WHERE locale = ?");

            // QuestMoneyReward.db2
            PrepareStatement(HotfixStatements.SEL_QUEST_MONEY_REWARD, "SELECT ID, Difficulty1, Difficulty2, Difficulty3, Difficulty4, Difficulty5, Difficulty6, " +
                "Difficulty7, Difficulty8, Difficulty9, Difficulty10 FROM quest_money_reward");

            // QuestSort.db2
            PrepareStatement(HotfixStatements.SEL_QUEST_SORT, "SELECT ID, SortName, UiOrderIndex FROM quest_sort");
            PrepareStatement(HotfixStatements.SEL_QUEST_SORT_LOCALE, "SELECT ID, SortName_lang FROM quest_sort_locale WHERE locale = ?");

            // QuestV2.db2
            PrepareStatement(HotfixStatements.SEL_QUEST_V2, "SELECT ID, UniqueBitFlag FROM quest_v2");

            // QuestXp.db2
            PrepareStatement(HotfixStatements.SEL_QUEST_XP, "SELECT ID, Difficulty1, Difficulty2, Difficulty3, Difficulty4, Difficulty5, Difficulty6, Difficulty7, " +
                "Difficulty8, Difficulty9, Difficulty10 FROM quest_xp");

            // RandPropPoints.db2
            PrepareStatement(HotfixStatements.SEL_RAND_PROP_POINTS, "SELECT ID, DamageReplaceStatF, DamageSecondaryF, DamageReplaceStat, DamageSecondary, EpicF1, " +
                "EpicF2, EpicF3, EpicF4, EpicF5, SuperiorF1, SuperiorF2, SuperiorF3, SuperiorF4, SuperiorF5, GoodF1, GoodF2, GoodF3, GoodF4, GoodF5, Epic1, " +
                "Epic2, Epic3, Epic4, Epic5, Superior1, Superior2, Superior3, Superior4, Superior5, Good1, Good2, Good3, Good4, Good5 FROM rand_prop_points");

            // SkillLine.db2
            PrepareStatement(HotfixStatements.SEL_SKILL_LINE, "SELECT DisplayName, AlternateVerb, Description, HordeDisplayName, OverrideSourceInfoDisplayName, ID, " +
                "CategoryID, SpellIconFileID, CanLink, ParentSkillLineID, ParentTierIndex, Flags, SpellBookSpellID FROM skill_line");
            PrepareStatement(HotfixStatements.SEL_SKILL_LINE_LOCALE, "SELECT ID, DisplayName_lang, AlternateVerb_lang, Description_lang, HordeDisplayName_lang" +
                " FROM skill_line_locale WHERE locale = ?");

            // SkillLineAbility.db2
            PrepareStatement(HotfixStatements.SEL_SKILL_LINE_ABILITY, "SELECT RaceMask, ID, SkillLine, Spell, MinSkillLineRank, ClassMask, SupercedesSpell, " +
                "AcquireMethod, TrivialSkillLineRankHigh, TrivialSkillLineRankLow, Flags, NumSkillUps, UniqueBit, TradeSkillCategoryID, SkillupSkillLineID" +
                " FROM skill_line_ability");

            // SkillRaceClassInfo.db2
            PrepareStatement(HotfixStatements.SEL_SKILL_RACE_CLASS_INFO, "SELECT ID, RaceMask, SkillID, ClassMask, Flags, Availability, MinLevel, SkillTierID" +
                " FROM skill_race_class_info");

            // SoundKit.db2
            PrepareStatement(HotfixStatements.SEL_SOUND_KIT, "SELECT ID, SoundType, VolumeFloat, Flags, MinDistance, DistanceCutoff, EAXDef, SoundKitAdvancedID, " +
                "VolumeVariationPlus, VolumeVariationMinus, PitchVariationPlus, PitchVariationMinus, DialogType, PitchAdjust, BusOverwriteID, MaxInstances" +
                " FROM sound_kit");

            // SpellAuraOptions.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_AURA_OPTIONS, "SELECT ID, DifficultyID, CumulativeAura, ProcCategoryRecovery, ProcChance, ProcCharges, " +
                "SpellProcsPerMinuteID, ProcTypeMask1, ProcTypeMask2, SpellID FROM spell_aura_options");

            // SpellAuraRestrictions.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_AURA_RESTRICTIONS, "SELECT ID, DifficultyID, CasterAuraState, TargetAuraState, ExcludeCasterAuraState, " +
                "ExcludeTargetAuraState, CasterAuraSpell, TargetAuraSpell, ExcludeCasterAuraSpell, ExcludeTargetAuraSpell, SpellID" +
                " FROM spell_aura_restrictions");

            // SpellCastTimes.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_CAST_TIMES, "SELECT ID, Base, Minimum FROM spell_cast_times");

            // SpellCastingRequirements.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_CASTING_REQUIREMENTS, "SELECT ID, SpellID, FacingCasterFlags, MinFactionID, MinReputation, RequiredAreasID, " +
                "RequiredAuraVision, RequiresSpellFocus FROM spell_casting_requirements");

            // SpellCategories.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_CATEGORIES, "SELECT ID, DifficultyID, Category, DefenseType, DispelType, Mechanic, PreventionType, " +
                "StartRecoveryCategory, ChargeCategory, SpellID FROM spell_categories");

            // SpellCategory.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_CATEGORY, "SELECT ID, Name, Flags, UsesPerWeek, MaxCharges, ChargeRecoveryTime, TypeMask FROM spell_category");
            PrepareStatement(HotfixStatements.SEL_SPELL_CATEGORY_LOCALE, "SELECT ID, Name_lang FROM spell_category_locale WHERE locale = ?");

            // SpellClassOptions.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_CLASS_OPTIONS, "SELECT ID, SpellID, ModalNextSpell, SpellClassSet, SpellClassMask1, SpellClassMask2, " +
                "SpellClassMask3, SpellClassMask4 FROM spell_class_options");

            // SpellCooldowns.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_COOLDOWNS, "SELECT ID, DifficultyID, CategoryRecoveryTime, RecoveryTime, StartRecoveryTime, SpellID" +
                " FROM spell_cooldowns");

            // SpellDuration.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_DURATION, "SELECT ID, Duration, MaxDuration FROM spell_duration");

            // SpellEffect.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_EFFECT, "SELECT ID, EffectAura, DifficultyID, EffectIndex, Effect, EffectAmplitude, EffectAttributes, " +
                "EffectAuraPeriod, EffectBonusCoefficient, EffectChainAmplitude, EffectChainTargets, EffectItemType, EffectMechanic, EffectPointsPerResource, " +
                "EffectPosFacing, EffectRealPointsPerLevel, EffectTriggerSpell, BonusCoefficientFromAP, PvpMultiplier, Coefficient, Variance, " +
                "ResourceCoefficient, GroupSizeBasePointsCoefficient, EffectBasePoints, EffectMiscValue1, EffectMiscValue2, EffectRadiusIndex1, " +
                "EffectRadiusIndex2, EffectSpellClassMask1, EffectSpellClassMask2, EffectSpellClassMask3, EffectSpellClassMask4, ImplicitTarget1, " +
                "ImplicitTarget2, SpellID FROM spell_effect");

            // SpellEquippedItems.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_EQUIPPED_ITEMS, "SELECT ID, SpellID, EquippedItemClass, EquippedItemInvTypes, EquippedItemSubclass" +
                " FROM spell_equipped_items");

            // SpellFocusObject.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_FOCUS_OBJECT, "SELECT ID, Name FROM spell_focus_object");
            PrepareStatement(HotfixStatements.SEL_SPELL_FOCUS_OBJECT_LOCALE, "SELECT ID, Name_lang FROM spell_focus_object_locale WHERE locale = ?");

            // SpellInterrupts.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_INTERRUPTS, "SELECT ID, DifficultyID, InterruptFlags, AuraInterruptFlags1, AuraInterruptFlags2, " +
                "ChannelInterruptFlags1, ChannelInterruptFlags2, SpellID FROM spell_interrupts");

            // SpellItemEnchantment.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_ITEM_ENCHANTMENT, "SELECT Name, HordeName, ID, EffectArg1, EffectArg2, EffectArg3, EffectScalingPoints1, " +
                "EffectScalingPoints2, EffectScalingPoints3, IconFileDataID, MinItemLevel, MaxItemLevel, TransmogUseConditionID, TransmogCost, " +
                "EffectPointsMin1, EffectPointsMin2, EffectPointsMin3, ItemVisual, Flags, RequiredSkillID, RequiredSkillRank, ItemLevel, Charges, Effect1, " +
                "Effect2, Effect3, ScalingClass, ScalingClassRestricted, ConditionID, MinLevel, MaxLevel FROM spell_item_enchantment");
            PrepareStatement(HotfixStatements.SEL_SPELL_ITEM_ENCHANTMENT_LOCALE, "SELECT ID, Name_lang, HordeName_lang FROM spell_item_enchantment_locale WHERE locale = ?");

            // SpellItemEnchantmentCondition.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_ITEM_ENCHANTMENT_CONDITION, "SELECT ID, LtOperandType1, LtOperandType2, LtOperandType3, LtOperandType4, " +
                "LtOperandType5, LtOperand1, LtOperand2, LtOperand3, LtOperand4, LtOperand5, Operator1, Operator2, Operator3, Operator4, Operator5, " +
                "RtOperandType1, RtOperandType2, RtOperandType3, RtOperandType4, RtOperandType5, RtOperand1, RtOperand2, RtOperand3, RtOperand4, RtOperand5, " +
                "Logic1, Logic2, Logic3, Logic4, Logic5 FROM spell_item_enchantment_condition");

            // SpellLabel.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_LABEL, "SELECT ID, LabelID, SpellID FROM spell_label");

            // SpellLevels.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_LEVELS, "SELECT ID, DifficultyID, MaxLevel, MaxPassiveAuraLevel, BaseLevel, SpellLevel, SpellID FROM spell_levels");

            // SpellMisc.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_MISC, "SELECT ID, Attributes1, Attributes2, Attributes3, Attributes4, Attributes5, Attributes6, Attributes7, " +
                "Attributes8, Attributes9, Attributes10, Attributes11, Attributes12, Attributes13, Attributes14, Attributes15, DifficultyID, " +
                "CastingTimeIndex, DurationIndex, RangeIndex, SchoolMask, Speed, LaunchDelay, MinDuration, SpellIconFileDataID, ActiveIconFileDataID, " +
                "ContentTuningID, ShowFutureSpellPlayerConditionID, SpellVisualScript, ActiveSpellVisualScript, SpellID FROM spell_misc");

            // SpellName.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_NAME, "SELECT ID, Name FROM spell_name");
            PrepareStatement(HotfixStatements.SEL_SPELL_NAME_LOCALE, "SELECT ID, Name_lang FROM spell_name_locale WHERE locale = ?");

            // SpellPower.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_POWER, "SELECT ID, OrderIndex, ManaCost, ManaCostPerLevel, ManaPerSecond, PowerDisplayID, AltPowerBarID, " +
                "PowerCostPct, PowerCostMaxPct, PowerPctPerSecond, PowerType, RequiredAuraSpellID, OptionalCost, SpellID FROM spell_power");

            // SpellRadius.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_RADIUS, "SELECT ID, Radius, RadiusPerLevel, RadiusMin, RadiusMax FROM spell_radius");

            // SpellRange.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_RANGE, "SELECT ID, DisplayName, DisplayNameShort, Flags, RangeMin1, RangeMin2, RangeMax1, RangeMax2" +
                " FROM spell_range");
            PrepareStatement(HotfixStatements.SEL_SPELL_RANGE_LOCALE, "SELECT ID, DisplayName_lang, DisplayNameShort_lang FROM spell_range_locale WHERE locale = ?");

            // SpellReagents.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_REAGENTS, "SELECT ID, SpellID, Reagent1, Reagent2, Reagent3, Reagent4, Reagent5, Reagent6, Reagent7, Reagent8, " +
                "ReagentCount1, ReagentCount2, ReagentCount3, ReagentCount4, ReagentCount5, ReagentCount6, ReagentCount7, ReagentCount8 FROM spell_reagents");

            // SpellShapeshift.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_SHAPESHIFT, "SELECT ID, SpellID, StanceBarOrder, ShapeshiftExclude1, ShapeshiftExclude2, ShapeshiftMask1, " +
                "ShapeshiftMask2 FROM spell_shapeshift");

            // SpellShapeshiftForm.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_SHAPESHIFT_FORM, "SELECT ID, Name, CreatureType, Flags, AttackIconFileID, BonusActionBar, CombatRoundTime, " +
                "DamageVariance, MountTypeID, CreatureDisplayID1, CreatureDisplayID2, CreatureDisplayID3, CreatureDisplayID4, PresetSpellID1, PresetSpellID2, " +
                "PresetSpellID3, PresetSpellID4, PresetSpellID5, PresetSpellID6, PresetSpellID7, PresetSpellID8 FROM spell_shapeshift_form");
            PrepareStatement(HotfixStatements.SEL_SPELL_SHAPESHIFT_FORM_LOCALE, "SELECT ID, Name_lang FROM spell_shapeshift_form_locale WHERE locale = ?");

            // SpellTargetRestrictions.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_TARGET_RESTRICTIONS, "SELECT ID, DifficultyID, ConeDegrees, MaxTargets, MaxTargetLevel, TargetCreatureType, " +
                "Targets, Width, SpellID FROM spell_target_restrictions");

            // SpellTotems.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_TOTEMS, "SELECT ID, SpellID, RequiredTotemCategoryID1, RequiredTotemCategoryID2, Totem1, Totem2" +
                " FROM spell_totems");

            // SpellVisualKit.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_VISUAL_KIT, "SELECT ID, FallbackPriority, FallbackSpellVisualKitId, DelayMin, DelayMax, Flags1, Flags2" +
                " FROM spell_visual_kit");

            // SpellXSpellVisual.db2
            PrepareStatement(HotfixStatements.SEL_SPELL_X_SPELL_VISUAL, "SELECT ID, DifficultyID, SpellVisualID, Probability, Priority, SpellIconFileID, " +
                "ActiveIconFileID, ViewerUnitConditionID, ViewerPlayerConditionID, CasterUnitConditionID, CasterPlayerConditionID, SpellID" +
                " FROM spell_x_spell_visual");

            // SummonProperties.db2
            PrepareStatement(HotfixStatements.SEL_SUMMON_PROPERTIES, "SELECT ID, Control, Faction, Title, Slot, Flags FROM summon_properties");

            // TactKey.db2
            PrepareStatement(HotfixStatements.SEL_TACT_KEY, "SELECT ID, Key1, Key2, Key3, Key4, Key5, Key6, Key7, Key8, Key9, Key10, Key11, Key12, Key13, Key14, Key15, " +
                "Key16 FROM tact_key");

            // Talent.db2
            PrepareStatement(HotfixStatements.SEL_TALENT, "SELECT ID, Description, TierID, Flags, ColumnIndex, ClassID, SpecID, SpellID, OverridesSpellID, " +
                "CategoryMask1, CategoryMask2 FROM talent");
            PrepareStatement(HotfixStatements.SEL_TALENT_LOCALE, "SELECT ID, Description_lang FROM talent_locale WHERE locale = ?");

            // TalentTab.db2
            PrepareStatement(HotfixStatements.SEL_TALENT_TAB, "SELECT ID, Name, BackgroundFile, OrderIndex, RaceMask, ClassMask FROM talent_tab");
            PrepareStatement(HotfixStatements.SEL_TALENT_TAB_LOCALE, "SELECT ID, Name_lang FROM talent_tab_locale WHERE locale = ?");

            // TaxiNodes.db2
            PrepareStatement(HotfixStatements.SEL_TAXI_NODES, "SELECT Name, PosX, PosY, PosZ, MapOffsetX, MapOffsetY, FlightMapOffsetX, FlightMapOffsetY, ID, " +
                "ContinentID, ConditionID, CharacterBitNumber, Flags, UiTextureKitID, MinimapAtlasMemberID, Facing, SpecialIconConditionID, " +
                "VisibilityConditionID, MountCreatureID1, MountCreatureID2 FROM taxi_nodes");
            PrepareStatement(HotfixStatements.SEL_TAXI_NODES_LOCALE, "SELECT ID, Name_lang FROM taxi_nodes_locale WHERE locale = ?");

            // TaxiPath.db2
            PrepareStatement(HotfixStatements.SEL_TAXI_PATH, "SELECT ID, FromTaxiNode, ToTaxiNode, Cost FROM taxi_path");

            // TaxiPathNode.db2
            PrepareStatement(HotfixStatements.SEL_TAXI_PATH_NODE, "SELECT LocX, LocY, LocZ, ID, PathID, NodeIndex, ContinentID, Flags, Delay, ArrivalEventID, " +
                "DepartureEventID FROM taxi_path_node");

            // TotemCategory.db2
            PrepareStatement(HotfixStatements.SEL_TOTEM_CATEGORY, "SELECT ID, Name, TotemCategoryType, TotemCategoryMask FROM totem_category");
            PrepareStatement(HotfixStatements.SEL_TOTEM_CATEGORY_LOCALE, "SELECT ID, Name_lang FROM totem_category_locale WHERE locale = ?");

            // TransportAnimation.db2
            PrepareStatement(HotfixStatements.SEL_TRANSPORT_ANIMATION, "SELECT ID, PosX, PosY, PosZ, SequenceID, TimeIndex, TransportID FROM transport_animation");

            // UiMap.db2
            PrepareStatement(HotfixStatements.SEL_UI_MAP, "SELECT Name, ID, ParentUiMapID, Flags, `System`, Type, BountySetID, BountyDisplayLocation, " +
                "VisibilityPlayerConditionID, HelpTextPosition, BkgAtlasID, AlternateUiMapGroup, ContentTuningID FROM ui_map");
            PrepareStatement(HotfixStatements.SEL_UI_MAP_LOCALE, "SELECT ID, Name_lang FROM ui_map_locale WHERE locale = ?");

            // UiMapAssignment.db2
            PrepareStatement(HotfixStatements.SEL_UI_MAP_ASSIGNMENT, "SELECT UiMinX, UiMinY, UiMaxX, UiMaxY, Region1X, Region1Y, Region1Z, Region2X, Region2Y, " +
                "Region2Z, ID, UiMapID, OrderIndex, MapID, AreaID, WmoDoodadPlacementID, WmoGroupID FROM ui_map_assignment");

            // UiMapLink.db2
            PrepareStatement(HotfixStatements.SEL_UI_MAP_LINK, "SELECT UiMinX, UiMinY, UiMaxX, UiMaxY, ID, ParentUiMapID, OrderIndex, ChildUiMapID, " +
                "OverrideHighlightFileDataID, OverrideHighlightAtlasID, Flags FROM ui_map_link");

            // UiMapXMapArt.db2
            PrepareStatement(HotfixStatements.SEL_UI_MAP_X_MAP_ART, "SELECT ID, PhaseID, UiMapArtID, UiMapID FROM ui_map_x_map_art");

            // WmoAreaTable.db2
            PrepareStatement(HotfixStatements.SEL_WMO_AREA_TABLE, "SELECT AreaName, ID, WmoID, NameSetID, WmoGroupID, SoundProviderPref, SoundProviderPrefUnderwater, " +
                "AmbienceID, UwAmbience, ZoneMusic, UwZoneMusic, IntroSound, UwIntroSound, AreaTableID, Flags FROM wmo_area_table");
            PrepareStatement(HotfixStatements.SEL_WMO_AREA_TABLE_LOCALE, "SELECT ID, AreaName_lang FROM wmo_area_table_locale WHERE locale = ?");

            // WorldEffect.db2
            PrepareStatement(HotfixStatements.SEL_WORLD_EFFECT, "SELECT ID, QuestFeedbackEffectID, WhenToDisplay, TargetType, TargetAsset, PlayerConditionID, " +
                "CombatConditionID FROM world_effect");

            // WorldMapOverlay.db2
            PrepareStatement(HotfixStatements.SEL_WORLD_MAP_OVERLAY, "SELECT ID, UiMapArtID, TextureWidth, TextureHeight, OffsetX, OffsetY, HitRectTop, HitRectBottom, " +
                "HitRectLeft, HitRectRight, PlayerConditionID, Flags, AreaID1, AreaID2, AreaID3, AreaID4 FROM world_map_overlay");

            // WorldStateExpression.db2
            PrepareStatement(HotfixStatements.SEL_WORLD_STATE_EXPRESSION, "SELECT ID, Expression FROM world_state_expression");
        }
    }

    public enum HotfixStatements
    {
        None = 0,

        SEL_ANIMATION_DATA,

        SEL_ANIM_KIT,

        SEL_AREA_GROUP_MEMBER,

        SEL_AREA_TABLE,
        SEL_AREA_TABLE_LOCALE,

        SEL_AREA_TRIGGER,

        SEL_AUCTION_HOUSE,
        SEL_AUCTION_HOUSE_LOCALE,

        SEL_AZERITE_EMPOWERED_ITEM,

        SEL_BANK_BAG_SLOT_PRICES,

        SEL_BANNED_ADDONS,

        SEL_BATTLEMASTER_LIST,
        SEL_BATTLEMASTER_LIST_LOCALE,

        SEL_CFG_REGIONS,

        SEL_CHAR_TITLES,
        SEL_CHAR_TITLES_LOCALE,

        SEL_CHARACTER_LOADOUT,

        SEL_CHARACTER_LOADOUT_ITEM,

        SEL_CHAT_CHANNELS,
        SEL_CHAT_CHANNELS_LOCALE,

        SEL_CHR_CLASSES,
        SEL_CHR_CLASSES_LOCALE,

        SEL_CHR_CLASSES_X_POWER_TYPES,

        SEL_CHR_CUSTOMIZATION_CHOICE,
        SEL_CHR_CUSTOMIZATION_CHOICE_LOCALE,

        SEL_CHR_CUSTOMIZATION_ELEMENT,

        SEL_CHR_CUSTOMIZATION_OPTION,
        SEL_CHR_CUSTOMIZATION_OPTION_LOCALE,

        SEL_CHR_CUSTOMIZATION_REQ,

        SEL_CHR_MODEL,

        SEL_CHR_RACE_X_CHR_MODEL,

        SEL_CHR_RACES,
        SEL_CHR_RACES_LOCALE,

        SEL_CINEMATIC_CAMERA,

        SEL_CINEMATIC_SEQUENCES,

        SEL_CONTENT_TUNING,

        SEL_CREATURE_DISPLAY_INFO,

        SEL_CREATURE_DISPLAY_INFO_EXTRA,

        SEL_CREATURE_FAMILY,
        SEL_CREATURE_FAMILY_LOCALE,

        SEL_CREATURE_MODEL_DATA,

        SEL_CREATURE_TYPE,
        SEL_CREATURE_TYPE_LOCALE,

        SEL_CRITERIA,

        SEL_CRITERIA_TREE,
        SEL_CRITERIA_TREE_LOCALE,

        SEL_CURRENCY_TYPES,
        SEL_CURRENCY_TYPES_LOCALE,

        SEL_CURVE,

        SEL_CURVE_POINT,

        SEL_DIFFICULTY,
        SEL_DIFFICULTY_LOCALE,

        SEL_DUNGEON_ENCOUNTER,
        SEL_DUNGEON_ENCOUNTER_LOCALE,

        SEL_DURABILITY_COSTS,

        SEL_DURABILITY_QUALITY,

        SEL_EMOTES,

        SEL_EMOTES_TEXT,

        SEL_EMOTES_TEXT_SOUND,

        SEL_FACTION,
        SEL_FACTION_LOCALE,

        SEL_FACTION_TEMPLATE,

        SEL_GAMEOBJECT_DISPLAY_INFO,

        SEL_GAMEOBJECTS,
        SEL_GAMEOBJECTS_LOCALE,

        SEL_GEM_PROPERTIES,

        SEL_HOLIDAYS,

        SEL_IMPORT_PRICE_ARMOR,

        SEL_IMPORT_PRICE_QUALITY,

        SEL_IMPORT_PRICE_SHIELD,

        SEL_IMPORT_PRICE_WEAPON,

        SEL_ITEM,

        SEL_ITEM_APPEARANCE,

        SEL_ITEM_CLASS,
        SEL_ITEM_CLASS_LOCALE,

        SEL_ITEM_CURRENCY_COST,

        SEL_ITEM_DAMAGE_AMMO,

        SEL_ITEM_DAMAGE_ONE_HAND,

        SEL_ITEM_DAMAGE_ONE_HAND_CASTER,

        SEL_ITEM_DAMAGE_TWO_HAND,

        SEL_ITEM_DAMAGE_TWO_HAND_CASTER,

        SEL_ITEM_DISENCHANT_LOOT,

        SEL_ITEM_EFFECT,

        SEL_ITEM_EXTENDED_COST,

        SEL_ITEM_LIMIT_CATEGORY,
        SEL_ITEM_LIMIT_CATEGORY_LOCALE,

        SEL_ITEM_MODIFIED_APPEARANCE,

        SEL_ITEM_PRICE_BASE,

        SEL_ITEM_SET,
        SEL_ITEM_SET_LOCALE,

        SEL_ITEM_SET_SPELL,

        SEL_ITEM_SPARSE,
        SEL_ITEM_SPARSE_LOCALE,

        SEL_KEYCHAIN,

        SEL_LANGUAGE_WORDS,

        SEL_LANGUAGES,
        SEL_LANGUAGES_LOCALE,

        SEL_LFG_DUNGEONS,
        SEL_LFG_DUNGEONS_LOCALE,

        SEL_LIGHT,

        SEL_LIQUID_TYPE,

        SEL_LOCK,

        SEL_MAIL_TEMPLATE,
        SEL_MAIL_TEMPLATE_LOCALE,

        SEL_MAP,
        SEL_MAP_LOCALE,

        SEL_MAP_DIFFICULTY,
        SEL_MAP_DIFFICULTY_LOCALE,

        SEL_MAP_DIFFICULTY_X_CONDITION,
        SEL_MAP_DIFFICULTY_X_CONDITION_LOCALE,

        SEL_MODIFIER_TREE,

        SEL_MOVIE,

        SEL_NAME_GEN,

        SEL_NAMES_PROFANITY,

        SEL_NAMES_RESERVED,

        SEL_NAMES_RESERVED_LOCALE,

        SEL_PHASE,

        SEL_PHASE_X_PHASE_GROUP,

        SEL_PLAYER_CONDITION,
        SEL_PLAYER_CONDITION_LOCALE,

        SEL_POWER_TYPE,

        SEL_PVP_DIFFICULTY,

        SEL_QUEST_FACTION_REWARD,

        SEL_QUEST_INFO,
        SEL_QUEST_INFO_LOCALE,

        SEL_QUEST_MONEY_REWARD,

        SEL_QUEST_SORT,
        SEL_QUEST_SORT_LOCALE,

        SEL_QUEST_V2,

        SEL_QUEST_XP,

        SEL_RAND_PROP_POINTS,

        SEL_SKILL_LINE,
        SEL_SKILL_LINE_LOCALE,

        SEL_SKILL_LINE_ABILITY,

        SEL_SKILL_RACE_CLASS_INFO,

        SEL_SOUND_KIT,

        SEL_SPELL_AURA_OPTIONS,

        SEL_SPELL_AURA_RESTRICTIONS,

        SEL_SPELL_CAST_TIMES,

        SEL_SPELL_CASTING_REQUIREMENTS,

        SEL_SPELL_CATEGORIES,

        SEL_SPELL_CATEGORY,
        SEL_SPELL_CATEGORY_LOCALE,

        SEL_SPELL_CLASS_OPTIONS,

        SEL_SPELL_COOLDOWNS,

        SEL_SPELL_DURATION,

        SEL_SPELL_EFFECT,

        SEL_SPELL_EQUIPPED_ITEMS,

        SEL_SPELL_FOCUS_OBJECT,
        SEL_SPELL_FOCUS_OBJECT_LOCALE,

        SEL_SPELL_INTERRUPTS,

        SEL_SPELL_ITEM_ENCHANTMENT,
        SEL_SPELL_ITEM_ENCHANTMENT_LOCALE,

        SEL_SPELL_ITEM_ENCHANTMENT_CONDITION,

        SEL_SPELL_LABEL,

        SEL_SPELL_LEVELS,

        SEL_SPELL_MISC,

        SEL_SPELL_NAME,
        SEL_SPELL_NAME_LOCALE,

        SEL_SPELL_POWER,

        SEL_SPELL_RADIUS,

        SEL_SPELL_RANGE,
        SEL_SPELL_RANGE_LOCALE,

        SEL_SPELL_REAGENTS,

        SEL_SPELL_SHAPESHIFT,

        SEL_SPELL_SHAPESHIFT_FORM,
        SEL_SPELL_SHAPESHIFT_FORM_LOCALE,

        SEL_SPELL_TARGET_RESTRICTIONS,

        SEL_SPELL_TOTEMS,

        SEL_SPELL_VISUAL_KIT,

        SEL_SPELL_X_SPELL_VISUAL,

        SEL_SUMMON_PROPERTIES,

        SEL_TACT_KEY,

        SEL_TALENT,
        SEL_TALENT_LOCALE,

        SEL_TALENT_TAB,
        SEL_TALENT_TAB_LOCALE,

        SEL_TAXI_NODES,
        SEL_TAXI_NODES_LOCALE,

        SEL_TAXI_PATH,

        SEL_TAXI_PATH_NODE,

        SEL_TOTEM_CATEGORY,
        SEL_TOTEM_CATEGORY_LOCALE,

        SEL_TRANSPORT_ANIMATION,

        SEL_UI_MAP,
        SEL_UI_MAP_LOCALE,

        SEL_UI_MAP_ASSIGNMENT,

        SEL_UI_MAP_LINK,

        SEL_UI_MAP_X_MAP_ART,

        SEL_WMO_AREA_TABLE,
        SEL_WMO_AREA_TABLE_LOCALE,

        SEL_WORLD_EFFECT,

        SEL_WORLD_MAP_OVERLAY,

        SEL_WORLD_STATE_EXPRESSION,

        MAX_HOTFIXDATABASE_STATEMENTS
    }
}
