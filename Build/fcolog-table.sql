DROP TABLE IF EXISTS [Log];
CREATE TABLE [Log] (
  [Id] INTEGER NOT NULL
, [Date] datetime NOT NULL
, [Level] nvarchar(50) NOT NULL COLLATE NOCASE
, [Thread] INTEGER NOT NULL
, [Logger] nvarchar(255) NOT NULL COLLATE NOCASE
, [Message] text DEFAULT NULL NULL
, CONSTRAINT [sqlite_master_PK_Log] PRIMARY KEY ([Id])
);