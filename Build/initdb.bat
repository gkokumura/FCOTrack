@ECHO OFF
SET DBFILE=
IF (%1)==() (SET DBFILE=FcoDB.db) ELSE SET DBFILE=%1

SET DBLOGFILE=
IF (%2)==() (SET DBLOGFILE=FcoLog.db) ELSE SET DBLOGFILE=%2

"%~dp0sqlite3.exe" %DBFILE% < "%~dp0fco-table.sql"
"%~dp0sqlite3.exe" %DBLOGFILE% < "%~dp0fcolog-table.sql"

@ECHO ON
ECHO Initializing the database...
@ECHO OFF