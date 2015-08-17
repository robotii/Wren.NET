class Foo {}

// A class is a class.
IO.print(Foo is Class) // expect: true

// Its metatype is also a class.
IO.print(Foo.type is Class) // expect: true

// The metatype's metatype is Class.
IO.print(Foo.type.type == Class) // expect: true

// And Class's metatype circles back onto itself.
IO.print(Foo.type.type.type == Class) // expect: true
IO.print(Foo.type.type.type.type == Class) // expect: true
IO.print(Foo.type.type.type.type.type == Class) // expect: true
