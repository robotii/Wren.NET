using System;
using System.Collections.Generic;
using System.Globalization;
using Wren.Core.Objects;
using Wren.Core.VM;
using ValueType = Wren.Core.Objects.ValueType;

namespace Wren.Core.Bytecode
{
    public enum TokenType
    {
        LeftParen,
        RightParen,
        LeftBracket,
        RightBracket,
        LeftBrace,
        RightBrace,
        Colon,
        Dot,
        DotDot,
        DotDotDot,
        Comma,
        Star,
        Slash,
        Percent,
        Plus,
        Minus,
        LtLt,
        GtGt,
        Pipe,
        Pipepipe,
        Caret,
        Amp,
        Ampamp,
        Bang,
        Tilde,
        Question,
        Eq,
        Lt,
        Gt,
        LtEq,
        GtEq,
        EqEq,
        BangEq,

        Break,
        Class,
        Construct,
        Else,
        False,
        For,
        Foreign,
        If,
        Import,
        In,
        Is,
        Null,
        Return,
        Static,
        Super,
        This,
        True,
        Var,
        While,

        Field,
        StaticField,
        Name,
        Number,
        String,

        Line,

        Error,
        Eof
    };

    public struct Token
    {
        public TokenType Type;

        // The beginning of the token, pointing directly into the source.
        public int Start;

        // The length of the token in characters.
        public int Length;

        // The 1-based line where the token appears.
        public int Line;
    };

    public class Parser
    {
        public WrenVM vm;

        // The module being parsed.
        public ObjModule Module;

        // Heap-allocated string representing the path to the code being parsed. Used
        // for stack traces.
        public string SourcePath;

        // The source code being parsed.
        public string Source;

        // The beginning of the currently-being-lexed token in [source].
        public int TokenStart;

        // The current character being lexed in [source].
        public int CurrentChar;

        // The 1-based line number of [currentChar].
        public int CurrentLine;

        // The most recently lexed token.
        public Token Current;

        // The most recently consumed/advanced token.
        public Token Previous;

        // If subsequent newline tokens should be discarded.
        public bool SkipNewlines;

        // Whether compile errors should be printed to stderr or discarded.
        public bool PrintErrors;

        // If a syntax or compile error has occurred.
        public bool HasError;

        // A buffer for the unescaped text of the current token if it's a string
        // literal. Unlike the raw token, this will have escape sequences translated
        // to their literal equivalent.
        public string Raw;

        // If a number literal is currently being parsed this will hold its value.
        public double Number;
    };

    struct Local
    {
        // The name of the local variable.
        public string Name;

        // The length of the local variable's name.
        public int Length;

        // The depth in the scope chain that this variable was declared at. Zero is
        // the outermost scope--parameters for a method, or the first local block in
        // top level code. One is the scope within that, etc.
        public int Depth;

        // If this local variable is being used as an upvalue.
        public bool IsUpvalue;
    };

    struct CompilerUpvalue
    {
        // True if this upvalue is capturing a local variable from the enclosing
        // function. False if it's capturing an upvalue.
        public bool IsLocal;

        // The index of the local or upvalue being captured in the enclosing function.
        public int Index;
    };

    class Loop
    {
        // Index of the instruction that the loop should jump back to.
        public int Start;

        // Index of the argument for the Instruction.JUMP_IF instruction used to exit the
        // loop. Stored so we can patch it once we know where the loop ends.
        public int ExitJump;

        // Index of the first instruction of the body of the loop.
        public int Body;

        // Depth of the scope(s) that need to be exited if a break is hit inside the
        // loop.
        public int ScopeDepth;

        // The loop enclosing this one, or null if this is the outermost loop.
        public Loop Enclosing;
    };

    // The different signature syntaxes for different kinds of methods.
    public enum SignatureType
    {
        // A name followed by a (possibly empty) parenthesized parameter list. Also
        // used for binary operators.
        Method,

        // Just a name. Also used for unary operators.
        Getter,

        // A name followed by "=".
        Setter,

        // A square bracketed parameter list.
        Subscript,

        // A square bracketed parameter list followed by "=".
        SubscriptSetter,

        // A constructor initializer function. This has a distinct signature to
        // prevent it from being invoked directly outside of the constructor on the
        // metaclass.
        Initializer
    };

    public class Signature
    {
        public string Name;
        public int Length;
        public SignatureType Type;
        public int Arity;
    };

    class ClassCompiler
    {
        // Symbol table for the fields of the class.
        public List<string> Fields;

        // True if the class being compiled is a foreign class.
        public bool IsForeign;

        // True if the current method being compiled is static.
        public bool IsStaticMethod;

        // The signature of the method being compiled.
        public Signature Signature;
    };

    public class Compiler
    {
        private readonly Parser _parser;
        public const int MaxLocals = 255;
        public const int MaxUpvalues = 255;
        public const int MaxConstants = (1 << 16);
        public const int MaxVariableName = 64;
        public const int MaxMethodSignature = 128;
        public const int MaxMethodName = 64;
        public const int MaxFields = 255;
        public const int MaxParameters = 16;

        private readonly Compiler _parent;
        private readonly List<Obj> _constants = new List<Obj>();
        private readonly Local[] _locals = new Local[MaxLocals + 1];
        private int _numLocals;

        private readonly CompilerUpvalue[] _upvalues = new CompilerUpvalue[MaxUpvalues];
        private int _numUpValues;

        private int _numParams;
        private int _scopeDepth;

        private Loop _loop;
        private ClassCompiler _enclosingClass;

        private readonly List<byte> _bytecode;

        private readonly int[] _debugSourceLines;

        private static void LexError(Parser parser, string format)
        {
            parser.HasError = true;
            if (!parser.PrintErrors) return;

            Console.Error.Write("[{0} line {1}] Error: ", parser.SourcePath, parser.CurrentLine);

            Console.Error.WriteLine(format);
        }

        private void Error(string format)
        {
            _parser.HasError = true;
            if (!_parser.PrintErrors) return;

            Token token = _parser.Previous;

            // If the parse error was caused by an error token, the lexer has already
            // reported it.
            if (token.Type == TokenType.Error) return;

            Console.Error.Write("[{0} line {1}] Error at ", _parser.SourcePath, token.Line);

            switch (token.Type)
            {
                case TokenType.Line:
                    Console.Error.Write("newline: ");
                    break;
                case TokenType.Eof:
                    Console.Error.Write("end of file: ");
                    break;
                default:
                    Console.Error.Write("'{0}': ", _parser.Source.Substring(token.Start, token.Length));
                    break;
            }

            Console.Error.WriteLine(format);
        }

        // Adds [constant] to the constant pool and returns its index.
        private int AddConstant(Obj constant)
        {
            // TODO: attempt to consolidate constants

            if (_constants.Count < MaxConstants)
            {
                _constants.Add(constant);
            }
            else
            {
                Error(string.Format("A function may only contain {0} unique constants.", MaxConstants));
            }

            return _constants.Count - 1;
        }

        // Initializes [compiler].
        public Compiler(Parser parser, Compiler parent, bool isFunction)
        {
            _parser = parser;
            _parent = parent;

            // Initialize this to null before allocating in case a GC gets triggered in
            // the middle of initializing the compiler.
            _constants = new List<Obj>();

            _numUpValues = 0;
            _numParams = 0;
            _loop = null;
            _enclosingClass = null;

            parser.vm.Compiler = this;

            if (parent == null)
            {
                _numLocals = 0;

                // Compiling top-level code, so the initial scope is module-level.
                _scopeDepth = -1;
            }
            else
            {
                // Declare a fake local variable for the receiver so that it's slot in the
                // stack is taken. For methods, we call this "this", so that we can resolve
                // references to that like a normal variable. For functions, they have no
                // explicit "this". So we pick a bogus name. That way references to "this"
                // inside a function will try to walk up the parent chain to find a method
                // enclosing the function whose "this" we can close over.
                _numLocals = 1;
                if (isFunction)
                {
                    _locals[0].Name = null;
                    _locals[0].Length = 0;
                }
                else
                {
                    _locals[0].Name = "this";
                    _locals[0].Length = 4;
                }
                _locals[0].Depth = -1;
                _locals[0].IsUpvalue = false;

                // The initial scope for function or method is a local scope.
                _scopeDepth = 0;
            }

            _bytecode = new List<byte>();
            _debugSourceLines = new int[16000];
        }

        // Lexing ----------------------------------------------------------------------

        // Returns true if [c] is a valid (non-initial) identifier character.
        static bool IsName(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
        }

        // Returns true if [c] is a digit.
        static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        // Returns the current character the parser is sitting on.
        static char PeekChar(Parser parser)
        {
            return parser.CurrentChar < parser.Source.Length ? parser.Source[parser.CurrentChar] : '\0';
        }

        // Returns the character after the current character.
        static char PeekNextChar(Parser parser)
        {
            // If we're at the end of the source, don't read past it.
            return parser.CurrentChar >= parser.Source.Length - 1 ? '\0' : parser.Source[parser.CurrentChar + 1];
        }

        // Advances the parser forward one character.
        static char NextChar(Parser parser)
        {
            char c = PeekChar(parser);
            parser.CurrentChar++;
            if (c == '\n') parser.CurrentLine++;
            return c;
        }

        // Sets the parser's current token to the given [type] and current character
        // range.
        static void MakeToken(Parser parser, TokenType type)
        {
            parser.Current.Type = type;
            parser.Current.Start = parser.TokenStart;
            parser.Current.Length = parser.CurrentChar - parser.TokenStart;
            parser.Current.Line = parser.CurrentLine;

            // Make line tokens appear on the line containing the "\n".
            if (type == TokenType.Line) parser.Current.Line--;
        }

        // If the current character is [c], then consumes it and makes a token of type
        // [two]. Otherwise makes a token of type [one].
        static void TwoCharToken(Parser parser, char c, TokenType two, TokenType one)
        {
            if (PeekChar(parser) == c)
            {
                NextChar(parser);
                MakeToken(parser, two);
                return;
            }

            MakeToken(parser, one);
        }

        // Skips the rest of the current line.
        static void SkipLineComment(Parser parser)
        {
            while (PeekChar(parser) != '\n' && PeekChar(parser) != '\0')
            {
                NextChar(parser);
            }
        }

        // Skips the rest of a block comment.
        static void SkipBlockComment(Parser parser)
        {
            NextChar(parser); // The opening "*".

            int nesting = 1;
            while (nesting > 0)
            {
                char c = PeekChar(parser);
                if (c == '\0')
                {
                    LexError(parser, "Unterminated block comment.");
                    return;
                }

                if (c == '/' && PeekNextChar(parser) == '*')
                {
                    NextChar(parser);
                    NextChar(parser);
                    nesting++;
                    continue;
                }
                if (c == '*' && PeekNextChar(parser) == '/')
                {
                    NextChar(parser);
                    NextChar(parser);
                    nesting--;
                    continue;
                }

                // Regular comment character.
                NextChar(parser);
            }
        }

        static string GetTokenString(Parser parser)
        {
            return parser.Source.Substring(parser.TokenStart, parser.CurrentChar - parser.TokenStart);
        }

        static int ReadHexDigit(Parser parser)
        {
            char c = NextChar(parser);
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;

            // Don't consume it if it isn't expected. Keeps us from reading past the end
            // of an unterminated string.
            parser.CurrentChar--;
            return -1;
        }

