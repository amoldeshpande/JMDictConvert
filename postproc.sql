
/*
CREATE INDEX [IDX_GLOSS_SEQ] ON [gloss](
[sequence]  ASC
);
CREATE INDEX [IDX_KANJI_SEQ] ON [kanji](
[sequence]  ASC
);
CREATE INDEX [IDX_READING_SEQ] ON [reading](
[sequence]  ASC
);
CREATE INDEX [IDX_SENSE_SEQ] ON [sense](
[sequence]  ASC
);
*/

create virtual table gloss_fts using fts3(sequence,meaning,lang);
create virtual table kanji_fts using fts3(sequence,keb);
create virtual table reading_fts using fts3(sequence,reb);
create virtual table romaji_fts using fts3(sequence,romaji);

insert into gloss_fts (sequence,meaning,lang) select sequence,meaning,gloss_lang from gloss;
insert into kanji_fts (sequence,keb) select sequence,keb from kanji;
insert into reading_fts (sequence,reb) select sequence,reb from reading;
insert into romaji_fts (sequence,romaji) select sequence,romaji from reading;
