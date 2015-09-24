class Foo {
  construct new() {}
  toString { "Foo" }
}

// Classes get an argument-less "new()" by default.
var foo = Foo.new()
System.print(foo is Foo) // expect: true
System.print(foo.toString) // expect: Foo
