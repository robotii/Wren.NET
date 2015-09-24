class A {}
class B is A {}
class C is B {}
var a = A.new()
var b = B.new()
var c = C.new()

System.print(a is A) // expect: true
System.print(a is B) // expect: false
System.print(a is C) // expect: false
System.print(b is A) // expect: true
System.print(b is B) // expect: true
System.print(b is C) // expect: false
System.print(c is A) // expect: true
System.print(c is B) // expect: true
System.print(c is C) // expect: true
