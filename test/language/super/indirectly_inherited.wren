class A {
  foo {
    System.print("A.foo")
  }
}

class B is A {}

class C is B {
  foo {
    System.print("C.foo")
    super.foo
  }
}

C.new().foo
// expect: C.foo
// expect: A.foo
