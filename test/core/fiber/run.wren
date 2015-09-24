var fiber = Fiber.new {
  System.print("fiber")
}

System.print("before") // expect: before
fiber.run()        // expect: fiber

// This does not get run since we exit when the run fiber completes.
System.print("nope")
