using System;
using System.Collections.Generic;
using System.Globalization;
using Wren.Core.Objects;
using Wren.Core.VM;
using ValueType = Wren.Core.VM.ValueType;

namespace Wren.Core.Bytecode
{
    public enum TokenType
    {
        TOKEN_LEFT_PAREN,
        TOKEN_RIGHT_PAREN,
        TOKEN_LEFT_BRACKET,
        TOKEN_RIGHT_BRACKET,
        TOKEN_LEFT_BRACE,
        TOKEN_RIGHT_BRACE,
        TOKEN_COLON,
        TOKEN_DOT,
        TOKEN_DOTDOT,
        TOKEN_DOTDOTDOT,
        TOKEN_COMMA,
        TOKEN_STAR,
        TOKEN_SLASH,
        TOKEN_PERCENT,
        TOKEN_PLUS,
        TOKEN_MINUS,
        TOKEN_LTLT,
        TOKEN_GTGT,
        TOKEN_PIPE,
        TOKEN_PIPEPIPE,
        TOKEN_CARET,
        TOKEN_AMP,
        TOKEN_AMPAMP,
        TOKEN_BANG,
        TOKEN_TILDE,
        TOKEN_QUESTION,
        TOKEN_EQ,
        TOKEN_LT,
        TOKEN_GT,
        TOKEN_LTEQ,
        TOKEN_GTEQ,
        TOKEN_EQEQ,
        TOKEN_BANGEQ,

        TOKEN_BREAK,
        TOKEN_CLASS,
        TOKEN_CONSTRUCT,
        TOKEN_ELSE,
        TOKEN_FALSE,
        TOKEN_FOR,
        TOKEN_FOREIGN,
        TOKEN_IF,
        TOKEN_IMPORT,
        TOKEN_IN,
        TOKEN_IS,
        TOKEN_NULL,
        TOKEN_RETURN,
        TOKEN_STATIC,
        TOKEN_SUPER,
        TOKEN_THIS,
        TOKEN_TRUE,
        TOKEN_VAR,
        TOKEN_WHILE,

        TOKEN_FIELD,
        TOKEN_STATIC_FIELD,
        TOKEN_NAME,
        TOKEN_NUMBER,
        TOKEN_STRING,

        TOKEN_LINE,

        TOKEN_ERROR,
        TOKEN_EOF
    };

    public struct Token
    {
        public TokenType type;

        // The beginning of the token, pointing directly into the source.
        public int start;

        // The length of the token in characters.
        public int length;

        // The 1-based line where the token appears.
        public int line;
    };

    public class Parser
    {
        public WrenVM vm;

        // The module being parsed.
        public ObjModule module;

        // Heap-allocated string representing the path to the code being parsed. Used
        // for stack traces.
        public string sourcePath;

        // The source code being parsed.
        public string source;

        // The beginning of the currently-being-lexed token in [source].
        public int tokenStart;

        // The current character being lexed in [source].
        public int currentChar;

        // The 1-based line number of [currentChar].
        public int currentLine;

        // The most recently lexed token.
        public Token current;

        // The most recently consumed/advanced token.
        public Token previous;

        // If subsequent newline tokens should be discarded.
        public bool skipNewlines;

        // Whether compile errors should be printed to stderr or discarded.
        public bool printErrors;

        // If a syntax or compile error has occurred.
        public bool hasError;

        // A buffer for the unescaped text of the current token if it's a string
        // literal. Unlike the raw token, this will have escape sequences translated
        // to their literal equivalent.
        public string raw;

        // If a number literal is currently being parsed this will hold its value.
        public double number;
    };

    struct Local
    {
        // The name of the local variable.
        public string name;

        // The length of the local variable's name.
        public int length;

        // The depth in the scope chain that this variable was declared at. Zero is
        // the outermost scope--parameters for a method, or the first local block in
        // top level code. One is the scope within that, etc.
        public int depth;

        // If this local variable is being used as an upvalue.
        public bool isUpvalue;
    };

    struct CompilerUpvalue
    {
        // True if this upvalue is capturing a local variable from the enclosing
        // function. False if it's capturing an upvalue.
        public bool isLocal;

        // The index of the local or upvalue being captured in the enclosing function.
        public int index;
    };

    class Loop
    {
        // Index of the instruction that the loop should jump back to.
        public int start;

        // Index of the argument for the Instruction.JUMP_IF instruction used to exit the
        // loop. Stored so we can patch it once we know where the loop ends.
        public int exitJump;

        // Index of the first instruction of the body of the loop.
        public int body;

        // Depth of the scope(s) that need to be exited if a break is hit inside the
        // loop.
        public int scopeDepth;

        // The loop enclosing this one, or null if this is the outermost loop.
        public Loop enclosing;
    };

    // The different signature syntaxes for different kinds of methods.
    public enum SignatureType
    {
        // A name followed by a (possibly empty) parenthesized parameter list. Also
        // used for binary operators.
        SIG_METHOD,

        // Just a name. Also used for unary operators.
        SIG_GETTER,

        // A name followed by "=".
        SIG_SETTER,

        // A square bracketed parameter list.
        SIG_SUBSCRIPT,

        // A square bracketed parameter list followed by "=".
        SIG_SUBSCRIPT_SETTER,

        // A constructor initializer function. This has a distinct signature to
        // prevent it from being invoked directly outside of the constructor on the
        // metaclass.
        SIG_INITIALIZER
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
        public List<string> fields;

        // True if the class being compiled is a foreign class.
        public bool isForeign;

        // True if the current method being compiled is static.
        public bool isStaticMethod;

        // The signature of the method being compiled.
        public Signature signature;
    };

    public class Compiler
    {
        private readonly Parser parser;
        public const int MAX_LOCALS = 255;
        public const int MAX_UPVALUES = 255;
        public const int MAX_CONSTANTS = (1 << 16);
        public const int MAX_VARIABLE_NAME = 64;
        public const int MAX_METHOD_SIGNATURE = 128;
        public const int MAX_METHOD_NAME = 64;
        public const int MAX_FIELDS = 255;
        public const int MAX_PARAMETERS = 16;

        private readonly Compiler parent;
        private readonly List<Value> constants = new List<Value>();
        private readonly Local[] locals = new Local[MAX_LOCALS + 1];
        private int numLocals;

        private readonly CompilerUpvalue[] upvalues = new CompilerUpvalue[MAX_UPVALUES];
        private int numUpValues;

        private int numParams;
        private int scopeDepth;

        private Loop loop;
        private ClassCompiler enclosingClass;

        private readonly List<byte> bytecode;

        private readonly int[] debugSourceLines;

        private static void LexError(Parser parser, string format)
        {
            parser.hasError = true;
            if (!parser.printErrors) return;

            Console.Error.Write("[{0} line {1}] Error: ", parser.sourcePath, parser.currentLine);

            Console.Error.WriteLine(format);
        }

        private void Error(string format)
        {
            parser.hasError = true;
            if (!parser.printErrors) return;

            Token token = parser.previous;

            // If the parse error was caused by an error token, the lexer has already
            // reported it.
            if (token.type == TokenType.TOKEN_ERROR) return;

            Console.Error.Write("[{0} line {1}] Error at ", parser.sourcePath, token.line);

            switch (token.type)
            {
                case TokenType.TOKEN_LINE:
                    Console.Error.Write("newline: ");
                    break;
                case TokenType.TOKEN_EOF:
                    Console.Error.Write("end of file: ");
                    break;
                default:
                    Console.Error.Write("'{0}': ", parser.source.Substring(token.start, token.length));
                    break;
            }

            Console.Error.WriteLine(format);
        }

        // Adds [constant] to the constant pool and returns its index.
        private int AddConstant(Value constant)
        {
            // TODO: it is too slow to consolidate constants this way
            /*int index = constants.FindIndex(b => Container.Equals(b, constant));
            if (index > -1)
                return index;*/

            if (constants.Count < MAX_CONSTANTS)
            {
                constants.Add(constant);
            }
            else
            {
                Error(string.Format("A function may only contain {0} unique constants.", MAX_CONSTANTS));
            }

            return constants.Count - 1;
        }

