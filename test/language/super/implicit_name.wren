class Base {
  foo {
    System.print("Base.foo")
  }
}

class Derived is Base {
  foo {
    System.print("Derived.foo")
    super
  }
}

Derived.new().foo
// expect: Derived.foo
// expect: Base.foo
