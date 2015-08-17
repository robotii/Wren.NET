var a = [1, 2, 3]
a.removeAt(0)
IO.print(a) // expect: [2, 3]

var b = [1, 2, 3]
b.removeAt(1)
IO.print(b) // expect: [1, 3]

var c = [1, 2, 3]
c.removeAt(2)
IO.print(c) // expect: [1, 2]

// Index backwards from end.
var d = [1, 2, 3]
d.removeAt(-3)
IO.print(d) // expect: [2, 3]

var e = [1, 2, 3]
e.removeAt(-2)
IO.print(e) // expect: [1, 3]

var f = [1, 2, 3]
f.removeAt(-1)
IO.print(f) // expect: [1, 2]

// Return the removed value.
IO.print([3, 4, 5].removeAt(1)) // expect: 4
