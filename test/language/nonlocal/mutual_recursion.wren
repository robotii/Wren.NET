class Foo {
  static bar { Bar.new() }
}

class Bar {
  static foo { Foo.new() }
}

System.print(Foo.bar) // expect: instance of Bar
System.print(Bar.foo) // expect: instance of Foo
