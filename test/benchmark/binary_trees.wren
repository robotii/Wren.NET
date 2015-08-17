// Ported from the Python version.

class Tree {
  new(item, depth) {
    _item = item
    if (depth > 0) {
      var item2 = item + item
      depth = depth - 1
      _left = new Tree(item2 - 1, depth)
      _right = new Tree(item2, depth)
    }
  }

  check {
    if (null == _left) {
      return _item
    }

    return _item + _left.check - _right.check
  }
}

var minDepth = 4
var maxDepth = 12
var stretchDepth = maxDepth + 1

var start = IO.clock

IO.print("stretch tree of depth ", stretchDepth, " check: ",
    new Tree(0, stretchDepth).check)

var longLivedTree = new Tree(0, maxDepth)

// iterations = 2 ** maxDepth
var iterations = 1
for (d in 0...maxDepth) {
  iterations = iterations * 2
}

var depth = minDepth
while (depth < stretchDepth) {
  var check = 0
  for (i in 1..iterations) {
    check = check + new Tree(i, depth).check + new Tree(-i, depth).check
  }

  IO.print((iterations * 2), " trees of depth ", depth, " check: ", check)
  iterations = iterations / 4
  depth = depth + 2
}

IO.print("long lived tree of depth ", maxDepth, " check: ", longLivedTree.check)
IO.print("elapsed: ", (IO.clock - start))
