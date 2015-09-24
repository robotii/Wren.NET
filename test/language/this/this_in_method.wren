class Foo {
  bar { this }
  baz { "baz" }
}

System.print(Foo.new().bar.baz) // expect: baz
