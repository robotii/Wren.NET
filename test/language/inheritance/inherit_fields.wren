class Foo {
  foo(a, b) {
    _field1 = a
    _field2 = b
  }

  fooPrint {
    IO.print(_field1)
    IO.print(_field2)
  }
}

class Bar is Foo {
  bar(a, b) {
    _field1 = a
    _field2 = b
  }

  barPrint {
    IO.print(_field1)
    IO.print(_field2)
  }
}

var bar = new Bar
bar.foo("foo 1", "foo 2")
bar.bar("bar 1", "bar 2")

bar.fooPrint
// expect: foo 1
// expect: foo 2

bar.barPrint
// expect: bar 1
// expect: bar 2