        // Initializes [compiler].
        public Compiler(Parser parser, Compiler parent, bool isFunction)
        {
            this.parser = parser;
            this.parent = parent;

            // Initialize this to null before allocating in case a GC gets triggered in
            // the middle of initializing the compiler.
            constants = new List<Value>();

            numUpValues = 0;
            numParams = 0;
            loop = null;
            enclosingClass = null;

            parser.vm.Compiler = this;

            if (parent == null)
            {
                numLocals = 0;

                // Compiling top-level code, so the initial scope is module-level.
                scopeDepth = -1;
            }
            else
            {
                // Declare a fake local variable for the receiver so that it's slot in the
                // stack is taken. For methods, we call this "this", so that we can resolve
                // references to that like a normal variable. For functions, they have no
                // explicit "this". So we pick a bogus name. That way references to "this"
                // inside a function will try to walk up the parent chain to find a method
                // enclosing the function whose "this" we can close over.
                numLocals = 1;
                if (isFunction)
                {
                    locals[0].name = null;
                    locals[0].length = 0;
                }
                else
                {
                    locals[0].name = "this";
                    locals[0].length = 4;
                }
                locals[0].depth = -1;
                locals[0].isUpvalue = false;

                // The initial scope for function or method is a local scope.
                scopeDepth = 0;
            }

            bytecode = new List<byte>();
            debugSourceLines = new int[16000];
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
            return parser.currentChar < parser.source.Length ? parser.source[parser.currentChar] : '\0';
        }

        // Returns the character after the current character.
        static char PeekNextChar(Parser parser)
        {
            // If we're at the end of the source, don't read past it.
            return parser.currentChar >= parser.source.Length - 1 ? '\0' : parser.source[parser.currentChar + 1];
        }

        // Advances the parser forward one character.
        static char NextChar(Parser parser)
        {
            char c = PeekChar(parser);
            parser.currentChar++;
            if (c == '\n') parser.currentLine++;
            return c;
        }

        // Sets the parser's current token to the given [type] and current character
        // range.
        static void MakeToken(Parser parser, TokenType type)
        {
            parser.current.type = type;
            parser.current.start = parser.tokenStart;
            parser.current.length = parser.currentChar - parser.tokenStart;
            parser.current.line = parser.currentLine;

            // Make line tokens appear on the line containing the "\n".
            if (type == TokenType.TOKEN_LINE) parser.current.line--;
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
            return parser.source.Substring(parser.tokenStart, parser.currentChar - parser.tokenStart);
        }

        static int ReadHexDigit(Parser parser)
        {
            char c = NextChar(parser);
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;

            // Don't consume it if it isn't expected. Keeps us from reading past the end
            // of an unterminated string.
            parser.currentChar--;
            return -1;
        }

        // Parses the numeric value of the current token.
        static void MakeNumber(Parser parser, bool isHex)
        {
            string s = GetTokenString(parser);
            try
            {
                parser.number = isHex ? Convert.ToInt32(s, 16) : Convert.ToDouble(s, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                LexError(parser, "Number too big");
            }
            MakeToken(parser, TokenType.TOKEN_NUMBER);
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

                while (IsDigit(peekchar(parser))) NextChar(parser);
            }

            MakeNumber(parser, false);
        }

        private static char peekchar(Parser parser)
        {
            throw new NotImplementedException();
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
                    type = TokenType.TOKEN_BREAK;
                    break;
                case "class":
                    type = TokenType.TOKEN_CLASS;
                    break;
                case "construct":
                    type = TokenType.TOKEN_CONSTRUCT;
                    break;
                case "else":
                    type = TokenType.TOKEN_ELSE;
                    break;
                case "false":
                    type = TokenType.TOKEN_FALSE;
                    break;
                case "for":
                    type = TokenType.TOKEN_FOR;
                    break;
                case "foreign":
                    type = TokenType.TOKEN_FOREIGN;
                    break;
                case "if":
                    type = TokenType.TOKEN_IF;
                    break;
                case "import":
                    type = TokenType.TOKEN_IMPORT;
                    break;
                case "in":
                    type = TokenType.TOKEN_IN;
                    break;
                case "is":
                    type = TokenType.TOKEN_IS;
                    break;
                case "null":
                    type = TokenType.TOKEN_NULL;
                    break;
                case "return":
                    type = TokenType.TOKEN_RETURN;
                    break;
                case "static":
                    type = TokenType.TOKEN_STATIC;
                    break;
                case "super":
                    type = TokenType.TOKEN_SUPER;
                    break;
                case "this":
                    type = TokenType.TOKEN_THIS;
                    break;
                case "true":
                    type = TokenType.TOKEN_TRUE;
                    break;
                case "var":
                    type = TokenType.TOKEN_VAR;
                    break;
                case "while":
                    type = TokenType.TOKEN_WHILE;
                    break;
            }

            MakeToken(parser, type);
        }

        // Adds [c] to the current string literal being tokenized.
        static void AddStringChar(Parser parser, char c)
        {
            parser.raw += c;
        }

