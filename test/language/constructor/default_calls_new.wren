class Foo {
  construct new() {
    System.print("Foo.new()")
  }
}

class Bar is Foo {}

Bar.new() // expect: Foo.new()
