var s = "abcd"
IO.print(s.iteratorValue(0)) // expect: a
IO.print(s.iteratorValue(1)) // expect: b
IO.print(s.iteratorValue(2)) // expect: c
IO.print(s.iteratorValue(3)) // expect: d

// 8-bit clean.
var t = "a\0b\0c"
IO.print(t.iteratorValue(0) == "a") // expect: true
IO.print(t.iteratorValue(1) == "\0") // expect: true
IO.print(t.iteratorValue(2) == "b") // expect: true
IO.print(t.iteratorValue(3) == "\0") // expect: true
IO.print(t.iteratorValue(4) == "c") // expect: true
