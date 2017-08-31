// 425. An error should be reported when compiling without /vo7+ that there is no conversion from PTR to the VOSTRUCT
// RvdH: At this moment the compiler does not report an error when assigning 
// a VOID PTR to a typed PTR when UnSafe is enabled. Should I change this ?
FUNCTION Start( ) AS VOID
	LOCAL p AS _winDRAWITEMSTRUCT
	p := MemAlloc(SizeOf(_winDRAWITEMSTRUCT))
	? p
RETURN

VOSTRUCT _winDRAWITEMSTRUCT
	MEMBER  CtlType AS DWORD
	MEMBER  CtlID AS DWORD

