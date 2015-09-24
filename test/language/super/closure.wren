class Base {
  toString { "Base" }
}

class Derived is Base {
  getClosure { Fn.new { super.toString } }
  toString { "Derived" }
}

var closure = Derived.new().getClosure
System.print(closure.call()) // expect: Base
