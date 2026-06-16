ALTER TABLE [Anime] ADD [AirDate] date NULL;
ALTER TABLE [Anime] ADD [Duration] nvarchar(max) NULL;
ALTER TABLE [Anime] ADD [Producer] nvarchar(max) NULL;
ALTER TABLE [Anime] ADD [Director] nvarchar(max) NULL;
ALTER TABLE [Anime] ADD [BangumiScore] float NULL;
ALTER TABLE [Anime] ADD [BangumiRank] int NULL;
ALTER TABLE [Anime] ADD [BangumiRatingCount] int NULL;

CREATE TABLE [AnimeTags] (
    [Id] int NOT NULL IDENTITY,
    [AnimeId] int NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Count] int NOT NULL,
    CONSTRAINT [PK_AnimeTags] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AnimeTags_Anime_AnimeId] FOREIGN KEY ([AnimeId]) REFERENCES [Anime] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AnimeTags_AnimeId_Count] ON [AnimeTags] ([AnimeId], [Count]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260613030948_AddAnimeMetadataAndTags', N'8.0.11');
