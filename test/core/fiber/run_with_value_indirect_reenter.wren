var a
var b

a = Fiber.new {
  System.print(2)
  b.run("ignored")
  System.print("nope")
}

b = Fiber.new {
  System.print(1)
  a.run("ignored")
  System.print(3)
}

b.call()
// expect: 1
// expect: 2
// expect: 3
System.print(4) // expect: 4
