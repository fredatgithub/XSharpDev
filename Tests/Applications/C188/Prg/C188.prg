// 188. Argument 1: cannot convert from 'Vulcan.__VOFloat' to 'float'
FUNCTION Start() AS VOID
LOCAL f AS FLOAT
f := 1.0
Real4Func(f)

FUNCTION Real4Func(r AS REAL4) AS VOID
