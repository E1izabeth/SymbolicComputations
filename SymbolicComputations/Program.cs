using System;
using System.Collections.Generic;
using System.Linq;
using static SymbolicComputations.KnownNames;
using R = System.Text.RegularExpressions;

namespace SymbolicComputations
{
    class Program
    {
        static readonly Expr EmptyList = List.Apply();

        static ExprParser SetupParser(Evaluator evaluator)
        {
            var parser = new ExprParser();

            parser.Tokenizer.RegisterUnaryOp("argPattern", "~", 0, arg => Pattern[arg]);
            parser.Tokenizer.RegisterBinaryOp("argPattern", "~", 0, (a, b) => List[
                List[a, b].FlattenLeaves(e => e is Apply l && l.Head.Equals(List) ? l.Args.ToArray() : new[] { e }).ToArray()
            ]);

            parser.Tokenizer.RegisterUnaryOp("minus", "-", 100, arg => Sub[0, arg]);
            parser.Tokenizer.RegisterBinaryOp("minus", "-", 20, (a, b) => Sub[a, b]);
            parser.Tokenizer.RegisterUnaryOp("plus", "+", 100, arg => arg);
            parser.Tokenizer.RegisterBinaryOp("plus", "+", 20, (a, b) => Sum[a, b]);
            parser.Tokenizer.RegisterBinaryOp("mul", "*", 30, (a, b) => Mul[a, b]);
            parser.Tokenizer.RegisterBinaryOp("div", "/", 30, (a, b) => Div[a, b]);
            parser.Tokenizer.RegisterBinaryOp("mod", "%", 30, (a, b) => Mod[a, b]);
            parser.Tokenizer.RegisterBinaryOp("power", "**", 40, (a, b) => "Power".Apply(a, b), true);

            parser.Tokenizer.RegisterUnaryOp("not", "!", 15, arg => Not[arg]);
            parser.Tokenizer.RegisterBinaryOp("or", "||", 6, (a, b) => Or[a, b]);
            parser.Tokenizer.RegisterBinaryOp("and", "&&", 7, (a, b) => And[a, b]);
            parser.Tokenizer.RegisterBinaryOp("xor", "^", 8, (a, b) => Xor[a, b]);

            parser.Tokenizer.RegisterBinaryOp("equal", "==", 9, (a, b) => Equal[a, b]);
            parser.Tokenizer.RegisterBinaryOp("notEqual", "!=", 9, (a, b) => NotEqual[a, b]);
            parser.Tokenizer.RegisterBinaryOp("less", "<", 10, (a, b) => Less[a, b]);
            parser.Tokenizer.RegisterBinaryOp("lessEq", "<=", 10, (a, b) => LessOrEqual[a, b]);
            parser.Tokenizer.RegisterBinaryOp("greater", ">", 10, (a, b) => Greater[a, b]);
            parser.Tokenizer.RegisterBinaryOp("greaterEq", ">=", 10, (a, b) => GreaterOrEqual[a, b]);

            parser.Tokenizer.RegisterBinaryOp("lambda", "-->", -1, (a, b) => Func[a, b], true);
            parser.Tokenizer.RegisterBinaryOp("set", "=", -100, (a, b) => Set[a, b]);
            parser.Tokenizer.RegisterBinaryOp("delayed", ":=", -100, (a, b) => Delayed[a, b]);
            parser.Tokenizer.RegisterBinaryOp("seq", ";", -101, (a, b) => Seq[a, b]);

            if (evaluator != null && evaluator.CurrentContext.TryResolve(Symbol("ops"), out var oe) && oe is Apply ops)
            {
                foreach (var item in ops.Args.OfType<Symbol>())
                {
                    {
                        if (evaluator.CurrentContext.TryGetAttribute(item, new Symbol("BinaryOp"), out var opInfo) && opInfo is Apply op)
                        {
                            if (op.Args.Count > 1 && op.Args[0] is String symbol && op.Args[1] is Number bp)
                            {
                                var invert = op.Args.Count > 2 && op.Args[2] is Symbol inv && inv == KnownNames.True;
                                parser.Tokenizer.RegisterBinaryOp(
                                    item.Name.Text, symbol.Text, (int)bp.Value.value,
                                    (a, b) => evaluator.CurrentContext.TryResolve(item[a, b], out var result) ? result : throw new ApplicationException(),
                                    invert
                                );
                            }
                        }
                    }
                    {
                        if (evaluator.CurrentContext.TryGetAttribute(item, new Symbol("UnaryOp"), out var opInfo) && opInfo is Apply op)
                        {
                            if (op.Args.Count == 2 && op.Args[0] is String symbol && op.Args[1] is Number bp)
                            {
                                parser.Tokenizer.RegisterUnaryOp(
                                    item.Name.Text, symbol.Text, (int)bp.Value.value,
                                    arg => evaluator.CurrentContext.TryResolve(item[arg], out var result) ? result : throw new ApplicationException()
                                );
                            }
                        }
                    }
                }
            }

            return parser;
        }

