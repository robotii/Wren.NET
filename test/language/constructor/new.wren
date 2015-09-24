class Foo {
  construct new() { System.print("zero") }
  construct new(a) { System.print(a) }
  construct new(a, b) { System.print(a + b) }

  toString { "Foo" }
}

// Can overload by arity.
Foo.new() // expect: zero
Foo.new("one") // expect: one
Foo.new("one", "two") // expect: onetwo

// Returns the new instance.
var foo = Foo.new() // expect: zero
System.print(foo is Foo) // expect: true
System.print(foo.toString) // expect: Foo
