var s = "abçd"
IO.print(s.iterate(null)) // expect: 0
IO.print(s.iterate(0)) // expect: 1
IO.print(s.iterate(1)) // expect: 2
IO.print(s.iterate(2)) // expect: 3
IO.print(s.iterate(3)) // expect: false
IO.print(s.iterate(4)) // expect: false

// Out of bounds.
IO.print(s.iterate(123)) // expect: false
IO.print(s.iterate(-1)) // expect: false

// Nothing to iterate in an empty string.
IO.print("".iterate(null)) // expect: false

// 8-bit clean.
IO.print("a\0b\0c".iterate(null)) // expect: 0
IO.print("a\0b\0c".iterate(0)) // expect: 1
IO.print("a\0b\0c".iterate(1)) // expect: 2
IO.print("a\0b\0c".iterate(2)) // expect: 3
IO.print("a\0b\0c".iterate(3)) // expect: 4
IO.print("a\0b\0c".iterate(4)) // expect: false
