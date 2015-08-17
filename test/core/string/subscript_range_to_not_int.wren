// skip: Range subscripts for strings don't currently work.
var a = "string"
a[1..2.5] // expect runtime error: Range end must be an integer.
