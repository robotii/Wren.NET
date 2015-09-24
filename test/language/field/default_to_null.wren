class Foo {
  write { System.print(_field) }
}

Foo.new().write // expect: null
