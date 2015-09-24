var fiber = Fiber.new {
  System.print("fiber")
  var result = Fiber.yield()
  System.print(result)
}

fiber.call() // expect: fiber
System.print("main") // expect: main
fiber.run("run") // expect: run

// This does not get run since we exit when the run fiber completes.
System.print("nope")
