// TODO: Is this right? Shouldn't it resolve to this.local?
{
  var local = "local"
  class Foo {
    method {
      System.print(local)
    }
  }

  Foo.new().method // expect: local
}