        static void Test()
        {
            var parser = SetupParser(null);

            var text = @"Seq[
                If[True, ~x, ~y] := x,
                If[False, ~x, ~y] := y,
                While[~x, ~y] := If[x, Seq[y, While[x, y]], Null],
                SetAttrs[If, HoldRest],
                SetAttrs[While, HoldAll],

                Fib[~x] := Seq[Fib[x] = If[Less[x, 2], 1, Fib[x-1] + Fib[x-2]]],

                f[~y] := If[a < b && c <= b, x1+x2+x3, y1**y2**y3],

                a=1;b=2;c=3;d=4;e=5,
                (a~b-->a+b)[10, 20]
            ]";

            var e = new Evaluator();
            Console.WriteLine(e.Evaluate(parser.Parse("(a~b-->a+b)[10, 20]")));

            // Func[List[a, b], Sum[a, b]],
            // Func[List[a, b, c], Sum[Sum[a, b], c]], 
            // Func[List[Pattern[a], b], Sum[a, b]]]


            //foreach (var item in tokenizer.Tokenize(text).ToList())
            //{
            //    Console.WriteLine(item);
            //}

            Console.WriteLine(parser.Parse(text));

        }

        static void Interpreter()
        {
            //var parser = SetupParser();
            var evaluator = new Evaluator();

            var str = @"Seq[
                            apple[~color,~weight]:=Module[
                                List[fcolor,fweight,getColorImpl,setColorImpl,getStateImpl],
                                Seq[
                                    fcolor=color,
                                    fweight=weight,
                                    getColorImpl[]:=fcolor,
                                    getStateImpl[]:=List[fcolor,fweight],
        
                                    List[
                                        Entry[getColor,getColorImpl],
                                        Entry[setColor,v-->(fcolor=v)],
                                        Entry[getState,getStateImpl]
                                    ]
                                ]
                            ],
                            If[True, ~x, ~y] := x,
                            If[False, ~x, ~y] := y,
                            While[~x, ~y] := If[x, Seq[y, While[x, y]], Null],
                            SetAttrs[If, HoldRest],
                            SetAttrs[While, HoldAll],
                            findByKey[~l,~k]:=Module[
                                List[t,r],
                                Seq[
                                    t=l,
                                    r=Null,
                                    While[
                                        Length[t]>0,
                                        If[
                                            First[First[t]]==k,
                                            Seq[
                                                r=Last[First[t]];
                                                t=List[]
                                            ],
                                            t=Rest[t]
                                        ]
                                    ],
                                    r
                                ]
                            ],
                            SetAttrs[findByKey,BinaryOp[""@"", 200000]],
                            ops=List[findByKey]
                        ]
                        ";
            evaluator.Evaluate(SetupParser(evaluator).Parse(str));

