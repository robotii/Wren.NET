#!/usr/bin/env python

from __future__ import print_function

from collections import defaultdict
from os import listdir
from os.path import abspath, basename, dirname, isdir, isfile, join, realpath, relpath, splitext
import re
from subprocess import Popen, PIPE
import sys

# Runs the tests.
WREN_DIR = dirname(dirname(realpath(__file__)))
WREN_APP = join(WREN_DIR, 'wren','bin','Release', 'wren')

EXPECT_PATTERN = re.compile(r'// expect: (.*)')
EXPECT_ERROR_PATTERN = re.compile(r'// expect error')
EXPECT_ERROR_LINE_PATTERN = re.compile(r'// expect error line (\d+)')
EXPECT_RUNTIME_ERROR_PATTERN = re.compile(r'// expect runtime error: (.+)')
ERROR_PATTERN = re.compile(r'\[.* line (\d+)\] Error')
STACK_TRACE_PATTERN = re.compile(r'\[.* line (\d+)\] in')
STDIN_PATTERN = re.compile(r'// stdin: (.*)')
SKIP_PATTERN = re.compile(r'// skip: (.*)')
NONTEST_PATTERN = re.compile(r'// nontest')

passed = 0
failed = 0
skipped = defaultdict(int)
num_skipped = 0
expectations = 0


def color_text(text, color):
  """Converts text to a string and wraps it in the ANSI escape sequence for
  color, if supported."""

  # No ANSI escapes on Windows.
  #if sys.platform == 'win32':
  return str(text)

  #return color + str(text) + '\033[0m'


def green(text):  return color_text(text, '\033[32m')
def pink(text):   return color_text(text, '\033[91m')
def red(text):    return color_text(text, '\033[31m')
def yellow(text): return color_text(text, '\033[33m')


def walk(dir, callback, ignored=None):
  """
  Walks [dir], and executes [callback] on each file unless it is [ignored].
  """

  if not ignored:
    ignored = []
  ignored += [".",".."]

  dir = abspath(dir)
  for file in [file for file in listdir(dir) if not file in ignored]:
    nfile = join(dir, file)
    if isdir(nfile):
      walk(nfile, callback)
    else:
      callback(nfile)


def print_line(line=None):
  # Erase the line.
  print('\033[2K', end='')
  # Move the cursor to the beginning.
  print('\r', end='')
  if line:
    print(line, end='')
    sys.stdout.flush()


