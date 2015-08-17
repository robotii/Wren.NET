// Returns characters (as strings).
IO.print("abcd"[0]) // expect: a
IO.print("abcd"[1]) // expect: b
IO.print("abcd"[2]) // expect: c
IO.print("abcd"[3]) // expect: d

// Allows indexing backwards from the end.
IO.print("abcd"[-4]) // expect: a
IO.print("abcd"[-3]) // expect: b
IO.print("abcd"[-2]) // expect: c
IO.print("abcd"[-1]) // expect: d

// Regression: Make sure the string's internal buffer size is correct.
IO.print("abcd"[1] == "b") // expect: true

IO.print("something"[0]) // expect: s
IO.print("something"[1]) // expect: o
IO.print("something"[3]) // expect: e
IO.print("something"[6]) // expect: i
IO.print("something T"[10]) // expect: T
IO.print("something"[-1]) // expect: g
IO.print("something"[-2]) // expect: n
IO.print("something"[-4]) // expect: h

// 8-bit clean.
IO.print("a\0b\0c"[0] == "a") // expect: true
IO.print("a\0b\0c"[1] == "\0") // expect: true
IO.print("a\0b\0c"[2] == "b") // expect: true
IO.print("a\0b\0c"[3] == "\0") // expect: true
IO.print("a\0b\0c"[4] == "c") // expect: true
