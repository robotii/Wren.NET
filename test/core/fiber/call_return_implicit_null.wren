var fiber = Fiber.new {
  System.print("fiber")
}

var result = fiber.call() // expect: fiber
System.print(result)          // expect: null
