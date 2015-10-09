System.print(0.sin)             // expect: 0
System.print((Num.pi / 2).sin)  // expect: 1

// these should of course be 0, but it's not that precise
System.print(Num.pi.sin)        // expect: 1.22460635382238E-16
System.print((2 * Num.pi).sin)  // expect: -2.44921270764475E-16
