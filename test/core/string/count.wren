System.print("".count)   // expect: 0
System.print("a string".count) // expect: 8

// 8-bit clean.
System.print("\0".count)  // expect: 1
System.print("a\0b".count)  // expect: 3
System.print("\0c".count)  // expect: 2
System.print(("a\0b" + "\0c").count)  // expect: 5
