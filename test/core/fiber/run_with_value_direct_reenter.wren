var fiber

fiber = Fiber.new {
  System.print(1) // expect: 1
  fiber.run("ignored")
  System.print(2) // expect: 2
}

fiber.call()
System.print(3) // expect: 3