class Outer {
  static staticMethod {
    __field = "outer"
    System.print(__field) // expect: outer

    class Inner {
      static staticMethod {
        __field = "inner"
        System.print(__field) // expect: inner
      }
    }

    Inner.staticMethod
    System.print(__field) // expect: outer
  }

  instanceMethod {
    __field = "outer"
    System.print(__field) // expect: outer

    class Inner {
      instanceMethod {
        __field = "inner"
        System.print(__field) // expect: inner
      }
    }

    Inner.new().instanceMethod
    System.print(__field) // expect: outer
  }
}

Outer.staticMethod
Outer.new().instanceMethod
