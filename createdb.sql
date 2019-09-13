CREATE TABLE [gloss] (
[entry_sequence_number] INTEGER  NOT NULL,
[gloss_id] INTEGER  PRIMARY KEY AUTOINCREMENT NOT NULL,
[gloss_lang] TEXT  NULL,
[meaning] TEXT  NULL,
[gender] TEXT  NULL,
[sense_id] INTEGER  NULL
);
CREATE TABLE [kanji] (
[sequence] INTEGER  NOT NULL,
[keb] TEXT  NULL,
[ke_inf] TEXT  NULL,
[ke_pri] TEXT  NULL,
[id] INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT
);
CREATE TABLE [reading] (
[sequence] INTEGER  NOT NULL,
[reb] TEXT  NULL,
[re_nokanji] TEXT  NULL,
[restrictions] TEXT  NULL,
[info] TEXT  NULL,
[re_pri] TEXT  NULL,
[id] INTEGER  PRIMARY KEY AUTOINCREMENT NOT NULL,
[romaji] TEXT  NULL
);
CREATE TABLE [sense] (
[sequence] INTEGER  NOT NULL,
[stagk] TEXT  NULL,
[stagr] TEXT  NULL,
[xref] TEXT  NULL,
[antonym] TEXT  NULL,
[part_of_speech] TEXT  NULL,
[field_of_application] TEXT  NULL,
[misc] TEXT  NULL,
[source_language] TEXT  NULL,
[dialect] TEXT  NULL,
[sense_id] INTEGER  NOT NULL,
[source_language_word] TEXT  NULL,
[id] INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT
);
