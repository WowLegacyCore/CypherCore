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

using Framework.Constants;
using Framework.Dynamic;
using Framework.GameMath;
using System;

namespace Game.DataStorage
{
    public sealed class AnimationDataRecord
    {
        public uint Id;
        public ushort Fallback;
        public byte BehaviorTier;
        public int BehaviorID;
        public int[] Flags = new int[2];
    }

    public sealed class AnimKitRecord
    {
        public uint Id;
        public uint OneShotDuration;
        public ushort OneShotStopAnimKitID;
        public ushort LowDefAnimKitID;
    }

    public sealed class AreaGroupMemberRecord
    {
        public uint Id;
        public ushort AreaID;
        public uint AreaGroupID;
    }

    public sealed class AreaTableRecord
    {
        public uint Id;
        public LocalizedString ZoneName;
        public LocalizedString AreaName;
        public ushort ContinentID;
        public ushort ParentAreaID;
        public short AreaBit;
        public byte SoundProviderPref;
        public byte SoundProviderPrefUnderwater;
        public ushort AmbienceID;
        public ushort UwAmbience;
        public ushort ZoneMusic;
        public ushort UwZoneMusic;
        public byte ExplorationLevel;
        public ushort IntroSound;
        public uint UwIntroSound;
        public byte FactionGroupMask;
        public float AmbientMultiplier;
        public byte MountFlags;
        public short PvpCombatWorldStateID;
        public byte WildBattlePetLevelMin;
        public byte WildBattlePetLevelMax;
        public byte WindSettingsID;
        public AreaFlags Flags;
        public AreaFlags2 Flags2;
        public ushort[] LiquidTypeID = new ushort[4];

        public bool IsSanctuary() => Flags.HasAnyFlag(AreaFlags.Sanctuary);
        public bool IsFlyable() => Flags.HasAnyFlag(AreaFlags.Outland) && !Flags.HasAnyFlag(AreaFlags.NoFlyZone);
    }

    public sealed class AreaTriggerRecord
    {
        public LocalizedString Message;
        public Vector3 Pos;
        public uint Id;
        public ushort ContinentID;
        public sbyte PhaseUseFlags;
        public ushort PhaseID;
        public ushort PhaseGroupID;
        public float Radius;
        public float BoxLength;
        public float BoxWidth;
        public float BoxHeight;
        public float BoxYaw;
        public sbyte ShapeType;
        public short ShapeID;
        public short AreaTriggerActionSetID;
        public sbyte Flags;
    }

    public sealed class AuctionHouseRecord
    {
        public uint Id;
        public LocalizedString Name;
        public ushort FactionID;
        public byte DepositRate;
        public byte ConsignmentRate;
    }

    public sealed class BankBagSlotPricesRecord
    {
        public uint Id;
        public uint Cost;
    }

