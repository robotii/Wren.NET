IO.print("abcd".indexOf("")) // expect: 0
IO.print("abcd".indexOf("cd")) // expect: 2
IO.print("abcd".indexOf("a")) // expect: 0
IO.print("abcd".indexOf("abcd")) // expect: 0
IO.print("abcd".indexOf("abcde")) // expect: -1
IO.print("abab".indexOf("ab")) // expect: 0

// More complex cases.
IO.print("abcdefabcdefg".indexOf("defg")) // expect: 9
IO.print("abcdabcdabcd".indexOf("dab")) // expect: 3
IO.print("abcdabcdabcdabcd".indexOf("dabcdabc")) // expect: 3
IO.print("abcdefg".indexOf("abcdef!")) // expect: -1

// Non-ASCII. Note that it returns byte indices, not code points.
IO.print("søméஃthîng".indexOf("e")) // expect: -1
IO.print("søméஃthîng".indexOf("m")) // expect: 3
IO.print("søméஃthîng".indexOf("thî")) // expect: 9

// 8-bit clean.
IO.print("a\0b\0c".indexOf("\0")) // expect: 1
IO.print("a\0b\0c".indexOf("a")) // expect: 0
IO.print("a\0b\0c".indexOf("b\0c")) // expect: 2
IO.print("a\0b\0c".indexOf("a\0b\0c\0d")) // expect: -1
IO.print("a\0b\0a\0b".indexOf("a\0b")) // expect: 0
