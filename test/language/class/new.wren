class Foo {}

var foo = new Foo
IO.print(foo is Foo) // expect: true

// TODO: Test precedence and grammar of what follows "new".
