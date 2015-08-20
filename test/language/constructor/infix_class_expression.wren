class Foo {
  +(other) { "Foo " + other }
}

IO.print(Foo.new() + "value") // expect: Foo value

// TODO: Other expressions following a constructor, like new Foo.bar("arg").
