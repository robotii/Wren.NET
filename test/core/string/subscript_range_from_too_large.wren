// skip: Range subscripts for strings don't currently work.
var a = "123"
a[3..2] // expect runtime error: Range start out of bounds.
