class Foo {
  method {
    return "ok"
    System.print("bad")
  }
}

System.print(Foo.new().method) // expect: ok
