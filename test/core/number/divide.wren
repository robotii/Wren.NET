System.print(8 / 2)         // expect: 4
System.print(12.34 / -0.4)  // expect: -30.85

// Divide by zero.
System.print(3 / 0)         // expect: Infinity
System.print(-3 / 0)        // expect: -Infinity
System.print(0 / 0)         // expect: NaN
System.print(-0 / 0)        // expect: NaN
System.print(3 / -0)        // expect: -Infinity
System.print(-3 / -0)       // expect: Infinity
System.print(0 / -0)        // expect: NaN
System.print(-0 / -0)       // expect: NaN
