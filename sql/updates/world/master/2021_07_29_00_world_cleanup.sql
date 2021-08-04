-------------------------------------------------------------------------
--
-- QUEST DATA
--
-------------------------------------------------------------------------

DROP TABLE `quest_reward_display_spell`;

ALTER TABLE `quest_template`
    DROP COLUMN `ContentTuningID`,
	DROP COLUMN `ManagedWorldStateID`,
	DROP COLUMN `QuestSessionBonus`,
	ADD `QuestLevel` INT(10) NOT NULL AFTER `QuestType`,
	ADD `ScalingFactionGroup` INT(10) NOT NULL AFTER `QuestLevel`,
	ADD `MaxScalingLevel` INT(10) NOT NULL AFTER `ScalingFactionGroup`,
	ADD `MinLevel` INT(10) NOT NULL AFTER `QuestPackageID`;

-- Clear `quest_template` table, needs to be bruteforced :poggers:
DELETE FROM `quest_template`;

-------------------------------------------------------------------------
--
-- SPELL DATA
--
-------------------------------------------------------------------------

ALTER TABLE `serverside_spell_effect` CHANGE `EffectBasePoints` `EffectBasePoints` INT(10);
ALTER TABLE `serverside_spell` DROP COLUMN `ContentTuningId`;