def run_script(app, path, type):
  global passed
  global failed
  global skipped
  global num_skipped
  global expectations

  if (splitext(path)[1] != '.wren'):
    return

  # Check if we are just running a subset of the tests.
  if len(sys.argv) == 2:
    this_test = relpath(path, join(WREN_DIR, 'test'))
    if not this_test.startswith(sys.argv[1]):
      return

  # Make a nice short path relative to the working directory.

  # Normalize it to use "/"
  path = relpath(path).replace("\\", "/")

  # Read the test and parse out the expectations.
  expect_output = []
  expect_error = []
  expect_runtime_error_line = 0
  expect_runtime_error = None
  expect_return = 0

  input_lines = []

  print_line('Passed: ' + green(passed) +
             ' Failed: ' + red(failed) +
             ' Skipped: ' + yellow(num_skipped))

  line_num = 1
  with open(path, 'r') as file:
    for line in file:
      match = EXPECT_PATTERN.search(line)
      if match:
        expect_output.append((match.group(1), line_num))
        expectations += 1

      match = EXPECT_ERROR_PATTERN.search(line)
      if match:
        expect_error.append(line_num)
        # If we expect compile errors, it should exit with 65.
        expect_return = 65
        expectations += 1

      match = EXPECT_ERROR_LINE_PATTERN.search(line)
      if match:
        expect_error.append(int(match.group(1)))
        # If we expect compile errors, it should exit with E65.
        expect_return = 65
        expectations += 1

      match = EXPECT_RUNTIME_ERROR_PATTERN.search(line)
      if match:
        expect_runtime_error_line = line_num
        expect_runtime_error = match.group(1)
        # If we expect a runtime error, it should exit with 70.
        expect_return = 70
        expectations += 1

      match = STDIN_PATTERN.search(line)
      if match:
        input_lines.append(match.group(1) + '\n')

      match = SKIP_PATTERN.search(line)
      if match:
        num_skipped += 1
        skipped[match.group(1)] += 1
        return

      match = NONTEST_PATTERN.search(line)
      if match:
        # Not a test file at all, so ignore it.
        return

      line_num += 1

  # If any input is fed to the test in stdin, concatetate it into one string.
  input_bytes = None
  if len(input_lines) > 0:
    input_bytes = "".join(input_lines).encode("utf-8")

  # Run the test.
  test_arg = path
  if type == "api test":
    # Just pass the suite name to API tests.
    test_arg = basename(splitext(test_arg)[0])

  print(test_arg)
  
  proc = Popen([app, test_arg], stdin=PIPE, stdout=PIPE, stderr=PIPE)
  (out, err) = proc.communicate(input_bytes)

  fails = []

  try:
    out = out.decode("utf-8").replace('\r\n', '\n')
    err = err.decode("utf-8").replace('\r\n', '\n')
  except:
    fails.append('Error decoding output.')

  # Validate that no unexpected errors occurred.
  if expect_return != 0 and err != '':
    lines = err.split('\n')
    if expect_runtime_error:
      # Make sure we got the right error.
      if lines[0] != expect_runtime_error:
        fails.append('Expected runtime error "' + expect_runtime_error +
          '" and got:')
        fails.append(lines[0])

    else:
      lines = err.split('\n')
      while len(lines) > 0:
        line = lines.pop(0)
        match = ERROR_PATTERN.search(line)
        if match:
          if float(match.group(1)) not in expect_error:
            fails.append('Unexpected error:')
            fails.append(line)
        elif line != '':
          fails.append('Unexpected output on stderr:')
          fails.append(line)
  else:
    for line in expect_error:
      fails.append('Expected error on line ' + str(line) + ' and got none.')
    if expect_runtime_error:
      fails.append('Expected runtime error "' + expect_runtime_error +
          '" and got none.')

  # Validate the exit code.
  if proc.returncode != expect_return:
    fails.append('Expected return code {0} and got {1}. Stderr:'
        .format(expect_return, proc.returncode))
    fails += err.split('\n')
  else:
    # Validate the output.
    expect_index = 0

    # Remove the trailing last empty line.
    out_lines = out.split('\n')
    if out_lines[-1] == '':
      del out_lines[-1]

    for line in out_lines:
      #if sys.version_info < (3, 0):
        #line = line.encode('utf-8')

      if type == "example":
        # Ignore output from examples.
        pass
      elif expect_index >= len(expect_output):
        fails.append('Got output "{0}" when none was expected.'.format(line))
      elif expect_output[expect_index][0] != line:
        fails.append('Expected output "{0}" on line {1} and got "{2}".'.
            format(expect_output[expect_index][0],
                   expect_output[expect_index][1], line))
      expect_index += 1

    while expect_index < len(expect_output):
      fails.append('Missing expected output "{0}" on line {1}.'.
          format(expect_output[expect_index][0],
                 expect_output[expect_index][1]))
      expect_index += 1

  # Display the results.
  if len(fails) == 0:
    passed += 1
  else:
    failed += 1
    print_line(red('FAIL') + ': ' + path)
    print('')
    for fail in fails:
      print('      ' + pink(fail))
    print('')


def run_test(path, example=False):
  run_script(WREN_APP, path, "test")


def run_api_test(path):
  pass


def run_example(path):
  run_script(WREN_APP, path, "example")

walk(join(WREN_DIR, 'test'), run_test, ignored=['benchmark'])

print_line()
if failed == 0:
  print('All ' + green(passed) + ' tests passed (' + str(expectations) +
        ' expectations).')
else:
  print(green(passed) + ' tests passed. ' + red(failed) + ' tests failed.')

for key in sorted(skipped.keys()):
  print('Skipped ' + yellow(skipped[key]) + ' tests: ' + key)

if failed != 0:
  sys.exit(1)
