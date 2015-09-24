var fiber = Fiber.new {
  System.print("fiber 1")
  Fiber.yield("yield 1")
  System.print("fiber 2")
  Fiber.yield("yield 2")
  System.print("fiber 3")
}

var result = fiber.call() // expect: fiber 1
System.print(result) // expect: yield 1
result = fiber.call() // expect: fiber 2
System.print(result) // expect: yield 2
result = fiber.call() // expect: fiber 3
System.print(result) // expect: null
