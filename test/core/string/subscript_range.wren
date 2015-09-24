var string = "abcde"
System.print(string[0..0]) // expect: a
System.print(string[1...1] == "") // expect: true
System.print(string[1..2]) // expect: bc
System.print(string[1...2]) // expect: b
System.print(string[2..4]) // expect: cde
System.print(string[2...5]) // expect: cde

// A backwards range reverses.
System.print(string[3..1]) // expect: dcb
System.print(string[3...1]) // expect: dc
System.print(string[3...3] == "") // expect: true

// Negative ranges index from the end.
System.print(string[-5..-2]) // expect: abcd
System.print(string[-5...-2]) // expect: abc
System.print(string[-3..-5]) // expect: cba
System.print(string[-3...-6]) // expect: cba

// Half-negative ranges are treated like the negative value is fixed before
// walking the range.
System.print(string[-5..3]) // expect: abcd
System.print(string[-3...5]) // expect: cde
System.print(string[-2..1]) // expect: dcb
System.print(string[-2...0]) // expect: dcb

System.print(string[1..-2]) // expect: bcd
System.print(string[2...-1]) // expect: cd
System.print(string[4..-5]) // expect: edcba
System.print(string[3...-6]) // expect: dcba

// An empty range at zero is allowed on an empty string.
System.print(""[0...0] == "") // expect: true
System.print(""[0..-1] == "") // expect: true
