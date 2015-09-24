var foo = "variable"

class Foo {
  foo { "method" }

  method {
    System.print(foo)
  }

  static foo { "class method" }

  static classMethod {
    System.print(foo)
  }
}

Foo.new().method // expect: method
Foo.classMethod  // expect: class method
