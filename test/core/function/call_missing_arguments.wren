var f2 = Fn.new {|a, b| IO.print(a, b) }
f2.call("a") // expect runtime error: Function expects more arguments.
