// 163. Compiler crash with RECOVER statement
FUNCTION Start() AS VOID
BEGIN SEQUENCE
RECOVER
	? "recover"
END SEQUENCE

