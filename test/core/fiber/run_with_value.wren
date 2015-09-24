var fiber = Fiber.new {
  System.print("fiber")
}

// The first value passed to the fiber is ignored, since there's no yield call
// to return it.
System.print("before")    // expect: before
fiber.run("ignored") // expect: fiber

// This does not get run since we exit when the run fiber completes.
System.print("nope")
