DROP TABLE IF EXISTS [Version];
CREATE TABLE [Version] (
  [Id] text NOT NULL
, [TableVersion] bigint NOT NULL
, CONSTRAINT [sqlite_autoindex_Version_1] PRIMARY KEY ([Id])
);

INSERT INTO Version Values("FCO", 2);
INSERT INTO Version Values("MainUAL",3);
INSERT INTO Version Values("UpgradeResult",2);
INSERT INTO Version VALUES('FCOList',1);
INSERT INTO Version VALUES ('FcoDB', 3);

DROP TABLE IF EXISTS [FCO];
CREATE TABLE [FCO] (
  [Id] VARCHAR(40) NOT NULL
, [FcoNumber] VARCHAR(20) NOT NULL
, [CountryName] VARCHAR(20) NOT NULL
, [TotalCount] int NOT NULL
, CONSTRAINT [sqlite_autoindex_FCO_1] PRIMARY KEY ([Id])
);


DROP TABLE IF EXISTS [MainUAL];
CREATE TABLE [MainUAL] (
  [Id] VARCHAR(40) NOT NULL
, [ShippedSystemSerialNo] VARCHAR(32) NOT NULL
, [MaintainedSystemSerialNo] VARCHAR(32) NOT NULL
, [CountryName] VARCHAR(20) NOT NULL
, [UpgradeCode] VARCHAR(32) NOT NULL
, [ModelNumber] VARCHAR(10) NULL
, [ProcessedDate] date NULL
, [CompletionStat] int NULL
, [CompletionDate] date NULL
, [FCONo] VARCHAR(20) NOT NULL
, [FCORev] VARCHAR(8) NULL
, CONSTRAINT [sqlite_autoindex_MainUAL_1] PRIMARY KEY ([Id])
);

DROP TABLE IF EXISTS [UpgradeResult];
CREATE TABLE [UpgradeResult] (
  [Id] VARCHAR(40) NOT NULL
, [SystemSerialNo] VARCHAR(32) NOT NULL
, [UpgradeCode] VARCHAR(32) NOT NULL
, [ModelNumber] VARCHAR(20) NULL
, [CompletionStat] int NULL
, CONSTRAINT [sqlite_autoindex_UpgradeResult_1] PRIMARY KEY ([Id])
);

DROP TABLE IF EXISTS [FcoList];
CREATE TABLE FcoList(
uniqueFcoNumber varchar(25) PRIMARY KEY 
);

CREATE INDEX idx_sn_uc_mn ON MainUAL(ShippedSystemSerialNo, UpgradeCode, ModelNumber);
