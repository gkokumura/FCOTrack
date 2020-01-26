DROP TABLE IF EXISTS [FcoList];
CREATE TABLE FcoList(uniqueFcoNumber varchar(25) PRIMARY KEY );

ALTER TABLE MainUAL ADD COLUMN FCORev varchar(5);
                
CREATE INDEX idx_fco ON MainUAL(FCONo);

INSERT INTO Version (Id, TableVersion) VALUES('FCOList',1);
INSERT INTO Version (Id, TableVersion) VALUES ('FcoDB', 3);
UPDATE Version SET TableVersion = 3 WHERE Id = 'MainUAL';

INSERT INTO FcoList (uniqueFcoNumber)
SELECT DISTINCT FcoNumber FROM FCO;