            string result;
            do
            {
                var parser = SetupParser(evaluator);

                Expr expr;
                try
                {
                    expr = parser.Parse(Console.ReadLine());
                }
                catch (ApplicationException ex)
                {
                    result = string.Empty;
                    Console.WriteLine(ex.ToString());
                    continue;
                }

                result = evaluator.Evaluate(expr).ToString();
                Console.WriteLine(result);
            } while (result.ToLower() != "exit");
        }


        static Expr s, Hold, t, m, test, a, b, c, l, k,
            stack, temp, cond, tokenslist, list,
            Tokenize, TokenKinds, GetToken, RemoveLast, RemoveLastImpl,
            Parse, ParseInternal, ParseApply, ParsingStep, ParseImpl,
            ParsingOpStep, ctx,
            token, p, upower, bpower, inverseAssociativity,
            eee, prev,
            NumberLiteral,
            SymbolLiteral,
            StringLiteral,
            Whitespaces,
            OpenedBracketChar,
            ClosedBracketChar,
            OpChar,
            CommaChar,
            MinusChar,
            StarChar,
            TildeChar,
            hold, args, tk, body,
            Unhold, Pow,
            holdLocal,
            bp, Commit, f, g;

        static Program()
        {
            SetupKnownNames<Program>();
        }

        static void Main()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
            //   Test();
            //Interpreter();
            //return;

            Expr ee = Seq[
                Delayed[If[True, ~x, ~y], x],
                Delayed[If[False, ~x, ~y], y],
                Delayed[While[~x, ~y], If[x, Seq[y, While[x, y]], Null]],
                SetAttrs[If, HoldRest],
                SetAttrs[While, HoldAll],

                Delayed[And[~x, ~y], If[x, y, False]],
                Delayed[Or[~x, ~y], If[x, True, y]],
                Delayed[Not[~x], If[x, False, True]],

                Delayed[Fib[~x], Seq[Set[Fib[x], Seq[Set[z, 1], If[Less[x, 2], 1, Sum[Fib[Sum[x, -1]], Fib[Sum[x, -2]]]]]]]],
                //Delayed[Fib[~x], If[Less[x, 2], 1, Sum[Fib[Sum[x, -1]], Fib[Sum[x, -2]]]]]

                Delayed[f[~y], Sum[x, 1]],
                //Delayed[Hold[~x], Seq[x]],

                //Delayed[test[~t, ~s], Block[
                //    List[Entry],
                //    Set[Entry[~a, ~b], "At"[a]],
                //    Set[Entry.Apply(), "No"],
                //    Match[t, s]
                //]],

                Delayed[Tokenize[~a], Module[
                    List[t, s, m],
                    Block[
                        List[Entry],
                        Set[t, a],
                        Set[p, Length[t]],
                        Set[s, EmptyList],

                        Set[TokenKinds, List[
                            token["^[0-9]+(\\.[0-9]+)?", NumberLiteral],
                            token["^\\w+", SymbolLiteral],
                            token["^\\\"[^\\\"\\\\]*(?:\\\\.[^\\\"\\\\]*)*\\\"", StringLiteral],
                            token["^\\s+", Whitespaces],
                            token["^//[^\\n\\r]+[\r\n]", Whitespaces],
                            token["^\\,", CommaChar],
                            token["^\\[", OpenedBracketChar],
                            token["^\\]", ClosedBracketChar],
                            token["^\\~", TildeChar],
                            token["^\\-", OpChar, 0, 10, False, Sub],
                            token["^\\+", OpChar, 0, 10, False, Sum],
                            token["^\\*", OpChar, -1, 20, False, Mul],
                            token["^\\/", OpChar, -1, 20, False, Div],
                            token["^\\^", OpChar, -1, 30, True, Pow]
                        ]],
                        Delayed[Entry[~x, ~y], Seq[
                            // Set[s, Append[s, token[Substring[t, x, y], Last[First[m]]]],
                            Set[s, Append[s, Prepend[Rest[First[m]], Substring[t, x, y]]],
                            Set[t, Substring[t, y, Length[t] - y]],
                            Set[m, EmptyList]
                        ]]],
                        While[Length[t] > 0, Seq[
                            Set[m, TokenKinds],
                            While[Length[m] > 0, Seq[
                                Match[t, First[First[m]]],
                                Set[m, Rest[m]]
                            ]],
                            If[Length[t] < p, Set[p, Length[t]], Set[t, ""]]
                        ]],
                        s
                    ]

                ]],

                Delayed[GetToken[~t], Block[List[token], Delayed[token[~x, ~y], y], t]],

            #region
                //////////////////Delayed[ParsingStep[~l, ~stack], Module[
                //////////////////    List[tokenslist, ParseInternal],

                //////////////////    Delayed[ParseInternal[token[~t, NumberLiteral]], List[hold[Introduce[t]], False, Rest[l]]],
                //////////////////    Delayed[ParseInternal[token[~t, SymbolLiteral]], List[hold[Introduce[t]], False, Rest[l]]], //If[Equal[GetToken[First[Rest[s]]], OpenedBracketChar], List[Introduce[t], Rest[l]], List[hhh[Introduce[t]]], Rest[l]]],
                //////////////////    Delayed[ParseInternal[token[~t, StringLiteral]], List[hold[t], False, Rest[l]]],

                //////////////////    //Delayed[ParseInternal[token[~t, OpenedBracketChar]], Module[
                //////////////////    //    List[list, temp, cond],
                //////////////////    //    Set[tokenslist, l],
                //////////////////    //    Set[list, EmptyList],
                //////////////////    //    Set[cond, True],

                //////////////////    //    While[NotEqual[GetToken[First[tokenslist]], ClosedBracketChar], Seq[
                //////////////////    //        Set[tokenslist, Rest[tokenslist]],
                //////////////////    //        While[And[NotEqual[GetToken[First[tokenslist]], CommaChar], NotEqual[GetToken[First[tokenslist]], ClosedBracketChar]], Seq[
                //////////////////    //            Set[temp, ParsingStep[tokenslist, list]],

                //////////////////    //            Set[tokenslist, Last[temp]],

                //////////////////    //            Set[cond, First[Rest[temp]]],
                //////////////////    //            If[cond, Set[list, RemoveLast[list]], Null],

                //////////////////    //            Set[list, Append[list, First[temp]]]
                //////////////////    //        ]]
                //////////////////    //    ]],
                //////////////////    //    List[
                //////////////////    //        Block[List[List], Set[List, Last[stack]], hold[list]], 
                //////////////////    //        True,
                //////////////////    //        Rest[tokenslist]
                //////////////////    //    ]
                //////////////////    //]],
                //////////////////    ParseInternal[First[l]]
                //////////////////]],

                //////////////////Delayed[Parse[~tokenslist], Module[
                //////////////////    List[list, stack, temp],
                //////////////////    Set[list, tokenslist],
                //////////////////    Set[stack, EmptyList],
                //////////////////    While[
                //////////////////        Length[list] > 0, 
                //////////////////        Seq[
                //////////////////            Set[temp, ParsingStep[list, stack]],
                //////////////////            Set[list, Last[temp]],
                //////////////////            Set[stack, Append[stack, First[temp]]]
                //////////////////        ]
                //////////////////    ],
                //////////////////    Last[stack]
                //////////////////]],
            #endregion
                Delayed[Parse[~tokenslist], Module[
                    List[ParseInternal, ParsingStep, prev, List, RemoveLast, RemoveLastImpl],

                    Delayed[RemoveLastImpl[~t, ~l], If[Length[t] > 1, RemoveLastImpl[Rest[t], Append[l, First[t]]], l]],
                    Delayed[RemoveLast[~t], RemoveLastImpl[t, EmptyList]],

                    Delayed[ParsingStep[~l, ~t], ParseInternal[First[l], Rest[l], Echo[t]]],

                    Delayed[ParseInternal[token[~t, NumberLiteral], ~l, ctx[~args]],
                        ParsingStep[l, ctx[Append[args, hold[Introduce[t]]]]]
                    ],
                    Delayed[ParseInternal[token[~t, SymbolLiteral], ~l, ctx[~args]],
                        ParsingStep[l, ctx[Append[args, hold[Introduce[t]]]]]
                    ],
                    Delayed[ParseInternal[token[~t, StringLiteral], ~l, ctx[~args]],
                        ParsingStep[l, ctx[Append[args, hold[Introduce[t]]]]]
                    ],
                    Delayed[ParseInternal[token[~t, Whitespaces], ~l, ctx[~args]],
                        ParsingStep[l, ctx[args]]
                    ],
                    Delayed[ParseInternal[token[~t, CommaChar], ~l, ctx[~args]],
                        ParsingStep[l, ctx[args]]
                    ],
                    Delayed[ParseInternal[token[~t, OpenedBracketChar], ~l, ctx[~args]],
                        ParsingStep[l, ctx[List[args]]]
                    ],
                    Delayed[ParseInternal[token[~t, ClosedBracketChar], ~l, ctx[~args]],
                        ParsingStep[l, ctx[Append[RemoveLast[First[args]], Block[List[List], Set[List, Last[First[args]]], hold[Rest[args]]]]]]
                    ],

                    Delayed[
                        ParseInternal[token[~t, OpChar, ~upower, ~bpower, ~inverseAssociativity, ~s], ~l, ctx[~args]],
                        ParsingStep[l, ctx[Append[args, hold[s]]]]
                    ],

                    Delayed[ParseInternal[Null, EmptyList, ctx[~t]], Block[List[List], Set[List, hold[Seq]], hold[t]]],

                    ParsingStep[tokenslist, ctx[EmptyList]]
                ]],

                Delayed[Unhold[~t], ReplaceAll[t, List[Entry[hold[~a], a]]]]
            ];

            #region 
            //(string, int) testtest(string tt, string ss)
            //{
            //    var entry = R.Regex.Match(tt, ss);
            //    if (entry.Success) return ("Yes", entry.Index);
            //    else return ("No", -1);
            //}

            var text = @"Seq[
                Delayed[If[True, ~x, ~y], x],
                Delayed[If[False, ~x, ~y], y],
                Delayed[While[~x, ~y], If[x, Seq[y, While[x, y]], Null]],
                SetAttrs[If, HoldRest],
                SetAttrs[While, HoldAll],

                Delayed[Fib[~x], Seq[Set[Fib[x], Seq[Set[z, 1], If[Less[x, 2], 1, Sum[Fib[Sum[x, -1]], Fib[Sum[x, -2]]]]]]]],

                Delayed[f[~y], Sum[x, 1]]
            ]";
            #endregion

            var e = new Evaluator
            {
                Logger = Console.WriteLine
            };
            var r = e.Evaluate(ee);
            Console.WriteLine("--------------------------------");
            Console.WriteLine("--------------------------------");
            //Console.WriteLine(e.Evaluate(GetToken[First[Rest[Tokenize["test[a,b]"]]]]).ToString());
            //Console.WriteLine(e.Evaluate(Tokenize["test[a,b]"]).ToString());
            //return;
            //Console.WriteLine("--------------------------------");
            //Console.WriteLine(e.Evaluate(Unhold[Parse[Tokenize["test[a,b]"]]]));
            //return;
            //Console.WriteLine("--------------------------------");

            //Console.WriteLine(e.Evaluate(Parse[Tokenize["List[List[List[a, b + c]]]"]]));
            //Console.WriteLine(e.Evaluate(Tokenize["Delayed[eee[Pattern[x]],Sum[Sum[3,5],x]]"]));
            //Console.WriteLine(e.Evaluate(Parse[Tokenize["Delayed[eee[Pattern[x]],Sum[Sum[3,5],x]]"]]));
            var tokens = List[
                token["Delayed", SymbolLiteral],
                token["[", OpenedBracketChar],
                token["eee", SymbolLiteral],
                token["[", OpenedBracketChar],
                token["Pattern", SymbolLiteral],
                token["[", OpenedBracketChar],
                token["x", SymbolLiteral],
                token["]", ClosedBracketChar],
                token["]", ClosedBracketChar],
                token[",", CommaChar],
                token["Sum", SymbolLiteral],
                token["[", OpenedBracketChar],
                token["Sum", SymbolLiteral],
                token["[", OpenedBracketChar],
                token["3", NumberLiteral],
                token[",", CommaChar],
                token["5", NumberLiteral],
                // token["+", OpChar, 0, 10, False, Sum],
                token["5", NumberLiteral],
                token["]", ClosedBracketChar],
                token[",", CommaChar],
                token["x", SymbolLiteral],
                token["]", ClosedBracketChar],
                token["]", ClosedBracketChar]
            ];
            ////Console.WriteLine(e.Evaluate(RemoveLast[List[1,2,3,4,5]]));
            Console.WriteLine(e.Evaluate(Parse[tokens]));
            //Console.WriteLine(e.Evaluate(Unhold[Parse[tokens]]));
            // Abort[Symbol[eee, List[Entry[Patterns[Entry[eee[Pattern[x]], Sum[Sum[3, 5], x]]], Attributes[]]]], "Symbol info"]
            // Abort[Symbol[eee, List[Entry[Patterns[Entry[eee[Pattern[x]], Sum[Sum[3, 5], x]]], Attributes[]]]], "Symbol info"]
            // Abort[Symbol[eee, List[Entry[Patterns[Entry[eee[Pattern[x]], Sum[Sum[3, 5], x]]], Attributes[]]]], "Symbol info"]
            //Console.WriteLine(e.Evaluate(Delayed[eee[Pattern[x]],Sum[Sum[3,5],x]]));
            Console.WriteLine(e.Evaluate(Definition[eee]));
            return;

            Console.WriteLine(e.Evaluate(Parse[List[
                token["test", SymbolLiteral],
                token["[", OpenedBracketChar],
                token["a", SymbolLiteral],
                token[",", CommaChar],
                token["b", SymbolLiteral],
                token["[", OpenedBracketChar],
                token["s", SymbolLiteral],
                token["]", ClosedBracketChar],
                token[",", CommaChar],
                token["3.14", NumberLiteral],
                token[",", CommaChar],
                token["b", StringLiteral],
                token["]", ClosedBracketChar]
            ]]));
            return;

            //for (; ; )
            //    Console.WriteLine(e.Evaluate(Parse[Console.ReadLine()]));

            Console.WriteLine("--------------------------------");
            var result = e.Evaluate(Tokenize["Seq[Set[x, 1], Module[List[x], Set[x, 123], f[0]]]"]);
            Console.WriteLine(result.ToString().Replace("k[", "\nk["));

            Console.WriteLine(">" + e.Evaluate(First[result]).ToString());
            Console.WriteLine(">" + e.Evaluate(First[First[result]]).ToString());
            Console.WriteLine(">" + e.Evaluate(Introduce[First[First[result]]]).ToString());


            //Console.WriteLine(e.Evaluate(Module[
            //    List[t],
            //    Set[t["No"], "AAAAAAAAAAAAAAAA"],
            //    Set[t["At"[~s]], "OK"],
            //    List[
            //        t[test["1231232", "312"]],
            //        t[test["abcd", "312"]]
            //    ]
            //]));

            return;

            var expr = e.Evaluate(Parse[text]); // Hold[expr]

            //Console.WriteLine(e.Evaluate(Definition[If]));
            //Console.WriteLine(e.Evaluate(Fib[15]));
            //Console.WriteLine(e.Evaluate(Seq[Set[x, 1], Module[List[x], Set[x, 123], f[0]]]));

            Console.WriteLine(e.Evaluate("test"));

            //Console.WriteLine(e.Evaluate(Fib[5]));

            Console.WriteLine(e.Evaluate(Concat["xx", Concat["9", "asdasd"]]));
        }

    }

}
