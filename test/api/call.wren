class Api {
  static noParams {
    System.print("noParams")
  }

  static zero() {
    System.print("zero")
  }

  static one(one) {
    System.print("one " + one.toString)
  }

  static two(one, two) {
    // Don't print null bytes.
    if (two is String && two.bytes.contains(0)) {
      two = two.bytes.toList
    }

    System.print("two " + one.toString + " " + two.toString)
  }

  static getValue(value) {
    // Return a new value if we aren't given one.
    if (value == null) return ["a", "b"]

    // Otherwise print it.
    System.print(value)
  }
}

// expect: noParams
// expect: zero
// expect: one 1
// expect: two 1 2

// expect: two true false
// expect: two 1.2 3.4
// expect: two 3 4
// expect: two string another
// expect: two null [a, b]
// expect: two str [98, 0, 121, 0, 116, 0, 101]
