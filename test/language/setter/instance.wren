class Foo {
  bar=(value) {
    System.print(value)
  }
}

var foo = Foo.new()
foo.bar = "value" // expect: value
