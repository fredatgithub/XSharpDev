// 241. error XS0266: Cannot implicitly convert type 'void*' to 'byte*'. An explicit conversion exists (are you missing a cast?)
FUNCTION Start() AS VOID
LOCAL pResult AS BYTE PTR
pResult := MemAlloc(123)