        // Reads [digits] hex digits in a string literal and returns their number value.
        int ReadHexEscape(int digits, string description)
        {
            int value = 0;
            for (int i = 0; i < digits; i++)
            {
                if (PeekChar(parser) == '"' || PeekChar(parser) == '\0')
                {
                    Error(string.Format("Incomplete {0} escape sequence.", description));

                    // Don't consume it if it isn't expected. Keeps us from reading past the
                    // end of an unterminated string.
                    parser.currentChar--;
                    break;
                }

                int digit = ReadHexDigit(parser);
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
            AddStringChar(parser, Convert.ToChar(i));
        }

        // Finishes lexing a string literal.
        void ReadString()
        {
            for (; ; )
            {
                char c = NextChar(parser);
                if (c == '"') break;

                if (c == '\0')
                {
                    LexError(parser, "Unterminated string.");

                    // Don't consume it if it isn't expected. Keeps us from reading past the
                    // end of an unterminated string.
                    parser.currentChar--;
                    break;
                }

                if (c == '\\')
                {
                    switch (NextChar(parser))
                    {
                        case '"': AddStringChar(parser, '"'); break;
                        case '\\': AddStringChar(parser, '\\'); break;
                        case '0': AddStringChar(parser, '\0'); break;
                        case 'a': AddStringChar(parser, '\a'); break;
                        case 'b': AddStringChar(parser, '\b'); break;
                        case 'f': AddStringChar(parser, '\f'); break;
                        case 'n': AddStringChar(parser, '\n'); break;
                        case 'r': AddStringChar(parser, '\r'); break;
                        case 't': AddStringChar(parser, '\t'); break;
                        case 'u': ReadUnicodeEscape(); break;
                        // TODO: 'U' for 8 octet Unicode escapes.
                        case 'v': AddStringChar(parser, '\v'); break;
                        case 'x':
                            AddStringChar(parser, (char)(0xFF & ReadHexEscape(2, "byte")));
                            break;

                        default:
                            LexError(parser, string.Format("Invalid escape character '{0}'.", parser.source[parser.currentChar - 1]));
                            break;
                    }
                }
                else
                {
                    AddStringChar(parser, c);
                }
            }

            MakeToken(parser, TokenType.TOKEN_STRING);
        }

        // Lex the next token and store it in [parser.current].
        void NextToken()
        {
            parser.previous = parser.current;

            // If we are out of tokens, don't try to tokenize any more. We *do* still
            // copy the TOKEN_EOF to previous so that code that expects it to be consumed
            // will still work.
            if (parser.current.type == TokenType.TOKEN_EOF) return;

            while (PeekChar(parser) != '\0')
            {
                parser.tokenStart = parser.currentChar;

                char c = NextChar(parser);
                switch (c)
                {
                    case '(':
                        MakeToken(parser, TokenType.TOKEN_LEFT_PAREN);
                        return;
                    case ')':
                        MakeToken(parser, TokenType.TOKEN_RIGHT_PAREN);
                        return;
                    case '[':
                        MakeToken(parser, TokenType.TOKEN_LEFT_BRACKET);
                        return;
                    case ']':
                        MakeToken(parser, TokenType.TOKEN_RIGHT_BRACKET);
                        return;
                    case '{':
                        MakeToken(parser, TokenType.TOKEN_LEFT_BRACE);
                        return;
                    case '}':
                        MakeToken(parser, TokenType.TOKEN_RIGHT_BRACE);
                        return;
                    case ':':
                        MakeToken(parser, TokenType.TOKEN_COLON);
                        return;
                    case '.':
                        if (PeekChar(parser) == '.')
                        {
                            NextChar(parser);
                            if (PeekChar(parser) == '.')
                            {
                                NextChar(parser);
                                MakeToken(parser, TokenType.TOKEN_DOTDOTDOT);
                                return;
                            }

                            MakeToken(parser, TokenType.TOKEN_DOTDOT);
                            return;
                        }

                        MakeToken(parser, TokenType.TOKEN_DOT);
                        return;

                    case ',':
                        MakeToken(parser, TokenType.TOKEN_COMMA);
                        return;
                    case '*':
                        MakeToken(parser, TokenType.TOKEN_STAR);
                        return;
                    case '%':
                        MakeToken(parser, TokenType.TOKEN_PERCENT);
                        return;
                    case '+':
                        MakeToken(parser, TokenType.TOKEN_PLUS);
                        return;
                    case '~':
                        MakeToken(parser, TokenType.TOKEN_TILDE);
                        return;
                    case '?':
                        MakeToken(parser, TokenType.TOKEN_QUESTION);
                        return;
                    case '/':
                        if (PeekChar(parser) == '/')
                        {
                            SkipLineComment(parser);
                            break;
                        }

                        if (PeekChar(parser) == '*')
                        {
                            SkipBlockComment(parser);
                            break;
                        }

                        MakeToken(parser, TokenType.TOKEN_SLASH);
                        return;

                    case '-':
                        MakeToken(parser, TokenType.TOKEN_MINUS);
                        return;

                    case '|':
                        TwoCharToken(parser, '|', TokenType.TOKEN_PIPEPIPE, TokenType.TOKEN_PIPE);
                        return;

                    case '&':
                        TwoCharToken(parser, '&', TokenType.TOKEN_AMPAMP, TokenType.TOKEN_AMP);
                        return;

                    case '^':
                        MakeToken(parser, TokenType.TOKEN_CARET);
                        return;

                    case '=':
                        TwoCharToken(parser, '=', TokenType.TOKEN_EQEQ, TokenType.TOKEN_EQ);
                        return;

                    case '<':
                        if (PeekChar(parser) == '<')
                        {
                            NextChar(parser);
                            MakeToken(parser, TokenType.TOKEN_LTLT);
                            return;
                        }

                        TwoCharToken(parser, '=', TokenType.TOKEN_LTEQ, TokenType.TOKEN_LT);
                        return;

                    case '>':
                        if (PeekChar(parser) == '>')
                        {
                            NextChar(parser);
                            MakeToken(parser, TokenType.TOKEN_GTGT);
                            return;
                        }

                        TwoCharToken(parser, '=', TokenType.TOKEN_GTEQ, TokenType.TOKEN_GT);
                        return;

                    case '!':
                        TwoCharToken(parser, '=', TokenType.TOKEN_BANGEQ, TokenType.TOKEN_BANG);
                        return;

                    case '\n':
                        MakeToken(parser, TokenType.TOKEN_LINE);
                        return;

                    case ' ':
                    case '\r':
                    case '\t':
                        // Skip forward until we run out of whitespace.
                        while (PeekChar(parser) == ' ' ||
                               PeekChar(parser) == '\r' ||
                               PeekChar(parser) == '\t')
                        {
                            NextChar(parser);
                        }
                        break;

                    case '"': ReadString();
                        return;
                    case '_':
                        ReadName(parser, PeekChar(parser) == '_' ? TokenType.TOKEN_STATIC_FIELD : TokenType.TOKEN_FIELD);
                        return;

                    case '@':
                        ReadName(parser, PeekChar(parser) == '@' ? TokenType.TOKEN_STATIC_FIELD : TokenType.TOKEN_FIELD);
                        return;

                    case '#':
                        // Ignore shebang on the first line.
                        if (PeekChar(parser) == '!' && parser.currentLine == 1)
                        {
                            SkipLineComment(parser);
                            break;
                        }

                        LexError(parser, string.Format("Invalid character '{0}'.", c));
                        return;

                    case '0':
                        if (PeekChar(parser) == 'x')
                        {
                            ReadHexNumber(parser);
                            return;
                        }

                        ReadNumber(parser);
                        return;

                    default:
                        if (IsName(c))
                        {
                            ReadName(parser, TokenType.TOKEN_NAME);
                        }
                        else if (IsDigit(c))
                        {
                            ReadNumber(parser);
                        }
                        else
                        {
                            LexError(parser, string.Format("Invalid character '{0}'.", c));
                        }
                        return;
                }
            }

            // If we get here, we're out of source, so just make EOF tokens.
            parser.tokenStart = parser.currentChar;
            MakeToken(parser, TokenType.TOKEN_EOF);
        }

        // Returns the type of the current token.
        private TokenType Peek()
        {
            return parser.current.type;
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
            if (parser.previous.type != expected)
            {
                Error(errorMessage);

                // If the next token is the one we want, assume the current one is just a
                // spurious error and discard it to minimize the number of cascaded errors.
                if (parser.current.type == expected) NextToken();
            }
        }

        // Matches one or more newlines. Returns true if at least one was found.
        private bool MatchLine()
        {
            if (!Match(TokenType.TOKEN_LINE)) return false;

            while (Match(TokenType.TOKEN_LINE))
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
            Consume(TokenType.TOKEN_LINE, errorMessage);
            IgnoreNewlines();
        }

        // Variables and scopes --------------------------------------------------------
        #region Variables and scopes

        private int Emit(int b)
        {
            bytecode.Add((byte)b);
            return bytecode.Count - 1;
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
            Local local = new Local { name = name, length = length, depth = scopeDepth, isUpvalue = false };
            locals[numLocals] = local;
            return numLocals++;
        }

        // Declares a variable in the current scope whose name is the given token.
        //
        // If [token] is `null`, uses the previously consumed token. Returns its symbol.
        private int DeclareVariable(Token? token)
        {
            if (token == null) token = parser.previous;

            Token t = token.Value;

            if (t.length > MAX_VARIABLE_NAME)
            {
                Error(string.Format("Variable name cannot be longer than {0} characters.", MAX_VARIABLE_NAME));
            }

            // Top-level module scope.
            if (scopeDepth == -1)
            {
                int symbol = parser.vm.DefineVariable(parser.module, parser.source.Substring(t.start, t.length), new Value(ValueType.Null));

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

            string tokenName = parser.source.Substring(t.start, t.length);

            // See if there is already a variable with this name declared in the current
            // scope. (Outer scopes are OK: those get shadowed.)
            for (int i = numLocals - 1; i >= 0; i--)
            {
                Local local = locals[i];
                // Once we escape this scope and hit an outer one, we can stop.
                if (local.depth < scopeDepth) break;

                if (local.length == t.length && tokenName == local.name)
                {
                    Error(string.Format("Variable '{0}' is already declared in this scope.", local.name));
                    return i;
                }
            }

            if (numLocals > MAX_LOCALS)
            {
                Error(string.Format("Cannot declare more than {0} variables in one scope.", MAX_LOCALS));
                return -1;
            }

            return DefineLocal(tokenName, t.length);
        }

        // Parses a name token and declares a variable in the current scope with that
        // name. Returns its slot.
        private int DeclareNamedVariable()
        {
            Consume(TokenType.TOKEN_NAME, "Expect variable name.");
            return DeclareVariable(null);
        }

        // Stores a variable with the previously defined symbol in the current scope.
        private void DefineVariable(int symbol)
        {
            // Store the variable. If it's a local, the result of the initializer is
            // in the correct slot on the stack already so we're done.
            if (scopeDepth >= 0) return;

            // It's a module-level variable, so store the value in the module slot and
            // then discard the temporary for the initializer.
            EmitShortArg(Instruction.STORE_MODULE_VAR, symbol);
            Emit(Instruction.POP);
        }

        // Starts a new local block scope.
        private void PushScope()
        {
            scopeDepth++;
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

            int local = numLocals - 1;
            while (local >= 0 && locals[local].depth >= depth)
            {
                // If the local was closed over, make sure the upvalue gets closed when it
                // goes out of scope on the stack.
                Emit(locals[local].isUpvalue ? Instruction.CLOSE_UPVALUE : Instruction.POP);

                local--;
            }

            return numLocals - local - 1;
        }

        // Closes the last pushed block scope and discards any local variables declared
        // in that scope. This should only be called in a statement context where no
        // temporaries are still on the stack.
        private void PopScope()
        {
            numLocals -= DiscardLocals(scopeDepth);
            scopeDepth--;
        }

        // Attempts to look up the name in the local variables of [compiler]. If found,
        // returns its index, otherwise returns -1.
        private int ResolveLocal(string name, int length)
        {
            // Look it up in the local scopes. Look in reverse order so that the most
            // nested variable is found first and shadows outer ones.
            for (int i = numLocals - 1; i >= 0; i--)
            {
                if (locals[i].length == length && name == locals[i].name)
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
            for (int i = 0; i < numUpValues; i++)
            {
                CompilerUpvalue upvalue = upvalues[i];
                if (upvalue.index == index && upvalue.isLocal == isLocal) return i;
            }

            // If we got here, it's a new upvalue.
            upvalues[numUpValues].isLocal = isLocal;
            upvalues[numUpValues].index = index;
            return numUpValues++;
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
            if (parent == null || enclosingClass != null) return -1;

            // See if it's a local variable in the immediately enclosing function.
            int local = parent.ResolveLocal(name, length);
            if (local != -1)
            {
                // Mark the local as an upvalue so we know to close it when it goes out of
                // scope.
                parent.locals[local].isUpvalue = true;

                return AddUpvalue(true, local);
            }

            // See if it's an upvalue in the immediately enclosing function. In other
            // words, if it's a local variable in a non-immediately enclosing function.
            // This "flattens" closures automatically: it adds upvalues to all of the
            // intermediate functions to get from the function where a local is declared
            // all the way into the possibly deeply nested function that is closing over
            // it.
            int upvalue = parent.FindUpvalue(name, length);
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
            return parser.module.Variables.FindIndex(v => v.Name == name);
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
            if (parser.hasError)
            {
                parser.vm.Compiler = parent;
                return null;
            }

            // Mark the end of the bytecode. Since it may contain multiple early returns,
            // we can't rely on Instruction.RETURN to tell us we're at the end.
            Emit(Instruction.END);

            // Create a function object for the code we just compiled.
            ObjFn fn = new ObjFn(parser.module,
                                        constants.ToArray(),
                                        numUpValues,
                                        numParams,
                                        bytecode.ToArray(),
                                        new ObjString(parser.sourcePath),
                                        debugName,
                                        debugSourceLines);

            // In the function that contains this one, load the resulting function object.
            if (parent != null)
            {
                int constant = parent.AddConstant(new Value(fn));

                // If the function has no upvalues, we don't need to create a closure.
                // We can just load and run the function directly.
                if (numUpValues == 0)
                {
                    parent.EmitShortArg(Instruction.CONSTANT, constant);
                }
                else
                {
                    // Capture the upvalues in the new closure object.
                    parent.EmitShortArg(Instruction.CLOSURE, constant);

                    // Emit arguments for each upvalue to know whether to capture a local or
                    // an upvalue.
                    // TODO: Do something more efficient here?
                    for (int i = 0; i < numUpValues; i++)
                    {
                        parent.Emit(upvalues[i].isLocal ? 1 : 0);
                        parent.Emit(upvalues[i].index);
                    }
                }
            }

            // Pop this compiler off the stack.
            parser.vm.Compiler = parent;

            return fn;
        }


        // Grammar ---------------------------------------------------------------------

        private enum Precedence
        {
            PREC_NONE,
            PREC_LOWEST,
            PREC_ASSIGNMENT, // =
            PREC_TERNARY, // ?:
            PREC_LOGICAL_OR, // ||
            PREC_LOGICAL_AND, // &&
            PREC_EQUALITY, // == !=
            PREC_IS, // is
            PREC_COMPARISON, // < > <= >=
            PREC_BITWISE_OR, // |
            PREC_BITWISE_XOR, // ^
            PREC_BITWISE_AND, // &
            PREC_BITWISE_SHIFT, // << >>
            PREC_RANGE, // .. ...
            PREC_TERM, // + -
            PREC_FACTOR, // * / %
            PREC_UNARY, // unary - ! ~
            PREC_CALL, // . () []
            PREC_PRIMARY
        };

        private delegate void GrammarFn(Compiler c, bool allowAssignment);

        private delegate void SignatureFn(Compiler compiler, Signature signature);

        private struct GrammarRule
        {
            public readonly GrammarFn prefix;
            public readonly GrammarFn infix;
            public readonly SignatureFn method;
            public readonly Precedence precedence;
            public readonly string name;

            public GrammarRule(GrammarFn prefix, GrammarFn infix, SignatureFn method, Precedence precedence, string name)
            {
                this.prefix = prefix;
                this.infix = infix;
                this.method = method;
                this.precedence = precedence;
                this.name = name;
            }
        };

        // Replaces the placeholder argument for a previous Instruction.JUMP or Instruction.JUMP_IF
        // instruction with an offset that jumps to the current end of bytecode.
        private void PatchJump(int offset)
        {
            // -2 to adjust for the bytecode for the jump offset itself.
            int jump = bytecode.Count - offset - 2;
            // TODO: Check for overflow.
            bytecode[offset] = (byte)((jump >> 8) & 0xff);
            bytecode[offset + 1] = (byte)(jump & 0xff);
        }

        // Parses a block body, after the initial "{" has been consumed.
        //
        // Returns true if it was a expression body, false if it was a statement body.
        // (More precisely, returns true if a value was left on the stack. An empty
        // block returns false.)
        private bool FinishBlock()
        {
            // Empty blocks do nothing.
            if (Match(TokenType.TOKEN_RIGHT_BRACE))
            {
                return false;
            }

            // If there's no line after the "{", it's a single-expression body.
            if (!MatchLine())
            {
                Expression();
                Consume(TokenType.TOKEN_RIGHT_BRACE, "Expect '}' at end of block.");
                return true;
            }

            // Empty blocks (with just a newline inside) do nothing.
            if (Match(TokenType.TOKEN_RIGHT_BRACE))
            {
                return false;
            }

            // Compile the definition list.
            do
            {
                Definition();

                // If we got into a weird error state, don't get stuck in a loop.
                if (Peek() == TokenType.TOKEN_EOF) return true;

                ConsumeLine("Expect newline after statement.");
            }
            while (!Match(TokenType.TOKEN_RIGHT_BRACE));
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
            if (numArgs == MAX_PARAMETERS + 1)
            {
                // Only show an error at exactly max + 1 so that we can keep parsing the
                // parameters and minimize cascaded errors.
                Error(string.Format("Methods cannot have more than {0} parameters.", MAX_PARAMETERS));
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
            while (Match(TokenType.TOKEN_COMMA));
        }

        // Gets the symbol for a method [name].
        private int MethodSymbol(string name)
        {
            if (!parser.vm.MethodNames.Contains(name))
            {
                parser.vm.MethodNames.Add(name);
            }

            int method = parser.vm.MethodNames.IndexOf(name);
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
                case SignatureType.SIG_METHOD:
                    name = SignatureParameterList(name, signature.Arity, '(', ')');
                    break;

                case SignatureType.SIG_GETTER:
                    // The signature is just the name.
                    break;

                case SignatureType.SIG_SETTER:
                    name += '=';
                    name = SignatureParameterList(name, 1, '(', ')');
                    break;

                case SignatureType.SIG_SUBSCRIPT:
                    name = SignatureParameterList(name, signature.Arity, '[', ']');
                    break;

                case SignatureType.SIG_SUBSCRIPT_SETTER:
                    name = SignatureParameterList(name, signature.Arity - 1, '[', ']');
                    name += '=';
                    name = SignatureParameterList(name, 1, '(', ')');
                    break;
                case SignatureType.SIG_INITIALIZER:
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
            Token token = parser.previous;
            signature.Type = type;
            signature.Arity = 0;
            signature.Name = parser.source.Substring(token.start, token.length);
            signature.Length = token.length;

            if (signature.Length > MAX_METHOD_NAME)
            {
                Error(string.Format("Method names cannot be longer than {0} characters.", MAX_METHOD_NAME));
                signature.Length = MAX_METHOD_NAME;
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
            while (Match(TokenType.TOKEN_COMMA));

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
                int constant = AddConstant(new Value(ValueType.Null));
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
            Signature called = new Signature { Type = SignatureType.SIG_GETTER, Arity = 0, Name = signature.Name, Length = signature.Length };

            // Parse the argument list, if any.
            if (Match(TokenType.TOKEN_LEFT_PAREN))
            {
                called.Type = SignatureType.SIG_METHOD;

                // Allow empty an argument list.
                if (Peek() != TokenType.TOKEN_RIGHT_PAREN)
                {
                    FinishArgumentList(called);
                }
                Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after arguments.");
            }

            // Parse the block argument, if any.
            if (Match(TokenType.TOKEN_LEFT_BRACE))
            {
                // Include the block argument in the arity.
                called.Type = SignatureType.SIG_METHOD;
                called.Arity++;

                Compiler fnCompiler = new Compiler(parser, this, true);

                // Make a dummy signature to track the arity.
                Signature fnSignature = new Signature { Arity = 0 };

                // Parse the parameter list, if any.
                if (Match(TokenType.TOKEN_PIPE))
                {
                    fnCompiler.FinishParameterList(fnSignature);
                    Consume(TokenType.TOKEN_PIPE, "Expect '|' after function parameters.");
                }

                fnCompiler.numParams = fnSignature.Arity;

                fnCompiler.FinishBody(false);

                String blockName = SignatureToString(called) + " block argument";
                fnCompiler.EndCompiler(blockName);
            }

            // TODO: Allow Grace-style mixfix methods?

            // If this is a super() call for an initializer, make sure we got an actual
            // argument list.
            if (signature.Type == SignatureType.SIG_INITIALIZER)
            {
                if (called.Type != SignatureType.SIG_METHOD)
                {
                    Error("A superclass constructor must have an argument list.");
                }

                called.Type = SignatureType.SIG_INITIALIZER;
            }

            CallSignature(instruction, called);
        }

        // Compiles a call whose name is the previously consumed token. This includes
        // getters, method calls with arguments, and setter calls.
        private void NamedCall(bool allowAssignment, Instruction instruction)
        {
            // Get the token for the method name.
            Signature signature = SignatureFromToken(new Signature(), SignatureType.SIG_GETTER);

            if (Match(TokenType.TOKEN_EQ))
            {
                if (!allowAssignment) Error("Invalid assignment.");

                IgnoreNewlines();

                // Build the setter signature.
                signature.Type = SignatureType.SIG_SETTER;
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
            int symbol = parser.module.Variables.FindIndex(v => v.Name == name);
            EmitShortArg(Instruction.LOAD_MODULE_VAR, symbol);
        }

        // A parenthesized expression.
        private static void Grouping(Compiler c, bool allowAssignment)
        {
            c.Expression();
            c.Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after expression.");
        }

        // A list literal.
        private static void List(Compiler c, bool allowAssignment)
        {
            // Load the List class.
            c.LoadCoreVariable("List");

            // Instantiate a new list.
            c.CallMethod(0, "new()");

            // Compile the list elements. Each one compiles to a ".add()" call.
            if (c.Peek() != TokenType.TOKEN_RIGHT_BRACKET)
            {
                do
                {
                    c.IgnoreNewlines();

                    if (c.Peek() == TokenType.TOKEN_RIGHT_BRACKET)
                        break;

                    // Push a copy of the list since the add() call will consume it.
                    c.Emit(Instruction.DUP);

                    // The element.
                    c.Expression();
                    c.CallMethod(1, "add(_)");

                    // Discard the result of the add() call.
                    c.Emit(Instruction.POP);
                } while (c.Match(TokenType.TOKEN_COMMA));
            }

            // Allow newlines before the closing ']'.
            c.IgnoreNewlines();
            c.Consume(TokenType.TOKEN_RIGHT_BRACKET, "Expect ']' after list elements.");
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
            if (c.Peek() != TokenType.TOKEN_RIGHT_BRACE)
            {
                do
                {
                    c.IgnoreNewlines();

                    if (c.Peek() == TokenType.TOKEN_RIGHT_BRACE)
                        break;

                    // Push a copy of the map since the subscript call will consume it.
                    c.Emit(Instruction.DUP);

                    // The key.
                    c.ParsePrecedence(false, Precedence.PREC_PRIMARY);
                    c.Consume(TokenType.TOKEN_COLON, "Expect ':' after map key.");

                    // The value.
                    c.Expression();

                    c.CallMethod(2, "[_]=(_)");

                    // Discard the result of the setter call.
                    c.Emit(Instruction.POP);
                } while (c.Match(TokenType.TOKEN_COMMA));
            }

            // Allow newlines before the closing '}'.
            c.IgnoreNewlines();
            c.Consume(TokenType.TOKEN_RIGHT_BRACE, "Expect '}' after map entries.");
        }

        // Unary operators like `-foo`.
        private static void UnaryOp(Compiler c, bool allowAssignment)
        {
            GrammarRule rule = c.GetRule(c.parser.previous.type);

            c.IgnoreNewlines();

            // Compile the argument.
            c.ParsePrecedence(false, Precedence.PREC_UNARY + 1);

            // Call the operator method on the left-hand side.
            c.CallMethod(0, rule.name);
        }

        private static void Boolean(Compiler c, bool allowAssignment)
        {
            c.Emit(c.parser.previous.type == TokenType.TOKEN_FALSE ? Instruction.FALSE : Instruction.TRUE);
        }

        // Walks the compiler chain to find the compiler for the nearest class
        // enclosing this one. Returns null if not currently inside a class definition.
        private Compiler getEnclosingClassCompiler()
        {
            Compiler compiler = this;
            while (compiler != null)
            {
                if (compiler.enclosingClass != null) return compiler;
                compiler = compiler.parent;
            }

            return null;
        }

        // Walks the compiler chain to find the nearest class enclosing this one.
        // Returns null if not currently inside a class definition.
        private ClassCompiler GetEnclosingClass()
        {
            Compiler compiler = getEnclosingClassCompiler();
            return compiler == null ? null : compiler.enclosingClass;
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
            else if (enclosingClass.isStaticMethod)
            {
                c.Error("Cannot use an instance field in a static method.");
            }
            else
            {
                // Look up the field, or implicitly define it.
                string fieldName = c.parser.source.Substring(c.parser.previous.start, c.parser.previous.length);
                field = enclosingClass.fields.IndexOf(fieldName);
                if (field < 0)
                {
                    enclosingClass.fields.Add(fieldName);
                    field = enclosingClass.fields.IndexOf(fieldName);
                }

                if (field >= MAX_FIELDS)
                {
                    c.Error(string.Format("A class can only have {0} fields.", MAX_FIELDS));
                }
            }

            // If there's an "=" after a field name, it's an assignment.
            bool isLoad = true;
            if (c.Match(TokenType.TOKEN_EQ))
            {
                if (!allowAssignment) c.Error("Invalid assignment.");

                // Compile the right-hand side.
                c.Expression();
                isLoad = false;
            }

            // If we're directly inside a method, use a more optimal instruction.
            if (c.parent != null && c.parent.enclosingClass == enclosingClass)
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
            if (Match(TokenType.TOKEN_EQ))
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

            Compiler classCompiler = c.getEnclosingClassCompiler();
            if (classCompiler == null)
            {
                c.Error("Cannot use a static field outside of a class definition.");
            }
            else
            {
                // Look up the name in the scope chain.
                Token token = c.parser.previous;

                // If this is the first time we've seen this static field, implicitly
                // define it as a variable in the scope surrounding the class definition.
                if (classCompiler.ResolveLocal(c.parser.source.Substring(token.start, token.length), token.length) == -1)
                {
                    int symbol = classCompiler.DeclareVariable(null);

                    // Implicitly initialize it to null.
                    classCompiler.Emit(Instruction.NULL);
                    classCompiler.DefineVariable(symbol);
                }

                // It definitely exists now, so resolve it properly. This is different from
                // the above resolveLocal() call because we may have already closed over it
                // as an upvalue.
                index = c.ResolveName(c.parser.source.Substring(token.start, token.length), token.length, out loadInstruction);
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
            Token token = c.parser.previous;

            Instruction loadInstruction;
            string varName = c.parser.source.Substring(token.start, token.length);
            int index = c.ResolveNonmodule(varName, token.length, out loadInstruction);
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
            int module = c.parser.module.Variables.FindIndex(v => v.Name == varName);
            if (module == -1)
            {
                if (IsLocalName(varName))
                {
                    c.Error("Undefined variable.");
                    return;
                }

                // If it's a nonlocal name, implicitly define a module-level variable in
                // the hopes that we get a real definition later.
                module = c.parser.vm.DeclareVariable(c.parser.module, varName);

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
            int constant = c.AddConstant(new Value(c.parser.number));

            // Compile the code to load the constant.
            c.EmitShortArg(Instruction.CONSTANT, constant);
        }

        // Parses a string literal and adds it to the constant table.
        private int StringConstant()
        {
            // Define a constant for the literal.
            int constant = AddConstant(new Value(parser.raw));

            parser.raw = "";

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
            if (c.Match(TokenType.TOKEN_DOT))
            {
                // Compile the superclass call.
                c.Consume(TokenType.TOKEN_NAME, "Expect method name after 'super.'.");
                c.NamedCall(allowAssignment, Instruction.SUPER_0);
            }
            else if (enclosingClass != null)
            {
                // No explicit name, so use the name of the enclosing method. Make sure we
                // check that enclosingClass isn't null first. We've already reported the
                // error, but we don't want to crash here.
                c.MethodCall(Instruction.SUPER_0, enclosingClass.signature);
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
            Signature signature = new Signature { Name = "", Length = 0, Type = SignatureType.SIG_SUBSCRIPT, Arity = 0 };

            // Parse the argument list.
            c.FinishArgumentList(signature);
            c.Consume(TokenType.TOKEN_RIGHT_BRACKET, "Expect ']' after arguments.");

            if (c.Match(TokenType.TOKEN_EQ))
            {
                if (!allowAssignment) c.Error("Invalid assignment.");

                signature.Type = SignatureType.SIG_SUBSCRIPT_SETTER;

                // Compile the assigned value.
                c.ValidateNumParameters(++signature.Arity);
                c.Expression();
            }

            c.CallSignature(Instruction.CALL_0, signature);
        }

        private static void Call(Compiler c, bool allowAssignment)
        {
            c.IgnoreNewlines();
            c.Consume(TokenType.TOKEN_NAME, "Expect method name after '.'.");
            c.NamedCall(allowAssignment, Instruction.CALL_0);
        }

        private static void and_(Compiler c, bool allowAssignment)
        {
            c.IgnoreNewlines();

            // Skip the right argument if the left is false.
            int jump = c.EmitJump(Instruction.AND);
            c.ParsePrecedence(false, Precedence.PREC_LOGICAL_AND);
            c.PatchJump(jump);
        }

        static void or_(Compiler c, bool allowAssignment)
        {
            c.IgnoreNewlines();

            // Skip the right argument if the left is true.
            int jump = c.EmitJump(Instruction.OR);
            c.ParsePrecedence(false, Precedence.PREC_LOGICAL_OR);
            c.PatchJump(jump);
        }

        private static void Conditional(Compiler c, bool allowAssignment)
        {
            // Ignore newline after '?'.
            c.IgnoreNewlines();

            // Jump to the else branch if the condition is false.
            int ifJump = c.EmitJump(Instruction.JUMP_IF);

            // Compile the then branch.
            c.ParsePrecedence(allowAssignment, Precedence.PREC_TERNARY);

            c.Consume(TokenType.TOKEN_COLON, "Expect ':' after then branch of conditional operator.");
            c.IgnoreNewlines();

            // Jump over the else branch when the if branch is taken.
            int elseJump = c.EmitJump(Instruction.JUMP);

            // Compile the else branch.
            c.PatchJump(ifJump);

            c.ParsePrecedence(allowAssignment, Precedence.PREC_ASSIGNMENT);

            // Patch the jump over the else.
            c.PatchJump(elseJump);
        }

        private static void InfixOp(Compiler c, bool allowAssignment)
        {
            GrammarRule rule = c.GetRule(c.parser.previous.type);

            // An infix operator cannot end an expression.
            c.IgnoreNewlines();

            // Compile the right-hand side.
            c.ParsePrecedence(false, rule.precedence + 1);

            // Call the operator method on the left-hand side.
            Signature signature = new Signature { Type = SignatureType.SIG_METHOD, Arity = 1, Name = rule.name, Length = rule.name.Length };
            c.CallSignature(Instruction.CALL_0, signature);
        }

        // Compiles a method signature for an infix operator.
        static void InfixSignature(Compiler c, Signature signature)
        {
            // Add the RHS parameter.
            signature.Type = SignatureType.SIG_METHOD;
            signature.Arity = 1;

            // Parse the parameter name.
            c.Consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after operator name.");
            c.DeclareNamedVariable();
            c.Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after parameter name.");
        }

        // Compiles a method signature for an unary operator (i.e. "!").
        private static void UnarySignature(Compiler c, Signature signature)
        {
            // Do nothing. The name is already complete.
            signature.Type = SignatureType.SIG_GETTER;
        }

        // Compiles a method signature for an operator that can either be unary or
        // infix (i.e. "-").
        private static void MixedSignature(Compiler c, Signature signature)
        {
            signature.Type = SignatureType.SIG_GETTER;

            // If there is a parameter, it's an infix operator, otherwise it's unary.
            if (c.Match(TokenType.TOKEN_LEFT_PAREN))
            {
                // Add the RHS parameter.
                signature.Type = SignatureType.SIG_METHOD;
                signature.Arity = 1;

                // Parse the parameter name.
                c.DeclareNamedVariable();
                c.Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after parameter name.");
            }
        }

        // Compiles an optional setter parameter in a method [signature].
        //
        // Returns `true` if it was a setter.
        private bool MaybeSetter(Signature signature)
        {
            // See if it's a setter.
            if (!Match(TokenType.TOKEN_EQ)) return false;

            // It's a setter.
            signature.Type = signature.Type == SignatureType.SIG_SUBSCRIPT
                ? SignatureType.SIG_SUBSCRIPT_SETTER
                : SignatureType.SIG_SETTER;

            // Parse the value parameter.
            Consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after '='.");
            DeclareNamedVariable();
            Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after parameter name.");

            signature.Arity++;

            return true;
        }

        // Compiles a method signature for a subscript operator.
        private static void SubscriptSignature(Compiler c, Signature signature)
        {
            signature.Type = SignatureType.SIG_SUBSCRIPT;

            // The signature currently has "[" as its name since that was the token that
            // matched it. Clear that out.
            signature.Length = 0;
            signature.Name = "";

            // Parse the parameters inside the subscript.
            c.FinishParameterList(signature);
            c.Consume(TokenType.TOKEN_RIGHT_BRACKET, "Expect ']' after parameters.");

            c.MaybeSetter(signature);
        }

        // Parses an optional parenthesized parameter list. Updates `type` and `arity`
        // in [signature] to match what was parsed.
        private void ParameterList(Signature signature)
        {
            // The parameter list is optional.
            if (!Match(TokenType.TOKEN_LEFT_PAREN)) return;

            signature.Type = SignatureType.SIG_METHOD;

            // Allow an empty parameter list.
            if (Match(TokenType.TOKEN_RIGHT_PAREN)) return;

            FinishParameterList(signature);
            Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after parameters.");
        }

        // Compiles a method signature for a named method or setter.
        private static void NamedSignature(Compiler c, Signature signature)
        {
            signature.Type = SignatureType.SIG_GETTER;

            // If it's a setter, it can't also have a parameter list.
            if (c.MaybeSetter(signature)) return;

            // Regular named method with an optional parameter list.
            c.ParameterList(signature);
        }

        // Compiles a method signature for a constructor.
        private static void ConstructorSignature(Compiler c, Signature signature)
        {
            c.Consume(TokenType.TOKEN_NAME, "Expect constructor name after 'construct'.");

            // Capture the name.
            signature = c.SignatureFromToken(signature, SignatureType.SIG_INITIALIZER);

            if (c.Match(TokenType.TOKEN_EQ))
            {
                c.Error("A constructor cannot be a setter.");
            }

            if (!c.Match(TokenType.TOKEN_LEFT_PAREN))
            {
                c.Error("A constructor cannot be a getter.");
                return;
            }

            if (c.Match(TokenType.TOKEN_RIGHT_PAREN)) return;

            c.FinishParameterList(signature);
            c.Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after parameters.");
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
        private readonly GrammarRule[] rules = 
{
  /* TOKEN_LEFT_PAREN    */ new GrammarRule(Grouping, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_RIGHT_PAREN   */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_LEFT_BRACKET  */ new GrammarRule(List, Subscript, SubscriptSignature, Precedence.PREC_CALL, null),
  /* TOKEN_RIGHT_BRACKET */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_LEFT_BRACE    */ new GrammarRule(Map, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_RIGHT_BRACE   */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_COLON         */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_DOT           */ new GrammarRule(null, Call, null, Precedence.PREC_CALL, null),
  /* TOKEN_DOTDOT        */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_RANGE, ".."),
  /* TOKEN_DOTDOTDOT     */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_RANGE, "..."),
  /* TOKEN_COMMA         */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_STAR          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_FACTOR, "*"),
  /* TOKEN_SLASH         */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_FACTOR, "/"),
  /* TOKEN_PERCENT       */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_FACTOR, "%"),
  /* TOKEN_PLUS          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_TERM, "+"),
  /* TOKEN_MINUS         */ new GrammarRule(UnaryOp, InfixOp, MixedSignature, Precedence.PREC_TERM, "-"),
  /* TOKEN_LTLT          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_BITWISE_SHIFT, "<<"),
  /* TOKEN_GTGT          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_BITWISE_SHIFT, ">>"),
  /* TOKEN_PIPE          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_BITWISE_OR, "|"),
  /* TOKEN_PIPEPIPE      */ new GrammarRule(null, or_, null, Precedence.PREC_LOGICAL_OR, null),
  /* TOKEN_CARET         */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_BITWISE_XOR, "^"),
  /* TOKEN_AMP           */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_BITWISE_AND, "&"),
  /* TOKEN_AMPAMP        */ new GrammarRule(null, and_, null, Precedence.PREC_LOGICAL_AND, null),
  /* TOKEN_BANG          */ new GrammarRule(UnaryOp, null, UnarySignature, Precedence.PREC_NONE, "!"),
  /* TOKEN_TILDE         */ new GrammarRule(UnaryOp, null, UnarySignature, Precedence.PREC_NONE, "~"),
  /* TOKEN_QUESTION      */ new GrammarRule(null, Conditional, null, Precedence.PREC_ASSIGNMENT, null),
  /* TOKEN_EQ            */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_LT            */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_COMPARISON, "<"),
  /* TOKEN_GT            */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_COMPARISON, ">"),
  /* TOKEN_LTEQ          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_COMPARISON, "<="),
  /* TOKEN_GTEQ          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_COMPARISON, ">="),
  /* TOKEN_EQEQ          */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_EQUALITY, "=="),
  /* TOKEN_BANGEQ        */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_EQUALITY, "!="),
  /* TOKEN_BREAK         */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_CLASS         */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_CONSTRUCT     */ new GrammarRule(null, null, ConstructorSignature, Precedence.PREC_NONE, null),
  /* TOKEN_ELSE          */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_FALSE         */ new GrammarRule(Boolean, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_FOR           */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_USING         */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_IF            */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_IMPORT        */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_IN            */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_IS            */ new GrammarRule(null, InfixOp, InfixSignature, Precedence.PREC_IS, "is"),
  /* TOKEN_NULL          */ new GrammarRule(null_, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_RETURN        */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_STATIC        */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_SUPER         */ new GrammarRule(super_, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_THIS          */ new GrammarRule(this_, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_TRUE          */ new GrammarRule(Boolean, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_VAR           */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_WHILE         */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_FIELD         */ new GrammarRule(Field, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_STATIC_FIELD  */ new GrammarRule(StaticField, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_NAME          */ new GrammarRule(Name, null, NamedSignature, Precedence.PREC_NONE, null),
  /* TOKEN_NUMBER        */ new GrammarRule(Number, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_STRING        */ new GrammarRule(string_, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_LINE          */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_ERROR         */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null),
  /* TOKEN_EOF           */ new GrammarRule(null, null, null, Precedence.PREC_NONE, null)
};

        // Gets the [GrammarRule] associated with tokens of [type].
        private GrammarRule GetRule(TokenType type)
        {
            return rules[(byte)type];
        }

        // The main entrypoint for the top-down operator precedence parser.
        private void ParsePrecedence(bool allowAssignment, Precedence precedence)
        {
            NextToken();
            GrammarFn prefix = rules[(int)parser.previous.type].prefix;

            if (prefix == null)
            {
                Error("Expected expression.");
                return;
            }

            prefix(this, allowAssignment);

            while (rules[(int)parser.current.type].precedence >= precedence)
            {
                NextToken();
                GrammarFn infix = rules[(int)parser.previous.type].infix;
                infix(this, allowAssignment);
            }
        }

        // Parses an expression. Unlike statements, expressions leave a resulting value
        // on the stack.
        private void Expression()
        {
            ParsePrecedence(true, Precedence.PREC_LOWEST);
        }

        // Parses a curly block or an expression statement. Used in places like the
        // arms of an if statement where either a single expression or a curly body is
        // allowed.
        private void Block()
        {
            // Curly block.
            if (Match(TokenType.TOKEN_LEFT_BRACE))
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
        private static int GetNumArguments(byte[] bytecode, List<Value> constants, int ip)
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
                        ObjFn loadedFn = (ObjFn)constants[constant].Obj;

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
            l.enclosing = loop;
            l.start = bytecode.Count - 1 - 1;
            l.scopeDepth = scopeDepth;
            loop = l;
        }

        // Emits the [Instruction.JUMP_IF] instruction used to test the loop condition and
        // potentially exit the loop. Keeps track of the instruction so we can patch it
        // later once we know where the end of the body is.
        private void TestExitLoop()
        {
            if (loop == null)
                return;
            loop.exitJump = EmitJump(Instruction.JUMP_IF);
        }

        // Compiles the body of the loop and tracks its extent so that contained "break"
        // statements can be handled correctly.
        private void LoopBody()
        {
            if (loop == null)
                return;
            loop.body = bytecode.Count - 1;
            Block();
        }

        // Ends the current innermost loop. Patches up all jumps and breaks now that
        // we know where the end of the loop is.
        private void EndLoop()
        {
            int loopOffset = bytecode.Count - 1 - loop.start + 2;
            // TODO: Check for overflow.
            EmitShortArg(Instruction.LOOP, loopOffset);

            PatchJump(loop.exitJump);

            // Find any break placeholder instructions (which will be Instruction.END in the
            // bytecode) and replace them with real jumps.
            int i = loop.body;
            while (i < bytecode.Count - 1)
            {
                if (bytecode[i] == (byte)Instruction.END)
                {
                    bytecode[i] = (byte)Instruction.JUMP;
                    PatchJump(i + 1);
                    i += 3;
                }
                else
                {
                    // Skip this instruction and its arguments.
                    i += 1 + GetNumArguments(bytecode.ToArray(), constants, i);
                }
            }

            loop = loop.enclosing;
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

            Consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after 'for'.");
            Consume(TokenType.TOKEN_NAME, "Expect for loop variable name.");

            // Remember the name of the loop variable.
            string name = parser.source.Substring(parser.previous.start, parser.previous.length);
            int length = parser.previous.length;

            Consume(TokenType.TOKEN_IN, "Expect 'in' after loop variable.");
            IgnoreNewlines();

            // Evaluate the sequence expression and store it in a hidden local variable.
            // The space in the variable name ensures it won't collide with a user-defined
            // variable.
            Expression();
            int seqSlot = DefineLocal("seq ", 4);

            // Create another hidden local for the iterator object.
            null_(this, false);
            int iterSlot = DefineLocal("iter ", 5);

            Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after loop expression.");

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
            Consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after 'while'.");
            Expression();
            Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after while condition.");

            TestExitLoop();
            LoopBody();
            EndLoop();
        }

        // Compiles a statement. These can only appear at the top-level or within
        // curly blocks. Unlike expressions, these do not leave a value on the stack.
        private void Statement()
        {
            if (Match(TokenType.TOKEN_BREAK))
            {
                if (loop == null)
                {
                    Error("Cannot use 'break' outside of a loop.");
                    return;
                }

                // Since we will be jumping out of the scope, make sure any locals in it
                // are discarded first.
                DiscardLocals(loop.scopeDepth + 1);

                // Emit a placeholder instruction for the jump to the end of the body. When
                // we're done compiling the loop body and know where the end is, we'll
                // replace these with `Instruction.JUMP` instructions with appropriate offsets.
                // We use `Instruction.END` here because that can't occur in the middle of
                // bytecode.
                EmitJump(Instruction.END);
                return;
            }

            if (Match(TokenType.TOKEN_FOR))
            {
                ForStatement();
                return;
            }

            if (Match(TokenType.TOKEN_IF))
            {
                // Compile the condition.
                Consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after 'if'.");
                Expression();
                Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after if condition.");

                // Jump to the else branch if the condition is false.
                int ifJump = EmitJump(Instruction.JUMP_IF);

                // Compile the then branch.
                Block();

                // Compile the else branch if there is one.
                if (Match(TokenType.TOKEN_ELSE))
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

            if (Match(TokenType.TOKEN_RETURN))
            {
                // Compile the return value.
                if (Peek() == TokenType.TOKEN_LINE)
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

            if (Match(TokenType.TOKEN_WHILE))
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

            bool isForeign = Match(TokenType.TOKEN_FOREIGN);
            classCompiler.isStaticMethod = Match(TokenType.TOKEN_STATIC);

            SignatureFn signatureFn = GetRule(parser.current.type).method;
            NextToken();

            if (signatureFn == null)
            {
                Error("Expect method definition.");
                return false;
            }

            // Build the method signature.
            Signature signature = SignatureFromToken(new Signature(), SignatureType.SIG_GETTER);
            classCompiler.signature = signature;

            Compiler methodCompiler = new Compiler(parser, this, false);
            signatureFn(methodCompiler, signature);

            if (classCompiler.isStaticMethod && signature.Type == SignatureType.SIG_INITIALIZER)
            {
                Error("A constructor cannot be static.");
            }

            String fullSignature = SignatureToString(signature);

            if (isForeign)
            {
                int constant = AddConstant(new Value(fullSignature));

                EmitShortArg(Instruction.CONSTANT, constant);
            }
            else
            {
                Consume(TokenType.TOKEN_LEFT_BRACE, "Expect '{' to begin method body.");
                methodCompiler.FinishBody(signature.Type == SignatureType.SIG_INITIALIZER);
                methodCompiler.EndCompiler(fullSignature);
            }

            // Define the method. For a constructor, this defines the instance
            // initializer method.
            int methodSymbol = SignatureSymbol(signature);
            DefineMethod(classSlot, classCompiler.isStaticMethod, methodSymbol);

            if (signature.Type == SignatureType.SIG_INITIALIZER)
            {
                signature.Type = SignatureType.SIG_METHOD;
                int constructorSymbol = SignatureSymbol(signature);
                CreateConstructor(signature, methodSymbol);
                DefineMethod(classSlot, true, constructorSymbol);
            }

            return true;
        }

        private void CreateConstructor(Signature signature, int initializerSymbol)
        {
            Compiler methodCompiler = new Compiler(parser, this, false);

            methodCompiler.Emit(enclosingClass.isForeign ? Instruction.FOREIGN_CONSTRUCT : Instruction.CONSTRUCT);
            methodCompiler.EmitShortArg(Instruction.CALL_0 + signature.Arity, initializerSymbol);
            methodCompiler.Emit(Instruction.RETURN);

            methodCompiler.EndCompiler("");
        }

        private void DefineMethod(int classSlot, bool isStaticMethod, int methodSymbol)
        {
            if (scopeDepth == 0)
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
            int nameConstant = AddConstant(new Value(parser.source.Substring(parser.previous.start, parser.previous.length)));

            EmitShortArg(Instruction.CONSTANT, nameConstant);

            // Load the superclass (if there is one).
            if (Match(TokenType.TOKEN_IS))
            {
                ParsePrecedence(false, Precedence.PREC_CALL);
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

            classCompiler.fields = fields;
            classCompiler.isForeign = isForeign;

            enclosingClass = classCompiler;

            // Compile the method definitions.
            Consume(TokenType.TOKEN_LEFT_BRACE, "Expect '{' after class declaration.");
            MatchLine();

            while (!Match(TokenType.TOKEN_RIGHT_BRACE))
            {
                if (!Method(classCompiler, slot)) break;

                // Don't require a newline after the last definition.
                if (Match(TokenType.TOKEN_RIGHT_BRACE)) break;

                ConsumeLine("Expect newline after definition in class.");
            }

            if (!isForeign)
            {
                // Update the class with the number of fields.
                bytecode[numFieldsInstruction] = (byte)fields.Count;
            }

            enclosingClass = null;

            PopScope();
        }

        private void Import()
        {
            Consume(TokenType.TOKEN_STRING, "Expect a string after 'import'.");
            int moduleConstant = StringConstant();

            // Load the module.
            EmitShortArg(Instruction.LOAD_MODULE, moduleConstant);

            // Discard the unused result value from calling the module's fiber.
            Emit(Instruction.POP);

            // The for clause is optional.
            if (!Match(TokenType.TOKEN_FOR)) return;

            // Compile the comma-separated list of variables to import.
            do
            {
                int slot = DeclareNamedVariable();

                // Define a string constant for the variable name.
                string varName = parser.source.Substring(parser.previous.start, parser.previous.length);
                int variableConstant = AddConstant(new Value(varName));

                // Load the variable from the other module.
                EmitShortArg(Instruction.IMPORT_VARIABLE, moduleConstant);
                EmitShort(variableConstant);

                // Store the result in the variable here.
                DefineVariable(slot);
            } while (Match(TokenType.TOKEN_COMMA));
        }

        private void VariableDefinition()
        {
            // Grab its name, but don't declare it yet. A (local) variable shouldn't be
            // in scope in its own initializer.
            Consume(TokenType.TOKEN_NAME, "Expect variable name.");
            Token nameToken = parser.previous;

            // Compile the initializer.
            if (Match(TokenType.TOKEN_EQ))
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
            if (Match(TokenType.TOKEN_CLASS))
            {
                ClassDefinition(false);
                return;
            }

            if (Match(TokenType.TOKEN_IMPORT))
            {
                Import();
                return;
            }

            if (Match(TokenType.TOKEN_VAR))
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
                module = module,
                sourcePath = sourcePath,
                source = source,
                tokenStart = 0,
                currentChar = 0,
                currentLine = 1,
                current = { type = TokenType.TOKEN_ERROR, start = 0, length = 0, line = 0 },
                skipNewlines = true,
                printErrors = printErrors,
                hasError = false,
                raw = ""
            };

            Compiler compiler = new Compiler(parser, null, true);

            // Read the first token.
            compiler.NextToken();

            compiler.IgnoreNewlines();

            while (!compiler.Match(TokenType.TOKEN_EOF))
            {
                compiler.Definition();

                // If there is no newline, it must be the end of the block on the same line.
                if (!compiler.MatchLine())
                {
                    compiler.Consume(TokenType.TOKEN_EOF, "Expect end of file.");
                    break;
                }
            }

            compiler.Emit(Instruction.NULL);
            compiler.Emit(Instruction.RETURN);

            // See if there are any implicitly declared module-level variables that never
            // got an explicit definition.
            // TODO: It would be nice if the error was on the line where it was used.
            for (int i = 0; i < parser.module.Variables.Count; i++)
            {
                ModuleVariable t = parser.module.Variables[i];
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
                            fn.Constants[constant] = new Value(classObj.Superclass);
                            break;
                        }

                    case Instruction.CLOSURE:
                        {
                            // Bind the nested closure too.
                            int constant = (fn.Bytecode[ip] << 8) | fn.Bytecode[ip + 1];
                            BindMethodCode(classObj, fn.Constants[constant].Obj as ObjFn);

                            ip += GetNumArguments(fn.Bytecode, new List<Value>(fn.Constants), ip - 1);
                            break;
                        }

                    case Instruction.END:
                        return;

                    default:
                        // Other instructions are unaffected, so just skip over them.
                        ip += GetNumArguments(fn.Bytecode, new List<Value>(fn.Constants), ip - 1);
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
                            ObjFn loadedFn = (ObjFn)fn.Constants[constant].Obj;

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
