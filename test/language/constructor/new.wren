class Foo {
  construct new() { IO.print("zero") }
  construct new(a) { IO.print(a) }
  construct new(a, b) { IO.print(a + b) }

  toString { "Foo" }
}

// Can overload by arity.
Foo.new() // expect: zero
Foo.new("one") // expect: one
Foo.new("one", "two") // expect: onetwo

// Returns the new instance.
var foo = Foo.new() // expect: zero
IO.print(foo is Foo) // expect: true
IO.print(foo.toString) // expect: Foo
