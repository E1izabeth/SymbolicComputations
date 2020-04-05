using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using R = System.Text.RegularExpressions;

namespace SymbolicComputations
{

    enum TokenKind
    {
        Number,
        Symbol,
        String,
        Whitespace,
        Comma,
        OpenBracket,
        CloseBracket,
        OpenGroup,
        CloseGroup,
        OpChar,
    }

    class OpTokenInfo
    {
        public string Name { get; private set; }
        public string Symbol { get; private set; }
        public R.Regex Pattern { get; private set; }

        public int? UnaryBindingPower { get; set; }
        public int? BinaryBindingPower { get; set; }
        public bool InverseBinaryAssociativity { get; set; }
        public Func<Expr, Expr> UnaryHandler { get; set; }
        public Func<Expr, Expr, Expr> BinaryHandler { get; set; }

        public OpTokenInfo(string name, string symbol, R.Regex pattern)
        {
            this.Name = name;
            this.Symbol = symbol;
            this.Pattern = pattern;
        }
    }

    class Tokenizer
    {
        static readonly Dictionary<TokenKind, OpTokenInfo> _builtinPatterns = new Dictionary<TokenKind, OpTokenInfo>();

        static Tokenizer()
        {
            void registerUtility(TokenKind kind, string pattern)
            {
                _builtinPatterns.Add(kind, new OpTokenInfo(kind.ToString(), null, new R.Regex(pattern)));
            }

            void registerUnary(TokenKind kind, string pattern, int precedence)
            {
                _builtinPatterns.Add(kind, new OpTokenInfo(kind.ToString(), null, new R.Regex(pattern)) { UnaryBindingPower = precedence });
            }

            void registerBinary(TokenKind kind, string pattern, int precedence, bool inverse = false)
            {
                _builtinPatterns.Add(kind, new OpTokenInfo(kind.ToString(), null, new R.Regex(pattern)) { BinaryBindingPower = precedence, InverseBinaryAssociativity = inverse });
            }

            registerUnary(TokenKind.Number, "^[0-9]+(\\.[0-9]+)?", int.MinValue);
            registerUnary(TokenKind.Symbol, "^\\w+", int.MinValue);
            // registerUnary(TokenKind.String, "^\"([^\"]|(\\.))*\"", int.MinValue);
            registerUnary(TokenKind.String, @"^""[^""\\]*(?:\\.[^""\\]*)*""", int.MinValue);

            registerUtility(TokenKind.Whitespace, "^((\\s+)|(//[^\\n\\r]+[\r\n]))");
            registerUtility(TokenKind.Comma, "^\\,");

            registerBinary(TokenKind.OpenBracket, "^\\[", 100000);
            registerUnary(TokenKind.OpenGroup, "^\\(", int.MaxValue);

            registerUnary(TokenKind.CloseBracket, "^\\]", int.MinValue);
            registerUnary(TokenKind.CloseGroup, "^\\)", int.MinValue);
        }

        readonly Dictionary<string, OpTokenInfo> _opTokenInfoByCharacter = new Dictionary<string, OpTokenInfo>();

        public Tokenizer()
        {
        }

        private OpTokenInfo RegisterOpSymbol(string name, string character)
        {
            if (_builtinPatterns.Values.Any(p => p.Pattern.IsMatch(character) || Enum.TryParse<TokenKind>(name, out var kind)))
                throw new ArgumentException();

            if (!_opTokenInfoByCharacter.TryGetValue(character, out var info))
                _opTokenInfoByCharacter.Add(character, info = new OpTokenInfo(name, character, new R.Regex("^" + R.Regex.Escape(character))));

            return info;
        }

        public void RegisterBinaryOp(string name, string character, int precedence, Func<Expr, Expr, Expr> handler, bool inverseAssociatity = false)
        {
            var info = this.RegisterOpSymbol(name, character);
            info.BinaryBindingPower = precedence;
            info.BinaryHandler = handler;
            info.InverseBinaryAssociativity = inverseAssociatity;
        }

        public void RegisterUnaryOp(string name, string character, int precedence, Func<Expr, Expr> unaryHandler)
        {
            var info = this.RegisterOpSymbol(name, character);
            info.UnaryBindingPower = precedence;
            info.UnaryHandler = unaryHandler;
        }

        public IEnumerable<Token> Tokenize(string source)
        {
            var text = source;
            var knownTokens = _builtinPatterns.Select(kv => (kv.Key, kv.Value)).Concat(
                _opTokenInfoByCharacter.Values.OrderByDescending(v => v.Symbol.Length)
                                       .Select(v => (TokenKind.OpChar, v))
            ).ToArray();

            var pos = 0;
            while (text.Length > 0)
            {
                var ok = false;

                foreach (var (kind, info) in knownTokens)
                {
                    var m = info.Pattern.Match(text);

                    if (m.Success)
                    {
                        yield return new Token(kind, m.Value, info, pos);
                        pos += m.Length;
                        text = text.Substring(m.Length);
                        ok = true;
                        break;
                    }
                }

                if (!ok)
                    throw new ApplicationException("error at " + pos + " near the text: " + source.Substring(pos, Math.Min(10, source.Length - pos)));
            }
        }
    }

    class Token
    {
        public TokenKind Kind { get; private set; }
        public string Content { get; private set; }
        public OpTokenInfo TokenInfo { get; private set; }
        public int Pos { get; private set; }

        public Token(TokenKind kind, string content, OpTokenInfo tokenInfo, int pos)
        {
            this.Kind = kind;
            this.Content = content;
            this.TokenInfo = tokenInfo;
            this.Pos = pos;
        }