    public sealed class BannedAddonsRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Version;
        public byte Flags;
    }

    public sealed class BattlemasterListRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Gametype;
        public LocalizedString ShortDescription;
        public LocalizedString LongDescription;
        public sbyte InstanceType;
        public byte MinLevel;
        public byte MaxLevel;
        public sbyte RatedPlayers;
        public sbyte MinPlayers;
        public sbyte MaxPlayers;
        public sbyte GroupsAllowed;
        public sbyte MaxGroupSize;
        public ushort HolidayWorldState;
        public BattlemasterListFlags Flags;
        public int IconFileDataID;
        public int RequiredPlayerConditionID;
        public short[] MapID = new short[16];
    }

    public sealed class BroadcastTextRecord
    {
        public LocalizedString Text;
        public LocalizedString Text1;
        public uint Id;
        public int LanguageID;
        public int ConditionID;
        public ushort EmotesID;
        public byte Flags;
        public uint ChatBubbleDurationMs;
        public uint[] SoundKitID = new uint[2];
        public ushort[] EmoteID = new ushort[3];
        public ushort[] EmoteDelay = new ushort[3];
    }

    public sealed class Cfg_RegionsRecord
    {
        public uint Id;
        public LocalizedString Tag;
        public ushort RegionID;
        public uint Raidorigin;
        public byte RegionGroupmask;
        public uint ChallengeOrigin;
    }

    public sealed class CharacterLoadoutRecord
    {
        public long RaceMask;
        public uint Id;
        public sbyte ChrClassID;
        public sbyte Purpose;
        public sbyte Field_2_5_1_38043_004;

        public bool IsForNewCharacter() => Purpose == 9;
    }

    public sealed class CharacterLoadoutItemRecord
    {
        public uint Id;
        public ushort CharacterLoadoutID;
        public uint ItemID;
    }

    public sealed class CharStartOutfitRecord
    {
        public uint Id;
        public byte ClassID;
        public byte SexID;
        public byte OutfitID;
        public uint PetDisplayID;
        public byte PetFamilyID;
        public int[] ItemID = new int[24];
        public int RaceID;
    }

    public sealed class CharTitlesRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Name1;
        public ushort MaskID;
        public sbyte Flags;
    }

    public sealed class ChatChannelsRecord
    {
        public LocalizedString Name;
        public LocalizedString Shortcut;
        public uint Id;
        public ChannelDBCFlags Flags;
        public sbyte FactionGroup;
        public int Ruleset;
    }

    public sealed class ChrClassesRecord
    {
        public LocalizedString Name;
        public string Filename;
        public LocalizedString NameMale;
        public LocalizedString NameFemale;
        public LocalizedString PetNameToken;
        public uint Id;
        public uint CreateScreenFileDataID;
        public uint SelectScreenFileDataID;
        public uint IconFileDataID;
        public uint LowResScreenFileDataID;
        public int StartingLevel;
        public uint Field_2_5_1_38043_011;
        public ushort Flags;
        public ushort CinematicSequenceID;
        public ushort DefaultSpec;
        public byte Field_2_0_0_6080_009;
        public byte PrimaryStatPriority;
        public PowerType DisplayPower;
        public byte RangedAttackPowerPerAgility;
        public byte AttackPowerPerAgility;
        public byte AttackPowerPerStrength;
        public byte SpellClassSet;
        public byte DamageBonusStat;
        public byte HasRelicSlot;
    }

    public sealed class ChrClassesXPowerTypesRecord
    {
        public uint Id;
        public sbyte PowerType;
        public uint ClassID;
    }

    public sealed class ChrCustomizationChoiceRecord
    {
        public LocalizedString Name;
        public uint Id;
        public uint ChrCustomizationOptionID;
        public uint ChrCustomizationReqID;
        public ushort OrderIndex;
        public ushort UiOrderIndex;
        public int Flags;
        public int[] SwatchColor = new int[2];
    }

    public sealed class ChrCustomizationElementRecord
    {
        public uint Id;
        public uint ChrCustomizationChoiceID;
        public int RelatedChrCustomizationChoiceID;
        public int ChrCustomizationGeosetID;
        public int ChrCustomizationSkinnedModelID;
        public int ChrCustomizationMaterialID;
        public int ChrCustomizationBoneSetID;
        public int ChrCustomizationCondModelID;
        public int ChrCustomizationDisplayInfoID;
        public int ChrCustItemGeoModifyID;
    }

    public sealed class ChrCustomizationOptionRecord
    {
        public LocalizedString Name;
        public uint Id;
        public ushort SecondaryID;
        public int Flags;
        public uint ChrModelID;
        public int OrderIndex;
        public int ChrCustomizationCategoryID;
        public int OptionType;
        public float BarberShopCostModifier;
        public int ChrCustomizationID;
        public int Requirement;
        public int SecondaryOrderIndex;
    }

    public sealed class ChrCustomizationReqRecord
    {
        public uint Id;
        public ChrCustomizationReqFlag Flags;
        public int ClassMask;
        public int ReqAchievementID;
        public int OverrideArchive;
        public int ReqItemModifiedAppearanceID;
    }

    public sealed class ChrModelRecord
    {
        public float[] FaceCustomizationOffset = new float[3];
        public float[] CustomizeOffset = new float[3];
        public uint Id;
        public int Sex;
        public uint DisplayID;
        public int CharComponentTextureLayoutID;
        public int Flags;
        public int SkeletonFileDataID;
        public int ModelFallbackChrModelID;
        public int TextureFallbackChrModelID;
        public int HelmVisFallbackChrModelID;
        public float CustomizeScale;
        public float CustomizeFacing;
        public float CameraDistanceOffset;
        public float BarberShopCameraOffsetScale;
        public float BarberShopCameraRotationOffset;
    }

    public sealed class ChrRacesRecord
    {
        public uint Id;
        public LocalizedString ClientPrefix;
        public LocalizedString ClientFileString;
        public LocalizedString Name;
        public LocalizedString NameFemale;
        public LocalizedString NameLowercase;
        public LocalizedString NameFemalelowercase;
        public LocalizedString NameRS;
        public LocalizedString NameRSfemale;
        public LocalizedString NameRSlowercase;
        public LocalizedString NameRSfemalelowercase;
        public LocalizedString RaceFantasyDescription;
        public LocalizedString NameRL;
        public LocalizedString NameRLfemale;
        public LocalizedString NameRLlowercase;
        public LocalizedString NameRLfemalelowercase;
        public ChrRacesFlag Flags;
        public uint MaleDisplayID;
        public uint FemaleDisplayID;
        public uint HighResMaleDisplayID;
        public uint HighResFemaleDisplayID;
        public int ResSicknessSpellID;
        public int SplashSoundID;
        public int CreateScreenFileDataID;
        public int SelectScreenFileDataID;
        public int LowResScreenFileDataID;
        public uint[] AlteredFormStartVisualKitID = new uint[3];
        public uint[] AlteredFormFinishVisualKitID = new uint[3];
        public int HeritageArmorAchievementID;
        public int StartingLevel;
        public int UiDisplayOrder;
        public int PlayableRaceBit;
        public int FemaleSkeletonFileDataID;
        public int MaleSkeletonFileDataID;
        public int HelmetAnimScalingRaceID;
        public int TransmogrifyDisabledSlotMask;
        public float[] AlteredFormCustomizeOffsetFallback = new float[3];
        public float AlteredFormCustomizeRotationFallback;
        public float[] Field_9_0_1_35256_033 = new float[3];
        public float[] Field_9_0_1_35256_034 = new float[3];
        public short FactionID;
        public ushort CinematicSequenceID;
        public sbyte BaseLanguage;
        public sbyte CreatureType;
        public sbyte Alliance;
        public sbyte RaceRelated;
        public sbyte UnalteredVisualRaceID;
        public sbyte DefaultClassID;
        public sbyte NeutralRaceID;
        public sbyte MaleModelFallbackRaceID;
        public sbyte MaleModelFallbackSex;
        public sbyte FemaleModelFallbackRaceID;
        public sbyte FemaleModelFallbackSex;
        public sbyte MaleTextureFallbackRaceID;
        public sbyte MaleTextureFallbackSex;
        public sbyte FemaleTextureFallbackRaceID;
        public sbyte FemaleTextureFallbackSex;
        public sbyte UnalteredVisualCustomizationRaceID;
    }

    public sealed class ChrRaceXChrModelRecord
    {
        public uint Id;
        public int ChrRacesID;
        public int ChrModelID;
    }

    public sealed class ChrSpecializationRecord
    {
        public LocalizedString Name;
        public LocalizedString FemaleName;
        public LocalizedString Description;
        public uint Id;
        public sbyte ClassID;
        public byte OrderIndex;
        public sbyte PetTalentType;
        public sbyte Role;
        public ChrSpecializationFlag Flags;
        public int SpellIconFileID;
        public sbyte PrimaryStatPriority;
        public int AnimReplacements;
        public uint[] MasterySpellID = new uint[2];

        public bool IsPetSpecialization() => ClassID == 0;
    }

    public sealed class ChrUpgradeBucketRecord
    {
        public uint Id;
        public ushort ChrSpecializationID;
        public int ChrUpgradeTierID;
    }

    public sealed class CinematicCameraRecord
    {
        public uint Id;
        public Vector3 Origin;
        public uint SoundID;
        public float OriginFacing;
        public uint FileDataID;
    }

    public sealed class CinematicSequencesRecord
    {
        public uint Id;
        public uint SoundID;
        public ushort[] Camera = new ushort[8];
    }

    public sealed class ClientSceneEffectRecord
    {
        public uint Id;
        public int SceneScriptPackageID;
    }

    public sealed class CloakDampeningRecord
    {
        public uint Id;
        public float TabardAngle;
        public float TabardDampening;
        public float ExpectedWeaponSize;
        public float[] Angle = new float[5];
        public float[] Dampening = new float[5];
        public float[] TailAngle = new float[2];
        public float[] TailDampening = new float[2];
    }

    public sealed class CloneEffectRecord
    {
        public uint Id;
        public int DurationMs;
        public int DelayMs;
        public int FadeInTimeMs;
        public int FadeOutTimeMs;
        public int StateSpellVisualKitID;
        public int StartSpellVisualKitID;
        public int OffsetMatrixID;
        public int Flags;
    }

    public sealed class CombatConditionRecord
    {
        public uint Id;
        public ushort WorldStateExpressionID;
        public ushort SelfConditionID;
        public ushort TargetConditionID;
        public byte FriendConditionLogic;
        public byte EnemyConditionLogic;
        public ushort[] FriendConditionID = new ushort[2];
        public byte[] FriendConditionOp = new byte[2];
        public byte[] FriendConditionCount = new byte[2];
        public ushort[] EnemyConditionID = new ushort[2];
        public byte[] EnemyConditionOp = new byte[2];
        public byte[] EnemyConditionCount = new byte[2];
    }

    public sealed class CommentatorIndirectSpellRecord
    {
        public uint Id;
        public int TalentSpellID;
        public int TriggeredAuraSpellID;
        public int ChrSpecID;
    }

    public sealed class CommentatorStartLocationRecord
    {
        public uint Id;
        public float[] Pos = new float[3];
        public int MapID;
    }

    public sealed class CommentatorTrackedCooldownRecord
    {
        public uint Id;
        public int SpellID;
        public byte Priority;
        public sbyte Flags;
        public int ChrSpecID;
    }

    public sealed class CommunityIconRecord
    {
        public uint Id;
        public int IconFileID;
        public int OrderIndex;
    }

    public sealed class ComponentModelFileDataRecord
    {
        public uint Id;
        public byte GenderIndex;
        public byte ClassID;
        public byte RaceID;
        public sbyte PositionIndex;
    }

    public sealed class ComponentTextureFileDataRecord
    {
        public uint Id;
        public byte GenderIndex;
        public byte ClassID;
        public byte RaceID;
    }

    public sealed class ConfigurationWarningRecord
    {
        public uint Id;
        public LocalizedString Warning;
        public uint Type;
    }

    public sealed class ContentTuningRecord
    {
        public uint Id;
        public int MinLevel;
        public int MaxLevel;
        public ContentTuningFlag Flags;
        public int ExpectedStatModID;
        public int DifficultyESMID;
    }

    public sealed class ContributionRecord
    {
        public LocalizedString Description;
        public LocalizedString Name;
        public uint Id;
        public int ManagedWorldStateInputID;
        public int OrderIndex;
        public int ContributionStyleContainer;
        public int[] UiTextureAtlasMemberID = new int[4];
    }

    public sealed class ContributionStyleRecord
    {
        public uint Id;
        public LocalizedString StateName;
        public LocalizedString TooltipLine;
        public int StateColor;
        public uint Flags;
        public int StatusBarAtlas;
        public int BorderAtlas;
        public int BannerAtlas;
    }

    public sealed class ContributionStyleContainerRecord
    {
        public uint Id;
        public int[] ContributionStyleID = new int[5];
    }

    public sealed class ConversationLineRecord
    {
        public uint Id;
        public uint BroadcastTextID;
        public uint SpellVisualKitID;
        public int AdditionalDuration;
        public ushort NextConversationLineID;
        public ushort AnimKitID;
        public byte SpeechType;
        public byte StartAnimation;
        public byte EndAnimation;
    }

    public sealed class CreatureRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString NameAlt;
        public LocalizedString Title;
        public LocalizedString TitleAlt;
        public sbyte Classification;
        public byte CreatureType;
        public ushort CreatureFamily;
        public byte StartAnimState;
        public int[] DisplayID = new int[4];
        public float[] DisplayProbability = new float[4];
        public int[] AlwaysItem = new int[3];
    }

    public sealed class CreatureDifficultyRecord
    {
        public uint Id;
        public sbyte ExpansionID;
        public sbyte MinLevel;
        public sbyte MaxLevel;
        public ushort FactionID;
        public int ContentTuningID;
        public int[] Flags = new int[8];
        public int CreatureID;
    }

    public sealed class CreatureDisplayInfoRecord
    {
        public uint Id;
        public ushort ModelID;
        public ushort SoundID;
        public sbyte SizeClass;
        public float CreatureModelScale;
        public byte CreatureModelAlpha;
        public byte BloodID;
        public int ExtendedDisplayInfoID;
        public ushort NPCSoundID;
        public ushort ParticleColorID;
        public int PortraitCreatureDisplayInfoID;
        public int PortraitTextureFileDataID;
        public ushort ObjectEffectPackageID;
        public ushort AnimReplacementSetID;
        public byte Flags;
        public int StateSpellVisualKitID;
        public float PlayerOverrideScale;
        public float PetInstanceScale;
        public sbyte UnarmedWeaponType;
        public int MountPoofSpellVisualKitID;
        public int DissolveEffectID;
        public sbyte Gender;
        public int DissolveOutEffectID;
        public sbyte CreatureModelMinLod;
        public int[] TextureVariationFileDataID = new int[3];
    }

    public sealed class CreatureDisplayInfoCondRecord
    {
        public long RaceMask;
        public uint Id;
        public sbyte OrderIndex;
        public sbyte Gender;
        public uint ClassMask;
        public int CreatureModelDataID;
        public int[] TextureVariationFileDataID = new int[3];
        public int CreatureDisplayInfoID;
    }

    public sealed class CreatureDisplayInfoCondXChoiceRecord
    {
        public uint Id;
        public int CreatureDisplayInfoCondID;
        public int ChrCustomizationChoiceID;
    }

    public sealed class CreatureDisplayInfoEvtRecord
    {
        public uint Id;
        public int Fourcc;
        public int SpellVisualKitID;
        public sbyte Flags;
        public int CreatureDisplayInfoID;
    }

    public sealed class CreatureDisplayInfoExtraRecord
    {
        public uint Id;
        public sbyte DisplayRaceID;
        public sbyte DisplaySexID;
        public sbyte DisplayClassID;
        public sbyte SkinID;
        public sbyte FaceID;
        public sbyte HairStyleID;
        public sbyte HairColorID;
        public sbyte FacialHairID;
        public sbyte Flags;
        public int BakeMaterialResourcesID;
        public int HDBakeMaterialResourcesID;
        public byte[] CustomDisplayOption = new byte[3];
    }

    public sealed class CreatureDisplayInfoGeosetDataRecord
    {
        public uint Id;
        public byte GeosetIndex;
        public byte GeosetValue;
        public int CreatureDisplayInfoID;
    }

    public sealed class CreatureDisplayInfoOptionRecord
    {
        public uint Id;
        public int ChrCustomizationOptionID;
        public int ChrCustomizationChoiceID;
        public int CreatureDisplayInfoExtraID;
    }

    public sealed class CreatureDisplayInfoTrnRecord
    {
        public uint Id;
        public int DstCreatureDisplayInfoID;
        public uint DissolveEffectID;
        public uint StartVisualKitID;
        public float MaxTime;
        public int FinishVisualKitID;
        public int SrcCreatureDisplayInfoID;
    }

    public sealed class CreatureDispXUiCameraRecord
    {
        public uint Id;
        public uint CreatureDisplayInfoID;
        public ushort UiCameraID;
    }

    public sealed class CreatureFamilyRecord
    {
        public uint Id;
        public LocalizedString Name;
        public float MinScale;
        public sbyte MinScaleLevel;
        public float MaxScale;
        public sbyte MaxScaleLevel;
        public ushort PetFoodMask;
        public sbyte PetTalentType;
        public int IconFileID;
        public short[] SkillLine = new short[2];
    }

    public sealed class CreatureImmunitiesRecord
    {
        public uint Id;
        public byte School;
        public uint DispelType;
        public byte MechanicsAllowed;
        public byte EffectsAllowed;
        public byte StatesAllowed;
        public byte Flags;
        public int[] Mechanic = new int[2];
        public uint[] Effect = new uint[9];
        public uint[] State = new uint[16];
    }

    public sealed class CreatureModelDataRecord
    {
        public uint Id;
        public float[] GeoBox = new float[6];
        public uint Flags;
        public uint FileDataID;
        public uint BloodID;
        public uint FootprintTextureID;
        public float FootprintTextureLength;
        public float FootprintTextureWidth;
        public float FootprintParticleScale;
        public uint FoleyMaterialID;
        public uint FootstepCameraEffectID;
        public uint DeathThudCameraEffectID;
        public uint SoundID;
        public uint SizeClass;
        public float CollisionWidth;
        public float CollisionHeight;
        public float WorldEffectScale;
        public uint CreatureGeosetDataID;
        public float HoverHeight;
        public float AttachedEffectScale;
        public float ModelScale;
        public float MissileCollisionRadius;
        public float MissileCollisionPush;
        public float MissileCollisionRaise;
        public float MountHeight;
        public float OverrideLootEffectScale;
        public float OverrideNameScale;
        public float OverrideSelectionRadius;
        public float TamedPetBaseScale;
    }

    public sealed class CreatureMovementInfoRecord
    {
        public uint Id;
        public float SmoothFacingChaseRate;
    }

    public sealed class CreatureSoundDataRecord
    {
        public uint Id;
        public uint SoundExertionID;
        public uint SoundExertionCriticalID;
        public uint SoundInjuryID;
        public uint SoundInjuryCriticalID;
        public uint SoundInjuryCrushingBlowID;
        public uint SoundDeathID;
        public uint SoundStunID;
        public uint SoundStandID;
        public uint SoundFootstepID;
        public uint SoundAggroID;
        public uint SoundWingFlapID;
        public uint SoundWingGlideID;
        public uint SoundAlertID;
        public uint SoundJumpStartID;
        public uint SoundJumpEndID;
        public uint SoundPetAttackID;
        public uint SoundPetOrderID;
        public uint SoundPetDismissID;
        public uint LoopSoundID;
        public uint BirthSoundID;
        public uint SpellCastDirectedSoundID;
        public uint SubmergeSoundID;
        public uint SubmergedSoundID;
        public uint WindupSoundID;
        public uint WindupCriticalSoundID;
        public uint ChargeSoundID;
        public uint ChargeCriticalSoundID;
        public uint BattleShoutSoundID;
        public uint BattleShoutCriticalSoundID;
        public uint TauntSoundID;
        public uint CreatureSoundDataIDPet;
        public float FidgetDelaySecondsMin;
        public float FidgetDelaySecondsMax;
        public byte CreatureImpactType;
        public uint NPCSoundID;
        public uint[] SoundFidget = new uint[5];
        public uint[] CustomAttack = new uint[4];
    }

    public sealed class CreatureSoundFidgetRecord
    {
        public uint Id;
        public int Fidget;
        public int Index;
        public int CreatureSoundDataID;
    }

    public sealed class CreatureSpellDataRecord
    {
        public uint Id;
        public int[] Spells = new int[4];
        public int[] Availability = new int[4];
    }

    public sealed class CreatureTypeRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte Flags;
    }

    public sealed class CreatureXContributionRecord
    {
        public uint Id;
        public int ContributionID;
        public int CreatureID;
    }

    public sealed class CreatureXDisplayInfoRecord
    {
        public uint Id;
        public int CreatureDisplayInfoID;
        public float Probability;
        public float Scale;
        public byte OrderIndex;
        public int CreatureID;
    }

    public sealed class CriteriaRecord
    {
        public uint Id;
        public short Type;
        public int Asset;
        public uint ModifierTreeID;
        public byte StartEvent;
        public int StartAsset;
        public ushort StartTimer;
        public byte FailEvent;
        public int FailAsset;
        public byte Flags;
        public short EligibilityWorldstateID;
        public sbyte EligibilityWorldstatevalue;
    }

    public sealed class CriteriaTreeRecord
    {
        public uint Id;
        public LocalizedString Description;
        public uint Parent;
        public uint Amount;
        public sbyte Operator;
        public uint CriteriaID;
        public int OrderIndex;
        public short Flags;
    }

    public sealed class CriteriaTreeXEffectRecord
    {
        public uint Id;
        public short WorldEffectID;
        public int CriteriaTreeID;
    }

    public sealed class CurrencyCategoryRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte Flags;
        public byte ExpansionID;
    }

    public sealed class CurrencyContainerRecord
    {
        public uint Id;
        public LocalizedString ContainerName;
        public LocalizedString ContainerDescription;
        public int MinAmount;
        public int MaxAmount;
        public int ContainerIconID;
        public int ContainerQuality;
        public int OnLootSpellVisualKitID;
        public int CurrencyTypeID;
    }

    public sealed class CurrencyTypesRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Description;
        public byte CategoryID;
        public int InventoryIconFileID;
        public uint SpellWeight;
        public byte SpellCategory;
        public uint MaxQty;
        public uint MaxEarnablePerWeek;
        public sbyte Quality;
        public int FactionID;
        public CurrencyFlags[] Flags = new CurrencyFlags[2];
    }

    public sealed class CurveRecord
    {
        public uint Id;
        public byte Type;
        public byte Flags;
    }

    public sealed class CurvePointRecord
    {
        public uint Id;
        public Vector2 Pos;
        public ushort CurveID;
        public byte OrderIndex;
    }

    public sealed class DeathThudLookupsRecord
    {
        public uint Id;
        public byte SizeClass;
        public byte TerrainTypeSoundID;
        public uint SoundEntryID;
        public uint SoundEntryIDWater;
    }

    public sealed class DecalPropertiesRecord
    {
        public uint Id;
        public int FileDataID;
        public int TopTextureBlendSetID;
        public int BotTextureBlendSetID;
        public float ModX;
        public float InnerRadius;
        public float OuterRadius;
        public float Rim;
        public float Gain;
        public int Flags;
        public float Scale;
        public float FadeIn;
        public float FadeOut;
        public byte Priority;
        public byte BlendMode;
        public int GameFlags;
        public int CasterDecalPropertiesID;
        public float ArbitraryBoxHeight;
    }

    public sealed class DeclinedWordRecord
    {
        public LocalizedString Word;
        public uint Id;
    }

    public sealed class DeclinedWordCasesRecord
    {
        public uint Id;
        public LocalizedString DeclinedWord;
        public sbyte CaseIndex;
        public int DeclinedWordID;
    }

    public sealed class DestructibleModelDataRecord
    {
        public uint Id;
        public sbyte State0ImpactEffectDoodadSet;
        public byte State0AmbientDoodadSet;
        public uint State1Wmo;
        public sbyte State1DestructionDoodadSet;
        public sbyte State1ImpactEffectDoodadSet;
        public byte State1AmbientDoodadSet;
        public uint State2Wmo;
        public sbyte State2DestructionDoodadSet;
        public sbyte State2ImpactEffectDoodadSet;
        public byte State2AmbientDoodadSet;
        public uint State3Wmo;
        public byte State3InitDoodadSet;
        public byte State3AmbientDoodadSet;
        public byte EjectDirection;
        public byte DoNotHighlight;
        public uint State0Wmo;
        public byte HealEffect;
        public ushort HealEffectSpeed;
        public byte State0NameSet;
        public byte State1NameSet;
        public byte State2NameSet;
        public byte State3NameSet;
    }

    public sealed class DeviceBlacklistRecord
    {
        public uint Id;
        public ushort VendorID;
        public ushort DeviceID;
    }

    public sealed class DifficultyRecord
    {
        public uint Id;
        public LocalizedString Name;
        public MapTypes InstanceType;
        public byte OrderIndex;
        public sbyte OldEnumValue;
        public byte FallbackDifficultyID;
        public byte MinPlayers;
        public byte MaxPlayers;
        public DifficultyFlags Flags;
        public byte ItemContext;
        public byte ToggleDifficultyID;
        public ushort GroupSizeHealthCurveID;
        public ushort GroupSizeDmgCurveID;
        public ushort GroupSizeSpellPointsCurveID;
    }

    public sealed class DissolveEffectRecord
    {
        public uint Id;
        public float Ramp;
        public float StartValue;
        public float EndValue;
        public float FadeInTime;
        public float FadeOutTime;
        public float Duration;
        public sbyte AttachID;
        public sbyte ProjectionType;
        public int TextureBlendSetID;
        public float Scale;
        public int Flags;
        public int CurveID;
        public uint Priority;
        public float FresnelIntensity;
    }

    public sealed class DriverBlacklistRecord
    {
        public uint Id;
        public ushort VendorID;
        public byte DeviceID;
        public uint DriverVersionHi;
        public uint DriverVersionLow;
        public byte OsVersion;
        public byte OsBits;
        public byte Flags;
    }

    public sealed class DungeonEncounterRecord
    {
        public LocalizedString Name;
        public uint Id;
        public short MapID;
        public sbyte DifficultyID;
        public int OrderIndex;
        public sbyte Bit;
        public int CreatureDisplayID;
        public byte Flags;
        public int SpellIconFileID;
    }

    public sealed class DurabilityCostsRecord
    {
        public uint Id;
        public ushort[] WeaponSubClassCost = new ushort[21];
        public ushort[] ArmorSubClassCost = new ushort[8];
    }

    public sealed class DurabilityQualityRecord
    {
        public uint Id;
        public float Data;
    }

    public sealed class EdgeGlowEffectRecord
    {
        public uint Id;
        public float Duration;
        public float FadeIn;
        public float FadeOut;
        public float FresnelCoefficient;
        public float GlowRed;
        public float GlowGreen;
        public float GlowBlue;
        public float GlowAlpha;
        public float GlowMultiplier;
        public sbyte Flags;
        public float InitialDelay;
        public int CurveID;
        public uint Priority;
    }

    public sealed class EmotesRecord
    {
        public uint Id;
        public long RaceMask;
        public LocalizedString EmoteSlashCommand;
        public int AnimID;
        public uint EmoteFlags;
        public byte EmoteSpecProc;
        public uint EmoteSpecProcParam;
        public uint EventSoundID;
        public uint SpellVisualKitID;
        public int ClassMask;
    }

    public sealed class EmotesTextRecord
    {
        public uint Id;
        public LocalizedString Name;
        public Emote EmoteID;
    }

    public sealed class EmotesTextDataRecord
    {
        public uint Id;
        public LocalizedString Text;
        public byte RelationshipFlags;
        public int EmotesTextID;
    }

    public sealed class EmotesTextSoundRecord
    {
        public uint Id;
        public byte RaceID;
        public byte ClassID;
        public byte SexID;
        public uint SoundID;
        public uint EmotesTextID;
    }

    public sealed class EnvironmentalDamageRecord
    {
        public uint Id;
        public sbyte EnumID;
        public ushort VisualkitID;
    }

    public sealed class ExhaustionRecord
    {
        public LocalizedString Name;
        public LocalizedString CombatLogText;
        public uint Id;
        public int Xp;
        public float Factor;
        public float OutdoorHours;
        public float InnHours;
        public float Threshold;
    }

    public sealed class ExpectedStatRecord
    {
        public uint Id;
        public int ExpansionID;
        public float CreatureHealth;
        public float PlayerHealth;
        public float CreatureAutoAttackDps;
        public float CreatureArmor;
        public float PlayerMana;
        public float PlayerPrimaryStat;
        public float PlayerSecondaryStat;
        public float ArmorConstant;
        public float CreatureSpellDamage;
        public uint Lvl;
    }

    public sealed class ExpectedStatModRecord
    {
        public uint Id;
        public float CreatureHealthMod;
        public float PlayerHealthMod;
        public float CreatureAutoAttackDPSMod;
        public float CreatureArmorMod;
        public float PlayerManaMod;
        public float PlayerPrimaryStatMod;
        public float PlayerSecondaryStatMod;
        public float ArmorConstantMod;
        public float CreatureSpellDamageMod;
    }

    public sealed class FactionRecord
    {
        public long[] ReputationRaceMask = new long[4];
        public LocalizedString Name;
        public LocalizedString Description;
        public uint Id;
        public short ReputationIndex;
        public ushort ParentFactionID;
        public byte Expansion;
        public uint FriendshipRepID;
        public byte Flags;
        public ushort ParagonFactionID;
        public short[] ReputationClassMask = new short[4];
        public ushort[] ReputationFlags = new ushort[4];
        public int[] ReputationBase = new int[4];
        public int[] ReputationMax = new int[4];
        public float[] ParentFactionMod = new float[2];
        public byte[] ParentFactionCap = new byte[2];

        public bool CanHaveReputation() => ReputationIndex >= 0;
    }

    public sealed class FactionGroupRecord
    {
        public LocalizedString InternalName;
        public LocalizedString Name;
        public uint Id;
        public byte MaskID;
        public int HonorCurrencyTextureFileID;
        public int ConquestCurrencyTextureFileID;
    }

    public sealed class FactionTemplateRecord
    {
        public uint Id;
        public ushort Faction;
        public ushort Flags;
        public byte FactionGroup;
        public byte FriendGroup;
        public byte EnemyGroup;
        public ushort[] Enemies = new ushort[4];
        public ushort[] Friend = new ushort[4];

        public bool IsFriendlyTo(FactionTemplateRecord entry)
        {
            if (this == entry)
                return true;

            if (entry.Faction != 0)
            {
                for (int i = 0; i < 4; ++i)
                    if (Enemies[i] == entry.Faction)
                        return false;
                for (int i = 0; i < 4; ++i)
                    if (Friend[i] == entry.Faction)
                        return true;
            }
            return (FriendGroup & entry.FactionGroup) != 0 || (FactionGroup & entry.FriendGroup) != 0;
        }
        public bool IsHostileTo(FactionTemplateRecord entry)
        {
            if (this == entry)
                return false;

            if (entry.Faction != 0)
            {
                for (int i = 0; i < 4; ++i)
                    if (Enemies[i] == entry.Faction)
                        return true;
                for (int i = 0; i < 4; ++i)
                    if (Friend[i] == entry.Faction)
                        return false;
            }
            return (EnemyGroup & entry.FactionGroup) != 0;
        }
        public bool IsHostileToPlayers() => (EnemyGroup & (byte)FactionMasks.Player) != 0;
        public bool IsNeutralToAll()
        {
            for (int i = 0; i < 4; ++i)
                if (Enemies[i] != 0)
                    return false;
            return EnemyGroup == 0 && FriendGroup == 0;
        }
        public bool IsContestedGuardFaction() => (Flags & (ushort)FactionTemplateFlags.ContestedGuard) != 0;
    }

    public sealed class FootprintTexturesRecord
    {
        public uint Id;
        public int FileDataID;
        public int TextureBlendsetID;
        public int Flags;
    }

    public sealed class FootstepTerrainLookupRecord
    {
        public uint Id;
        public ushort CreatureFootstepID;
        public sbyte TerrainSoundID;
        public uint SoundID;
        public uint SoundIDSplash;
    }
    public sealed class FullScreenEffectRecord
    {
        public uint Id;
        public uint Flags;
        public float Saturation;
        public float GammaRed;
        public float GammaGreen;
        public float GammaBlue;
        public float MaskOffsetY;
        public float MaskSizeMultiplier;
        public float MaskPower;
        public float ColorMultiplyRed;
        public float ColorMultiplyGreen;
        public float ColorMultiplyBlue;
        public float ColorMultiplyOffsetY;
        public float ColorMultiplyMultiplier;
        public float ColorMultiplyPower;
        public float ColorAdditionRed;
        public float ColorAdditionGreen;
        public float ColorAdditionBlue;
        public float ColorAdditionOffsetY;
        public float ColorAdditionMultiplier;
        public float ColorAdditionPower;
        public int OverlayTextureFileDataID;
        public float BlurIntensity;
        public float BlurOffsetY;
        public float BlurMultiplier;
        public float BlurPower;
        public uint EffectFadeInMs;
        public uint EffectFadeOutMs;
        public uint TextureBlendSetID;
    }

    public sealed class GameClockDebugRecord
    {
        public uint Id;
        public int Offset;
    }

    public sealed class GameObjectArtKitRecord
    {
        public uint Id;
        public int AttachModelFileID;
        public int[] TextureVariationFileID = new int[3];
    }

    public sealed class GameObjectDiffAnimMapRecord
    {
        public uint Id;
        public byte DifficultyID;
        public byte Animation;
        public ushort AttachmentDisplayID;
        public int GameObjectDiffAnimID;
    }

    public sealed class GameObjectDisplayInfoRecord
    {
        public uint Id;
        public LocalizedString ModelName;
        public float[] GeoBox = new float[6];
        public int FileDataID;
        public short ObjectEffectPackageID;
        public float OverrideLootEffectScale;
        public float OverrideNameScale;

        public Vector3 GeoBoxMin
        {
            get { return new Vector3(GeoBox[0], GeoBox[1], GeoBox[2]); }
            set { GeoBox[0] = value.X; GeoBox[1] = value.Y; GeoBox[2] = value.Z; }
        }
        public Vector3 GeoBoxMax
        {
            get { return new Vector3(GeoBox[3], GeoBox[4], GeoBox[5]); }
            set { GeoBox[3] = value.X; GeoBox[4] = value.Y; GeoBox[5] = value.Z; }
        }
    }

    public sealed class GameObjectDisplayInfoXSoundKitRecord
    {
        public uint Id;
        public uint SoundKitID;
        public sbyte EventIndex;
        public int GameObjectDisplayInfoID;
    }

    public sealed class GameObjectsRecord
    {
        public LocalizedString Name;
        public float[] Pos = new float[3];
        public float[] Rot = new float[4];
        public uint Id;
        public ushort OwnerID;
        public uint DisplayID;
        public float Scale;
        public GameObjectTypes TypeID;
        public byte PhaseUseFlags;
        public ushort PhaseID;
        public ushort PhaseGroupID;
        public int[] PropValue = new int[8];
    }

    public sealed class GameTipsRecord
    {
        public uint Id;
        public LocalizedString Text;
        public byte SortIndex;
        public ushort MinLevel;
        public ushort MaxLevel;
    }

    public sealed class GemPropertiesRecord
    {
        public uint Id;
        public ushort EnchantID;
        public SocketColor Type;
        public ushort MinItemlevel;
    }

    public sealed class GlobalStringsRecord
    {
        public uint Id;
        public LocalizedString BaseTag;
        public LocalizedString TagText;
        public byte Flags;
    }

    public sealed class GlyphBindableSpellRecord
    {
        public uint Id;
        public int SpellID;
        public int GlyphPropertiesID;
    }

    public sealed class GlyphExclusiveCategoryRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class GlyphPropertiesRecord
    {
        public uint Id;
        public uint SpellID;
        public byte GlyphType;
        public byte GlyphExclusiveCategoryID;
        public ushort SpellIconID;
    }

    public sealed class GlyphRequiredSpecRecord
    {
        public uint Id;
        public ushort ChrSpecializationID;
        public uint GlyphPropertiesID;
    }

    public sealed class GMSurveyAnswersRecord
    {
        public uint Id;
        public LocalizedString Answer;
        public byte SortIndex;
        public int GMSurveyQuestionID;
    }

    public sealed class GMSurveyCurrentSurveyRecord
    {
        public uint Id;
        public byte GMSURVEYID;
    }

    public sealed class GMSurveyQuestionsRecord
    {
        public uint Id;
        public LocalizedString Question;
    }

    public sealed class GMSurveySurveysRecord
    {
        public uint Id;
        public byte[] Q = new byte[15];
    }

    public sealed class GradientEffectRecord
    {
        public uint Id;
        public float Colors0R;
        public float Colors0G;
        public float Colors0B;
        public float Colors1R;
        public float Colors1G;
        public float Colors1B;
        public float Colors2R;
        public float Colors2G;
        public float Colors2B;
        public float Alpha1;
        public float Alpha2;
        public float EdgeColorR;
        public float EdgeColorG;
        public float EdgeColorB;
        public int Field_8_1_0_28440_014;
        public int Field_8_1_0_28440_015;
    }

    public sealed class GroundEffectDoodadRecord
    {
        public uint Id;
        public int ModelFileID;
        public byte Flags;
        public float Animscale;
        public float Pushscale;
    }

    public sealed class GroundEffectTextureRecord
    {
        public uint Id;
        public uint Density;
        public byte Sound;
        public ushort[] DoodadID = new ushort[4];
        public sbyte[] DoodadWeight = new sbyte[4];
    }

    public sealed class GroupFinderActivityRecord
    {
        public uint Id;
        public LocalizedString FullName;
        public LocalizedString ShortName;
        public byte GroupFinderCategoryID;
        public sbyte OrderIndex;
        public byte GroupFinderActivityGrpID;
        public byte Field_2_5_1_38043_005;
        public uint Flags;
        public ushort MinGearLevelSuggestion;
        public ushort MapID;
        public byte DifficultyID;
        public ushort AreaID;
        public byte MaxPlayers;
        public byte DisplayType;
        public byte Field_2_5_1_38043_013;
        public int Field_2_5_2_39570_014;
    }

    public sealed class GroupFinderActivityGrpRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte OrderIndex;
    }

    public sealed class GroupFinderCategoryRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte OrderIndex;
        public byte Flags;
        public int Field_2_5_2_39570_003;
    }

    public sealed class GuildColorBackgroundRecord
    {
        public uint Id;
        public byte Red;
        public byte Blue;
        public byte Green;
    }

    public sealed class GuildColorBorderRecord
    {
        public uint Id;
        public byte Red;
        public byte Blue;
        public byte Green;
    }

    public sealed class GuildColorEmblemRecord
    {
        public uint Id;
        public byte Red;
        public byte Blue;
        public byte Green;
    }

    public sealed class GuildEmblemRecord
    {
        public uint Id;
        public int EmblemID;
        public int TextureFileDataID;
    }

    public sealed class GuildPerkSpellsRecord
    {
        public uint Id;
        public uint SpellID;
    }

    public sealed class GuildShirtBackgroundRecord
    {
        public uint Id;
        public int Component;
        public int FileDataID;
        public int ShirtID;
        public int Color;
    }

    public sealed class GuildShirtBorderRecord
    {
        public uint Id;
        public int Tier;
        public int Component;
        public int ShirtID;
        public int FileDataID;
        public int Color;
    }

    public sealed class GuildTabardBackgroundRecord
    {
        public uint Id;
        public int Tier;
        public int Component;
        public int FileDataID;
        public int Color;
    }

    public sealed class GuildTabardBorderRecord
    {
        public uint Id;
        public int BorderID;
        public int Tier;
        public int Component;
        public int FileDataID;
        public int Color;
    }

    public sealed class GuildTabardEmblemRecord
    {
        public uint Id;
        public int Component;
        public int Color;
        public int FileDataID;
        public int EmblemID;
    }

    public sealed class HeirloomRecord
    {
        public LocalizedString SourceText;
        public uint Id;
        public uint ItemID;
        public int LegacyUpgradedItemID;
        public uint StaticUpgradedItemID;
        public sbyte SourceTypeEnum;
        public byte Flags;
        public int LegacyItemID;
        public int[] UpgradeItemID = new int[4];
        public ushort[] UpgradeItemBonusListID = new ushort[4];
    }

    public sealed class HelmetAnimScalingRecord
    {
        public uint Id;
        public int RaceID;
        public float Amount;
        public int HelmetGeosetVisDataID;
    }

    public sealed class HelmetGeosetDataRecord
    {
        public uint Id;
        public int RaceID;
        public sbyte HideGeosetGroup;
        public byte RaceBitSelection;
        public int HelmetGeosetVisDataID;
    }

    public sealed class HighlightColorRecord
    {
        public uint Id;
        public sbyte Type;
        public int StartColor;
        public int MidColor;
        public int EndColor;
        public byte Flags;
    }

    public sealed class HolidayDescriptionsRecord
    {
        public uint Id;
        public LocalizedString Description;
    }

    public sealed class HolidayNamesRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class HolidaysRecord
    {
        public uint Id;
        public ushort Region;
        public byte Looping;
        public uint HolidayNameID;
        public uint HolidayDescriptionID;
        public byte Priority;
        public sbyte CalendarFilterType;
        public byte Flags;
        public uint Field_1_13_2_30073_008;
        public ushort[] Duration = new ushort[10];
        public uint[] Date = new uint[16];
        public byte[] CalendarFlags = new byte[10];
        public int[] TextureFileDataID = new int[3];
    }

    public sealed class HotfixesRecord
    {
        public uint Id;
        public LocalizedString Tablename;
        public int ObjectID;
        public int Flags;
        public int PushID;
    }

    public sealed class ImportPriceArmorRecord
    {
        public uint Id;
        public float ClothModifier;
        public float LeatherModifier;
        public float ChainModifier;
        public float PlateModifier;
    }

    public sealed class ImportPriceQualityRecord
    {
        public uint Id;
        public float Data;
    }

    public sealed class ImportPriceShieldRecord
    {
        public uint Id;
        public float Data;
    }

    public sealed class ImportPriceWeaponRecord
    {
        public uint Id;
        public float Data;
    }

    public sealed class InvasionClientDataRecord
    {
        public LocalizedString Name;
        public float[] IconLocation = new float[2];
        public uint Id;
        public int WorldStateID;
        public int UiTextureAtlasMemberID;
        public int ScenarioID;
        public uint WorldQuestID;
        public int WorldStateValue;
        public int InvasionEnabledWorldStateID;
        public int AreaTableID;
    }

    public sealed class ItemRecord
    {
        public uint Id;
        public ItemClass ClassID;
        public byte SubclassID;
        public byte Material;
        public InventoryType InventoryType;
        public int RequiredLevel;
        public byte SheatheType;
        public ushort RandomSelect;
        public ushort ItemRandomSuffixGroupID;
        public sbyte SoundOverridesubclassID;
        public ushort ModifiedCraftingReagentItemID;
        public int IconFileDataID;
        public byte Field_2_5_1_38043_012;
        public int Field_2_5_1_38043_013;
        public uint MaxDurability;
        public byte AmmoType;
        public byte[] DamageType = new byte[5];
        public short[] DefensiveStats = new short[7];
        public ushort[] DamageMin = new ushort[5];
        public ushort[] DamageMax = new ushort[5];
    }

    public sealed class ItemAppearanceRecord
    {
        public uint Id;
        public byte DisplayType;
        public uint ItemDisplayInfoID;
        public int DefaultIconFileDataID;
        public int UiOrder;
    }

    public sealed class ItemAppearanceXUiCameraRecord
    {
        public uint Id;
        public ushort ItemAppearanceID;
        public ushort UiCameraID;
    }

    public sealed class ItemArmorQualityRecord
    {
        public uint Id;
        public float[] QualityMod = new float[7];
    }

    public sealed class ItemArmorShieldRecord
    {
        public uint Id;
        public float[] Quality = new float[7];
        public ushort ItemLevel;
    }

    public sealed class ItemArmorTotalRecord
    {
        public uint Id;
        public short ItemLevel;
        public float Cloth;
        public float Leather;
        public float Mail;
        public float Plate;
    }

    public sealed class ItemBagFamilyRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class ItemBonusRecord
    {
        public uint Id;
        public int[] Value = new int[4];
        public ushort ParentItemBonusListID;
        public ItemBonusType Type;
        public byte OrderIndex;
    }

    public sealed class ItemBonusListLevelDeltaRecord
    {
        public short ItemLevelDelta;
        public uint Id;
    }

    public sealed class ItemBonusTreeNodeRecord
    {
        public uint Id;
        public byte ItemContext;
        public ushort ChildItemBonusTreeID;
        public ushort ChildItemBonusListID;
        public ushort ChildItemLevelSelectorID;
        public uint ParentItemBonusTreeID;
    }

    public sealed class ItemChildEquipmentRecord
    {
        public uint Id;
        public uint ChildItemID;
        public byte ChildItemEquipSlot;
        public uint ParentItemID;
    }

    public sealed class ItemClassRecord
    {
        public uint Id;
        public LocalizedString ClassName;
        public sbyte ClassID;
        public float PriceModifier;
        public byte Flags;
    }

    public sealed class ItemContextPickerEntryRecord
    {
        public uint Id;
        public byte ItemCreationContext;
        public byte OrderIndex;
        public int PVal;
        public uint Flags;
        public uint PlayerConditionID;
        public uint ItemContextPickerID;
    }

    public sealed class ItemCurrencyCostRecord
    {
        public uint Id;
        public uint ItemID;
    }

    public sealed class ItemDamageRecord
    {
        public uint Id;
        public ushort ItemLevel;
        public float[] Quality = new float[7];
    }

    public sealed class ItemDisenchantLootRecord
    {
        public uint Id;
        public sbyte Subclass;
        public byte Quality;
        public ushort MinLevel;
        public ushort MaxLevel;
        public ushort SkillRequired;
        public sbyte ExpansionID;
        public uint Class;
    }

    public sealed class ItemDisplayInfoRecord
    {
        public uint Id;
        public int ItemVisual;
        public int ParticleColorID;
        public uint ItemRangedDisplayInfoID;
        public uint OverrideSwooshSoundKitID;
        public int SheatheTransformMatrixID;
        public int StateSpellVisualKitID;
        public int SheathedSpellVisualKitID;
        public uint UnsheathedSpellVisualKitID;
        public int Flags;
        public uint[] ModelResourcesID = new uint[2];
        public int[] ModelMaterialResourcesID = new int[2];
        public int[] Field_8_2_0_30080_011 = new int[2];
        public int[] GeosetGroup = new int[6];
        public int[] AttachmentGeosetGroup = new int[6];
        public int[] HelmetGeosetVis = new int[2];
    }

    public sealed class ItemDisplayInfoMaterialResRecord
    {
        public uint Id;
        public sbyte ComponentSection;
        public int MaterialResourcesID;
        public int ItemDisplayInfoID;
    }

    public sealed class ItemDisplayXUiCameraRecord
    {
        public uint Id;
        public int ItemDisplayInfoID;
        public ushort UiCameraID;
    }

    public sealed class ItemEffectRecord
    {
        public uint Id;
        public byte LegacySlotIndex;
        public ItemSpelltriggerType TriggerType;
        public short Charges;
        public int CoolDownMSec;
        public int CategoryCoolDownMSec;
        public ushort SpellCategoryID;
        public int SpellID;
        public ushort ChrSpecializationID;
        public uint ParentItemID;
    }

    public sealed class ItemExtendedCostRecord
    {
        public uint Id;
        public ushort RequiredArenaRating;
        public byte ArenaBracket;
        public byte Flags;
        public byte MinFactionID;
        public byte MinReputation;
        public byte RequiredAchievement;
        public uint[] ItemID = new uint[5];
        public ushort[] ItemCount = new ushort[5];
        public ushort[] CurrencyID = new ushort[5];
        public uint[] CurrencyCount = new uint[5];
    }

    public sealed class ItemGroupSoundsRecord
    {
        public uint Id;
        public uint[] Sound = new uint[4];
    }

    public sealed class ItemLevelSelectorRecord
    {
        public uint Id;
        public ushort MinItemLevel;
        public ushort ItemLevelSelectorQualitySetID;
    }

    public sealed class ItemLevelSelectorQualityRecord
    {
        public uint Id;
        public uint QualityItemBonusListID;
        public sbyte Quality;
        public uint ParentILSQualitySetID;
    }

    public sealed class ItemLevelSelectorQualitySetRecord
    {
        public uint Id;
        public short IlvlRare;
        public short IlvlEpic;
    }

    public sealed class ItemLimitCategoryRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte Quantity;
        public byte Flags;
    }

    public sealed class ItemLimitCategoryConditionRecord
    {
        public uint Id;
        public sbyte AddQuantity;
        public uint PlayerConditionID;
        public uint ParentItemLimitCategoryID;
    }

    public sealed class ItemModifiedAppearanceRecord
    {
        public uint Id;
        public uint ItemID;
        public uint ItemAppearanceModifierID;
        public int ItemAppearanceID;
        public int OrderIndex;
        public int TransmogSourceTypeEnum;
    }

    public sealed class ItemModifiedAppearanceExtraRecord
    {
        public uint Id;
        public int IconFileDataID;
        public int UnequippedIconFileDataID;
        public byte SheatheType;
        public sbyte DisplayWeaponSubclassID;
        public sbyte DisplayInventoryType;
    }

    public sealed class ItemNameDescriptionRecord
    {
        public uint Id;
        public LocalizedString Description;
        public int Color;
    }

    public sealed class ItemPetFoodRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class ItemPriceBaseRecord
    {
        public uint Id;
        public ushort ItemLevel;
        public float Armor;
        public float Weapon;
    }

    public sealed class ItemRandomPropertiesRecord
    {
        public uint Id;
        public LocalizedString Name;
        public ushort[] Enchantment = new ushort[5];
    }

    public sealed class ItemRandomSuffixRecord
    {
        public uint Id;
        public LocalizedString Name;
        public ushort[] Enchantment = new ushort[5];
        public ushort[] AllocationPct = new ushort[5];
    }

    public sealed class ItemRangedDisplayInfoRecord
    {
        public uint Id;
        public uint CastSpellVisualID;
        public uint AutoAttackSpellVisualID;
        public uint QuiverFileDataID;
        public uint MissileSpellVisualEffectNameID;
    }

    public sealed class ItemSetRecord
    {
        public uint Id;
        public LocalizedString Name;
        public ItemSetFlags SetFlags;
        public uint RequiredSkill;
        public ushort RequiredSkillRank;
        public uint[] ItemID = new uint[17];
    }

    public sealed class ItemSetSpellRecord
    {
        public uint Id;
        public ushort ChrSpecID;
        public uint SpellID;
        public byte Threshold;
        public uint ItemSetID;
    }

    public sealed class ItemSparseRecord
    {
        public uint Id;
        public long AllowableRace;
        public LocalizedString Description;
        public LocalizedString Display3;
        public LocalizedString Display2;
        public LocalizedString Display1;
        public LocalizedString Display;
        public float DmgVariance;
        public uint DurationInInventory;
        public float QualityModifier;
        public uint BagFamily;
        public float ItemRange;
        public float[] StatPercentageOfSocket = new float[10];
        public int[] StatPercentEditor = new int[10];
        public uint Stackable;
        public uint MaxCount;
        public uint RequiredAbility;
        public uint SellPrice;
        public uint BuyPrice;
        public uint VendorStackCount;
        public float PriceVariance;
        public float PriceRandomValue;
        public int[] Flags = new int[4];
        public int OppositeFactionItemID;
        public uint MaxDurability;
        public ushort ItemNameDescriptionID;
        public ushort RequiredTransmogHoliday;
        public ushort RequiredHoliday;
        public ushort LimitCategory;
        public ushort GemProperties;
        public ushort SocketMatchenchantmentID;
        public ushort TotemCategoryID;
        public ushort InstanceBound;
        public ushort[] ZoneBound = new ushort[2];
        public ushort ItemSet;
        public ushort LockID;
        public ushort StartQuestID;
        public ushort PageID;
        public ushort ItemDelay;
        public ushort MinFactionID;
        public ushort RequiredSkillRank;
        public ushort RequiredSkill;
        public ushort ItemLevel;
        public short AllowableClass;
        public ushort ItemRandomSuffixGroupID;
        public ushort RandomSelect;
        public ushort[] DamageMin = new ushort[5];
        public ushort[] DamageMax = new ushort[5];
        public short[] DefensiveStats = new short[7];
        public ushort ScalingStatDistributionID;
        public byte ExpansionID;
        public byte ArtifactID;
        public byte SpellWeight;
        public byte SpellWeightCategory;
        public byte[] SocketType = new byte[3];
        public byte SheatheType;
        public byte Material;
        public byte PageMaterialID;
        public byte LanguageID;
        public byte Bonding;
        public byte DamageType;
        public sbyte[] StatModifierBonusStat = new sbyte[10];
        public byte ContainerSlots;
        public byte MinReputation;
        public byte RequiredPVPMedal;
        public byte RequiredPVPRank;
        public InventoryType InventoryType;
        public byte OverallQualityID;
        public byte AmmoType;
        public sbyte[] StatValue = new sbyte[10];
        public sbyte RequiredLevel;
    }

    public sealed class ItemSpecRecord
    {
        public uint Id;
        public byte MinLevel;
        public byte MaxLevel;
        public byte ItemType;
        public ItemSpecStat PrimaryStat;
        public ItemSpecStat SecondaryStat;
        public ushort SpecializationID;
    }

    public sealed class ItemSpecOverrideRecord
    {
        public uint Id;
        public ushort SpecID;
        public uint ItemID;
    }

    public sealed class ItemSubClassRecord
    {
        public uint Id;
        public LocalizedString DisplayName;
        public LocalizedString VerboseName;
        public sbyte ClassID;
        public sbyte SubClassID;
        public byte AuctionHouseSortOrder;
        public sbyte PrerequisiteProficiency;
        public short Flags;
        public sbyte DisplayFlags;
        public sbyte WeaponSwingSize;
        public sbyte PostrequisiteProficiency;
    }

    public sealed class ItemSubClassMaskRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte ClassID;
        public uint Mask;
    }

    public sealed class ItemUpgradeRecord
    {
        public uint Id;
        public byte ItemUpgradePathID;
        public byte ItemLevelIncrement;
        public ushort PrerequisiteID;
        public ushort CurrencyType;
        public uint CurrencyAmount;
    }

    public sealed class ItemVisualsRecord
    {
        public uint Id;
        public int[] ModelFileID = new int[5];
    }

    public sealed class ItemVisualsXEffectRecord
    {
        public uint Id;
        public sbyte AttachmentID;
        public sbyte DisplayWeaponSubclassID;
        public int SpellVisualKitID;
        public int AttachmentModelFileID;
        public float Scale;
        public int ItemVisualsID;
    }

    public sealed class ItemXBonusTreeRecord
    {
        public uint Id;
        public ushort ItemBonusTreeID;
        public uint ItemID;
    }

    public sealed class JournalEncounterRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Description;
        public float[] Map = new float[2];
        public ushort JournalInstanceID;
        public uint OrderIndex;
        public ushort FirstSectionID;
        public ushort UiMapID;
        public uint MapDisplayConditionID;
        public byte Flags;
        public sbyte DifficultyMask;
    }

    public sealed class JournalEncounterCreatureRecord
    {
        public LocalizedString Name;
        public LocalizedString Description;
        public uint Id;
        public ushort JournalEncounterID;
        public uint CreatureDisplayInfoID;
        public uint FileDataID;
        public byte OrderIndex;
        public uint UiModelSceneID;
    }

    public sealed class JournalEncounterItemRecord
    {
        public uint Id;
        public ushort JournalEncounterID;
        public uint ItemID;
        public sbyte FactionMask;
        public byte Flags;
        public sbyte DifficultyMask;
    }

    public sealed class JournalEncounterSectionRecord
    {
        public uint Id;
        public LocalizedString Title;
        public LocalizedString BodyText;
        public ushort JournalEncounterID;
        public byte OrderIndex;
        public ushort ParentSectionID;
        public ushort FirstChildSectionID;
        public ushort NextSiblingSectionID;
        public byte Type;
        public uint IconCreatureDisplayInfoID;
        public int UiModelSceneID;
        public int SpellID;
        public int IconFileDataID;
        public ushort Flags;
        public ushort IconFlags;
        public sbyte DifficultyMask;
    }

    public sealed class JournalEncounterXDifficultyRecord
    {
        public uint Id;
        public byte DifficultyID;
        public int JournalEncounterID;
    }

    public sealed class JournalEncounterXMapLocRecord
    {
        public uint Id;
        public float[] Map = new float[2];
        public int JournalEncounterID;
        public int MapDisplayConditionID;
        public byte Flags;
        public int UiMapID;
    }

    public sealed class JournalInstanceRecord
    {
        public LocalizedString Name;
        public LocalizedString Description;
        public uint Id;
        public ushort MapID;
        public int BackgroundFileDataID;
        public int ButtonFileDataID;
        public int ButtonSmallFileDataID;
        public int LoreFileDataID;
        public byte OrderIndex;
        public byte Flags;
        public ushort AreaID;
    }

    public sealed class JournalItemXDifficultyRecord
    {
        public uint Id;
        public byte DifficultyID;
        public int JournalEncounterItemID;
    }

    public sealed class JournalSectionXDifficultyRecord
    {
        public uint Id;
        public byte DifficultyID;
        public int JournalEncounterSectionID;
    }

    public sealed class JournalTierRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class JournalTierXInstanceRecord
    {
        public uint Id;
        public ushort JournalTierID;
        public ushort JournalInstanceID;
    }

    public sealed class KeychainRecord
    {
        public uint Id;
        public byte[] Key = new byte[32];
    }

    public sealed class KeystoneAffixRecord
    {
        public LocalizedString Name;
        public LocalizedString Description;
        public uint Id;
        public int FiledataID;
    }

    public sealed class LanguagesRecord
    {
        public LocalizedString Name;
        public uint Id;
    }

    public sealed class LanguageWordsRecord
    {
        public uint Id;
        public string Word;
        public byte LanguageID;
    }

    public sealed class LFGDungeonExpansionRecord
    {
        public uint Id;
        public byte ExpansionLevel;
        public ushort RandomID;
        public byte HardLevelmin;
        public byte HardLevelmax;
        public int TargetLevelmin;
        public int TargetLevelmax;
        public int LfgID;
    }

    public sealed class LFGDungeonGroupRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte TypeID;
        public byte ParentGroupID;
        public ushort OrderIndex;
    }

    public sealed class LFGDungeonsRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Description;
        public byte MinLevel;
        public ushort MaxLevel;
        public LfgType TypeID;
        public byte Subtype;
        public sbyte Faction;
        public int IconTextureFileID;
        public int RewardsBgTextureFileID;
        public int PopupBgTextureFileID;
        public byte ExpansionLevel;
        public short MapID;
        public Difficulty DifficultyID;
        public float MinGear;
        public byte GroupID;
        public byte OrderIndex;
        public uint RequiredPlayerconditionID;
        public byte TargetLevel;
        public byte TargetLevelmin;
        public ushort TargetLevelmax;
        public ushort RandomID;
        public ushort ScenarioID;
        public ushort FinalEncounterID;
        public byte CountTank;
        public byte CountHealer;
        public byte CountDamage;
        public byte MinCounttank;
        public byte MinCounthealer;
        public byte MinCountdamage;
        public ushort BonusReputationamount;
        public ushort MentorItemLevel;
        public byte MentorCharLevel;
        public LfgFlags[] Flags = new LfgFlags[2];
    }

    public sealed class LfgDungeonsGroupingMapRecord
    {
        public uint Id;
        public ushort RandomLfgDungeonsID;
        public byte GroupID;
        public int LfgDungeonsID;
    }

    public sealed class LFGRoleRequirementRecord
    {
        public uint Id;
        public sbyte RoleType;
        public uint PlayerConditionID;
        public int LfgDungeonsID;
    }

    public sealed class LightRecord
    {
        public uint Id;
        public Vector3 GameCoords;
        public float GameFalloffStart;
        public float GameFalloffEnd;
        public short ContinentID;
        public ushort[] LightParamsID = new ushort[8];
    }

    public sealed class LightDataRecord
    {
        public uint Id;
        public ushort LightParamID;
        public ushort Time;
        public int DirectColor;
        public int AmbientColor;
        public int SkyTopColor;
        public int SkyMiddleColor;
        public int SkyBand1Color;
        public int SkyBand2Color;
        public int SkySmogColor;
        public int SkyFogColor;
        public int SunColor;
        public int CloudSunColor;
        public int CloudEmissiveColor;
        public int CloudLayer1AmbientColor;
        public int CloudLayer2AmbientColor;
        public int OceanCloseColor;
        public int OceanFarColor;
        public int RiverCloseColor;
        public int RiverFarColor;
        public int ShadowOpacity;
        public float FogEnd;
        public float FogScaler;
        public float FogDensity;
        public float FogHeight;
        public float FogHeightScaler;
        public float FogHeightDensity;
        public float Field_9_0_1_33978_026;
        public float Field_9_1_0_38312_027;
        public float Field_9_1_0_38312_028;
        public float SunFogAngle;
        public float CloudDensity;
        public uint ColorGradingFileDataID;
        public uint DarkerColorGradingFileDataID;
        public int HorizonAmbientColor;
        public int GroundAmbientColor;
        public uint EndFogColor;
        public float EndFogColorDistance;
        public float Field_9_1_0_38312_037;
        public uint SunFogColor;
        public float SunFogStrength;
        public uint FogHeightColor;
        public uint Field_9_1_0_38312_041;
        public float[] FogHeightCoefficients = new float[4];
        public float[] MainFogCoefficients = new float[4];
        public float[] Field_9_1_0_38312_044 = new float[4];
    }

    public sealed class LightningRecord
    {
        public uint Id;
        public float[] BoltDirection = new float[2];
        public int[] SoundKitID = new int[3];
        public float BoltDirectionVariance;
        public float MinDivergence;
        public float MaxDivergence;
        public float MinConvergenceSpeed;
        public float MaxConvergenceSpeed;
        public float SegmentSize;
        public float MinBoltWidth;
        public float MaxBoltWidth;
        public float MinBoltHeight;
        public float MaxBoltHeight;
        public int MaxSegmentCount;
        public float MinStrikeTime;
        public float MaxStrikeTime;
        public float MinEndTime;
        public float MaxEndTime;
        public float MinFadeTime;
        public float MaxFadeTime;
        public float Field_1_13_2_30073_020Min;
        public float Field_1_13_2_30073_021Max;
        public int FlashColor;
        public int BoltColor;
        public float Brightness;
        public float MinCloudDepth;
        public float MaxCloudDepth;
        public float MinFadeInStrength;
        public float MaxFadeInStrength;
        public float MinStrikeStrength;
        public float MaxStrikeStrength;
        public float GroundBrightnessScalar;
        public float BoltBrightnessScalar;
        public float CloudBrightnessScalar;
        public float SoundEmitterDistance;
    }

    public sealed class LightParamsRecord
    {
        public float[] OverrideCelestialSphere = new float[3];
        public uint Id;
        public byte HighlightSky;
        public ushort LightSkyboxID;
        public byte CloudTypeID;
        public float Glow;
        public float WaterShallowAlpha;
        public float WaterDeepAlpha;
        public float OceanShallowAlpha;
        public float OceanDeepAlpha;
        public sbyte Flags;
        public int SsaoSettingsID;
    }

    public sealed class LightSkyboxRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte Flags;
        public int SkyboxFileDataID;
        public int CelestialSkyboxFileDataID;
    }

    public sealed class LiquidMaterialRecord
    {
        public uint Id;
        public sbyte Flags;
        public sbyte LVF;
    }

    public sealed class LiquidObjectRecord
    {
        public uint Id;
        public float FlowDirection;
        public float FlowSpeed;
        public short LiquidTypeID;
        public byte Fishable;
        public byte Reflection;
    }

    public sealed class LiquidTypeRecord
    {
        public uint Id;
        public string Name;
        public string[] Texture = new string[6];
        public ushort Flags;
        public byte SoundBank;
        public uint SoundID;
        public uint SpellID;
        public float MaxDarkenDepth;
        public float FogDarkenIntensity;
        public float AmbDarkenIntensity;
        public float DirDarkenIntensity;
        public ushort LightID;
        public float ParticleScale;
        public byte ParticleMovement;
        public byte ParticleTexSlots;
        public byte MaterialID;
        public int MinimapStaticCol;
        public byte[] FrameCountTexture = new byte[6];
        public int[] Color = new int[2];
        public float[] Float = new float[18];
        public uint[] Int = new uint[4];
        public float[] Coefficient = new float[4];
    }

    public sealed class LoadingScreensRecord
    {
        public uint Id;
        public int NarrowScreenFileDataID;
        public int WideScreenFileDataID;
        public int WideScreen169FileDataID;
    }

    public sealed class LoadingScreenTaxiSplinesRecord
    {
        public uint Id;
        public ushort PathID;
        public byte LegIndex;
        public ushort LoadingScreenID;
        public float[] Locx = new float[10];
        public float[] Locy = new float[10];
    }

    public sealed class LocaleRecord
    {
        public uint Id;
        public byte WowLocale;
        public int FontFileDataID;
        public byte Secondary;
    }

    public sealed class LocationRecord
    {
        public uint Id;
        public float[] Pos = new float[3];
        public float[] Rot = new float[3];
    }

    public sealed class LockRecord
    {
        public uint Id;
        public int[] Index = new int[8];
        public ushort[] Skill = new ushort[8];
        public byte[] Type = new byte[8];
        public byte[] Action = new byte[8];
    }

    public sealed class LockTypeRecord
    {
        public LocalizedString Name;
        public LocalizedString ResourceName;
        public LocalizedString Verb;
        public LocalizedString CursorName;
        public uint Id;
    }

    public sealed class LookAtControllerRecord
    {
        public uint Id;
        public float ReactionEnableDistance;
        public uint ReactionWarmUpTimeMSMin;
        public uint ReactionWarmUpTimeMSMax;
        public ushort ReactionEnableFOVDeg;
        public float ReactionGiveupDistance;
        public uint ReactionGiveupFOVDeg;
        public ushort ReactionGiveupTimeMS;
        public ushort ReactionIgnoreTimeMinMS;
        public ushort ReactionIgnoreTimeMaxMS;
        public byte MaxTorsoYaw;
        public byte MaxTorsoYawWhileMoving;
        public uint MaxTorsoPitchUp;
        public uint MaxTorsoPitchDown;
        public byte MaxHeadYaw;
        public byte MaxHeadPitch;
        public float TorsoSpeedFactor;
        public float HeadSpeedFactor;
        public byte Flags;
    }

    public sealed class MailTemplateRecord
    {
        public uint Id;
        public LocalizedString Body;
    }

    public sealed class ManagedWorldStateRecord
    {
        public uint Id;
        public int CurrentStageWorldStateID;
        public int ProgressWorldStateID;
        public uint UpTimeSecs;
        public uint DownTimeSecs;
        public int AccumulationStateTargetValue;
        public int DepletionStateTargetValue;
        public int AccumulationAmountPerMinute;
        public int DepletionAmountPerMinute;
        public int[] OccurrencesWorldStateID = new int[4];
    }

    public sealed class ManagedWorldStateBuffRecord
    {
        public uint Id;
        public int BuffSpellID;
        public uint PlayerConditionID;
        public uint OccurrenceValue;
        public int ManagedWorldStateID;
    }

    public sealed class ManagedWorldStateInputRecord
    {
        public uint Id;
        public int ManagedWorldStateID;
        public int QuestID;
        public int ValidInputConditionID;
    }

    public sealed class ManifestInterfaceActionIconRecord
    {
        public uint Id;
    }

    public sealed class ManifestInterfaceDataRecord
    {
        public uint Id;
        public LocalizedString FilePath;
        public LocalizedString FileName;
    }

    public sealed class ManifestInterfaceItemIconRecord
    {
        public uint Id;
    }

    public sealed class ManifestInterfaceTOCDataRecord
    {
        public uint Id;
        public LocalizedString FilePath;
    }

    public sealed class MapRecord
    {
        public uint Id;
        public LocalizedString Directory;
        public LocalizedString MapName;
        public LocalizedString MapDescription0;
        public LocalizedString MapDescription1;
        public LocalizedString PvpShortDescription;
        public LocalizedString PvpLongDescription;
        public byte MapType;
        public MapTypes InstanceType;
        public byte ExpansionID;
        public ushort AreaTableID;
        public short LoadingScreenID;
        public short TimeOfDayOverride;
        public short ParentMapID;
        public short CosmeticParentMapID;
        public byte TimeOffset;
        public float MinimapIconScale;
        public short CorpseMapID;
        public byte MaxPlayers;
        public short WindSettingsID;
        public int ZmpFileDataID;
        public MapFlags[] Flags = new MapFlags[2];

        public Expansion Expansion() => (Expansion)ExpansionID;

        public bool IsDungeon() => InstanceType == MapTypes.Instance || InstanceType == MapTypes.Raid;
        public bool IsNonRaidDungeon() => InstanceType == MapTypes.Instance;
        public bool IsInstanceable() => InstanceType == MapTypes.Instance || InstanceType == MapTypes.Raid || InstanceType == MapTypes.Battleground || InstanceType == MapTypes.Arena;
        public bool IsRaid() => InstanceType == MapTypes.Raid;
        public bool IsBattleground() => InstanceType == MapTypes.Battleground;
        public bool IsBattleArena() => InstanceType == MapTypes.Arena;
        public bool IsBattlegroundOrArena() => IsBattleArena() || IsBattleground();
        public bool IsWorldMap() => InstanceType == MapTypes.Common;

        public bool GetEntrancePos(out int mapId, out float x, out float y)
        {
            mapId = 0;
            x = 0.0f;
            y = 0.0f;

            if (CorpseMapID < 0)
                return false;

            mapId = CorpseMapID;
            return true;
        }

        public bool IsContinent() => Id == 0 || Id == 1 || Id == 530;
        public bool IsDynamicDifficultyMap() => Flags[0].HasAnyFlag(MapFlags.CanToggleDifficulty);
    }

    public sealed class MapCelestialBodyRecord
    {
        public uint Id;
        public short CelestialBodyID;
        public uint PlayerConditionID;
        public int MapID;
    }

    public sealed class MapChallengeModeRecord
    {
        public LocalizedString Name;
        public uint Id;
        public ushort MapID;
        public byte Flags;
        public short[] CriteriaCount = new short[3];
    }

    public sealed class MapDifficultyRecord
    {
        public uint Id;
        public LocalizedString Message;
        public uint ItemContextPickerID;
        public int ContentTuningID;
        public byte DifficultyID;
        public byte LockID;
        public byte ResetInterval;
        public byte MaxPlayers;
        public byte ItemContext;
        public byte Flags;
        public uint MapID;

        public uint GetRaidDuration()
        {
            if (ResetInterval == 1)
                return 86400;
            if (ResetInterval == 2)
                return 604800;
            return 0;
        }
    }

    public sealed class MapDifficultyXConditionRecord
    {
        public uint Id;
        public LocalizedString FailureDescription;
        public uint PlayerConditionID;
        public int OrderIndex;
        public uint MapDifficultyID;
    }

    public sealed class MapLoadingScreenRecord
    {
        public uint Id;
        public float[] Min = new float[2];
        public float[] Max = new float[2];
        public int LoadingScreenID;
        public int OrderIndex;
        public int MapID;
    }

    public sealed class MarketingPromotionsXLocaleRecord
    {
        public uint Id;
        public LocalizedString AcceptURL;
        public byte PromotionID;
        public sbyte LocaleID;
        public int AdTexture;
        public int LogoTexture;
        public int AcceptButtonTexture;
        public int DeclineButtonTexture;
    }

    public sealed class MaterialRecord
    {
        public uint Id;
        public byte Flags;
        public uint FoleySoundID;
        public uint SheatheSoundID;
        public uint UnsheatheSoundID;
    }

    public sealed class MinorTalentRecord
    {
        public uint Id;
        public int SpellID;
        public int OrderIndex;
        public int ChrSpecializationID;
    }

    public sealed class MissileTargetingRecord
    {
        public uint Id;
        public float TurnLingering;
        public float PitchLingering;
        public float MouseLingering;
        public float EndOpacity;
        public float ArcSpeed;
        public float ArcRepeat;
        public float ArcWidth;
        public float ImpactTexRadius;
        public int ArcTextureFileID;
        public int ImpactTextureFileID;
        public float[] ImpactRadius = new float[2];
        public int[] ImpactModelFileID = new int[2];
    }

    public sealed class ModelAnimCloakDampeningRecord
    {
        public uint Id;
        public uint AnimationDataID;
        public uint CloakDampeningID;
        public int FileDataID;
    }

    public sealed class ModelFileDataRecord
    {
        public uint Id;
        public byte Flags;
        public byte LodCount;
        public uint ModelResourcesID;
    }

    public sealed class ModelRibbonQualityRecord
    {
        public uint Id;
        public byte RibbonQualityID;
        public int FileDataID;
    }

    public sealed class ModifierTreeRecord
    {
        public uint Id;
        public uint Parent;
        public sbyte Operator;
        public sbyte Amount;
        public int Type;
        public int Asset;
        public int SecondaryAsset;
        public sbyte TertiaryAsset;
    }

    public sealed class MovieRecord
    {
        public uint Id;
        public byte Volume;
        public byte KeyID;
        public uint AudioFileDataID;
        public uint SubtitleFileDataID;
    }

    public sealed class MovieFileDataRecord
    {
        public uint Id;
        public ushort Resolution;
    }

    public sealed class MovieVariationRecord
    {
        public uint Id;
        public uint FileDataID;
        public uint OverlayFileDataID;
        public int MovieID;
    }

    public sealed class MultiStatePropertiesRecord
    {
        public uint Id;
        public float[] Offset = new float[3];
        public int GameObjectID;
        public byte StateIndex;
        public int GameEventID;
        public float Facing;
        public int TransitionInID;
        public int TransitionOutID;
        public int CollisionHull;
        public uint Flags;
        public int SpellVisualKitID;
        public int MultiPropertiesID;
    }

    public sealed class MultiTransitionPropertiesRecord
    {
        public uint Id;
        public uint TransitionType;
        public uint DurationMS;
        public uint Flags;
        public int StartSpellVisualKitID;
        public int EndSpellVisualKitID;
    }

    public sealed class MythicPlusSeasonRewardLevelsRecord
    {
        public uint Id;
        public int DifficultyLevel;
        public int WeeklyRewardLevel;
        public int EndOfRunRewardLevel;
        public int Season;
    }

    public sealed class NameGenRecord
    {
        public uint Id;
        public string Name;
        public byte RaceID;
        public byte Sex;
    }

    public sealed class NamesProfanityRecord
    {
        public uint Id;
        public string Name;
        public sbyte Language;
    }

    public sealed class NamesReservedRecord
    {
        public uint Id;
        public string Name;
    }

    public sealed class NamesReservedLocaleRecord
    {
        public uint Id;
        public string Name;
        public byte LocaleMask;
    }

    public sealed class NPCModelItemSlotDisplayInfoRecord
    {
        public uint Id;
        public int ItemDisplayInfoID;
        public sbyte ItemSlot;
        public int NpcModelID;
    }

    public sealed class NPCSoundsRecord
    {
        public uint Id;
        public uint[] SoundID = new uint[4];
    }

    public sealed class NumTalentsAtLevelRecord
    {
        public uint Id;
        public int NumTalents;
        public int NumTalentsDeathKnight;
        public int NumTalentsDemonHunter;
    }

    public sealed class ObjectEffectRecord
    {
        public uint Id;
        public float[] Offset = new float[3];
        public ushort ObjectEffectGroupID;
        public byte TriggerType;
        public byte EventType;
        public byte EffectRecType;
        public uint EffectRecID;
        public sbyte Attachment;
        public uint ObjectEffectModifierID;
    }

    public sealed class ObjectEffectModifierRecord
    {
        public uint Id;
        public float[] Param = new float[4];
        public byte InputType;
        public byte MapType;
        public byte OutputType;
    }

    public sealed class ObjectEffectPackageElemRecord
    {
        public uint Id;
        public ushort ObjectEffectPackageID;
        public ushort ObjectEffectGroupID;
        public ushort StateType;
    }

    public sealed class OccluderRecord
    {
        public uint Id;
        public int MapID;
        public byte Type;
        public byte SplineType;
        public byte Red;
        public byte Green;
        public byte Blue;
        public byte Alpha;
        public byte Flags;
    }

    public sealed class OccluderCurtainRecord
    {
        public uint Id;
        public int MapID;
        public int Field_2_5_1_38043_001;
        public int Field_2_5_1_38043_002;
        public int Field_2_5_1_38043_003;
        public int Field_2_5_1_38043_004;
        public int Field_2_5_1_38043_005;
    }

    public sealed class OccluderLocationRecord
    {
        public float[] Pos = new float[3];
        public float[] Rot = new float[3];
        public uint Id;
        public int MapID;
    }

    public sealed class OccluderNodeRecord
    {
        public uint Id;
        public ushort OccluderID;
        public short Sequence;
        public int LocationID;
    }

    public sealed class OutlineEffectRecord
    {
        public uint Id;
        public uint PassiveHighlightColorID;
        public uint HighlightColorID;
        public int Priority;
        public int Flags;
        public float Range;
        public uint[] UnitConditionID = new uint[2];
    }

    public sealed class OverrideSpellDataRecord
    {
        public uint Id;
        public uint[] Spells = new uint[10];
        public int PlayerActionbarFileDataID;
        public byte Flags;
    }

    public sealed class PageTextMaterialRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class PaperDollItemFrameRecord
    {
        public uint Id;
        public LocalizedString ItemButtonName;
        public int SlotIconFileID;
        public byte SlotNumber;
    }

    public sealed class ParagonReputationRecord
    {
        public uint Id;
        public uint FactionID;
        public int LevelThreshold;
        public int QuestID;
    }

    public sealed class ParticleColorRecord
    {
        public uint Id;
        public int[] Start = new int[3];
        public int[] MID = new int[3];
        public int[] End = new int[3];
    }

    public sealed class ParticulateRecord
    {
        public uint Id;
        public int MapID;
        public int PlayerConditionID;
    }

    public sealed class ParticulateSoundRecord
    {
        public uint Id;
        public int ParticulateID;
        public int DaySound;
        public int NightSound;
        public int EnterSound;
        public int ExitSound;
    }

    public sealed class PathRecord
    {
        public uint Id;
        public byte Type;
        public byte SplineType;
        public byte Red;
        public byte Green;
        public byte Blue;
        public byte Alpha;
        public byte Flags;
    }

    public sealed class PathNodeRecord
    {
        public uint Id;
        public ushort PathID;
        public short Sequence;
        public int LocationID;
    }

    public sealed class PathNodePropertyRecord
    {
        public uint Id;
        public ushort PathID;
        public ushort Sequence;
        public byte PropertyIndex;
        public int Value;
    }

    public sealed class PathPropertyRecord
    {
        public uint Id;
        public ushort PathID;
        public byte PropertyIndex;
        public int Value;
    }

    public sealed class PetLoyaltyRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class PetPersonalityRecord
    {
        public uint Id;
        public float[] HappinessDamage = new float[3];
        public int[] HappinessThreshold = new int[3];
        public float[] DamageModifier = new float[3];
        public float[] Field_1_13_0_28211_003 = new float[8];
    }

    public sealed class PhaseRecord
    {
        public uint Id;
        public PhaseEntryFlags Flags;
    }

    public sealed class PhaseShiftZoneSoundsRecord
    {
        public uint Id;
        public ushort AreaID;
        public byte WMOAreaID;
        public ushort PhaseID;
        public ushort PhaseGroupID;
        public byte PhaseUseFlags;
        public uint ZoneIntroMusicID;
        public uint ZoneMusicID;
        public ushort SoundAmbienceID;
        public byte SoundProviderPreferencesID;
        public uint UWZoneIntroMusicID;
        public uint UWZoneMusicID;
        public ushort UWSoundAmbienceID;
        public byte UWSoundProviderPreferencesID;
    }

    public sealed class PhaseXPhaseGroupRecord
    {
        public uint Id;
        public ushort PhaseID;
        public uint PhaseGroupID;
    }

    public sealed class PlayerConditionRecord
    {
        public long RaceMask;
        public LocalizedString FailureDescription;
        public uint Id;
        public ushort MinLevel;
        public ushort MaxLevel;
        public int ClassMask;
        public uint SkillLogic;
        public byte LanguageID;
        public byte MinLanguage;
        public int MaxLanguage;
        public ushort MaxFactionID;
        public byte MaxReputation;
        public uint ReputationLogic;
        public sbyte CurrentPvpFaction;
        public byte PvpMedal;
        public uint PrevQuestLogic;
        public uint CurrQuestLogic;
        public uint CurrentCompletedQuestLogic;
        public uint SpellLogic;
        public uint ItemLogic;
        public byte ItemFlags;
        public uint AuraSpellLogic;
        public ushort WorldStateExpressionID;
        public byte WeatherID;
        public byte PartyStatus;
        public byte LifetimeMaxPVPRank;
        public uint AchievementLogic;
        public sbyte Gender;
        public sbyte NativeGender;
        public uint AreaLogic;
        public uint LfgLogic;
        public uint CurrencyLogic;
        public uint QuestKillID;
        public uint QuestKillLogic;
        public sbyte MinExpansionLevel;
        public sbyte MaxExpansionLevel;
        public int MinAvgItemLevel;
        public int MaxAvgItemLevel;
        public ushort MinAvgEquippedItemLevel;
        public ushort MaxAvgEquippedItemLevel;
        public byte PhaseUseFlags;
        public ushort PhaseID;
        public uint PhaseGroupID;
        public byte Flags;
        public sbyte ChrSpecializationIndex;
        public sbyte ChrSpecializationRole;
        public uint ModifierTreeID;
        public sbyte PowerType;
        public byte PowerTypeComp;
        public byte PowerTypeValue;
        public int WeaponSubclassMask;
        public byte MaxGuildLevel;
        public byte MinGuildLevel;
        public sbyte MaxExpansionTier;
        public sbyte MinExpansionTier;
        public byte MinPVPRank;
        public byte MaxPVPRank;
        public ushort[] SkillID = new ushort[4];
        public ushort[] MinSkill = new ushort[4];
        public ushort[] MaxSkill = new ushort[4];
        public uint[] MinFactionID = new uint[3];
        public byte[] MinReputation = new byte[3];
        public uint[] PrevQuestID = new uint[4];
        public uint[] CurrQuestID = new uint[4];
        public uint[] CurrentCompletedQuestID = new uint[4];
        public uint[] SpellID = new uint[4];
        public uint[] ItemID = new uint[4];
        public uint[] ItemCount = new uint[4];
        public ushort[] Explored = new ushort[2];
        public uint[] Time = new uint[2];
        public uint[] AuraSpellID = new uint[4];
        public byte[] AuraStacks = new byte[4];
        public ushort[] Achievement = new ushort[4];
        public ushort[] AreaID = new ushort[4];
        public byte[] LfgStatus = new byte[4];
        public byte[] LfgCompare = new byte[4];
        public uint[] LfgValue = new uint[4];
        public uint[] CurrencyID = new uint[4];
        public uint[] CurrencyCount = new uint[4];
        public uint[] QuestKillMonster = new uint[6];
        public int[] MovementFlags = new int[2];
    }

    public sealed class PositionerRecord
    {
        public uint Id;
        public ushort FirstStateID;
        public byte Flags;
        public float StartLife;
        public byte StartLifePercent;
    }

    public sealed class PositionerStateRecord
    {
        public uint Id;
        public uint NextStateID;
        public uint TransformMatrixID;
        public uint PosEntryID;
        public uint RotEntryID;
        public uint ScaleEntryID;
        public uint Flags;
        public float EndLife;
        public byte EndLifePercent;
    }

    public sealed class PositionerStateEntryRecord
    {
        public uint Id;
        public float ParamA;
        public float ParamB;
        public uint CurveID;
        public short SrcValType;
        public short SrcVal;
        public short DstValType;
        public short DstVal;
        public sbyte EntryType;
        public sbyte Style;
        public sbyte SrcType;
        public sbyte DstType;
    }

    public sealed class PowerDisplayRecord
    {
        public uint Id;
        public LocalizedString GlobalStringBaseTag;
        public byte ActualType;
        public byte Red;
        public byte Green;
        public byte Blue;
    }

    public sealed class PowerTypeRecord
    {
        public uint Id;
        public string NameGlobalStringTag;
        public string CostGlobalStringTag;
        public PowerType PowerTypeEnum;
        public sbyte MinPower;
        public int MaxBasePower;
        public sbyte CenterPower;
        public sbyte DefaultPower;
        public ushort DisplayModifier;
        public short RegenInterruptTimeMS;
        public float RegenPeace;
        public float RegenCombat;
        public short Flags;
    }

    public sealed class PrestigeLevelInfoRecord
    {
        public uint Id;
        public LocalizedString Name;
        public int HonorLevel;
        public int BadgeTextureFileDataID;
        public byte Flags;
        public int AwardedAchievementID;
    }

    public sealed class PvpBracketTypesRecord
    {
        public uint Id;
        public sbyte BracketID;
        public uint[] WeeklyQuestID = new uint[4];
    }

    public sealed class PvpDifficultyRecord
    {
        public uint Id;
        public byte RangeIndex;
        public byte MinLevel;
        public byte MaxLevel;
        public uint MapID;

        public BattlegroundBracketId GetBracketId() => (BattlegroundBracketId)RangeIndex;
    }

    public sealed class PvpItemRecord
    {
        public uint Id;
        public uint ItemID;
        public byte ItemLevelDelta;
    }

    public sealed class PvpScalingEffectRecord
    {
        public uint Id;
        public int SpecializationID;
        public int PvpScalingEffectTypeID;
        public float Value;
    }

    public sealed class PvpScalingEffectTypeRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class PvpTalentRecord
    {
        public LocalizedString Description;
        public uint Id;
        public int SpecID;
        public uint SpellID;
        public uint OverridesSpellID;
        public int Flags;
        public int ActionBarSpellID;
        public int PvpTalentCategoryID;
        public int LevelRequired;
    }

    public sealed class PvpTalentCategoryRecord
    {
        public uint Id;
        public byte TalentSlotMask;
    }

    public sealed class PvpTalentSlotUnlockRecord
    {
        public uint Id;
        public sbyte Slot;
        public uint LevelRequired;
        public int DeathKnightLevelRequired;
        public int DemonHunterLevelRequired;
    }

    public sealed class PvpTierRecord
    {
        public uint Id;
        public LocalizedString Name;
        public short MinRating;
        public short MaxRating;
        public int PrevTier;
        public int NextTier;
        public sbyte BracketID;
        public sbyte Rank;
        public int RankIcon;
    }

    public sealed class QuestFactionRewardRecord
    {
        public uint Id;
        public short[] Difficulty = new short[10];
    }

    public sealed class QuestFeedbackEffectRecord
    {
        public uint Id;
        public uint FileDataID;
        public ushort MinimapAtlasMemberID;
        public byte AttachPoint;
        public byte PassiveHighlightColorType;
        public byte Priority;
        public byte Flags;
    }

    public sealed class QuestInfoRecord
    {
        public uint Id;
        public LocalizedString InfoName;
        public sbyte Type;
        public byte Modifiers;
        public ushort Profession;
    }

    public sealed class QuestLineRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Description;
        public uint QuestID;
    }

    public sealed class QuestLineXQuestRecord
    {
        public uint Id;
        public uint QuestLineID;
        public uint QuestID;
        public uint OrderIndex;
    }

    public sealed class QuestMoneyRewardRecord
    {
        public uint Id;
        public uint[] Difficulty = new uint[10];
    }

    public sealed class QuestObjectiveRecord
    {
        public uint Id;
        public LocalizedString Description;
        public byte Type;
        public int Amount;
        public int ObjectID;
        public byte OrderIndex;
        public byte Flags;
        public byte StorageIndex;
        public int QuestID;
    }

    public sealed class QuestPackageItemRecord
    {
        public uint Id;
        public ushort PackageID;
        public uint ItemID;
        public uint ItemQuantity;
        public QuestPackageFilter DisplayType;
    }

    public sealed class QuestSortRecord
    {
        public uint Id;
        public LocalizedString SortName;
        public sbyte UiOrderIndex;
    }

    public sealed class QuestV2Record
    {
        public uint Id;
        public ushort UniqueBitFlag;
    }

    public sealed class QuestV2CliTaskRecord
    {
        public long FiltRaces;
        public LocalizedString QuestTitle;
        public LocalizedString BulletText;
        public uint Id;
        public ushort UniqueBitFlag;
        public uint ConditionID;
        public uint FiltActiveQuest;
        public short FiltClasses;
        public uint FiltCompletedQuestLogic;
        public uint FiltMaxFactionID;
        public uint FiltMaxFactionValue;
        public uint FiltMinFactionID;
        public uint FiltMinFactionValue;
        public uint FiltMinSkillID;
        public uint FiltMinSkillValue;
        public uint FiltNonActiveQuest;
        public uint BreadCrumbID;
        public int StartItem;
        public int WorldStateExpressionID;
        public uint Field_2_5_1_38043_019;
        public int Field_2_5_1_38043_020;
        public uint Field_2_5_1_38043_021;
        public uint Field_2_5_1_38043_022;
        public uint[] FiltCompletedQuest = new uint[3];
    }

    public sealed class QuestXGroupActivityRecord
    {
        public uint Id;
        public uint QuestID;
        public uint GroupFinderActivityID;
    }

    public sealed class QuestXPRecord
    {
        public uint Id;
        public ushort[] Difficulty = new ushort[10];
    }

    public sealed class RandPropPointsRecord
    {
        public uint Id;
        public int DamageReplaceStat;
        public uint[] Epic = new uint[5];
        public uint[] Superior = new uint[5];
        public uint[] Good = new uint[5];
    }

    public sealed class RelicSlotTierRequirementRecord
    {
        public uint Id;
        public byte RelicIndex;
        public byte RelicTier;
        public int PlayerConditionID;
    }

    public sealed class RelicTalentRecord
    {
        public uint Id;
        public int Type;
        public ushort ArtifactPowerID;
        public byte ArtifactPowerLabel;
        public int PVal;
        public int Flags;
    }

    public sealed class ResearchBranchRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte ResearchFieldID;
        public ushort CurrencyID;
        public int TextureFileID;
        public int BigTextureFileID;
        public int ItemID;
    }

    public sealed class ResearchFieldRecord
    {
        public LocalizedString Name;
        public uint Id;
        public byte Slot;
    }

    public sealed class ResearchProjectRecord
    {
        public LocalizedString Name;
        public LocalizedString Description;
        public uint Id;
        public byte Rarity;
        public int SpellID;
        public ushort ResearchBranchID;
        public byte NumSockets;
        public int TextureFileID;
        public uint RequiredWeight;
    }

    public sealed class ResearchSiteRecord
    {
        public uint Id;
        public LocalizedString Name;
        public short MapID;
        public int QuestPOIBlobID;
        public uint AreaPOIIconEnum;
    }

    public sealed class ResistancesRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte Flags;
        public uint FizzleSoundID;
    }

    public sealed class RewardPackRecord
    {
        public uint Id;
        public int CharTitleID;
        public uint Money;
        public sbyte ArtifactXPDifficulty;
        public float ArtifactXPMultiplier;
        public byte ArtifactXPCategoryID;
        public uint TreasurePickerID;
    }

    public sealed class RewardPackXCurrencyTypeRecord
    {
        public uint Id;
        public uint CurrencyTypeID;
        public int Quantity;
        public uint RewardPackID;
    }

    public sealed class RewardPackXItemRecord
    {
        public uint Id;
        public uint ItemID;
        public uint ItemQuantity;
        public uint RewardPackID;
    }

    public sealed class RibbonQualityRecord
    {
        public uint Id;
        public byte NumStrips;
        public float MaxSampleTimeDelta;
        public float AngleThreshold;
        public float MinDistancePerSlice;
        public uint Flags;
    }

    public sealed class RulesetItemUpgradeRecord
    {
        public uint Id;
        public int ItemID;
        public ushort ItemUpgradeID;
    }

    public sealed class ScalingStatDistributionRecord
    {
        public uint Id;
        public ushort PlayerLevelToItemLevelCurveID;
        public int MinLevel;
        public int MaxLevel;
    }

    public sealed class ScenarioRecord
    {
        public uint Id;
        public LocalizedString Name;
        public ushort AreaTableID;
        public byte Type;
        public byte Flags;
        public uint UiTextureKitID;
    }

    public sealed class ScenarioEventEntryRecord
    {
        public uint Id;
        public byte TriggerType;
        public uint TriggerAsset;
    }

    public sealed class ScenarioStepRecord
    {
        public uint Id;
        public LocalizedString Description;
        public LocalizedString Title;
        public ushort ScenarioID;
        public uint CriteriatreeID;
        public uint RewardQuestID;
        public int RelatedStep;
        public ushort Supersedes;
        public byte OrderIndex;
        public byte Flags;
        public uint VisibilityPlayerConditionID;
        public ushort WidgetSetID;
    }

    public sealed class SceneScriptRecord
    {
        public uint Id;
        public ushort FirstSceneScriptID;
        public ushort NextSceneScriptID;
    }

    public sealed class SceneScriptGlobalTextRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Script;
    }

    public sealed class SceneScriptPackageRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class SceneScriptPackageMemberRecord
    {
        public uint Id;
        public ushort SceneScriptPackageID;
        public ushort SceneScriptID;
        public ushort ChildSceneScriptPackageID;
        public byte OrderIndex;
    }

    public sealed class SceneScriptTextRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Script;
    }

    public sealed class ScheduledIntervalRecord
    {
        public uint Id;
        public int Flags;
        public int RepeatType;
        public int DurationSecs;
        public int OffsetSecs;
        public int DateAlignmentType;
    }

    public sealed class ScreenEffectRecord
    {
        public uint Id;
        public LocalizedString Name;
        public int[] Param = new int[4];
        public sbyte Effect;
        public uint FullScreenEffectID;
        public ushort LightParamsID;
        public ushort LightParamsFadeIn;
        public ushort LightParamsFadeOut;
        public uint SoundAmbienceID;
        public uint ZoneMusicID;
        public short TimeOfDayOverride;
        public sbyte EffectMask;
        public byte LightFlags;
    }

    public sealed class ScreenEffectTypeRecord
    {
        public uint Id;
        public int Priority;
    }

    public sealed class ScreenLocationRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class SDReplacementModelRecord
    {
        public uint Id;
        public int SdFileDataID;
    }

    public sealed class SeamlessSiteRecord
    {
        public uint Id;
        public int MapID;
    }

    public sealed class ServerMessagesRecord
    {
        public uint Id;
        public LocalizedString Text;
    }

    public sealed class ShadowyEffectRecord
    {
        public uint Id;
        public int PrimaryColor;
        public int SecondaryColor;
        public float Duration;
        public float Value;
        public float FadeInTime;
        public float FadeOutTime;
        public sbyte AttachPos;
        public sbyte Flags;
        public float InnerStrength;
        public float OuterStrength;
        public float InitialDelay;
        public int CurveID;
        public uint Priority;
    }

    public sealed class SiegeablePropertiesRecord
    {
        public uint Id;
        public uint Health;
        public int DamageSpellVisualKitID;
        public int HealingSpellVisualKitID;
        public uint Flags;
    }

    public sealed class SkillLineRecord
    {
        public LocalizedString DisplayName;
        public LocalizedString AlternateVerb;
        public LocalizedString Description;
        public LocalizedString HordeDisplayName;
        public LocalizedString NeutralDisplayName;
        public uint Id;
        public SkillCategory CategoryID;
        public int SpellIconFileID;
        public sbyte CanLink;
        public uint ParentSkillLineID;
        public int ParentTierIndex;
        public ushort Flags;
        public int SpellBookSpellID;
    }

    public sealed class SkillLineAbilityRecord
    {
        public long RaceMask;
        public uint Id;
        public ushort SkillLine;
        public uint Spell;
        public short MinSkillLineRank;
        public int ClassMask;
        public uint SupercedesSpell;
        public AbilityLearnType AcquireMethod;
        public ushort TrivialSkillLineRankHigh;
        public ushort TrivialSkillLineRankLow;
        public SkillLineAbilityFlags Flags;
        public byte NumSkillUps;
        public short UniqueBit;
        public short TradeSkillCategoryID;
        public ushort SkillupSkillLineID;
        public int[] CharacterPoints = new int[2];
    }

    public sealed class SkillLineCategoryRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte SortIndex;
    }

    public sealed class SkillRaceClassInfoRecord
    {
        public uint Id;
        public long RaceMask;
        public ushort SkillID;
        public int ClassMask;
        public SkillRaceClassInfoFlags Flags;
        public sbyte Availability;
        public sbyte MinLevel;
        public ushort SkillTierID;
    }

    public sealed class SkySceneXPlayerConditionRecord
    {
        public uint Id;
        public int PlayerConditionID;
        public int SkySceneID;
    }

    public sealed class SoundAmbienceRecord
    {
        public uint Id;
        public byte Flags;
        public uint SoundFilterID;
        public uint FlavorSoundFilterID;
        public uint[] AmbienceID = new uint[2];
        public uint[] AmbienceStartID = new uint[2];
        public uint[] AmbienceStopID = new uint[2];
    }

    public sealed class SoundAmbienceFlavorRecord
    {
        public uint Id;
        public uint SoundEntriesIDDay;
        public uint SoundEntriesIDNight;
        public int SoundAmbienceID;
    }

    public sealed class SoundBusRecord
    {
        public uint Id;
        public byte Flags;
        public byte DefaultPriority;
        public byte DefaultPriorityPenalty;
        public float DefaultVolume;
        public byte DefaultPlaybackLimit;
        public sbyte BusEnumID;
        public int Parent;
    }

    public sealed class SoundBusOverrideRecord
    {
        public uint Id;
        public int SoundBusID;
        public uint PlayerConditionID;
        public byte PlaybackLimit;
        public float Volume;
        public byte Priority;
        public byte PriorityPenalty;
    }

    public sealed class SoundEmitterPillPointsRecord
    {
        public uint Id;
        public float[] Position = new float[3];
        public ushort SoundEmittersID;
    }

    public sealed class SoundEmittersRecord
    {
        public LocalizedString Name;
        public float[] Position = new float[3];
        public float[] Direction = new float[3];
        public uint Id;
        public uint SoundEntriesID;
        public ushort WorldStateExpressionID;
        public byte EmitterType;
        public ushort PhaseID;
        public uint PhaseGroupID;
        public byte PhaseUseFlags;
        public byte Flags;
        public int MapID;
    }

    public sealed class SoundEnvelopeRecord
    {
        public uint Id;
        public int SoundKitID;
        public byte EnvelopeType;
        public uint Flags;
        public int CurveID;
        public ushort DecayIndex;
        public ushort SustainIndex;
        public ushort ReleaseIndex;
    }

    public sealed class SoundFilterRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class SoundFilterElemRecord
    {
        public uint Id;
        public float[] Params = new float[9];
        public sbyte FilterType;
        public int SoundFilterID;
    }

    public sealed class SoundKitRecord
    {
        public uint Id;
        public byte SoundType;
        public float VolumeFloat;
        public ushort Flags;
        public float MinDistance;
        public float DistanceCutoff;
        public byte EAXDef;
        public uint SoundKitAdvancedID;
        public float VolumeVariationPlus;
        public float VolumeVariationMinus;
        public float PitchVariationPlus;
        public float PitchVariationMinus;
        public sbyte DialogType;
        public float PitchAdjust;
        public ushort BusOverwriteID;
        public byte MaxInstances;
    }

    public sealed class SoundKitAdvancedRecord
    {
        public uint Id;
        public uint SoundKitID;
        public float InnerRadius2D;
        public float OuterRadius2D;
        public uint TimeA;
        public uint TimeB;
        public uint TimeC;
        public uint TimeD;
        public int RandomOffsetRange;
        public sbyte Usage;
        public uint TimeIntervalMin;
        public uint TimeIntervalMax;
        public uint DelayMin;
        public uint DelayMax;
        public byte VolumeSliderCategory;
        public float DuckToSFX;
        public float DuckToMusic;
        public float DuckToAmbience;
        public float DuckToDialog;
        public float DuckToSuppressors;
        public float DuckToCinematicSFX;
        public float DuckToCinematicMusic;
        public float InnerRadiusOfInfluence;
        public float OuterRadiusOfInfluence;
        public uint TimeToDuck;
        public uint TimeToUnduck;
        public float InsideAngle;
        public float OutsideAngle;
        public float OutsideVolume;
        public byte MinRandomPosOffset;
        public ushort MaxRandomPosOffset;
        public int MsOffset;
        public uint TimeCooldownMin;
        public uint TimeCooldownMax;
        public byte MaxInstancesBehavior;
        public byte VolumeControlType;
        public int VolumeFadeInTimeMin;
        public int VolumeFadeInTimeMax;
        public uint VolumeFadeInCurveID;
        public int VolumeFadeOutTimeMin;
        public int VolumeFadeOutTimeMax;
        public uint VolumeFadeOutCurveID;
        public float ChanceToPlay;
    }

    public sealed class SoundKitChildRecord
    {
        public uint Id;
        public uint SoundKitID;
        public uint ParentSoundKitID;
    }

    public sealed class SoundKitEntryRecord
    {
        public uint Id;
        public uint SoundKitID;
        public int FileDataID;
        public byte Frequency;
        public float Volume;
    }

    public sealed class SoundKitFallbackRecord
    {
        public uint Id;
        public uint SoundKitID;
        public uint FallbackSoundKitID;
    }

    public sealed class SoundOverrideRecord
    {
        public uint Id;
        public ushort ZoneIntroMusicID;
        public ushort ZoneMusicID;
        public ushort SoundAmbienceID;
        public byte SoundProviderPreferencesID;
        public byte Flags;
    }

    public sealed class SoundProviderPreferencesRecord
    {
        public uint Id;
        public LocalizedString Description;
        public sbyte EAXEnvironmentSelection;
        public float EAXDecayTime;
        public float EAX2EnvironmentSize;
        public float EAX2EnvironmentDiffusion;
        public short EAX2Room;
        public short EAX2RoomHF;
        public float EAX2DecayHFRatio;
        public short EAX2Reflections;
        public float EAX2ReflectionsDelay;
        public short EAX2Reverb;
        public float EAX2ReverbDelay;
        public float EAX2RoomRolloff;
        public float EAX2AirAbsorption;
        public sbyte EAX3RoomLF;
        public float EAX3DecayLFRatio;
        public float EAX3EchoTime;
        public float EAX3EchoDepth;
        public float EAX3ModulationTime;
        public float EAX3ModulationDepth;
        public float EAX3HFReference;
        public float EAX3LFReference;
        public ushort Flags;
    }

    public sealed class SourceInfoRecord
    {
        public uint Id;
        public LocalizedString SourceText;
        public sbyte PvpFaction;
        public sbyte SourceTypeEnum;
        public int SpellID;
    }

    public sealed class SpamMessagesRecord
    {
        public uint Id;
        public LocalizedString Text;
    }

    public sealed class SpecializationSpellsRecord
    {
        public LocalizedString Description;
        public uint Id;
        public ushort SpecID;
        public uint SpellID;
        public uint OverridesSpellID;
        public byte DisplayOrder;
    }

    public sealed class SpecializationSpellsDisplayRecord
    {
        public uint Id;
        public ushort SpecializationID;
        public uint[] SpellID = new uint[6];
    }

    public sealed class SpecSetMemberRecord
    {
        public uint Id;
        public uint ChrSpecializationID;
        public int SpecSet;
    }

    public sealed class SpellRecord
    {
        public uint Id;
        public LocalizedString NameSubtext;
        public LocalizedString Description;
        public LocalizedString AuraDescription;
    }

    public sealed class SpellActionBarPrefRecord
    {
        public uint Id;
        public int SpellID;
        public ushort PreferredActionBarMask;
    }

    public sealed class SpellActivationOverlayRecord
    {
        public uint Id;
        public int[] IconHighlightSpellClassMask = new int[4];
        public int SpellID;
        public int OverlayFileDataID;
        public sbyte ScreenLocationID;
        public uint SoundEntriesID;
        public int Color;
        public float Scale;
        public sbyte TriggerType;
    }

    public sealed class SpellAuraOptionsRecord
    {
        public uint Id;
        public byte DifficultyID;
        public uint CumulativeAura;
        public uint ProcCategoryRecovery;
        public byte ProcChance;
        public int ProcCharges;
        public ushort SpellProcsPerMinuteID;
        public int[] ProcTypeMask = new int[2];
        public uint SpellID;
    }

    public sealed class SpellAuraRestrictionsRecord
    {
        public uint Id;
        public byte DifficultyID;
        public byte CasterAuraState;
        public byte TargetAuraState;
        public byte ExcludeCasterAuraState;
        public byte ExcludeTargetAuraState;
        public uint CasterAuraSpell;
        public uint TargetAuraSpell;
        public uint ExcludeCasterAuraSpell;
        public uint ExcludeTargetAuraSpell;
        public uint SpellID;
    }

    public sealed class SpellAuraVisibilityRecord
    {
        public uint Id;
        public sbyte Type;
        public sbyte Flags;
        public int SpellID;
    }

    public sealed class SpellAuraVisXChrSpecRecord
    {
        public uint Id;
        public short ChrSpecializationID;
        public int SpellAuraVisibilityID;
    }

    public sealed class SpellCastingRequirementsRecord
    {
        public uint Id;
        public uint SpellID;
        public byte FacingCasterFlags;
        public ushort MinFactionID;
        public sbyte MinReputation;
        public ushort RequiredAreasID;
        public byte RequiredAuraVision;
        public ushort RequiresSpellFocus;
    }

    public sealed class SpellCastTimesRecord
    {
        public uint Id;
        public int Base;
        public short PerLevel;
        public int Minimum;
    }

    public sealed class SpellCategoriesRecord
    {
        public uint Id;
        public byte DifficultyID;
        public ushort Category;
        public sbyte DefenseType;
        public sbyte DispelType;
        public sbyte Mechanic;
        public sbyte PreventionType;
        public ushort StartRecoveryCategory;
        public ushort ChargeCategory;
        public uint SpellID;
    }

    public sealed class SpellCategoryRecord
    {
        public uint Id;
        public LocalizedString Name;
        public SpellCategoryFlags Flags;
        public byte UsesPerWeek;
        public byte MaxCharges;
        public int ChargeRecoveryTime;
        public int TypeMask;
    }

    public sealed class SpellChainEffectsRecord
    {
        public uint Id;
        public float AvgSegLen;
        public float NoiseScale;
        public float TexCoordScale;
        public uint SegDuration;
        public ushort SegDelay;
        public uint Flags;
        public ushort JointCount;
        public float JointOffsetRadius;
        public byte JointsPerMinorJoint;
        public byte MinorJointsPerMajorJoint;
        public float MinorJointScale;
        public float MajorJointScale;
        public float JointMoveSpeed;
        public float JointSmoothness;
        public float MinDurationBetweenJointJumps;
        public float MaxDurationBetweenJointJumps;
        public float WaveHeight;
        public float WaveFreq;
        public float WaveSpeed;
        public float MinWaveAngle;
        public float MaxWaveAngle;
        public float MinWaveSpin;
        public float MaxWaveSpin;
        public float ArcHeight;
        public float MinArcAngle;
        public float MaxArcAngle;
        public float MinArcSpin;
        public float MaxArcSpin;
        public float DelayBetweenEffects;
        public float MinFlickerOnDuration;
        public float MaxFlickerOnDuration;
        public float MinFlickerOffDuration;
        public float MaxFlickerOffDuration;
        public float PulseSpeed;
        public float PulseOnLength;
        public float PulseFadeLength;
        public byte Alpha;
        public byte Red;
        public byte Green;
        public byte Blue;
        public byte BlendMode;
        public byte RenderLayer;
        public float WavePhase;
        public float TimePerFlipFrame;
        public float VariancePerFlipFrame;
        public uint TextureParticleFileDataID;
        public float StartWidth;
        public float EndWidth;
        public ushort WidthScaleCurveID;
        public byte NumFlipFramesU;
        public byte NumFlipFramesV;
        public uint SoundKitID;
        public float ParticleScaleMultiplier;
        public float ParticleEmissionRateMultiplier;
        public ushort[] SpellChainEffectID = new ushort[11];
        public float[] TextureCoordScaleU = new float[3];
        public float[] TextureCoordScaleV = new float[3];
        public float[] TextureRepeatLengthU = new float[3];
        public float[] TextureRepeatLengthV = new float[3];
        public int[] TextureFileDataID = new int[3];
    }

    public sealed class SpellClassOptionsRecord
    {
        public uint Id;
        public uint SpellID;
        public uint ModalNextSpell;
        public byte SpellClassSet;
        public FlagArray128 SpellClassMask;
    }

    public sealed class SpellCooldownsRecord
    {
        public uint Id;
        public byte DifficultyID;
        public uint CategoryRecoveryTime;
        public uint RecoveryTime;
        public uint StartRecoveryTime;
        public uint SpellID;
    }

    public sealed class SpellCraftUIRecord
    {
        public uint Id;
        public sbyte CastUI;
    }

    public sealed class SpellDescriptionVariablesRecord
    {
        public uint Id;
        public LocalizedString Variables;
    }

    public sealed class SpellDispelTypeRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString InternalName;
        public byte ImmunityPossible;
        public byte Mask;
    }

    public sealed class SpellDurationRecord
    {
        public uint Id;
        public int Duration;
        public uint DurationPerLevel;
        public int MaxDuration;
    }

    public sealed class SpellEffectRecord
    {
        public uint Id;
        public uint DifficultyID;
        public uint EffectIndex;
        public uint Effect;
        public float EffectAmplitude;
        public SpellEffectAttributes EffectAttributes;
        public short EffectAura;
        public uint EffectAuraPeriod;
        public int EffectBasePoints;
        public float EffectBonusCoefficient;
        public float EffectChainAmplitude;
        public int EffectChainTargets;
        public int EffectDieSides;
        public uint EffectItemType;
        public int EffectMechanic;
        public float EffectPointsPerResource;
        public float EffectPosFacing;
        public float EffectRealPointsPerLevel;
        public uint EffectTriggerSpell;
        public float BonusCoefficientFromAP;
        public float PvpMultiplier;
        public float Coefficient;
        public float Variance;
        public float ResourceCoefficient;
        public float GroupSizeBasePointsCoefficient;
        public int[] EffectMiscValue = new int[2];
        public uint[] EffectRadiusIndex = new uint[2];
        public FlagArray128 EffectSpellClassMask;
        public short[] ImplicitTarget = new short[2];
        public uint SpellID;
    }

    public sealed class SpellEffectAutoDescriptionRecord
    {
        public uint Id;
        public LocalizedString EffectDescription;
        public LocalizedString AuraDescription;
        public int SpellEffectType;
        public int AuraEffectType;
        public sbyte PointsSign;
        public sbyte TargetType;
        public sbyte SchoolMask;
        public int EffectOrderIndex;
        public int AuraOrderIndex;
    }

    public sealed class SpellEffectEmissionRecord
    {
        public uint Id;
        public float EmissionRate;
        public float ModelScale;
        public short AreaModelID;
        public sbyte Flags;
    }

    public sealed class SpellEquippedItemsRecord
    {
        public uint Id;
        public uint SpellID;
        public sbyte EquippedItemClass;
        public int EquippedItemInvTypes;
        public int EquippedItemSubclass;
    }

    public sealed class SpellFlyoutRecord
    {
        public uint Id;
        public long RaceMask;
        public LocalizedString Name;
        public LocalizedString Description;
        public byte Flags;
        public int ClassMask;
        public int SpellIconFileID;
    }

    public sealed class SpellFlyoutItemRecord
    {
        public uint Id;
        public int SpellID;
        public byte Slot;
        public int SpellFlyoutID;
    }

    public sealed class SpellFocusObjectRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class SpellInterruptsRecord
    {
        public uint Id;
        public byte DifficultyID;
        public short InterruptFlags;
        public int[] AuraInterruptFlags = new int[2];
        public int[] ChannelInterruptFlags = new int[2];
        public uint SpellID;
    }

    public sealed class SpellItemEnchantmentRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString HordeName;
        public uint[] EffectArg = new uint[3];
        public float[] EffectScalingPoints = new float[3];
        public uint TransmogUnlockConditionID;
        public uint TransmogCost;
        public uint IconFileDataID;
        public ushort[] EffectPointsMin = new ushort[3];
        public ushort ItemVisual;
        public EnchantmentSlotMask Flags;
        public ushort RequiredSkillID;
        public ushort RequiredSkillRank;
        public ushort ItemLevel;
        public byte Charges;
        public ItemEnchantmentType[] Effect = new ItemEnchantmentType[3];
        public sbyte ScalingClass;
        public sbyte ScalingClassRestricted;
        public byte ConditionID;
        public byte MinLevel;
        public byte MaxLevel;
    }

    public sealed class SpellItemEnchantmentConditionRecord
    {
        public uint Id;
        public byte[] LtOperandType = new byte[5];
        public uint[] LtOperand = new uint[5];
        public byte[] Operator = new byte[5];
        public byte[] RtOperandType = new byte[5];
        public byte[] RtOperand = new byte[5];
        public byte[] Logic = new byte[5];
    }

    public sealed class SpellKeyboundOverrideRecord
    {
        public uint Id;
        public LocalizedString Function;
        public sbyte Type;
        public int Data;
        public int Field_9_1_0_38709_003;
    }

    public sealed class SpellLabelRecord
    {
        public uint Id;
        public uint LabelID;
        public uint SpellID;
    }

    public sealed class SpellLearnSpellRecord
    {
        public uint Id;
        public uint SpellID;
        public uint LearnSpellID;
        public uint OverridesSpellID;
    }

    public sealed class SpellLevelsRecord
    {
        public uint Id;
        public byte DifficultyID;
        public ushort BaseLevel;
        public ushort MaxLevel;
        public ushort SpellLevel;
        public byte MaxPassiveAuraLevel;
        public uint SpellID;
    }

    public sealed class SpellMechanicRecord
    {
        public uint Id;
        public LocalizedString StateName;
    }

    public sealed class SpellMiscRecord
    {
        public uint Id;
        public byte DifficultyID;
        public ushort CastingTimeIndex;
        public ushort DurationIndex;
        public ushort RangeIndex;
        public byte SchoolMask;
        public float Speed;
        public float LaunchDelay;
        public float MinDuration;
        public uint SpellIconFileDataID;
        public uint ActiveIconFileDataID;
        public int[] Attributes = new int[14];
        public uint SpellID;
    }

    public sealed class SpellMissileRecord
    {
        public uint Id;
        public int SpellID;
        public byte Flags;
        public float DefaultPitchMin;
        public float DefaultPitchMax;
        public float DefaultSpeedMin;
        public float DefaultSpeedMax;
        public float RandomizeFacingMin;
        public float RandomizeFacingMax;
        public float RandomizePitchMin;
        public float RandomizePitchMax;
        public float RandomizeSpeedMin;
        public float RandomizeSpeedMax;
        public float Gravity;
        public float MaxDuration;
        public float CollisionRadius;
    }

    public sealed class SpellMissileMotionRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString ScriptBody;
        public byte Flags;
        public byte MissileCount;
    }

    public sealed class SpellNameRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class SpellPowerRecord
    {
        public uint Id;
        public byte OrderIndex;
        public int ManaCost;
        public int ManaCostPerLevel;
        public int ManaPerSecond;
        public uint PowerDisplayID;
        public int AltPowerBarID;
        public float PowerCostPct;
        public float PowerCostMaxPct;
        public float PowerPctPerSecond;
        public PowerType PowerType;
        public uint RequiredAuraSpellID;
        public uint OptionalCost;
        public uint SpellID;
    }

    public sealed class SpellPowerDifficultyRecord
    {
        public uint Id;
        public byte DifficultyID;
        public byte OrderIndex;
    }

    public sealed class SpellProceduralEffectRecord
    {
        public uint Id;
        public sbyte Type;
        public float[] Value = new float[4];
    }

    public sealed class SpellProcsPerMinuteRecord
    {
        public uint Id;
        public float BaseProcRate;
        public byte Flags;
    }

    public sealed class SpellProcsPerMinuteModRecord
    {
        public uint Id;
        public SpellProcsPerMinuteModType Type;
        public ushort Param;
        public float Coeff;
        public uint SpellProcsPerMinuteID;
    }

    public sealed class SpellRadiusRecord
    {
        public uint Id;
        public float Radius;
        public float RadiusPerLevel;
        public float RadiusMin;
        public float RadiusMax;
    }

    public sealed class SpellRangeRecord
    {
        public uint Id;
        public LocalizedString DisplayName;
        public LocalizedString DisplayNameShort;
        public SpellRangeFlag Flags;
        public float[] RangeMin = new float[2];
        public float[] RangeMax = new float[2];
    }

    public sealed class SpellReagentsRecord
    {
        public uint Id;
        public uint SpellID;
        public int[] Reagent = new int[8];
        public ushort[] ReagentCount = new ushort[8];
    }

    public sealed class SpellReagentsCurrencyRecord
    {
        public uint Id;
        public uint SpellID;
        public ushort CurrencyTypesID;
        public ushort CurrencyCount;
    }

    public sealed class SpellScalingRecord
    {
        public uint Id;
        public uint SpellID;
        public int Class;
        public uint MinScalingLevel;
        public uint MaxScalingLevel;
        public ushort ScalesFromItemLevel;
    }

    public sealed class SpellShapeshiftRecord
    {
        public uint Id;
        public uint SpellID;
        public sbyte StanceBarOrder;
        public uint[] ShapeshiftExclude = new uint[2];
        public uint[] ShapeshiftMask = new uint[2];
    }

    public sealed class SpellShapeshiftFormRecord
    {
        public uint Id;
        public LocalizedString Name;
        public sbyte CreatureType;
        public SpellShapeshiftFormFlags Flags;
        public int AttackIconFileID;
        public sbyte BonusActionBar;
        public ushort CombatRoundTime;
        public float DamageVariance;
        public ushort MountTypeID;
        public uint[] CreatureDisplayID = new uint[4];
        public uint[] PresetSpellID = new uint[8];
    }

    public sealed class SpellSpecialUnitEffectRecord
    {
        public uint Id;
        public ushort SpellVisualEffectNameID;
        public uint PositionerID;
    }

    public sealed class SpellTargetRestrictionsRecord
    {
        public uint Id;
        public byte DifficultyID;
        public float ConeDegrees;
        public byte MaxTargets;
        public uint MaxTargetLevel;
        public ushort TargetCreatureType;
        public int Targets;
        public float Width;
        public uint SpellID;
    }

    public sealed class SpellTotemsRecord
    {
        public uint Id;
        public uint SpellID;
        public ushort[] RequiredTotemCategoryID = new ushort[2];
        public uint[] Totem = new uint[2];
    }

    public sealed class SpellVisualRecord
    {
        public uint Id;
        public float[] MissileCastOffset = new float[3];
        public float[] MissileImpactOffset = new float[3];
        public uint AnimEventSoundID;
        public int Flags;
        public sbyte MissileAttachment;
        public sbyte MissileDestinationAttachment;
        public uint MissileCastPositionerID;
        public uint MissileImpactPositionerID;
        public int MissileTargetingKit;
        public uint HostileSpellVisualID;
        public uint CasterSpellVisualID;
        public ushort SpellVisualMissileSetID;
        public ushort DamageNumberDelay;
        public uint LowViolenceSpellVisualID;
        public uint RaidSpellVisualMissileSetID;
        public ushort AreaModel;
        public sbyte HasMissile;
    }

    public sealed class SpellVisualAnimRecord
    {
        public uint Id;
        public int InitialAnimID;
        public int LoopAnimID;
        public ushort AnimKitID;
    }

    public sealed class SpellVisualColorEffectRecord
    {
        public uint Id;
        public float Duration;
        public int Color;
        public byte Flags;
        public byte Type;
        public ushort RedCurveID;
        public ushort GreenCurveID;
        public ushort BlueCurveID;
        public ushort AlphaCurveID;
        public ushort OpacityCurveID;
        public float ColorMultiplier;
        public uint PositionerID;
    }

    public sealed class SpellVisualEffectNameRecord
    {
        public uint Id;
        public int ModelFileDataID;
        public float BaseMissileSpeed;
        public float Scale;
        public float MinAllowedScale;
        public float MaxAllowedScale;
        public float Alpha;
        public uint Flags;
        public int TextureFileDataID;
        public float EffectRadius;
        public uint Type;
        public int GenericID;
        public uint RibbonQualityID;
        public int DissolveEffectID;
        public int ModelPosition;
    }

    public sealed class SpellVisualEventRecord
    {
        public uint Id;
        public int StartEvent;
        public int EndEvent;
        public int StartMinOffsetMs;
        public int StartMaxOffsetMs;
        public int EndMinOffsetMs;
        public int EndMaxOffsetMs;
        public int TargetType;
        public int SpellVisualKitID;
        public int SpellVisualID;
    }

    public sealed class SpellVisualKitRecord
    {
        public uint Id;
        public uint FallbackSpellVisualKitID;
        public ushort DelayMin;
        public ushort DelayMax;
        public float FallbackPriority;
        public int[] Flags = new int[2];
    }

    public sealed class SpellVisualKitAreaModelRecord
    {
        public uint Id;
        public int ModelFileDataID;
        public byte Flags;
        public ushort LifeTime;
        public float EmissionRate;
        public float Spacing;
        public float ModelScale;
    }

    public sealed class SpellVisualKitEffectRecord
    {
        public uint Id;
        public int EffectType;
        public int Effect;
        public int ParentSpellVisualKitID;
    }

    public sealed class SpellVisualKitModelAttachRecord
    {
        public float[] Offset = new float[3];
        public float[] OffsetVariation = new float[3];
        public uint Id;
        public ushort SpellVisualEffectNameID;
        public sbyte AttachmentID;
        public ushort PositionerID;
        public float Yaw;
        public float Pitch;
        public float Roll;
        public float YawVariation;
        public float PitchVariation;
        public float RollVariation;
        public float Scale;
        public float ScaleVariation;
        public short StartAnimID;
        public short AnimID;
        public short EndAnimID;
        public ushort AnimKitID;
        public byte Flags;
        public uint LowDefModelAttachID;
        public float StartDelay;
        public int ParentSpellVisualKitID;
    }

    public sealed class SpellVisualMissileRecord
    {
        public float[] CastOffset = new float[3];
        public float[] ImpactOffset = new float[3];
        public uint Id;
        public ushort SpellVisualEffectNameID;
        public uint SoundEntriesID;
        public sbyte Attachment;
        public sbyte DestinationAttachment;
        public ushort CastPositionerID;
        public ushort ImpactPositionerID;
        public int FollowGroundHeight;
        public uint FollowGroundDropSpeed;
        public ushort FollowGroundApproach;
        public uint Flags;
        public ushort SpellMissileMotionID;
        public uint AnimKitID;
        public short SpellVisualMissileSetID;
    }

    public sealed class SpellXDescriptionVariablesRecord
    {
        public uint Id;
        public int SpellID;
        public int SpellDescriptionVariablesID;
    }

    public sealed class SpellXSpellVisualRecord
    {
        public uint Id;
        public byte DifficultyID;
        public uint SpellVisualID;
        public float Probability;
        public byte Flags;
        public byte Priority;
        public int SpellIconFileID;
        public int ActiveIconFileID;
        public ushort ViewerUnitConditionID;
        public uint ViewerPlayerConditionID;
        public ushort CasterUnitConditionID;
        public uint CasterPlayerConditionID;
        public uint SpellID;
    }

    public sealed class SSAOSettingsRecord
    {
        public uint Id;
        public float Field_8_2_0_30080_001;
        public float Field_8_2_0_30080_002;
        public float Field_8_2_0_30080_003;
        public float Radius;
    }

    public sealed class StableSlotPricesRecord
    {
        public uint Id;
        public ushort Cost;
    }

    public sealed class StartupFilesRecord
    {
        public uint Id;
        public int FileDataID;
        public int Locale;
        public int BytesRequired;
    }

    public sealed class StartupStringsRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Message;
    }

    public sealed class StationeryRecord
    {
        public uint Id;
        public uint ItemID;
        public byte Flags;
        public int[] TextureFileDataID = new int[2];
    }

    public sealed class SummonPropertiesRecord
    {
        public uint Id;
        public SummonCategory Control;
        public uint Faction;
        public SummonTitle Title;
        public int Slot;
        public SummonPropFlags Flags;
    }

    public sealed class TactKeyRecord
    {
        public uint Id;
        public byte[] Key = new byte[16];
    }

    public sealed class TactKeyLookupRecord
    {
        public uint Id;
        public byte[] TACTID = new byte[8];
    }

    public sealed class TalentRecord
    {
        public uint Id;
        public LocalizedString Description;
        public byte TierID;
        public byte Flags;
        public byte ColumnIndex;
        public ushort TabID;
        public byte ClassID;
        public ushort SpecID;
        public uint SpellID;
        public uint OverridesSpellID;
        public int RequiredSpellID;
        public byte[] CategoryMask = new byte[2];
        public uint[] SpellRank = new uint[9];
        public int[] PrereqTalent = new int[3];
        public int[] PrereqRank = new int[3];
    }

    public sealed class TalentTabRecord
    {
        public uint Id;
        public LocalizedString Name;
        public string BackgroundFile;
        public int OrderIndex;
        public int RaceMask;
        public int ClassMask;
    }

    public sealed class TaxiNodesRecord
    {
        public LocalizedString Name;
        public Vector3 Pos;
        public Vector2 MapOffset;
        public Vector2 FlightMapOffset;
        public uint Id;
        public uint ContinentID;
        public ushort ConditionID;
        public ushort CharacterBitNumber;
        public TaxiNodeFlags Flags;
        public int UiTextureKitID;
        public float Facing;
        public uint SpecialIconConditionID;
        public uint VisibilityConditionID;
        public uint[] MountCreatureID = new uint[2];
    }

    public sealed class TaxiPathRecord
    {
        public uint Id;
        public ushort FromTaxiNode;
        public ushort ToTaxiNode;
        public uint Cost;
    }

    public sealed class TaxiPathNodeRecord
    {
        public Vector3 Loc;
        public uint Id;
        public ushort PathID;
        public uint NodeIndex;
        public ushort ContinentID;
        public TaxiPathNodeFlags Flags;
        public uint Delay;
        public uint ArrivalEventID;
        public uint DepartureEventID;
    }

    public sealed class TerrainMaterialRecord
    {
        public uint Id;
        public byte Shader;
        public int EnvMapDiffuseFileID;
        public int EnvMapSpecularFileID;
    }

    public sealed class TerrainTypeRecord
    {
        public uint Id;
        public LocalizedString TerrainDesc;
        public ushort FootstepSprayRun;
        public ushort FootstepSprayWalk;
        public byte SoundID;
        public byte Flags;
        public int TerrainID;
    }

    public sealed class TerrainTypeSoundsRecord
    {
        public uint Id;
        public LocalizedString Name;
    }

    public sealed class TextureBlendSetRecord
    {
        public uint Id;
        public int[] TextureFileDataID = new int[3];
        public byte SwizzleRed;
        public byte SwizzleGreen;
        public byte SwizzleBlue;
        public byte SwizzleAlpha;
        public int Flags;
        public float[] TextureScrollRateU = new float[3];
        public float[] TextureScrollRateV = new float[3];
        public float[] TextureScaleU = new float[3];
        public float[] TextureScaleV = new float[3];
        public float[] ModX = new float[4];
    }

    public sealed class TextureFileDataRecord
    {
        public uint Id;
        public byte UsageType;
        public int MaterialResourcesID;
    }

    public sealed class TotemCategoryRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte TotemCategoryType;
        public int TotemCategoryMask;
    }

    public sealed class ToyRecord
    {
        public LocalizedString SourceText;
        public uint Id;
        public int ItemID;
        public byte Flags;
        public sbyte SourceTypeEnum;
    }

    public sealed class TradeSkillCategoryRecord
    {
        public LocalizedString Name;
        public LocalizedString HordeName;
        public uint Id;
        public ushort ParentTradeSkillCategoryID;
        public ushort SkillLineID;
        public short OrderIndex;
        public byte Flags;
    }

    public sealed class TransformMatrixRecord
    {
        public uint Id;
        public float[] Pos = new float[3];
        public float Yaw;
        public float Pitch;
        public float Roll;
        public float Scale;
    }

    public sealed class TransportAnimationRecord
    {
        public uint Id;
        public float[] Pos = new float[3];
        public byte SequenceID;
        public uint TimeIndex;
        public uint TransportID;
    }

    public sealed class TransportPhysicsRecord
    {
        public uint Id;
        public float WaveAmp;
        public float WaveTimeScale;
        public float RollAmp;
        public float RollTimeScale;
        public float PitchAmp;
        public float PitchTimeScale;
        public float MaxBank;
        public float MaxBankTurnSpeed;
        public float SpeedDampThresh;
        public float SpeedDamp;
    }

    public sealed class TransportRotationRecord
    {
        public uint Id;
        public float[] Rot = new float[4];
        public uint TimeIndex;
        public uint GameObjectsID;
    }

    public sealed class TrophyRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte TrophyTypeID;
        public uint GameObjectDisplayInfoID;
        public uint PlayerConditionID;
    }

    public sealed class UiCameraRecord
    {
        public uint Id;
        public LocalizedString Name;
        public float[] Pos = new float[3];
        public float[] LookAt = new float[3];
        public float[] Up = new float[3];
        public byte UiCameraTypeID;
        public int AnimID;
        public short AnimFrame;
        public sbyte AnimVariation;
        public byte Flags;
    }

    public sealed class UiCameraTypeRecord
    {
        public uint Id;
        public LocalizedString Name;
        public uint Width;
        public uint Height;
    }

    public sealed class UiCamFbackTransmogChrRaceRecord
    {
        public uint Id;
        public byte ChrRaceID;
        public byte Gender;
        public byte InventoryType;
        public byte Variation;
        public ushort UiCameraID;
    }

    public sealed class UiCamFbackTransmogWeaponRecord
    {
        public uint Id;
        public byte ItemClass;
        public byte ItemSubclass;
        public byte InventoryType;
        public ushort UiCameraID;
    }

    public sealed class UiCanvasRecord
    {
        public uint Id;
        public short Width;
        public short Height;
    }

    public sealed class UIExpansionDisplayInfoRecord
    {
        public uint Id;
        public int ExpansionLogo;
        public int ExpansionBanner;
        public uint ExpansionLevel;
    }

    public sealed class UIExpansionDisplayInfoIconRecord
    {
        public uint Id;
        public LocalizedString FeatureDescription;
        public int ParentID;
        public int FeatureIcon;
    }

    public sealed class UiMapRecord
    {
        public LocalizedString Name;
        public uint Id;
        public int ParentUiMapID;
        public UiMapFlag Flags;
        public int System;
        public UiMapType Type;
        public int BountySetID;
        public uint BountyDisplayLocation;
        public int VisibilityPlayerConditionID;
        public sbyte HelpTextPosition;
        public int BkgAtlasID;
        public uint LevelRangeMin;
        public uint LevelRangeMax;

        public UiMapFlag GetFlags() => Flags;
    }

    public sealed class UiMapArtRecord
    {
        public uint Id;
        public int HighlightFileDataID;
        public int HighlightAtlasID;
        public int UiMapArtStyleID;
    }

    public sealed class UiMapArtStyleLayerRecord
    {
        public uint Id;
        public byte LayerIndex;
        public ushort LayerWidth;
        public ushort LayerHeight;
        public ushort TileWidth;
        public ushort TileHeight;
        public float MinScale;
        public float MaxScale;
        public int AdditionalZoomSteps;
        public int UiMapArtStyleID;
    }

    public sealed class UiMapArtTileRecord
    {
        public uint Id;
        public byte RowIndex;
        public byte ColIndex;
        public byte LayerIndex;
        public int FileDataID;
        public int UiMapArtID;
    }

    public sealed class UiMapAssignmentRecord
    {
        public Vector2 UiMin;
        public Vector2 UiMax;
        public Vector3[] Region = new Vector3[2];
        public uint Id;
        public int UiMapID;
        public int OrderIndex;
        public int MapID;
        public int AreaID;
        public int WmoDoodadPlacementID;
        public int WmoGroupID;
    }

    public sealed class UiMapFogOfWarRecord
    {
        public uint Id;
        public int UiMapID;
        public int PlayerConditionID;
        public int UiMapFogOfWarVisID;
    }

    public sealed class UiMapFogOfWarVisualizationRecord
    {
        public uint Id;
        public uint BackgroundAtlasID;
        public uint MaskAtlasID;
        public float MaskScalar;
    }

    public sealed class UiMapGroupMemberRecord
    {
        public uint Id;
        public LocalizedString Name;
        public int UiMapGroupID;
        public int UiMapID;
        public int FloorIndex;
        public sbyte RelativeHeightIndex;
    }

    public sealed class UiMapLinkRecord
    {
        public Vector2 UiMin;
        public Vector2 UiMax;
        public uint Id;
        public int ParentUiMapID;
        public int OrderIndex;
        public int ChildUiMapID;
        public int OverrideHighlightFdID;
        public int OverrideHighlightAtlasID;
        public int Flags;
    }

    public sealed class UiMapXMapArtRecord
    {
        public uint Id;
        public int PhaseID;
        public int UiMapArtID;
        public uint UiMapID;
    }

    public sealed class UiModelSceneRecord
    {
        public uint Id;
        public sbyte UiSystemType;
        public byte Flags;
    }

    public sealed class UiModelSceneActorRecord
    {
        public LocalizedString ScriptTag;
        public float[] Position = new float[3];
        public uint Id;
        public byte Flags;
        public int UiModelSceneActorDisplayID;
        public float OrientationYaw;
        public float OrientationPitch;
        public float OrientationRoll;
        public float NormalizedScale;
        public int UiModelSceneID;
    }

    public sealed class UiModelSceneActorDisplayRecord
    {
        public uint Id;
        public uint AnimationID;
        public uint SequenceVariation;
        public uint AnimKitID;
        public uint SpellVisualKitID;
        public float Alpha;
        public float Scale;
        public float AnimSpeed;
    }

    public sealed class UiModelSceneCameraRecord
    {
        public LocalizedString ScriptTag;
        public float[] Target = new float[3];
        public float[] ZoomedTargetOffset = new float[3];
        public uint Id;
        public byte Flags;
        public byte CameraType;
        public float Yaw;
        public float Pitch;
        public float Roll;
        public float ZoomedYawOffset;
        public float ZoomedPitchOffset;
        public float ZoomedRollOffset;
        public float ZoomDistance;
        public float MinZoomDistance;
        public float MaxZoomDistance;
        public int UiModelSceneID;
    }

    public sealed class UiPartyPoseRecord
    {
        public uint Id;
        public int UiWidgetSetID;
        public int VictoryUiModelSceneID;
        public int DefeatUiModelSceneID;
        public int VictorySoundKitID;
        public int DefeatSoundKitID;
        public int MapID;
    }

    public sealed class UIScriptedAnimationEffectRecord
    {
        public uint Id;
        public int Visual;
        public int VisualScale;
        public int Duration;
        public int Trajectory;
        public int StartSoundKitID;
        public int FinishSoundKitID;
        public int StartBehavior;
        public int FinishBehavior;
        public int FinishEffectID;
        public float YawRadians;
        public float PitchRadians;
        public float RollRadians;
        public float OffsetX;
        public float OffsetY;
        public float OffsetZ;
        public float AnimationSpeed;
        public int Animation;
        public int AnimationStartOffset;
        public int Alpha;
        public int StartAlphaFade;
        public int StartAlphaFadeDuration;
        public int EndAlphaFade;
        public int EndAlphaFadeDuration;
        public int LoopingSoundKitID;
        public int ParticleOverrideScale;
        public int Flags;
    }

    public sealed class UiTextureAtlasRecord
    {
        public uint Id;
        public int FileDataID;
        public ushort AtlasWidth;
        public ushort AtlasHeight;
        public byte UiCanvasID;
    }

    public sealed class UiTextureAtlasElementRecord
    {
        public LocalizedString Name;
        public uint Id;
    }

    public sealed class UiTextureAtlasMemberRecord
    {
        public LocalizedString CommittedName;
        public uint Id;
        public ushort UiTextureAtlasID;
        public short CommittedLeft;
        public short CommittedRight;
        public short CommittedTop;
        public short CommittedBottom;
        public ushort UiTextureAtlasElementID;
        public short OverrideWidth;
        public short OverrideHeight;
        public sbyte CommittedFlags;
        public byte UiCanvasID;
    }

    public sealed class UiTextureKitRecord
    {
        public uint Id;
        public LocalizedString KitPrefix;
    }

    public sealed class UiWidgetRecord
    {
        public uint Id;
        public LocalizedString WidgetTag;
        public ushort ParentSetID;
        public int VisID;
        public int MapID;
        public int PlayerConditionID;
        public uint OrderIndex;
    }

    public sealed class UiWidgetConstantSourceRecord
    {
        public uint Id;
        public ushort ReqID;
        public int Value;
        public int ParentWidgetID;
    }

    public sealed class UiWidgetDataSourceRecord
    {
        public uint Id;
        public ushort SourceID;
        public sbyte SourceType;
        public ushort ReqID;
        public int ParentWidgetID;
    }

    public sealed class UiWidgetStringSourceRecord
    {
        public uint Id;
        public LocalizedString Value;
        public ushort ReqID;
        public int ParentWidgetID;
    }

    public sealed class UiWidgetVisualizationRecord
    {
        public uint Id;
        public sbyte VisType;
        public int TextureKit;
        public int FrameTextureKit;
        public short SizeSetting;
    }

    public sealed class UnitBloodRecord
    {
        public uint Id;
        public uint[] CombatBloodSpurtFront = new uint[2];
        public uint[] CombatBloodSpurtBack = new uint[2];
    }

    public sealed class UnitBloodLevelsRecord
    {
        public uint Id;
        public byte[] Violencelevel = new byte[3];
    }

    public sealed class UnitConditionRecord
    {
        public uint Id;
        public byte Flags;
        public byte[] Variable = new byte[8];
        public sbyte[] Op = new sbyte[8];
        public int[] Value = new int[8];
    }

    public sealed class UnitPowerBarRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Cost;
        public LocalizedString OutOfError;
        public LocalizedString ToolTip;
        public uint MinPower;
        public uint MaxPower;
        public ushort StartPower;
        public byte CenterPower;
        public float RegenerationPeace;
        public float RegenerationCombat;
        public byte BarType;
        public ushort Flags;
        public float StartInset;
        public float EndInset;
        public int[] FileDataID = new int[6];
        public int[] Color = new int[6];
    }

    public sealed class VehicleRecord
    {
        public uint Id;
        public VehicleFlags Flags;
        public byte FlagsB;
        public float TurnSpeed;
        public float PitchSpeed;
        public float PitchMin;
        public float PitchMax;
        public float MouseLookOffsetPitch;
        public float CameraFadeDistScalarMin;
        public float CameraFadeDistScalarMax;
        public float CameraPitchOffset;
        public float FacingLimitRight;
        public float FacingLimitLeft;
        public float CameraYawOffset;
        public ushort VehicleUIIndicatorID;
        public int MissileTargetingID;
        public byte UiLocomotionType;
        public ushort[] SeatID = new ushort[8];
        public ushort[] PowerDisplayID = new ushort[3];
    }

    public sealed class VehicleSeatRecord
    {
        public uint Id;
        public Vector3 AttachmentOffset;
        public Vector3 CameraOffset;
        public VehicleSeatFlags Flags;
        public VehicleSeatFlagsB FlagsB;
        public int FlagsC;
        public sbyte AttachmentID;
        public float EnterPreDelay;
        public float EnterSpeed;
        public float EnterGravity;
        public float EnterMinDuration;
        public float EnterMaxDuration;
        public float EnterMinArcHeight;
        public float EnterMaxArcHeight;
        public int EnterAnimStart;
        public int EnterAnimLoop;
        public int RideAnimStart;
        public int RideAnimLoop;
        public int RideUpperAnimStart;
        public int RideUpperAnimLoop;
        public float ExitPreDelay;
        public float ExitSpeed;
        public float ExitGravity;
        public float ExitMinDuration;
        public float ExitMaxDuration;
        public float ExitMinArcHeight;
        public float ExitMaxArcHeight;
        public int ExitAnimStart;
        public int ExitAnimLoop;
        public int ExitAnimEnd;
        public short VehicleEnterAnim;
        public sbyte VehicleEnterAnimBone;
        public short VehicleExitAnim;
        public sbyte VehicleExitAnimBone;
        public short VehicleRideAnimLoop;
        public sbyte VehicleRideAnimLoopBone;
        public sbyte PassengerAttachmentID;
        public float PassengerYaw;
        public float PassengerPitch;
        public float PassengerRoll;
        public float VehicleEnterAnimDelay;
        public float VehicleExitAnimDelay;
        public sbyte VehicleAbilityDisplay;
        public uint EnterUISoundID;
        public uint ExitUISoundID;
        public int UiSkinFileDataID;
        public float CameraEnteringDelay;
        public float CameraEnteringDuration;
        public float CameraExitingDelay;
        public float CameraExitingDuration;
        public float CameraPosChaseRate;
        public float CameraFacingChaseRate;
        public float CameraEnteringZoom;
        public float CameraSeatZoomMin;
        public float CameraSeatZoomMax;
        public short EnterAnimKitID;
        public short RideAnimKitID;
        public short ExitAnimKitID;
        public short VehicleEnterAnimKitID;
        public short VehicleRideAnimKitID;
        public short VehicleExitAnimKitID;
        public short CameraModeID;

        public bool CanEnterOrExit() => Flags.HasAnyFlag(VehicleSeatFlags.CanEnterOrExit) || Flags.HasAnyFlag(VehicleSeatFlags.HasLowerAnimForEnter) || Flags.HasAnyFlag(VehicleSeatFlags.HasLowerAnimForRide);
        public bool CanSwitchFromSeat() => Flags.HasAnyFlag(VehicleSeatFlags.CanSwitch);
        public bool IsUsableByOverride() => Flags.HasAnyFlag(VehicleSeatFlags.Uncontrolled | VehicleSeatFlags.Unk18) ||
            FlagsB.HasAnyFlag(VehicleSeatFlagsB.UsableForced | VehicleSeatFlagsB.UsableForced2 | VehicleSeatFlagsB.UsableForced3 | VehicleSeatFlagsB.UsableForced4);
        public bool IsEjectable() => FlagsB.HasAnyFlag(VehicleSeatFlagsB.Ejectable);
    }

    public sealed class VehicleUIIndicatorRecord
    {
        public uint Id;
        public int BackgroundTextureFileID;
    }

    public sealed class VehicleUIIndSeatRecord
    {
        public uint Id;
        public byte VirtualSeatIndex;
        public float XPos;
        public float YPos;
        public int VehicleUIIndicatorID;
    }

    public sealed class VignetteRecord
    {
        public uint Id;
        public LocalizedString Name;
        public uint PlayerConditionID;
        public uint VisibleTrackingQuestID;
        public uint QuestFeedbackEffectID;
        public uint Flags;
        public float MaxHeight;
        public float MinHeight;
        public sbyte VignetteType;
        public int RewardQuestID;
    }

    public sealed class VirtualAttachmentRecord
    {
        public uint Id;
        public LocalizedString Name;
        public short PositionerID;
    }

    public sealed class VirtualAttachmentCustomizationRecord
    {
        public uint Id;
        public short VirtualAttachmentID;
        public int FileDataID;
        public short PositionerID;
    }

    public sealed class VocalUISoundsRecord
    {
        public uint Id;
        public byte VocalUIEnum;
        public byte RaceID;
        public byte ClassID;
        public uint[] NormalSoundID = new uint[2];
    }

    public sealed class VolumeFogConditionRecord
    {
        public uint Id;
        public int PlayerConditionID;
        public float WhenFalse;
        public float WhenTrue;
        public int VFOGUID;
    }

    public sealed class WbAccessControlListRecord
    {
        public uint Id;
        public LocalizedString URL;
        public ushort GrantFlags;
        public byte RevokeFlags;
        public byte WowEditInternal;
        public byte RegionID;
    }

    public sealed class WbCertWhitelistRecord
    {
        public uint Id;
        public LocalizedString Domain;
        public byte GrantAccess;
        public byte RevokeAccess;
        public byte WowEditInternal;
    }

    public sealed class WeaponImpactSoundsRecord
    {
        public uint Id;
        public byte WeaponSubClassID;
        public byte ParrySoundType;
        public byte ImpactSource;
        public uint[] ImpactSoundID = new uint[11];
        public uint[] CritImpactSoundID = new uint[11];
        public uint[] PierceImpactSoundID = new uint[11];
        public uint[] PierceCritImpactSoundID = new uint[11];
    }

    public sealed class WeaponSwingSounds2Record
    {
        public uint Id;
        public byte SwingType;
        public byte Crit;
        public uint SoundID;
    }

    public sealed class WeaponTrailRecord
    {
        public uint Id;
        public int FileDataID;
        public float Roll;
        public float Pitch;
        public float Yaw;
        public int[] TextureFileDataID = new int[3];
        public float[] TextureScrollRateU = new float[3];
        public float[] TextureScrollRateV = new float[3];
        public float[] TextureScaleU = new float[3];
        public float[] TextureScaleV = new float[3];
    }

    public sealed class WeaponTrailModelDefRecord
    {
        public uint Id;
        public int ModelFileDataID;
        public ushort WeaponTrailID;
        public int AnimEnumID;
    }

    public sealed class WeaponTrailParamRecord
    {
        public uint Id;
        public byte Hand;
        public float Duration;
        public float FadeOutTime;
        public float EdgeLifeSpan;
        public float InitialDelay;
        public float SmoothSampleAngle;
        public sbyte OverrideAttachTop;
        public sbyte OverrideAttachBot;
        public byte Flags;
        public int WeaponTrailID;
    }

    public sealed class WeatherRecord
    {
        public uint Id;
        public byte Type;
        public float TransitionSkyBox;
        public uint AmbienceID;
        public ushort SoundAmbienceID;
        public byte EffectType;
        public int EffectTextureFileDataID;
        public byte WindSettingsID;
        public float Scale;
        public float Volatility;
        public float TwinkleIntensity;
        public float FallModifier;
        public float RotationalSpeed;
        public int ParticulateFileDataID;
        public float VolumeEdgeFadeStart;
        public int OverrideColor;
        public float OverrideColorIntensity;
        public float OverrideCount;
        public float OverrideOpacity;
        public int VolumeFlags;
        public int LightningID;
        public float[] Intensity = new float[2];
        public float[] EffectColor = new float[3];
    }

    public sealed class WeatherXParticulateRecord
    {
        public uint Id;
        public int FileDataID;
        public int ParentWeatherID;
    }

    public sealed class WindSettingsRecord
    {
        public uint Id;
        public float[] BaseDir = new float[3];
        public float[] VarianceDir = new float[3];
        public float[] MaxStepDir = new float[3];
        public float BaseMag;
        public float VarianceMagOver;
        public float VarianceMagUnder;
        public float MaxStepMag;
        public float Frequency;
        public float Duration;
        public byte Flags;
    }

    public sealed class WMOAreaTableRecord
    {
        public LocalizedString AreaName;
        public uint Id;
        public ushort WmoID;
        public byte NameSetID;
        public int WmoGroupID;
        public byte SoundProviderPref;
        public byte SoundProviderPrefUnderwater;
        public ushort AmbienceID;
        public ushort UwAmbience;
        public ushort ZoneMusic;
        public uint UwZoneMusic;
        public ushort IntroSound;
        public ushort UwIntroSound;
        public ushort AreaTableID;
        public byte Flags;
    }

    public sealed class WMOMinimapTextureRecord
    {
        public uint Id;
        public ushort GroupNum;
        public byte BlockX;
        public byte BlockY;
        public int FileDataID;
        public int WMOID;
    }

    public sealed class WorldBossLockoutRecord
    {
        public uint Id;
        public LocalizedString Name;
        public uint TrackingQuestID;
    }

    public sealed class WorldChunkSoundsRecord
    {
        public uint Id;
        public ushort MapID;
        public int SoundOverrideID;
        public byte ChunkX;
        public byte ChunkY;
        public byte SubchunkX;
        public byte SubchunkY;
    }

    public sealed class WorldEffectRecord
    {
        public uint Id;
        public uint QuestFeedbackEffectID;
        public byte WhenToDisplay;
        public byte TargetType;
        public int TargetAsset;
        public uint PlayerConditionID;
        public ushort CombatConditionID;
    }

    public sealed class WorldElapsedTimerRecord
    {
        public uint Id;
        public LocalizedString Name;
        public byte Type;
        public byte Flags;
    }

    public sealed class WorldMapOverlayRecord
    {
        public uint Id;
        public uint UiMapArtID;
        public ushort TextureWidth;
        public ushort TextureHeight;
        public int OffsetX;
        public int OffsetY;
        public int HitRectTop;
        public int HitRectBottom;
        public int HitRectLeft;
        public int HitRectRight;
        public uint PlayerConditionID;
        public uint Flags;
        public uint[] AreaID = new uint[4];
    }

    public sealed class WorldMapOverlayTileRecord
    {
        public uint Id;
        public byte RowIndex;
        public byte ColIndex;
        public byte LayerIndex;
        public int FileDataID;
        public int WorldMapOverlayID;
    }

    public sealed class WorldStateExpressionRecord
    {
        public uint Id;
        public string Expression;
    }

    public sealed class WorldStateUIRecord
    {
        public LocalizedString Icon;
        public LocalizedString String;
        public LocalizedString Tooltip;
        public LocalizedString DynamicTooltip;
        public LocalizedString ExtendedUI;
        public uint Id;
        public short MapID;
        public ushort AreaID;
        public ushort StateVariable;
        public byte Type;
        public int DynamicIconFileID;
        public int DynamicFlashIconFileID;
        public byte OrderIndex;
        public byte PhaseUseFlags;
        public ushort PhaseID;
        public ushort PhaseGroupID;
        public ushort[] ExtendedUIStateVariable = new ushort[3];
    }

    public sealed class WorldStateZoneSoundsRecord
    {
        public uint Id;
        public ushort WorldStateID;
        public ushort WorldStateValue;
        public ushort AreaID;
        public uint WMOAreaID;
        public ushort ZoneIntroMusicID;
        public ushort ZoneMusicID;
        public ushort SoundAmbienceID;
        public byte SoundProviderPreferencesID;
    }

    public sealed class WorldPVPAreaRecord
    {
        public uint Id;
        public ushort AreaID;
        public ushort NextTimeworldstate;
        public ushort GameTimeworldstate;
        public ushort BattlePopulatetime;
        public byte MinLevel;
        public byte MaxLevel;
        public short MapID;
    }

    public sealed class ZoneIntroMusicTableRecord
    {
        public uint Id;
        public LocalizedString Name;
        public uint SoundID;
        public byte Priority;
        public ushort MinDelayMinutes;
    }

    public sealed class ZoneLightRecord
    {
        public uint Id;
        public LocalizedString Name;
        public ushort MapID;
        public ushort LightID;
        public byte Flags;
        public int PlayerConditionID;
    }

    public sealed class ZoneLightPointRecord
    {
        public uint Id;
        public float[] Pos = new float[2];
        public byte PointOrder;
        public int ZoneLightID;
    }

    public sealed class ZoneMusicRecord
    {
        public uint Id;
        public LocalizedString SetName;
        public uint[] SilenceIntervalMin = new uint[2];
        public uint[] SilenceIntervalMax = new uint[2];
        public uint[] Sounds = new uint[2];
    }

    public sealed class ZoneStoryRecord
    {
        public uint Id;
        public byte PlayerFactionGroupID;
        public uint DisplayAchievementID;
        public uint DisplayUIMapID;
        public int PlayerUIMapID;
    }
}