        // Parses the numeric value of the current token.
        static void MakeNumber(Parser parser, bool isHex)
        {
            string s = GetTokenString(parser);
            try
            {
                parser.Number = isHex ? Convert.ToInt32(s, 16) : Convert.ToDouble(s, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                LexError(parser, "Number too big");
            }
            MakeToken(parser, TokenType.Number);
        }

        // Finishes lexing a hexadecimal number literal.
        static void ReadHexNumber(Parser parser)
        {
            // Skip past the `x` used to denote a hexadecimal literal.
            NextChar(parser);

            // Iterate over all the valid hexadecimal digits found.
            while (ReadHexDigit(parser) != -1)
            {
            }

            MakeNumber(parser, true);
        }

        // Finishes lexing a number literal.
        static void ReadNumber(Parser parser)
        {
            while (IsDigit(PeekChar(parser))) NextChar(parser);

            // See if it has a floating point. Make sure there is a digit after the "."
            // so we don't get confused by method calls on number literals.
            if (PeekChar(parser) == '.' && IsDigit(PeekNextChar(parser)))
            {
                NextChar(parser);
                while (IsDigit(PeekChar(parser))) NextChar(parser);
            }

            // See if the number is in scientific notation
            if (PeekChar(parser) == 'e' || PeekChar(parser) == 'E')
            {
                NextChar(parser);

                // if the exponant is negative
                if (PeekChar(parser) == '-') NextChar(parser);

                if (!IsDigit(PeekChar(parser)))
                {
                    LexError(parser, "Unterminated scientific notation.");
                }

                while (IsDigit(PeekChar(parser))) NextChar(parser);
            }

            MakeNumber(parser, false);
        }

        // Finishes lexing an identifier. Handles reserved words.
        static void ReadName(Parser parser, TokenType type)
        {
            while (IsName(PeekChar(parser)) || IsDigit(PeekChar(parser)))
            {
                NextChar(parser);
            }

            string tokenName = GetTokenString(parser);

            switch (tokenName)
            {
                case "break":
                    type = TokenType.Break;
                    break;
                case "class":
                    type = TokenType.Class;
                    break;
                case "construct":
                    type = TokenType.Construct;
                    break;
                case "else":
                    type = TokenType.Else;
                    break;
                case "false":
                    type = TokenType.False;
                    break;
                case "for":
                    type = TokenType.For;
                    break;
                case "foreign":
                    type = TokenType.Foreign;
                    break;
                case "if":
                    type = TokenType.If;
                    break;
                case "import":
                    type = TokenType.Import;
                    break;
                case "in":
                    type = TokenType.In;
                    break;
                case "is":
                    type = TokenType.Is;
                    break;
                case "null":
                    type = TokenType.Null;
                    break;
                case "return":
                    type = TokenType.Return;
                    break;
                case "static":
                    type = TokenType.Static;
                    break;
                case "super":
                    type = TokenType.Super;
                    break;
                case "this":
                    type = TokenType.This;
                    break;
                case "true":
                    type = TokenType.True;
                    break;
                case "var":
                    type = TokenType.Var;
                    break;
                case "while":
                    type = TokenType.While;
                    break;
            }

            MakeToken(parser, type);
        }

        // Adds [c] to the current string literal being tokenized.
        static void AddStringChar(Parser parser, char c)
        {
            parser.Raw += c;
        }

        // Reads [digits] hex digits in a string literal and returns their number value.
        int ReadHexEscape(int digits, string description)
        {
            int value = 0;
            for (int i = 0; i < digits; i++)
            {
                if (PeekChar(_parser) == '"' || PeekChar(_parser) == '\0')
                {
                    Error(string.Format("Incomplete {0} escape sequence.", description));

                    // Don't consume it if it isn't expected. Keeps us from reading past the
                    // end of an unterminated string.
                    _parser.CurrentChar--;
                    break;
                }

                int digit = ReadHexDigit(_parser);
                if (digit == -1)
                {
                    Error(string.Format("Invalid {0} escape sequence.", description));
                    break;
                }

                value = (value * 16) | digit;
            }

            return value;
        }

        // Reads a four hex digit Unicode escape sequence in a string literal.
        void ReadUnicodeEscape()
        {
            // Read the next four characters and parse them into a unicode value (char)
            int i = ReadHexEscape(4, "unicode");
            AddStringChar(_parser, Convert.ToChar(i));
        }

        // Finishes lexing a string literal.
        void ReadString()
        {
            for (; ; )
            {
                char c = NextChar(_parser);
                if (c == '"') break;

                if (c == '\0')
                {
                    LexError(_parser, "Unterminated string.");

                    // Don't consume it if it isn't expected. Keeps us from reading past the
                    // end of an unterminated string.
                    _parser.CurrentChar--;
                    break;
                }

                if (c == '\\')
                {
                    switch (NextChar(_parser))
                    {
                        case '"': AddStringChar(_parser, '"'); break;
                        case '\\': AddStringChar(_parser, '\\'); break;
                        case '0': AddStringChar(_parser, '\0'); break;
                        case 'a': AddStringChar(_parser, '\a'); break;
                        case 'b': AddStringChar(_parser, '\b'); break;
                        case 'f': AddStringChar(_parser, '\f'); break;
                        case 'n': AddStringChar(_parser, '\n'); break;
                        case 'r': AddStringChar(_parser, '\r'); break;
                        case 't': AddStringChar(_parser, '\t'); break;
                        case 'u': ReadUnicodeEscape(); break;
                        // TODO: 'U' for 8 octet Unicode escapes.
                        case 'v': AddStringChar(_parser, '\v'); break;
                        case 'x':
                            AddStringChar(_parser, (char)(0xFF & ReadHexEscape(2, "byte")));
                            break;

                        default:
                            LexError(_parser, string.Format("Invalid escape character '{0}'.", _parser.Source[_parser.CurrentChar - 1]));
                            break;
                    }
                }
                else
                {
                    AddStringChar(_parser, c);
                }
            }

            MakeToken(_parser, TokenType.String);
        }

        // Lex the next token and store it in [parser.current].
        void NextToken()
        {
            _parser.Previous = _parser.Current;

            // If we are out of tokens, don't try to tokenize any more. We *do* still
            // copy the TOKEN_EOF to previous so that code that expects it to be consumed
            // will still work.
            if (_parser.Current.Type == TokenType.Eof) return;

            while (PeekChar(_parser) != '\0')
            {
                _parser.TokenStart = _parser.CurrentChar;

                char c = NextChar(_parser);
                switch (c)
                {
                    case '(':
                        MakeToken(_parser, TokenType.LeftParen);
                        return;
                    case ')':
                        MakeToken(_parser, TokenType.RightParen);
                        return;
                    case '[':
                        MakeToken(_parser, TokenType.LeftBracket);
                        return;
                    case ']':
                        MakeToken(_parser, TokenType.RightBracket);
                        return;
                    case '{':
                        MakeToken(_parser, TokenType.LeftBrace);
                        return;
                    case '}':
                        MakeToken(_parser, TokenType.RightBrace);
                        return;
                    case ':':
                        MakeToken(_parser, TokenType.Colon);
                        return;
                    case '.':
                        if (PeekChar(_parser) == '.')
                        {
                            NextChar(_parser);
                            if (PeekChar(_parser) == '.')
                            {
                                NextChar(_parser);
                                MakeToken(_parser, TokenType.DotDotDot);
                                return;
                            }

                            MakeToken(_parser, TokenType.DotDot);
                            return;
                        }

                        MakeToken(_parser, TokenType.Dot);
                        return;

                    case ',':
                        MakeToken(_parser, TokenType.Comma);
                        return;
                    case '*':
                        MakeToken(_parser, TokenType.Star);
                        return;
                    case '%':
                        MakeToken(_parser, TokenType.Percent);
                        return;
                    case '+':
                        MakeToken(_parser, TokenType.Plus);
                        return;
                    case '~':
                        MakeToken(_parser, TokenType.Tilde);
                        return;
                    case '?':
                        MakeToken(_parser, TokenType.Question);
                        return;
                    case '/':
                        if (PeekChar(_parser) == '/')
                        {
                            SkipLineComment(_parser);
                            break;
                        }

                        if (PeekChar(_parser) == '*')
                        {
                            SkipBlockComment(_parser);
                            break;
                        }

                        MakeToken(_parser, TokenType.Slash);
                        return;

                    case '-':
                        MakeToken(_parser, TokenType.Minus);
                        return;

                    case '|':
                        TwoCharToken(_parser, '|', TokenType.Pipepipe, TokenType.Pipe);
                        return;

                    case '&':
                        TwoCharToken(_parser, '&', TokenType.Ampamp, TokenType.Amp);
                        return;

                    case '^':
                        MakeToken(_parser, TokenType.Caret);
                        return;

                    case '=':
                        TwoCharToken(_parser, '=', TokenType.EqEq, TokenType.Eq);
                        return;

                    case '<':
                        if (PeekChar(_parser) == '<')
                        {
                            NextChar(_parser);
                            MakeToken(_parser, TokenType.LtLt);
                            return;
                        }

                        TwoCharToken(_parser, '=', TokenType.LtEq, TokenType.Lt);
                        return;

                    case '>':
                        if (PeekChar(_parser) == '>')
                        {
                            NextChar(_parser);
                            MakeToken(_parser, TokenType.GtGt);
                            return;
                        }

                        TwoCharToken(_parser, '=', TokenType.GtEq, TokenType.Gt);
                        return;

                    case '!':
                        TwoCharToken(_parser, '=', TokenType.BangEq, TokenType.Bang);
                        return;

                    case '\n':
                        MakeToken(_parser, TokenType.Line);
                        return;

                    case ' ':
                    case '\r':
                    case '\t':
                        // Skip forward until we run out of whitespace.
                        while (PeekChar(_parser) == ' ' ||
                               PeekChar(_parser) == '\r' ||
                               PeekChar(_parser) == '\t')
                        {
                            NextChar(_parser);
                        }
                        break;

                    case '"': ReadString();
                        return;
                    case '_':
                        ReadName(_parser, PeekChar(_parser) == '_' ? TokenType.StaticField : TokenType.Field);
                        return;

                    case '@':
                        ReadName(_parser, PeekChar(_parser) == '@' ? TokenType.StaticField : TokenType.Field);
                        return;

                    case '#':
                        // Ignore shebang on the first line.
                        if (PeekChar(_parser) == '!' && _parser.CurrentLine == 1)
                        {
                            SkipLineComment(_parser);
                            break;
                        }

                        LexError(_parser, string.Format("Invalid character '{0}'.", c));
                        return;

                    case '0':
                        if (PeekChar(_parser) == 'x')
                        {
                            ReadHexNumber(_parser);
                            return;
                        }

                        ReadNumber(_parser);
                        return;

                    default:
                        if (IsName(c))
                        {
                            ReadName(_parser, TokenType.Name);
                        }
                        else if (IsDigit(c))
                        {
                            ReadNumber(_parser);
                        }
                        else
                        {
                            LexError(_parser, string.Format("Invalid character '{0}'.", c));
                        }
                        return;
                }
            }

            // If we get here, we're out of source, so just make EOF tokens.
            _parser.TokenStart = _parser.CurrentChar;
            MakeToken(_parser, TokenType.Eof);
        }

        // Returns the type of the current token.
        private TokenType Peek()
        {
            return _parser.Current.Type;
        }

        // Consumes the current token if its type is [expected]. Returns true if a
        // token was consumed.
        private bool Match(TokenType expected)
        {
            if (Peek() != expected) return false;

            NextToken();
            return true;
        }

        // Consumes the current token. Emits an error if its type is not [expected].
        private void Consume(TokenType expected, string errorMessage)
        {
            NextToken();
            if (_parser.Previous.Type != expected)
            {
                Error(errorMessage);

                // If the next token is the one we want, assume the current one is just a
                // spurious error and discard it to minimize the number of cascaded errors.
                if (_parser.Current.Type == expected) NextToken();
            }
        }

        // Matches one or more newlines. Returns true if at least one was found.
        private bool MatchLine()
        {
            if (!Match(TokenType.Line)) return false;

            while (Match(TokenType.Line))
            {
            }
            return true;
        }

        // Consumes the current token if its type is [expected]. Returns true if a
        // token was consumed. Since [expected] is known to be in the middle of an
        // expression, any newlines following it are consumed and discarded.
        private void IgnoreNewlines()
        {
            MatchLine();
        }

        // Consumes the current token. Emits an error if it is not a newline. Then
        // discards any duplicate newlines following it.
        private void ConsumeLine(string errorMessage)
        {
            Consume(TokenType.Line, errorMessage);
            IgnoreNewlines();
        }

        // Variables and scopes --------------------------------------------------------
        #region Variables and scopes

        private int Emit(int b)
        {
            _bytecode.Add((byte)b);
            return _bytecode.Count - 1;
        }

        private void Emit(Instruction b)
        {
            Emit((byte)b);
        }

        // Emits one 16-bit argument, which will be written big endian.
        private void EmitShort(int arg)
        {
            Emit((arg >> 8) & 0xff);
            Emit(arg & 0xff);
        }

        // Emits one bytecode instruction followed by a 8-bit argument. Returns the
        // index of the argument in the bytecode.
        private int EmitByteArg(Instruction instruction, int arg)
        {
            Emit(instruction);
            return Emit(arg);
        }

        // Emits one bytecode instruction followed by a 16-bit argument, which will be
        // written big endian.
        private void EmitShortArg(Instruction instruction, int arg)
        {
            Emit(instruction);
            EmitShort(arg);
        }

        // Emits [instruction] followed by a placeholder for a jump offset. The
        // placeholder can be patched by calling [jumpPatch]. Returns the index of the
        // placeholder.
        private int EmitJump(Instruction instruction)
        {
            Emit(instruction);
            Emit(0xff);
            return Emit(0xff) - 1;
        }

        // Create a new local variable with [name]. Assumes the current scope is local
        // and the name is unique.
        private int DefineLocal(string name, int length)
        {
            Local local = new Local { Name = name, Length = length, Depth = _scopeDepth, IsUpvalue = false };
            _locals[_numLocals] = local;
            return _numLocals++;
        }

        // Declares a variable in the current scope whose name is the given token.
        //
        // If [token] is `null`, uses the previously consumed token. Returns its symbol.
        private int DeclareVariable(Token? token)
        {
            if (token == null) token = _parser.Previous;

            Token t = token.Value;

            if (t.Length > MaxVariableName)
            {
                Error(string.Format("Variable name cannot be longer than {0} characters.", MaxVariableName));
            }

            // Top-level module scope.
            if (_scopeDepth == -1)
            {
                int symbol = _parser.vm.DefineVariable(_parser.Module, _parser.Source.Substring(t.Start, t.Length), new Obj(ValueType.Null));

                switch (symbol)
                {
                    case -1:
                        Error("Module variable is already defined.");
                        break;
                    case -2:
                        Error("Too many module variables defined.");
                        break;
                }

                return symbol;
            }

            string tokenName = _parser.Source.Substring(t.Start, t.Length);

            // See if there is already a variable with this name declared in the current
            // scope. (Outer scopes are OK: those get shadowed.)
            for (int i = _numLocals - 1; i >= 0; i--)
            {
                Local local = _locals[i];
                // Once we escape this scope and hit an outer one, we can stop.
                if (local.Depth < _scopeDepth) break;

                if (local.Length == t.Length && tokenName == local.Name)
                {
                    Error(string.Format("Variable '{0}' is already declared in this scope.", local.Name));
                    return i;
                }
            }

            if (_numLocals > MaxLocals)
            {
                Error(string.Format("Cannot declare more than {0} variables in one scope.", MaxLocals));
                return -1;
            }

            return DefineLocal(tokenName, t.Length);
        }

        // Parses a name token and declares a variable in the current scope with that
        // name. Returns its slot.
        private int DeclareNamedVariable()
        {
            Consume(TokenType.Name, "Expect variable name.");
            return DeclareVariable(null);
        }

        // Stores a variable with the previously defined symbol in the current scope.
        private void DefineVariable(int symbol)
        {
            // Store the variable. If it's a local, the result of the initializer is
            // in the correct slot on the stack already so we're done.
            if (_scopeDepth >= 0) return;

            // It's a module-level variable, so store the value in the module slot and
            // then discard the temporary for the initializer.
            EmitShortArg(Instruction.STORE_MODULE_VAR, symbol);
            Emit(Instruction.POP);
        }

        // Starts a new local block scope.
        private void PushScope()
        {
            _scopeDepth++;
        }

        // Generates code to discard local variables at [depth] or greater. Does *not*
        // actually undeclare variables or pop any scopes, though. This is called
        // directly when compiling "break" statements to ditch the local variables
        // before jumping out of the loop even though they are still in scope *past*
        // the break instruction.
        //
        // Returns the number of local variables that were eliminated.
        private int DiscardLocals(int depth)
        {
            //ASSERT(compiler.scopeDepth > -1, "Cannot exit top-level scope.");

            int local = _numLocals - 1;
            while (local >= 0 && _locals[local].Depth >= depth)
            {
                // If the local was closed over, make sure the upvalue gets closed when it
                // goes out of scope on the stack.
                Emit(_locals[local].IsUpvalue ? Instruction.CLOSE_UPVALUE : Instruction.POP);

                local--;
            }

            return _numLocals - local - 1;
        }

        // Closes the last pushed block scope and discards any local variables declared
        // in that scope. This should only be called in a statement context where no
        // temporaries are still on the stack.
        private void PopScope()
        {
            _numLocals -= DiscardLocals(_scopeDepth);
            _scopeDepth--;
        }

        // Attempts to look up the name in the local variables of [compiler]. If found,
        // returns its index, otherwise returns -1.
        private int ResolveLocal(string name, int length)
        {
            // Look it up in the local scopes. Look in reverse order so that the most
            // nested variable is found first and shadows outer ones.
            for (int i = _numLocals - 1; i >= 0; i--)
            {
                if (_locals[i].Length == length && name == _locals[i].Name)
                {
                    return i;
                }
            }

            return -1;
        }

        // Adds an upvalue to [compiler]'s function with the given properties. Does not
        // add one if an upvalue for that variable is already in the list. Returns the
        // index of the uvpalue.
        private int AddUpvalue(bool isLocal, int index)
        {
            // Look for an existing one.
            for (int i = 0; i < _numUpValues; i++)
            {
                CompilerUpvalue upvalue = _upvalues[i];
                if (upvalue.Index == index && upvalue.IsLocal == isLocal) return i;
            }

            // If we got here, it's a new upvalue.
            _upvalues[_numUpValues].IsLocal = isLocal;
            _upvalues[_numUpValues].Index = index;
            return _numUpValues++;
        }

        // Attempts to look up [name] in the functions enclosing the one being compiled
        // by [compiler]. If found, it adds an upvalue for it to this compiler's list
        // of upvalues (unless it's already in there) and returns its index. If not
        // found, returns -1.
        //
        // If the name is found outside of the immediately enclosing function, this
        // will flatten the closure and add upvalues to all of the intermediate
        // functions so that it gets walked down to this one.
        //
        // If it reaches a method boundary, this stops and returns -1 since methods do
        // not close over local variables.
        private int FindUpvalue(string name, int length)
        {
            // If we are at a method boundary or the top level, we didn't find it.
            if (_parent == null || _enclosingClass != null) return -1;

            // See if it's a local variable in the immediately enclosing function.
            int local = _parent.ResolveLocal(name, length);
            if (local != -1)
            {
                // Mark the local as an upvalue so we know to close it when it goes out of
                // scope.
                _parent._locals[local].IsUpvalue = true;

                return AddUpvalue(true, local);
            }

            // See if it's an upvalue in the immediately enclosing function. In other
            // words, if it's a local variable in a non-immediately enclosing function.
            // This "flattens" closures automatically: it adds upvalues to all of the
            // intermediate functions to get from the function where a local is declared
            // all the way into the possibly deeply nested function that is closing over
            // it.
            int upvalue = _parent.FindUpvalue(name, length);
            if (upvalue != -1)
            {
                return AddUpvalue(false, upvalue);
            }

            // If we got here, we walked all the way up the parent chain and couldn't
            // find it.
            return -1;
        }

        // Look up [name] in the current scope to see what name it is bound to. Returns
        // the index of the name either in local scope, or the enclosing function's
        // upvalue list. Does not search the module scope. Returns -1 if not found.
        //
        // Sets [loadInstruction] to the instruction needed to load the variable. Will
        // be [Instruction.LOAD_LOCAL] or [Instruction.LOAD_UPVALUE].
        private int ResolveNonmodule(string name, int length, out Instruction loadInstruction)
        {
            // Look it up in the local scopes. Look in reverse order so that the most
            // nested variable is found first and shadows outer ones.
            loadInstruction = Instruction.LOAD_LOCAL;
            int local = ResolveLocal(name, length);
            if (local != -1) return local;

            // If we got here, it's not a local, so lets see if we are closing over an
            // outer local.
            loadInstruction = Instruction.LOAD_UPVALUE;
            return FindUpvalue(name, length);
        }

        // Look up [name] in the current scope to see what name it is bound to. Returns
        // the index of the name either in module scope, local scope, or the enclosing
        // function's upvalue list. Returns -1 if not found.
        //
        // Sets [loadInstruction] to the instruction needed to load the variable. Will
        // be one of [Instruction.LOAD_LOCAL], [Instruction.LOAD_UPVALUE], or [Instruction.LOAD_MODULE_VAR].
        private int ResolveName(string name, int length, out Instruction loadInstruction)
        {
            int nonmodule = ResolveNonmodule(name, length, out loadInstruction);
            if (nonmodule != -1) return nonmodule;

            loadInstruction = Instruction.LOAD_MODULE_VAR;
            return _parser.Module.Variables.FindIndex(v => v.Name == name);
        }

        private void LoadLocal(int slot)
        {
            if (slot <= 8)
            {
                Emit(Instruction.LOAD_LOCAL_0 + slot);
                return;
            }

            EmitByteArg(Instruction.LOAD_LOCAL, slot);
        }

        // Finishes [compiler], which is compiling a function, method, or chunk of top
        // level code. If there is a parent compiler, then this emits code in the
        // parent compiler to load the resulting function.
        private ObjFn EndCompiler(string debugName)
        {
            // If we hit an error, don't bother creating the function since it's borked
            // anyway.
            if (_parser.HasError)
            {
                _parser.vm.Compiler = _parent;
                return null;
            }

            // Mark the end of the bytecode. Since it may contain multiple early returns,
            // we can't rely on Instruction.RETURN to tell us we're at the end.
            Emit(Instruction.END);

            // Create a function object for the code we just compiled.
            ObjFn fn = new ObjFn(_parser.Module,
                                        _constants.ToArray(),
                                        _numUpValues,
                                        _numParams,
                                        _bytecode.ToArray(),
                                        new ObjString(_parser.SourcePath),
                                        debugName,
                                        _debugSourceLines);

            // In the function that contains this one, load the resulting function object.
            if (_parent != null)
            {
                int constant = _parent.AddConstant(fn);

                // If the function has no upvalues, we don't need to create a closure.
                // We can just load and run the function directly.
                if (_numUpValues == 0)
                {
                    _parent.EmitShortArg(Instruction.CONSTANT, constant);
                }
                else
                {
                    // Capture the upvalues in the new closure object.
                    _parent.EmitShortArg(Instruction.CLOSURE, constant);

                    // Emit arguments for each upvalue to know whether to capture a local or
                    // an upvalue.
                    // TODO: Do something more efficient here?
                    for (int i = 0; i < _numUpValues; i++)
                    {
                        _parent.Emit(_upvalues[i].IsLocal ? 1 : 0);
                        _parent.Emit(_upvalues[i].Index);
                    }
                }
            }

            // Pop this compiler off the stack.
            _parser.vm.Compiler = _parent;

            return fn;
        }


        // Grammar ---------------------------------------------------------------------

        private enum Precedence
        {
            None,
            Lowest,
            Assignment, // =
            Ternary, // ?:
            LogicalOr, // ||
            LogicalAnd, // &&
            Equality, // == !=
            Is, // is
            Comparison, // < > <= >=
            BitwiseOr, // |
            BitwiseXor, // ^
            BitwiseAnd, // &
            BitwiseShift, // << >>
            Range, // .. ...
            Term, // + -
            Factor, // * / %
            Unary, // unary - ! ~
            Call, // . () []
            Primary
        };

        private delegate void GrammarFn(Compiler c, bool allowAssignment);

        private delegate void SignatureFn(Compiler compiler, Signature signature);

        private struct GrammarRule
        {
            public readonly GrammarFn Prefix;
            public readonly GrammarFn Infix;
            public readonly SignatureFn Method;
            public readonly Precedence Precedence;
            public readonly string Name;

            public GrammarRule(GrammarFn prefix, GrammarFn infix, SignatureFn method, Precedence precedence, string name)
            {
                Prefix = prefix;
                Infix = infix;
                Method = method;
                Precedence = precedence;
                Name = name;
            }
        };

        // Replaces the placeholder argument for a previous Instruction.JUMP or Instruction.JUMP_IF
        // instruction with an offset that jumps to the current end of bytecode.
        private void PatchJump(int offset)
        {
            // -2 to adjust for the bytecode for the jump offset itself.
            int jump = _bytecode.Count - offset - 2;
            // TODO: Check for overflow.
            _bytecode[offset] = (byte)((jump >> 8) & 0xff);
            _bytecode[offset + 1] = (byte)(jump & 0xff);
        }

        // Parses a block body, after the initial "{" has been consumed.
        //
        // Returns true if it was a expression body, false if it was a statement body.
        // (More precisely, returns true if a value was left on the stack. An empty
        // block returns false.)
        private bool FinishBlock()
        {
            // Empty blocks do nothing.
            if (Match(TokenType.RightBrace))
            {
                return false;
            }

            // If there's no line after the "{", it's a single-expression body.
            if (!MatchLine())
            {
                Expression();
                Consume(TokenType.RightBrace, "Expect '}' at end of block.");
                return true;
            }

            // Empty blocks (with just a newline inside) do nothing.
            if (Match(TokenType.RightBrace))
            {
                return false;
            }

            // Compile the definition list.
            do
            {
                Definition();

                // If we got into a weird error state, don't get stuck in a loop.
                if (Peek() == TokenType.Eof) return true;

                ConsumeLine("Expect newline after statement.");
            }
            while (!Match(TokenType.RightBrace));
            return false;
        }

        // Parses a method or function body, after the initial "{" has been consumed.
        private void FinishBody(bool isConstructor)
        {
            bool isExpressionBody = FinishBlock();

            if (isConstructor)
            {
                // If the constructor body evaluates to a value, discard it.
                if (isExpressionBody)
                    Emit(Instruction.POP);

                // The receiver is always stored in the first local slot.
                Emit(Instruction.LOAD_LOCAL_0);
            }
            else if (!isExpressionBody)
            {
                // Implicitly return null in statement bodies.
                Emit(Instruction.NULL);
            }

            Emit(Instruction.RETURN);
        }

        // The VM can only handle a certain number of parameters, so check that we
        // haven't exceeded that and give a usable error.
        private void ValidateNumParameters(int numArgs)
        {
            if (numArgs == MaxParameters + 1)
            {
                // Only show an error at exactly max + 1 so that we can keep parsing the
                // parameters and minimize cascaded errors.
                Error(string.Format("Methods cannot have more than {0} parameters.", MaxParameters));
            }
        }

        // Parses the rest of a comma-separated parameter list after the opening
        // delimeter. Updates `arity` in [signature] with the number of parameters.
        private void FinishParameterList(Signature signature)
        {
            do
            {
                IgnoreNewlines();
                ValidateNumParameters(++signature.Arity);

                // Define a local variable in the method for the parameter.
                DeclareNamedVariable();
            }
            while (Match(TokenType.Comma));
        }

        // Gets the symbol for a method [name].
        private int MethodSymbol(string name)
        {
            if (!_parser.vm.MethodNames.Contains(name))
            {
                _parser.vm.MethodNames.Add(name);
            }

            int method = _parser.vm.MethodNames.IndexOf(name);
            return method;
        }

        // Appends characters to [name] (and updates [length]) for [numParams] "_"
        // surrounded by [leftBracket] and [rightBracket].
        static string SignatureParameterList(string name, int numParams, char leftBracket, char rightBracket)
        {
            name += leftBracket;
            for (int i = 0; i < numParams; i++)
            {
                if (i > 0) name += ',';
                name += '_';
            }
            name += rightBracket;
            return name;
        }

        // Fills [name] with the stringified version of [signature] and updates
        // [length] to the resulting length.
        private static string SignatureToString(Signature signature)
        {
            // Build the full name from the signature.
            string name = signature.Name;

            switch (signature.Type)
            {
                case SignatureType.Method:
                    name = SignatureParameterList(name, signature.Arity, '(', ')');
                    break;

                case SignatureType.Getter:
                    // The signature is just the name.
                    break;

                case SignatureType.Setter:
                    name += '=';
                    name = SignatureParameterList(name, 1, '(', ')');
                    break;

                case SignatureType.Subscript:
                    name = SignatureParameterList(name, signature.Arity, '[', ']');
                    break;

                case SignatureType.SubscriptSetter:
                    name = SignatureParameterList(name, signature.Arity - 1, '[', ']');
                    name += '=';
                    name = SignatureParameterList(name, 1, '(', ')');
                    break;
                case SignatureType.Initializer:
                    name = "init " + signature.Name;
                    name = SignatureParameterList(name, signature.Arity, '(', ')');
                    break;
            }
            return name;
        }

        // Gets the symbol for a method with [signature].
        private int SignatureSymbol(Signature signature)
        {
            // Build the full name from the signature.
            string name = SignatureToString(signature);
            return MethodSymbol(name);
        }

        // Initializes [signature] from the last consumed token.
        private Signature SignatureFromToken(Signature signature, SignatureType type)
        {
            // Get the token for the method name.
            Token token = _parser.Previous;
            signature.Type = type;
            signature.Arity = 0;
            signature.Name = _parser.Source.Substring(token.Start, token.Length);
            signature.Length = token.Length;

            if (signature.Length > MaxMethodName)
            {
                Error(string.Format("Method names cannot be longer than {0} characters.", MaxMethodName));
                signature.Length = MaxMethodName;
            }

            return signature;
        }

        // Parses a comma-separated list of arguments. Modifies [signature] to include
        // the arity of the argument list.
        private void FinishArgumentList(Signature signature)
        {
            do
            {
                IgnoreNewlines();
                ValidateNumParameters(++signature.Arity);
                Expression();
            }
            while (Match(TokenType.Comma));

            // Allow a newline before the closing delimiter.
            IgnoreNewlines();
        }

        // Compiles a method call with [signature] using [instruction].
        private void CallSignature(Instruction instruction, Signature signature)
        {
            int symbol = SignatureSymbol(signature);
            EmitShortArg((instruction + signature.Arity), symbol);

            if (instruction == Instruction.SUPER_0)
            {
                // Super calls need to be statically bound to the class's superclass. This
                // ensures we call the right method even when a method containing a super
                // call is inherited by another subclass.
                //
                // We bind it at class definition time by storing a reference to the
                // superclass in a constant. So, here, we create a slot in the constant
                // table and store null in it. When the method is bound, we'll look up the
                // superclass then and store it in the constant slot.
                int constant = AddConstant(new Obj(ValueType.Null));
                EmitShort(constant);
            }
        }

        // Compiles a method call with [numArgs] for a method with [name] with [length].
        private void CallMethod(int numArgs, string name)
        {
            int symbol = MethodSymbol(name);
            EmitShortArg(Instruction.CALL_0 + numArgs, symbol);
        }

        // Compiles an (optional) argument list and then calls it.
        private void MethodCall(Instruction instruction, Signature signature)
        {
            Signature called = new Signature { Type = SignatureType.Getter, Arity = 0, Name = signature.Name, Length = signature.Length };

            // Parse the argument list, if any.
            if (Match(TokenType.LeftParen))
            {
                called.Type = SignatureType.Method;

                // Allow empty an argument list.
                if (Peek() != TokenType.RightParen)
                {
                    FinishArgumentList(called);
                }
                Consume(TokenType.RightParen, "Expect ')' after arguments.");
            }

            // Parse the block argument, if any.
            if (Match(TokenType.LeftBrace))
            {
                // Include the block argument in the arity.
                called.Type = SignatureType.Method;
                called.Arity++;

                Compiler fnCompiler = new Compiler(_parser, this, true);

                // Make a dummy signature to track the arity.
                Signature fnSignature = new Signature { Arity = 0 };

                // Parse the parameter list, if any.
                if (Match(TokenType.Pipe))
                {
                    fnCompiler.FinishParameterList(fnSignature);
                    Consume(TokenType.Pipe, "Expect '|' after function parameters.");
                }

                fnCompiler._numParams = fnSignature.Arity;

                fnCompiler.FinishBody(false);

                String blockName = SignatureToString(called) + " block argument";
                fnCompiler.EndCompiler(blockName);
            }

            // TODO: Allow Grace-style mixfix methods?

            // If this is a super() call for an initializer, make sure we got an actual
            // argument list.
            if (signature.Type == SignatureType.Initializer)
            {
                if (called.Type != SignatureType.Method)
                {
                    Error("A superclass constructor must have an argument list.");
                }

                called.Type = SignatureType.Initializer;
            }

            CallSignature(instruction, called);
        }

        // Compiles a call whose name is the previously consumed token. This includes
        // getters, method calls with arguments, and setter calls.
        private void NamedCall(bool allowAssignment, Instruction instruction)
        {
            // Get the token for the method name.
            Signature signature = SignatureFromToken(new Signature(), SignatureType.Getter);

            if (Match(TokenType.Eq))
            {
                if (!allowAssignment) Error("Invalid assignment.");

                IgnoreNewlines();

                // Build the setter signature.
                signature.Type = SignatureType.Setter;
                signature.Arity = 1;

                // Compile the assigned value.
                Expression();
                CallSignature(instruction, signature);
            }
            else
            {
                MethodCall(instruction, signature);
            }
        }

        // Loads the receiver of the currently enclosing method. Correctly handles
        // functions defined inside methods.
        private void LoadThis()
        {
            Instruction loadInstruction;
            int index = ResolveNonmodule("this", 4, out loadInstruction);
            if (loadInstruction == Instruction.LOAD_LOCAL)
            {
                LoadLocal(index);
            }
            else
            {
                EmitByteArg(loadInstruction, index);
            }
        }

        private void LoadCoreVariable(string name)
        {
            int symbol = _parser.Module.Variables.FindIndex(v => v.Name == name);
            EmitShortArg(Instruction.LOAD_MODULE_VAR, symbol);
        }

        // A parenthesized expression.
        private static void Grouping(Compiler c, bool allowAssignment)
        {
            c.Expression();
            c.Consume(TokenType.RightParen, "Expect ')' after expression.");
        }

        // A list literal.
        private static void List(Compiler c, bool allowAssignment)
        {
            // Load the List class.
            c.LoadCoreVariable("List");

            // Instantiate a new list.
            c.CallMethod(0, "new()");

            // Compile the list elements. Each one compiles to a ".add()" call.
            if (c.Peek() != TokenType.RightBracket)
            {
                do
                {
                    c.IgnoreNewlines();

                    if (c.Peek() == TokenType.RightBracket)
                        break;

                    // Push a copy of the list since the add() call will consume it.
                    c.Emit(Instruction.DUP);

                    // The element.
                    c.Expression();
                    c.CallMethod(1, "add(_)");

                    // Discard the result of the add() call.
                    c.Emit(Instruction.POP);
                } while (c.Match(TokenType.Comma));
            }

            // Allow newlines before the closing ']'.
            c.IgnoreNewlines();
            c.Consume(TokenType.RightBracket, "Expect ']' after list elements.");
        }

        // A map literal.
        private static void Map(Compiler c, bool allowAssignment)
        {
            // Load the Map class.
            c.LoadCoreVariable("Map");

            // Instantiate a new map.
            c.CallMethod(0, "new()");

            // Compile the map elements. Each one is compiled to just invoke the
            // subscript setter on the map.
            if (c.Peek() != TokenType.RightBrace)
            {
                do
                {
                    c.IgnoreNewlines();

                    if (c.Peek() == TokenType.RightBrace)
                        break;

                    // Push a copy of the map since the subscript call will consume it.
                    c.Emit(Instruction.DUP);

                    // The key.
                    c.ParsePrecedence(false, Precedence.Primary);
                    c.Consume(TokenType.Colon, "Expect ':' after map key.");

                    // The value.
                    c.Expression();

                    c.CallMethod(2, "[_]=(_)");

                    // Discard the result of the setter call.
                    c.Emit(Instruction.POP);
                } while (c.Match(TokenType.Comma));
            }

            // Allow newlines before the closing '}'.
            c.IgnoreNewlines();
            c.Consume(TokenType.RightBrace, "Expect '}' after map entries.");
        }

        // Unary operators like `-foo`.
        private static void UnaryOp(Compiler c, bool allowAssignment)
        {
            GrammarRule rule = c.GetRule(c._parser.Previous.Type);

            c.IgnoreNewlines();

            // Compile the argument.
            c.ParsePrecedence(false, Precedence.Unary + 1);

            // Call the operator method on the left-hand side.
            c.CallMethod(0, rule.Name);
        }

        private static void Boolean(Compiler c, bool allowAssignment)
        {
            c.Emit(c._parser.Previous.Type == TokenType.False ? Instruction.FALSE : Instruction.TRUE);
        }

        // Walks the compiler chain to find the compiler for the nearest class
        // enclosing this one. Returns null if not currently inside a class definition.
        private Compiler GetEnclosingClassCompiler()
        {
            Compiler compiler = this;
            while (compiler != null)
            {
                if (compiler._enclosingClass != null) return compiler;
                compiler = compiler._parent;
            }

            return null;
        }

        // Walks the compiler chain to find the nearest class enclosing this one.
        // Returns null if not currently inside a class definition.
        private ClassCompiler GetEnclosingClass()
        {
            Compiler compiler = GetEnclosingClassCompiler();
            return compiler == null ? null : compiler._enclosingClass;
        }

        private static void Field(Compiler c, bool allowAssignment)
        {
            // Initialize it with a fake value so we can keep parsing and minimize the
            // number of cascaded errors.
            int field = 255;

            ClassCompiler enclosingClass = c.GetEnclosingClass();

            if (enclosingClass == null)
            {
                c.Error("Cannot reference a field outside of a class definition.");
            }
            else if (enclosingClass.IsStaticMethod)
            {
                c.Error("Cannot use an instance field in a static method.");
            }
            else
            {
                // Look up the field, or implicitly define it.
                string fieldName = c._parser.Source.Substring(c._parser.Previous.Start, c._parser.Previous.Length);
                field = enclosingClass.Fields.IndexOf(fieldName);
                if (field < 0)
                {
                    enclosingClass.Fields.Add(fieldName);
                    field = enclosingClass.Fields.IndexOf(fieldName);
                }

                if (field >= MaxFields)
                {
                    c.Error(string.Format("A class can only have {0} fields.", MaxFields));
                }
            }

            // If there's an "=" after a field name, it's an assignment.
            bool isLoad = true;
            if (c.Match(TokenType.Eq))
            {
                if (!allowAssignment) c.Error("Invalid assignment.");

                // Compile the right-hand side.
                c.Expression();
                isLoad = false;
            }

            // If we're directly inside a method, use a more optimal instruction.
            if (c._parent != null && c._parent._enclosingClass == enclosingClass)
            {
                c.EmitByteArg(isLoad ? Instruction.LOAD_FIELD_THIS : Instruction.STORE_FIELD_THIS,
                            field);
            }
            else
            {
                c.LoadThis();
                c.EmitByteArg(isLoad ? Instruction.LOAD_FIELD : Instruction.STORE_FIELD, field);
            }
        }

        // Compiles a read or assignment to a variable at [index] using
        // [loadInstruction].
        private void Variable(bool allowAssignment, int index, Instruction loadInstruction)
        {
            // If there's an "=" after a bare name, it's a variable assignment.
            if (Match(TokenType.Eq))
            {
                if (!allowAssignment) Error("Invalid assignment.");

                // Compile the right-hand side.
                Expression();

                // Emit the store instruction.
                switch (loadInstruction)
                {
                    case Instruction.LOAD_LOCAL:
                        EmitByteArg(Instruction.STORE_LOCAL, index);
                        break;
                    case Instruction.LOAD_UPVALUE:
                        EmitByteArg(Instruction.STORE_UPVALUE, index);
                        break;
                    case Instruction.LOAD_MODULE_VAR:
                        EmitShortArg(Instruction.STORE_MODULE_VAR, index);
                        break;
                }
            }
            else switch (loadInstruction)
                {
                    case Instruction.LOAD_MODULE_VAR:
                        EmitShortArg(loadInstruction, index);
                        break;
                    case Instruction.LOAD_LOCAL:
                        LoadLocal(index);
                        break;
                    default:
                        EmitByteArg(loadInstruction, index);
                        break;
                }
        }

        private static void StaticField(Compiler c, bool allowAssignment)
        {
            Instruction loadInstruction = Instruction.LOAD_LOCAL;
            int index = 255;

            Compiler classCompiler = c.GetEnclosingClassCompiler();
            if (classCompiler == null)
            {
                c.Error("Cannot use a static field outside of a class definition.");
            }
            else
            {
                // Look up the name in the scope chain.
                Token token = c._parser.Previous;

                // If this is the first time we've seen this static field, implicitly
                // define it as a variable in the scope surrounding the class definition.
                if (classCompiler.ResolveLocal(c._parser.Source.Substring(token.Start, token.Length), token.Length) == -1)
                {
                    int symbol = classCompiler.DeclareVariable(null);

                    // Implicitly initialize it to null.
                    classCompiler.Emit(Instruction.NULL);
                    classCompiler.DefineVariable(symbol);
                }

                // It definitely exists now, so resolve it properly. This is different from
                // the above resolveLocal() call because we may have already closed over it
                // as an upvalue.
                index = c.ResolveName(c._parser.Source.Substring(token.Start, token.Length), token.Length, out loadInstruction);
            }

            c.Variable(allowAssignment, index, loadInstruction);
        }

        // Returns `true` if [name] is a local variable name (starts with a lowercase
        // letter).
        static bool IsLocalName(string name)
        {
            return name[0] >= 'a' && name[0] <= 'z';
        }

        // Compiles a variable name or method call with an implicit receiver.
        private static void Name(Compiler c, bool allowAssignment)
        {
            // Look for the name in the scope chain up to the nearest enclosing method.
            Token token = c._parser.Previous;

            Instruction loadInstruction;
            string varName = c._parser.Source.Substring(token.Start, token.Length);
            int index = c.ResolveNonmodule(varName, token.Length, out loadInstruction);
            if (index != -1)
            {
                c.Variable(allowAssignment, index, loadInstruction);
                return;
            }

            // TODO: The fact that we return above here if the variable is known and parse
            // an optional argument list below if not means that the grammar is not
            // context-free. A line of code in a method like "someName(foo)" is a parse
            // error if "someName" is a defined variable in the surrounding scope and not
            // if it isn't. Fix this. One option is to have "someName(foo)" always
            // resolve to a self-call if there is an argument list, but that makes
            // getters a little confusing.

            // If we're inside a method and the name is lowercase, treat it as a method
            // on this.
            if (IsLocalName(varName) && c.GetEnclosingClass() != null)
            {
                c.LoadThis();
                c.NamedCall(allowAssignment, Instruction.CALL_0);
                return;
            }

            // Otherwise, look for a module-level variable with the name.
            int module = c._parser.Module.Variables.FindIndex(v => v.Name == varName);
            if (module == -1)
            {
                if (IsLocalName(varName))
                {
                    c.Error("Undefined variable.");
                    return;
                }

                // If it's a nonlocal name, implicitly define a module-level variable in
                // the hopes that we get a real definition later.
                module = c._parser.vm.DeclareVariable(c._parser.Module, varName);

                if (module == -2)
                {
                    c.Error("Too many module variables defined.");
                }
            }

            c.Variable(allowAssignment, module, Instruction.LOAD_MODULE_VAR);
        }

        private static void null_(Compiler c, bool allowAssignment)
        {
            c.Emit(Instruction.NULL);
        }

        private static void Number(Compiler c, bool allowAssignment)
        {
            int constant = c.AddConstant(new Obj(c._parser.Number));

            // Compile the code to load the constant.
            c.EmitShortArg(Instruction.CONSTANT, constant);
        }

        // Parses a string literal and adds it to the constant table.
        private int StringConstant()
        {
            // Define a constant for the literal.
            int constant = AddConstant(Obj.MakeString(_parser.Raw));

            _parser.Raw = "";

            return constant;
        }

        private static void string_(Compiler c, bool allowAssignment)
        {
            int constant = c.StringConstant();

            // Compile the code to load the constant.
            c.EmitShortArg(Instruction.CONSTANT, constant);
        }

        private static void super_(Compiler c, bool allowAssignment)
        {
            ClassCompiler enclosingClass = c.GetEnclosingClass();

            if (enclosingClass == null)
            {
                c.Error("Cannot use 'super' outside of a method.");
            }
            
            c.LoadThis();

            // TODO: Super operator calls.

            // See if it's a named super call, or an unnamed one.
            if (c.Match(TokenType.Dot))
            {
                // Compile the superclass call.
                c.Consume(TokenType.Name, "Expect method name after 'super.'.");
                c.NamedCall(allowAssignment, Instruction.SUPER_0);
            }
            else if (enclosingClass != null)
            {
                // No explicit name, so use the name of the enclosing method. Make sure we
                // check that enclosingClass isn't null first. We've already reported the
                // error, but we don't want to crash here.
                c.MethodCall(Instruction.SUPER_0, enclosingClass.Signature);
            }
        }

        private static void this_(Compiler c, bool allowAssignment)
        {
            if (c.GetEnclosingClass() == null)
            {
                c.Error("Cannot use 'this' outside of a method.");
                return;
            }

            c.LoadThis();
        }

        // Subscript or "array indexing" operator like `foo[bar]`.
        private static void Subscript(Compiler c, bool allowAssignment)
        {
            Signature signature = new Signature { Name = "", Length = 0, Type = SignatureType.Subscript, Arity = 0 };

            // Parse the argument list.
            c.FinishArgumentList(signature);
            c.Consume(TokenType.RightBracket, "Expect ']' after arguments.");

            if (c.Match(TokenType.Eq))
            {
                if (!allowAssignment) c.Error("Invalid assignment.");

                signature.Type = SignatureType.SubscriptSetter;

                // Compile the assigned value.
                c.ValidateNumParameters(++signature.Arity);
                c.Expression();
            }

            c.CallSignature(Instruction.CALL_0, signature);
        }

        private static void Call(Compiler c, bool allowAssignment)
        {
            c.IgnoreNewlines();
            c.Consume(TokenType.Name, "Expect method name after '.'.");
            c.NamedCall(allowAssignment, Instruction.CALL_0);
        }

        private static void and_(Compiler c, bool allowAssignment)
        {
            c.IgnoreNewlines();

            // Skip the right argument if the left is false.
            int jump = c.EmitJump(Instruction.AND);
            c.ParsePrecedence(false, Precedence.LogicalAnd);
            c.PatchJump(jump);
        }

        static void or_(Compiler c, bool allowAssignment)
        {
            c.IgnoreNewlines();

            // Skip the right argument if the left is true.
            int jump = c.EmitJump(Instruction.OR);
            c.ParsePrecedence(false, Precedence.LogicalOr);
            c.PatchJump(jump);
        }

        private static void Conditional(Compiler c, bool allowAssignment)
        {
            // Ignore newline after '?'.
            c.IgnoreNewlines();

            // Jump to the else branch if the condition is false.
            int ifJump = c.EmitJump(Instruction.JUMP_IF);

            // Compile the then branch.
            c.ParsePrecedence(allowAssignment, Precedence.Ternary);

            c.Consume(TokenType.Colon, "Expect ':' after then branch of conditional operator.");
            c.IgnoreNewlines();

            // Jump over the else branch when the if branch is taken.
            int elseJump = c.EmitJump(Instruction.JUMP);

            // Compile the else branch.
            c.PatchJump(ifJump);

            c.ParsePrecedence(allowAssignment, Precedence.Assignment);

            // Patch the jump over the else.
            c.PatchJump(elseJump);
        }

        private static void InfixOp(Compiler c, bool allowAssignment)
        {
            GrammarRule rule = c.GetRule(c._parser.Previous.Type);

            // An infix operator cannot end an expression.
            c.IgnoreNewlines();

            // Compile the right-hand side.
            c.ParsePrecedence(false, rule.Precedence + 1);

            // Call the operator method on the left-hand side.
            Signature signature = new Signature { Type = SignatureType.Method, Arity = 1, Name = rule.Name, Length = rule.Name.Length };
            c.CallSignature(Instruction.CALL_0, signature);
        }

        // Compiles a method signature for an infix operator.
        static void InfixSignature(Compiler c, Signature signature)
        {
            // Add the RHS parameter.
            signature.Type = SignatureType.Method;
            signature.Arity = 1;

            // Parse the parameter name.
            c.Consume(TokenType.LeftParen, "Expect '(' after operator name.");
            c.DeclareNamedVariable();
            c.Consume(TokenType.RightParen, "Expect ')' after parameter name.");
        }

        // Compiles a method signature for an unary operator (i.e. "!").
        private static void UnarySignature(Compiler c, Signature signature)
        {
            // Do nothing. The name is already complete.
            signature.Type = SignatureType.Getter;
        }

        // Compiles a method signature for an operator that can either be unary or
        // infix (i.e. "-").
        private static void MixedSignature(Compiler c, Signature signature)
        {
            signature.Type = SignatureType.Getter;

            // If there is a parameter, it's an infix operator, otherwise it's unary.
            if (c.Match(TokenType.LeftParen))
            {
                // Add the RHS parameter.
                signature.Type = SignatureType.Method;
                signature.Arity = 1;

                // Parse the parameter name.
                c.DeclareNamedVariable();
                c.Consume(TokenType.RightParen, "Expect ')' after parameter name.");
            }
        }

        // Compiles an optional setter parameter in a method [signature].
        //
        // Returns `true` if it was a setter.
        private bool MaybeSetter(Signature signature)
        {
            // See if it's a setter.
            if (!Match(TokenType.Eq)) return false;

            // It's a setter.
            signature.Type = signature.Type == SignatureType.Subscript
                ? SignatureType.SubscriptSetter
                : SignatureType.Setter;

            // Parse the value parameter.
            Consume(TokenType.LeftParen, "Expect '(' after '='.");
            DeclareNamedVariable();
            Consume(TokenType.RightParen, "Expect ')' after parameter name.");

            signature.Arity++;

            return true;
        }

        // Compiles a method signature for a subscript operator.
        private static void SubscriptSignature(Compiler c, Signature signature)
        {
            signature.Type = SignatureType.Subscript;

            // The signature currently has "[" as its name since that was the token that
            // matched it. Clear that out.
            signature.Length = 0;
            signature.Name = "";

            // Parse the parameters inside the subscript.
            c.FinishParameterList(signature);
            c.Consume(TokenType.RightBracket, "Expect ']' after parameters.");

            c.MaybeSetter(signature);
        }

        // Parses an optional parenthesized parameter list. Updates `type` and `arity`
        // in [signature] to match what was parsed.
        private void ParameterList(Signature signature)
        {
            // The parameter list is optional.
            if (!Match(TokenType.LeftParen)) return;

            signature.Type = SignatureType.Method;

            // Allow an empty parameter list.
            if (Match(TokenType.RightParen)) return;

            FinishParameterList(signature);
            Consume(TokenType.RightParen, "Expect ')' after parameters.");
        }

        // Compiles a method signature for a named method or setter.
        private static void NamedSignature(Compiler c, Signature signature)
        {
            signature.Type = SignatureType.Getter;

            // If it's a setter, it can't also have a parameter list.
            if (c.MaybeSetter(signature)) return;

            // Regular named method with an optional parameter list.
            c.ParameterList(signature);
        }

        // Compiles a method signature for a constructor.
        private static void ConstructorSignature(Compiler c, Signature signature)
        {
            c.Consume(TokenType.Name, "Expect constructor name after 'construct'.");

            // Capture the name.
            signature = c.SignatureFromToken(signature, SignatureType.Initializer);

            if (c.Match(TokenType.Eq))
            {
                c.Error("A constructor cannot be a setter.");
            }

            if (!c.Match(TokenType.LeftParen))
            {
                c.Error("A constructor cannot be a getter.");
                return;
            }

            if (c.Match(TokenType.RightParen)) return;

            c.FinishParameterList(signature);
            c.Consume(TokenType.RightParen, "Expect ')' after parameters.");
        }

        // This table defines all of the parsing rules for the prefix and infix
        // expressions in the grammar. Expressions are parsed using a Pratt parser.
        //
        // See: http://journal.stuffwithstuff.com/2011/03/19/pratt-parsers-expression-parsing-made-easy/
        /*
        #define UNUSED                     { null, null, null, PREC_NONE, null }
        #define PREFIX(fn)                 { fn, null, null, PREC_NONE, null }
        #define INFIX(prec, fn)            { null, fn, null, prec, null }
        #define INFIX_OPERATOR(prec, name) { null, infixOp, infixSignature, prec, name }
        #define PREFIX_OPERATOR(name)      { unaryOp, null, unarySignature, PREC_NONE, name }
        #define OPERATOR(name)             { unaryOp, infixOp, mixedSignature, PREC_TERM, name }
        */
        private readonly GrammarRule[] _rules = 
{
  /* TOKEN_LEFT_PAREN    */ new GrammarRule(Grouping, null, null, Precedence.None, null),
  /* TOKEN_RIGHT_PAREN   */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_LEFT_BRACKET  */ new GrammarRule(List, Subscript, SubscriptSignature, Precedence.Call, null),
  /* TOKEN_RIGHT_BRACKET */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_LEFT_BRACE    */ new GrammarRule(Map, null, null, Precedence.None, null),
  /* TOKEN_RIGHT_BRACE   */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_COLON         */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_DOT           */ new GrammarRule(null, Call, null, Precedence.Call, null),
  /* TOKEN_DOTDOT        */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Range, ".."),
  /* TOKEN_DOTDOTDOT     */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Range, "..."),
  /* TOKEN_COMMA         */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_STAR          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Factor, "*"),
  /* TOKEN_SLASH         */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Factor, "/"),
  /* TOKEN_PERCENT       */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Factor, "%"),
  /* TOKEN_PLUS          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Term, "+"),
  /* TOKEN_MINUS         */ new GrammarRule(UnaryOp, InfixOp, MixedSignature, Precedence.Term, "-"),
  /* TOKEN_LTLT          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.BitwiseShift, "<<"),
  /* TOKEN_GTGT          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.BitwiseShift, ">>"),
  /* TOKEN_PIPE          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.BitwiseOr, "|"),
  /* TOKEN_PIPEPIPE      */ new GrammarRule(null, or_, null, Precedence.LogicalOr, null),
  /* TOKEN_CARET         */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.BitwiseXor, "^"),
  /* TOKEN_AMP           */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.BitwiseAnd, "&"),
  /* TOKEN_AMPAMP        */ new GrammarRule(null, and_, null, Precedence.LogicalAnd, null),
  /* TOKEN_BANG          */ new GrammarRule(UnaryOp, null, UnarySignature, Precedence.None, "!"),
  /* TOKEN_TILDE         */ new GrammarRule(UnaryOp, null, UnarySignature, Precedence.None, "~"),
  /* TOKEN_QUESTION      */ new GrammarRule(null, Conditional, null, Precedence.Assignment, null),
  /* TOKEN_EQ            */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_LT            */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Comparison, "<"),
  /* TOKEN_GT            */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Comparison, ">"),
  /* TOKEN_LTEQ          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Comparison, "<="),
  /* TOKEN_GTEQ          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Comparison, ">="),
  /* TOKEN_EQEQ          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Equality, "=="),
  /* TOKEN_BANGEQ        */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Equality, "!="),
  /* TOKEN_BREAK         */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_CLASS         */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_CONSTRUCT     */ new GrammarRule(null, null, ConstructorSignature, Precedence.None, null),
  /* TOKEN_ELSE          */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_FALSE         */ new GrammarRule(Boolean, null, null, Precedence.None, null),
  /* TOKEN_FOR           */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_USING         */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_IF            */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_IMPORT        */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_IN            */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_IS            */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.Is, "is"),
  /* TOKEN_NULL          */ new GrammarRule(null_, null, null, Precedence.None, null),
  /* TOKEN_RETURN        */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_STATIC        */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_SUPER         */ new GrammarRule(super_, null, null, Precedence.None, null),
  /* TOKEN_THIS          */ new GrammarRule(this_, null, null, Precedence.None, null),
  /* TOKEN_TRUE          */ new GrammarRule(Boolean, null, null, Precedence.None, null),
  /* TOKEN_VAR           */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_WHILE         */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_FIELD         */ new GrammarRule(Field, null, null, Precedence.None, null),
  /* TOKEN_STATIC_FIELD  */ new GrammarRule(StaticField, null, null, Precedence.None, null),
  /* TOKEN_NAME          */ new GrammarRule(Name, null, NamedSignature, Precedence.None, null),
  /* TOKEN_NUMBER        */ new GrammarRule(Number, null, null, Precedence.None, null),
  /* TOKEN_STRING        */ new GrammarRule(string_, null, null, Precedence.None, null),
  /* TOKEN_LINE          */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_ERROR         */ new GrammarRule(null, null, null, Precedence.None, null),
  /* TOKEN_EOF           */ new GrammarRule(null, null, null, Precedence.None, null)
};

        // Gets the [GrammarRule] associated with tokens of [type].
        private GrammarRule GetRule(TokenType type)
        {
            return _rules[(byte)type];
        }

        // The main entrypoint for the top-down operator precedence parser.
        private void ParsePrecedence(bool allowAssignment, Precedence precedence)
        {
            NextToken();
            GrammarFn prefix = _rules[(int)_parser.Previous.Type].Prefix;

            if (prefix == null)
            {
                Error("Expected expression.");
                return;
            }

            prefix(this, allowAssignment);

            while (_rules[(int)_parser.Current.Type].Precedence >= precedence)
            {
                NextToken();
                GrammarFn infix = _rules[(int)_parser.Previous.Type].Infix;
                infix(this, allowAssignment);
            }
        }

        // Parses an expression. Unlike statements, expressions leave a resulting value
        // on the stack.
        private void Expression()
        {
            ParsePrecedence(true, Precedence.Lowest);
        }

        // Parses a curly block or an expression statement. Used in places like the
        // arms of an if statement where either a single expression or a curly body is
        // allowed.
        private void Block()
        {
            // Curly block.
            if (Match(TokenType.LeftBrace))
            {
                PushScope();
                if (FinishBlock())
                {
                    // Block was an expression, so discard it.
                    Emit(Instruction.POP);
                }
                PopScope();
                return;
            }

            // Single statement body.
            Statement();
        }

        // Returns the number of arguments to the instruction at [ip] in [fn]'s
        // bytecode.
        private static int GetNumArguments(byte[] bytecode, List<Obj> constants, int ip)
        {
            Instruction instruction = (Instruction)bytecode[ip];
            switch (instruction)
            {
                case Instruction.NULL:
                case Instruction.FALSE:
                case Instruction.TRUE:
                case Instruction.POP:
                case Instruction.DUP:
                case Instruction.CLOSE_UPVALUE:
                case Instruction.RETURN:
                case Instruction.END:
                case Instruction.LOAD_LOCAL_0:
                case Instruction.LOAD_LOCAL_1:
                case Instruction.LOAD_LOCAL_2:
                case Instruction.LOAD_LOCAL_3:
                case Instruction.LOAD_LOCAL_4:
                case Instruction.LOAD_LOCAL_5:
                case Instruction.LOAD_LOCAL_6:
                case Instruction.LOAD_LOCAL_7:
                case Instruction.LOAD_LOCAL_8:
                case Instruction.CONSTRUCT:
                case Instruction.FOREIGN_CONSTRUCT:
                case Instruction.FOREIGN_CLASS:
                    return 0;

                case Instruction.LOAD_LOCAL:
                case Instruction.STORE_LOCAL:
                case Instruction.LOAD_UPVALUE:
                case Instruction.STORE_UPVALUE:
                case Instruction.LOAD_FIELD_THIS:
                case Instruction.STORE_FIELD_THIS:
                case Instruction.LOAD_FIELD:
                case Instruction.STORE_FIELD:
                case Instruction.CLASS:
                    return 1;

                case Instruction.CONSTANT:
                case Instruction.LOAD_MODULE_VAR:
                case Instruction.STORE_MODULE_VAR:
                case Instruction.CALL_0:
                case Instruction.CALL_1:
                case Instruction.CALL_2:
                case Instruction.CALL_3:
                case Instruction.CALL_4:
                case Instruction.CALL_5:
                case Instruction.CALL_6:
                case Instruction.CALL_7:
                case Instruction.CALL_8:
                case Instruction.CALL_9:
                case Instruction.CALL_10:
                case Instruction.CALL_11:
                case Instruction.CALL_12:
                case Instruction.CALL_13:
                case Instruction.CALL_14:
                case Instruction.CALL_15:
                case Instruction.CALL_16:
                case Instruction.JUMP:
                case Instruction.LOOP:
                case Instruction.JUMP_IF:
                case Instruction.AND:
                case Instruction.OR:
                case Instruction.METHOD_INSTANCE:
                case Instruction.METHOD_STATIC:
                case Instruction.LOAD_MODULE:
                    return 2;

                case Instruction.SUPER_0:
                case Instruction.SUPER_1:
                case Instruction.SUPER_2:
                case Instruction.SUPER_3:
                case Instruction.SUPER_4:
                case Instruction.SUPER_5:
                case Instruction.SUPER_6:
                case Instruction.SUPER_7:
                case Instruction.SUPER_8:
                case Instruction.SUPER_9:
                case Instruction.SUPER_10:
                case Instruction.SUPER_11:
                case Instruction.SUPER_12:
                case Instruction.SUPER_13:
                case Instruction.SUPER_14:
                case Instruction.SUPER_15:
                case Instruction.SUPER_16:
                case Instruction.IMPORT_VARIABLE:
                    return 4;

                case Instruction.CLOSURE:
                    {
                        int constant = (bytecode[ip + 1] << 8) | bytecode[ip + 2];
                        ObjFn loadedFn = (ObjFn)constants[constant];

                        // There are two bytes for the constant, then two for each upvalue.
                        return 2 + (loadedFn.NumUpvalues * 2);
                    }

                default:
                    return 0;
            }
        }

        // Marks the beginning of a loop. Keeps track of the current instruction so we
        // know what to loop back to at the end of the body.
        private void StartLoop(Loop l)
        {
            l.Enclosing = _loop;
            l.Start = _bytecode.Count - 1 - 1;
            l.ScopeDepth = _scopeDepth;
            _loop = l;
        }

        // Emits the [Instruction.JUMP_IF] instruction used to test the loop condition and
        // potentially exit the loop. Keeps track of the instruction so we can patch it
        // later once we know where the end of the body is.
        private void TestExitLoop()
        {
            if (_loop == null)
                return;
            _loop.ExitJump = EmitJump(Instruction.JUMP_IF);
        }

        // Compiles the body of the loop and tracks its extent so that contained "break"
        // statements can be handled correctly.
        private void LoopBody()
        {
            if (_loop == null)
                return;
            _loop.Body = _bytecode.Count - 1;
            Block();
        }

        // Ends the current innermost loop. Patches up all jumps and breaks now that
        // we know where the end of the loop is.
        private void EndLoop()
        {
            int loopOffset = _bytecode.Count - 1 - _loop.Start + 2;
            // TODO: Check for overflow.
            EmitShortArg(Instruction.LOOP, loopOffset);

            PatchJump(_loop.ExitJump);

            // Find any break placeholder instructions (which will be Instruction.END in the
            // bytecode) and replace them with real jumps.
            int i = _loop.Body;
            while (i < _bytecode.Count - 1)
            {
                if (_bytecode[i] == (byte)Instruction.END)
                {
                    _bytecode[i] = (byte)Instruction.JUMP;
                    PatchJump(i + 1);
                    i += 3;
                }
                else
                {
                    // Skip this instruction and its arguments.
                    i += 1 + GetNumArguments(_bytecode.ToArray(), _constants, i);
                }
            }

            _loop = _loop.Enclosing;
        }

        private void ForStatement()
        {
            // A for statement like:
            //
            //     for (i in sequence.expression) {
            //       IO.write(i)
            //     }
            //
            // Is compiled to bytecode almost as if the source looked like this:
            //
            //     {
            //       var seq_ = sequence.expression
            //       var iter_
            //       while (iter_ = seq_.iterate(iter_)) {
            //         var i = seq_.iteratorValue(iter_)
            //         IO.write(i)
            //       }
            //     }
            //
            // It's not exactly this, because the synthetic variables `seq_` and `iter_`
            // actually get names that aren't valid identfiers, but that's the basic
            // idea.
            //
            // The important parts are:
            // - The sequence expression is only evaluated once.
            // - The .iterate() method is used to advance the iterator and determine if
            //   it should exit the loop.
            // - The .iteratorValue() method is used to get the value at the current
            //   iterator position.

            // Create a scope for the hidden local variables used for the iterator.
            PushScope();

            Consume(TokenType.LeftParen, "Expect '(' after 'for'.");
            Consume(TokenType.Name, "Expect for loop variable name.");

            // Remember the name of the loop variable.
            string name = _parser.Source.Substring(_parser.Previous.Start, _parser.Previous.Length);
            int length = _parser.Previous.Length;

            Consume(TokenType.In, "Expect 'in' after loop variable.");
            IgnoreNewlines();

            // Evaluate the sequence expression and store it in a hidden local variable.
            // The space in the variable name ensures it won't collide with a user-defined
            // variable.
            Expression();
            int seqSlot = DefineLocal("seq ", 4);

            // Create another hidden local for the iterator object.
            null_(this, false);
            int iterSlot = DefineLocal("iter ", 5);

            Consume(TokenType.RightParen, "Expect ')' after loop expression.");

            Loop l = new Loop();
            StartLoop(l);

            // Advance the iterator by calling the ".iterate" method on the sequence.
            LoadLocal(seqSlot);
            LoadLocal(iterSlot);

            CallMethod(1, "iterate(_)");

            // Store the iterator back in its local for the next iteration.
            EmitByteArg(Instruction.STORE_LOCAL, iterSlot);
            // TODO: We can probably get this working with a bit less stack juggling.

            TestExitLoop();

            // Get the current value in the sequence by calling ".iteratorValue".
            LoadLocal(seqSlot);
            LoadLocal(iterSlot);

            CallMethod(1, "iteratorValue(_)");

            // Bind the loop variable in its own scope. This ensures we get a fresh
            // variable each iteration so that closures for it don't all see the same one.
            PushScope();
            DefineLocal(name, length);

            LoopBody();

            // Loop variable.
            PopScope();

            EndLoop();

            // Hidden variables.
            PopScope();
        }

        private void WhileStatement()
        {
            Loop l = new Loop();
            StartLoop(l);

            // Compile the condition.
            Consume(TokenType.LeftParen, "Expect '(' after 'while'.");
            Expression();
            Consume(TokenType.RightParen, "Expect ')' after while condition.");

            TestExitLoop();
            LoopBody();
            EndLoop();
        }

        // Compiles a statement. These can only appear at the top-level or within
        // curly blocks. Unlike expressions, these do not leave a value on the stack.
        private void Statement()
        {
            if (Match(TokenType.Break))
            {
                if (_loop == null)
                {
                    Error("Cannot use 'break' outside of a loop.");
                    return;
                }

                // Since we will be jumping out of the scope, make sure any locals in it
                // are discarded first.
                DiscardLocals(_loop.ScopeDepth + 1);

                // Emit a placeholder instruction for the jump to the end of the body. When
                // we're done compiling the loop body and know where the end is, we'll
                // replace these with `Instruction.JUMP` instructions with appropriate offsets.
                // We use `Instruction.END` here because that can't occur in the middle of
                // bytecode.
                EmitJump(Instruction.END);
                return;
            }

            if (Match(TokenType.For))
            {
                ForStatement();
                return;
            }

            if (Match(TokenType.If))
            {
                // Compile the condition.
                Consume(TokenType.LeftParen, "Expect '(' after 'if'.");
                Expression();
                Consume(TokenType.RightParen, "Expect ')' after if condition.");

                // Jump to the else branch if the condition is false.
                int ifJump = EmitJump(Instruction.JUMP_IF);

                // Compile the then branch.
                Block();

                // Compile the else branch if there is one.
                if (Match(TokenType.Else))
                {
                    // Jump over the else branch when the if branch is taken.
                    int elseJump = EmitJump(Instruction.JUMP);

                    PatchJump(ifJump);

                    Block();

                    // Patch the jump over the else.
                    PatchJump(elseJump);
                }
                else
                {
                    PatchJump(ifJump);
                }

                return;
            }

            if (Match(TokenType.Return))
            {
                // Compile the return value.
                if (Peek() == TokenType.Line)
                {
                    // Implicitly return null if there is no value.
                    Emit(Instruction.NULL);
                }
                else
                {
                    Expression();
                }

                Emit(Instruction.RETURN);
                return;
            }

            if (Match(TokenType.While))
            {
                WhileStatement();
                return;
            }

            // Expression statement.
            Expression();
            Emit(Instruction.POP);
        }

        // Compiles a method definition inside a class body. Returns the symbol in the
        // method table for the new method.
        private bool Method(ClassCompiler classCompiler, int classSlot)
        {

            bool isForeign = Match(TokenType.Foreign);
            classCompiler.IsStaticMethod = Match(TokenType.Static);

            SignatureFn signatureFn = GetRule(_parser.Current.Type).Method;
            NextToken();

            if (signatureFn == null)
            {
                Error("Expect method definition.");
                return false;
            }

            // Build the method signature.
            Signature signature = SignatureFromToken(new Signature(), SignatureType.Getter);
            classCompiler.Signature = signature;

            Compiler methodCompiler = new Compiler(_parser, this, false);
            signatureFn(methodCompiler, signature);

            if (classCompiler.IsStaticMethod && signature.Type == SignatureType.Initializer)
            {
                Error("A constructor cannot be static.");
            }

            String fullSignature = SignatureToString(signature);

            if (isForeign)
            {
                int constant = AddConstant(Obj.MakeString(fullSignature));

                EmitShortArg(Instruction.CONSTANT, constant);
            }
            else
            {
                Consume(TokenType.LeftBrace, "Expect '{' to begin method body.");
                methodCompiler.FinishBody(signature.Type == SignatureType.Initializer);
                methodCompiler.EndCompiler(fullSignature);
            }

            // Define the method. For a constructor, this defines the instance
            // initializer method.
            int methodSymbol = SignatureSymbol(signature);
            DefineMethod(classSlot, classCompiler.IsStaticMethod, methodSymbol);

            if (signature.Type == SignatureType.Initializer)
            {
                signature.Type = SignatureType.Method;
                int constructorSymbol = SignatureSymbol(signature);
                CreateConstructor(signature, methodSymbol);
                DefineMethod(classSlot, true, constructorSymbol);
            }

            return true;
        }

        private void CreateConstructor(Signature signature, int initializerSymbol)
        {
            Compiler methodCompiler = new Compiler(_parser, this, false);

            methodCompiler.Emit(_enclosingClass.IsForeign ? Instruction.FOREIGN_CONSTRUCT : Instruction.CONSTRUCT);
            methodCompiler.EmitShortArg(Instruction.CALL_0 + signature.Arity, initializerSymbol);
            methodCompiler.Emit(Instruction.RETURN);

            methodCompiler.EndCompiler("");
        }

        private void DefineMethod(int classSlot, bool isStaticMethod, int methodSymbol)
        {
            if (_scopeDepth == 0)
            {
                EmitShortArg(Instruction.LOAD_MODULE_VAR, classSlot);
            }
            else
            {
                LoadLocal(classSlot);
            }

            Instruction inst = isStaticMethod ? Instruction.METHOD_STATIC : Instruction.METHOD_INSTANCE;
            EmitShortArg(inst, methodSymbol);
        }

        // Compiles a class definition. Assumes the "class" token has already been
        // consumed.
        private void ClassDefinition(bool isForeign)
        {
            // Create a variable to store the class in.
            int slot = DeclareNamedVariable();

            // Make a string constant for the name.
            int nameConstant = AddConstant(Obj.MakeString(_parser.Source.Substring(_parser.Previous.Start, _parser.Previous.Length)));

            EmitShortArg(Instruction.CONSTANT, nameConstant);

            // Load the superclass (if there is one).
            if (Match(TokenType.Is))
            {
                ParsePrecedence(false, Precedence.Call);
            }
            else
            {
                // Implicitly inherit from Object.
                LoadCoreVariable("Object");
            }

            // Store a placeholder for the number of fields argument. We don't know
            // the value until we've compiled all the methods to see which fields are
            // used.
            int numFieldsInstruction = -1;
            if (isForeign)
            {
                Emit(Instruction.FOREIGN_CLASS);
            }
            else
            {
                numFieldsInstruction = EmitByteArg(Instruction.CLASS, 255);
            }

            // Store it in its name.
            DefineVariable(slot);

            // Push a local variable scope. Static fields in a class body are hoisted out
            // into local variables declared in this scope. Methods that use them will
            // have upvalues referencing them.
            PushScope();

            ClassCompiler classCompiler = new ClassCompiler();

            // Set up a symbol table for the class's fields. We'll initially compile
            // them to slots starting at zero. When the method is bound to the class, the
            // bytecode will be adjusted by [BindMethod] to take inherited fields
            // into account.
            List<string> fields = new List<string>();

            classCompiler.Fields = fields;
            classCompiler.IsForeign = isForeign;

            _enclosingClass = classCompiler;

            // Compile the method definitions.
            Consume(TokenType.LeftBrace, "Expect '{' after class declaration.");
            MatchLine();

            while (!Match(TokenType.RightBrace))
            {
                if (!Method(classCompiler, slot)) break;

                // Don't require a newline after the last definition.
                if (Match(TokenType.RightBrace)) break;

                ConsumeLine("Expect newline after definition in class.");
            }

            if (!isForeign)
            {
                // Update the class with the number of fields.
                _bytecode[numFieldsInstruction] = (byte)fields.Count;
            }

            _enclosingClass = null;

            PopScope();
        }

        private void Import()
        {
            Consume(TokenType.String, "Expect a string after 'import'.");
            int moduleConstant = StringConstant();

            // Load the module.
            EmitShortArg(Instruction.LOAD_MODULE, moduleConstant);

            // Discard the unused result value from calling the module's fiber.
            Emit(Instruction.POP);

            // The for clause is optional.
            if (!Match(TokenType.For)) return;

            // Compile the comma-separated list of variables to import.
            do
            {
                int slot = DeclareNamedVariable();

                // Define a string constant for the variable name.
                string varName = _parser.Source.Substring(_parser.Previous.Start, _parser.Previous.Length);
                int variableConstant = AddConstant(Obj.MakeString(varName));

                // Load the variable from the other module.
                EmitShortArg(Instruction.IMPORT_VARIABLE, moduleConstant);
                EmitShort(variableConstant);

                // Store the result in the variable here.
                DefineVariable(slot);
            } while (Match(TokenType.Comma));
        }

        private void VariableDefinition()
        {
            // Grab its name, but don't declare it yet. A (local) variable shouldn't be
            // in scope in its own initializer.
            Consume(TokenType.Name, "Expect variable name.");
            Token nameToken = _parser.Previous;

            // Compile the initializer.
            if (Match(TokenType.Eq))
            {
                Expression();
            }
            else
            {
                // Default initialize it to null.
                null_(this, false);
            }

            // Now put it in scope.
            int symbol = DeclareVariable(nameToken);
            DefineVariable(symbol);
        }

        // Compiles a "definition". These are the statements that bind new variables.
        // They can only appear at the top level of a block and are prohibited in places
        // like the non-curly body of an if or while.
        private void Definition()
        {
            bool isForeign = Match(TokenType.Foreign);

            if (Match(TokenType.Class))
            {
                ClassDefinition(isForeign);
                return;
            }

            // TODO: Disallow foreign to be used outside a class

            if (Match(TokenType.Import))
            {
                Import();
                return;
            }

            if (Match(TokenType.Var))
            {
                VariableDefinition();
                return;
            }

            Block();
        }

        public static ObjFn Compile(WrenVM vm, ObjModule module, string sourcePath, string source, bool printErrors)
        {
            Parser parser = new Parser
            {
                vm = vm,
                Module = module,
                SourcePath = sourcePath,
                Source = source,
                TokenStart = 0,
                CurrentChar = 0,
                CurrentLine = 1,
                Current = { Type = TokenType.Error, Start = 0, Length = 0, Line = 0 },
                SkipNewlines = true,
                PrintErrors = printErrors,
                HasError = false,
                Raw = ""
            };

            Compiler compiler = new Compiler(parser, null, true);

            // Read the first token.
            compiler.NextToken();

            compiler.IgnoreNewlines();

            while (!compiler.Match(TokenType.Eof))
            {
                compiler.Definition();

                // If there is no newline, it must be the end of the block on the same line.
                if (!compiler.MatchLine())
                {
                    compiler.Consume(TokenType.Eof, "Expect end of file.");
                    break;
                }
            }

            compiler.Emit(Instruction.NULL);
            compiler.Emit(Instruction.RETURN);

            // See if there are any implicitly declared module-level variables that never
            // got an explicit definition.
            // TODO: It would be nice if the error was on the line where it was used.
            for (int i = 0; i < parser.Module.Variables.Count; i++)
            {
                ModuleVariable t = parser.Module.Variables[i];
                if (t.Container.Type == ValueType.Undefined)
                {
                    compiler.Error(string.Format("Variable '{0}' is used but not defined.", t.Name));
                }
            }

            return compiler.EndCompiler("(script)");
        }

        public static void BindMethodCode(ObjClass classObj, ObjFn fn)
        {
            int ip = 0;
            for (; ; )
            {
                Instruction instruction = (Instruction)fn.Bytecode[ip++];
                switch (instruction)
                {
                    case Instruction.LOAD_FIELD:
                    case Instruction.STORE_FIELD:
                    case Instruction.LOAD_FIELD_THIS:
                    case Instruction.STORE_FIELD_THIS:
                        // Shift this class's fields down past the inherited ones. We don't
                        // check for overflow here because we'll see if the number of fields
                        // overflows when the subclass is created.
                        fn.Bytecode[ip++] += (byte)classObj.Superclass.NumFields;
                        break;

                    case Instruction.SUPER_0:
                    case Instruction.SUPER_1:
                    case Instruction.SUPER_2:
                    case Instruction.SUPER_3:
                    case Instruction.SUPER_4:
                    case Instruction.SUPER_5:
                    case Instruction.SUPER_6:
                    case Instruction.SUPER_7:
                    case Instruction.SUPER_8:
                    case Instruction.SUPER_9:
                    case Instruction.SUPER_10:
                    case Instruction.SUPER_11:
                    case Instruction.SUPER_12:
                    case Instruction.SUPER_13:
                    case Instruction.SUPER_14:
                    case Instruction.SUPER_15:
                    case Instruction.SUPER_16:
                        {
                            // Skip over the symbol.
                            ip += 2;

                            // Fill in the constant slot with a reference to the superclass.
                            int constant = (fn.Bytecode[ip] << 8) | fn.Bytecode[ip + 1];
                            fn.Constants[constant] = classObj.Superclass;
                            break;
                        }

                    case Instruction.CLOSURE:
                        {
                            // Bind the nested closure too.
                            int constant = (fn.Bytecode[ip] << 8) | fn.Bytecode[ip + 1];
                            BindMethodCode(classObj, fn.Constants[constant] as ObjFn);

                            ip += GetNumArguments(fn.Bytecode, new List<Obj>(fn.Constants), ip - 1);
                            break;
                        }

                    case Instruction.END:
                        return;

                    default:
                        // Other instructions are unaffected, so just skip over them.
                        ip += GetNumArguments(fn.Bytecode, new List<Obj>(fn.Constants), ip - 1);
                        break;
                }
            }
        }

        #endregion

        public static string DumpByteCode(WrenVM vm, ObjFn fn)
        {
            string s = "";
            int ip = 0;
            byte[] bytecode = fn.Bytecode;
            while (ip < bytecode.Length)
            {
                Instruction instruction = (Instruction)bytecode[ip++];
                s += instruction + " ";
                switch (instruction)
                {
                    case Instruction.NULL:
                    case Instruction.FALSE:
                    case Instruction.TRUE:
                    case Instruction.POP:
                    case Instruction.DUP:
                    case Instruction.CLOSE_UPVALUE:
                    case Instruction.RETURN:
                    case Instruction.END:
                    case Instruction.LOAD_LOCAL_0:
                    case Instruction.LOAD_LOCAL_1:
                    case Instruction.LOAD_LOCAL_2:
                    case Instruction.LOAD_LOCAL_3:
                    case Instruction.LOAD_LOCAL_4:
                    case Instruction.LOAD_LOCAL_5:
                    case Instruction.LOAD_LOCAL_6:
                    case Instruction.LOAD_LOCAL_7:
                    case Instruction.LOAD_LOCAL_8:
                        s += ("\n");
                        break;

                    case Instruction.LOAD_LOCAL:
                    case Instruction.STORE_LOCAL:
                    case Instruction.LOAD_UPVALUE:
                    case Instruction.STORE_UPVALUE:
                    case Instruction.LOAD_FIELD_THIS:
                    case Instruction.STORE_FIELD_THIS:
                    case Instruction.LOAD_FIELD:
                    case Instruction.STORE_FIELD:
                    case Instruction.CLASS:
                        s += (bytecode[ip++] + "\n");
                        break;


                    case Instruction.CALL_0:
                    case Instruction.CALL_1:
                    case Instruction.CALL_2:
                    case Instruction.CALL_3:
                    case Instruction.CALL_4:
                    case Instruction.CALL_5:
                    case Instruction.CALL_6:
                    case Instruction.CALL_7:
                    case Instruction.CALL_8:
                    case Instruction.CALL_9:
                    case Instruction.CALL_10:
                    case Instruction.CALL_11:
                    case Instruction.CALL_12:
                    case Instruction.CALL_13:
                    case Instruction.CALL_14:
                    case Instruction.CALL_15:
                    case Instruction.CALL_16:
                        int method = (bytecode[ip] << 8) + bytecode[ip + 1];
                        s += vm.MethodNames[method] + "\n";
                        ip += 2;
                        break;
                    case Instruction.CONSTANT:
                    case Instruction.LOAD_MODULE_VAR:
                    case Instruction.STORE_MODULE_VAR:
                    case Instruction.JUMP:
                    case Instruction.LOOP:
                    case Instruction.JUMP_IF:
                    case Instruction.AND:
                    case Instruction.OR:
                    case Instruction.METHOD_INSTANCE:
                    case Instruction.METHOD_STATIC:
                    case Instruction.LOAD_MODULE:
                        int method1 = (bytecode[ip] << 8) + bytecode[ip + 1];
                        s += method1 + "\n";
                        ip += 2;
                        break;

                    case Instruction.SUPER_0:
                    case Instruction.SUPER_1:
                    case Instruction.SUPER_2:
                    case Instruction.SUPER_3:
                    case Instruction.SUPER_4:
                    case Instruction.SUPER_5:
                    case Instruction.SUPER_6:
                    case Instruction.SUPER_7:
                    case Instruction.SUPER_8:
                    case Instruction.SUPER_9:
                    case Instruction.SUPER_10:
                    case Instruction.SUPER_11:
                    case Instruction.SUPER_12:
                    case Instruction.SUPER_13:
                    case Instruction.SUPER_14:
                    case Instruction.SUPER_15:
                    case Instruction.SUPER_16:
                    case Instruction.IMPORT_VARIABLE:
                        s += (bytecode[ip++]);
                        s += (" ");
                        s += (bytecode[ip++]);
                        s += (" ");
                        s += (bytecode[ip++]);
                        s += (" ");
                        s += (bytecode[ip++] + "\n");
                        break;

                    case Instruction.CLOSURE:
                        {
                            int constant = (bytecode[ip + 1] << 8) | bytecode[ip + 2];
                            ObjFn loadedFn = (ObjFn)fn.Constants[constant];

                            // There are two bytes for the constant, then two for each upvalue.
                            int j = 2 + (loadedFn.NumUpvalues * 2);
                            while (j > 0)
                            {
                                s += (bytecode[ip++]);
                                s += (" ");
                                j--;
                            }
                            s += "\n";
                        }
                        break;
                }
            }
            return s;
        }
    }
}