        public override string ToString()
        {
            return (this.TokenInfo == null ? this.Kind.ToString() : this.TokenInfo.Name) + "@" + this.Pos + ": " + this.Content;
        }
    }

    class ExprParser
    {
        class ParsingContext
        {
            public Token CurrentToken { get; private set; }
            public bool HasNextToken { get; private set; }

            readonly IEnumerator<Token> _tokens;

            public ParsingContext(IEnumerable<Token> tokens)
            {
                _tokens = tokens.Where(t => t.Kind != TokenKind.Whitespace).GetEnumerator();
                this.HasNextToken = true;
                this.Advance();
            }

            public Token ExpectToken(TokenKind kind)
            {
                var t = this.Advance();
                if (t.Kind != kind)
                    throw new ApplicationException("Unexpected " + t.Kind + " while expecting " + kind + " at " + t.Pos);

                return t;
            }

            public Token Advance()
            {
                if (!this.HasNextToken)
                    throw new ApplicationException("Unexpected end of expression");

                this.CurrentToken = _tokens.Current;
                this.HasNextToken = _tokens.MoveNext();
                return this.CurrentToken;
            }

            public Token Peek()
            {
                return this.HasNextToken ? _tokens.Current : null;
            }
        }

        public Tokenizer Tokenizer { get; private set; }

        readonly Dictionary<TokenKind, Func<ParsingContext, int, Expr>> _prefixHandlers;
        readonly Dictionary<TokenKind, Func<ParsingContext, int, Expr, Expr>> _infixHandlers;

        public ExprParser()
        {
            this.Tokenizer = new Tokenizer();

            _prefixHandlers = new Dictionary<TokenKind, Func<ParsingContext, int, Expr>>()
            {
                { TokenKind.Number, (ctx, bp) => double.Parse(ctx.CurrentToken.Content) },
                { TokenKind.Symbol, (ctx, bp) => KnownNames.Symbol(ctx.CurrentToken.Content) },
                { TokenKind.String, (ctx, bp) => R.Regex.Replace(ctx.CurrentToken.Content.Substring(1, ctx.CurrentToken.Content.Length - 2), @"\\(.)", "$1") },
                { TokenKind.OpenGroup, (ctx, bp) => {
                    var inner = this.ParseInternal(ctx);
                    ctx.ExpectToken(TokenKind.CloseGroup);
                    return inner;
                } },
                { TokenKind.OpChar, (ctx, bp) => {
                    var opToken = ctx.CurrentToken;
                    var arg = this.ParseInternal(ctx, bp);
                    return opToken.TokenInfo.UnaryHandler(arg);
                } },
            };

            _infixHandlers = new Dictionary<TokenKind, Func<ParsingContext, int, Expr, Expr>>()
            {
                { TokenKind.OpenBracket, (ctx, bp, left) => {
                    var args = new List<Expr>();
                    if (ctx.Peek().Kind != TokenKind.CloseBracket)
                    {
                        args.Add(this.ParseInternal(ctx));
                        while(ctx.Peek().Kind == TokenKind.Comma){
                            ctx.Advance();
                            args.Add(this.ParseInternal(ctx));
                        }
                    }
                    ctx.ExpectToken(TokenKind.CloseBracket);
                    return new Apply(left, args.ToArray());
                } },
                { TokenKind.OpChar, (ctx, bp, left) => {
                    var opToken = ctx.CurrentToken;
                    var right = this.ParseInternal(ctx, bp);
                    return opToken.TokenInfo.BinaryHandler(left, right);
                } },
            };
        }

        public Expr Parse(string text)
        {
            var ctx = new ParsingContext(this.Tokenizer.Tokenize(text));
            return this.ParseInternal(ctx);
        }

        private Expr ParseInternal(ParsingContext ctx)
        {
            return this.ParseInternal(ctx, int.MinValue);
        }

        private Expr ParseInternal(ParsingContext ctx, int rbp)
        {
            var prevToken = ctx.CurrentToken;
            var left = this.ParsePrefix(ctx);

            while (this.GetBindingPower(false, ctx.Peek()) > rbp ||
                (prevToken != null && ctx.Peek() != null && ctx.Peek().TokenInfo == prevToken.TokenInfo && prevToken.TokenInfo.InverseBinaryAssociativity))
            {
                left = this.ParseInfix(ctx, left);
            }

            return left;
        }

        private Expr ParsePrefix(ParsingContext ctx)
        {
            var token = ctx.Advance();
            if (!_prefixHandlers.TryGetValue(token.Kind, out var handler))
                throw new ApplicationException("Unexpected leading expression syntax " + token.TokenInfo + " at " + token.Pos);

            return handler(ctx, this.GetBindingPower(true, token));
        }

        private Expr ParseInfix(ParsingContext ctx, Expr left)
        {
            var token = ctx.Advance();
            if (!_infixHandlers.TryGetValue(token.Kind, out var handler))
                throw new ApplicationException("Unexpected infix expression syntax " + token.TokenInfo + " at " + token.Pos);

            return handler(ctx, this.GetBindingPower(false, token), left);
        }

        private int GetBindingPower(bool prefix, Token token)
        {
            if (token == null)
                return int.MinValue;

            var precedence = prefix ? token.TokenInfo.UnaryBindingPower
                                    : token.TokenInfo.BinaryBindingPower;

            if (!precedence.HasValue)
            {
                if (token.Kind != TokenKind.OpChar)
                    precedence = int.MinValue;
                else
                    throw new ApplicationException("Operator " + token.TokenInfo + " cannot be applied in " + (prefix ? "prefix" : "infix") + " form at " + token.Pos);
            }

            return precedence.Value;
        }
    }
}
