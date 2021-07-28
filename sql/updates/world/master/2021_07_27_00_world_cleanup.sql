DELETE FROM `spell_script_names` WHERE `ScriptName` IN (
    'spell_item_scroll_of_recall',
    'spell_item_artifical_stamina',
    'spell_item_artifical_damage',
    'spell_mage_arcane_barrage',
    'spell_pal_fist_of_justice',
    'spell_pal_righteous_protector',
    'spell_pal_selfless_healer'
);

UPDATE `trinity_string` SET `content_default` = 'GUID %s, faction is %u, flags is %u, npcflag is %u, npcflag2 is %u, dynflag is %u.' WHERE `entry` = 128;