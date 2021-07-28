ALTER TABLE `creature_template` ADD `npcflags2` INT(10) AFTER `npcflag`;

UPDATE `creature_template` SET `npcflags2` = (`npcflag` >> 32) & 0xFFFFFFFF;

ALTER TABLE `creature_template` CHANGE `npcflag` `npcflags` INT(10) AFTER `faction`;