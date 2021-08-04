DELETE FROM `spell_script_names` WHERE `ScriptName` IN (
    'spell_gen_running_wild'
);

DELETE FROM `command` WHERE `name` IN (
    'scene',
    'scene cancel',
    'scene debug',
    'scene play',
    'scene playpackage',
    'list scenes',
    'achievement',
    'achievement add'
);

ALTER TABLE `playerchoice_response_reward` DROP COLUMN `PackageId`;
ALTER TABLE `serverside_spell` DROP COLUMN `AttributesEx14`;

DROP TABLE `achievement_dbc`;
DROP TABLE `achievement_reward`;
DROP TABLE `achievement_reward_locale`;
DROP TABLE `guild_rewards_req_achievements`;
DROP TABLE `player_factionchange_achievement`;

ALTER TABLE `race_unlock_requirement` DROP COLUMN `achievementId`;
ALTER TABLE `access_requirement` DROP COLUMN `completed_achievement`;
