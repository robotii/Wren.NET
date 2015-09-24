var fiber = Fiber.new {
  System.print("fiber")
  return "result"
}

var result = fiber.call() // expect: fiber
System.print(result)          // expect: result
