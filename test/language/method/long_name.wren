class Foo {
  thisHasAMethodNameThatIsExactly64CharactersLongWhichIsTheMaximum {
    return "result"
  }
}

System.print(Foo.new().thisHasAMethodNameThatIsExactly64CharactersLongWhichIsTheMaximum) // expect: result
