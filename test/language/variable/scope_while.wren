// Body has its own scope.
var a = "outer"
var i = 0
while ((i = i + 1) <= 1) {
  var a = "inner"
}
IO.print(a) // expect: outer
