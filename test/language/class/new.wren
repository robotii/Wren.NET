class Foo {}

var foo = Foo.new()
System.print(foo is Foo) // expect: true

// TODO: Test precedence and grammar of what follows "new".
