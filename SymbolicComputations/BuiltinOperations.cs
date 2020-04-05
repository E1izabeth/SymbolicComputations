using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SymbolicComputations
{
    [System.AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    sealed class SymbolicAttribute : Attribute
    {
        public int MinArgs { get; private set; }

        public ReadOnlyCollection<Expr> Attrs { get; private set; }

        public SymbolicAttribute()
            : this(0, new Enum[0]) { }

        public SymbolicAttribute(int minArgs)
            : this(minArgs, new Enum[0]) { }

        public SymbolicAttribute(params Enum[] attrs)
            : this(0, attrs) { }

        public SymbolicAttribute(int minArgs, params Enum[] attrs)
        {
            this.MinArgs = minArgs;
            this.Attrs = new ReadOnlyCollection<Expr>(attrs.Select(a => new Symbol(a.ToString())).ToArray());
        }
    }

    public class BuiltinOperations
    {

        static readonly Dictionary<string, Func<BuiltinOperations, IList<Expr>, Expr>> _methods;
        private long _moduleNumber;

        static BuiltinOperations()
        {
            _methods = typeof(BuiltinOperations).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                                .Where(m => m.GetCustomAttribute<SymbolicAttribute>() != null &&
                                                            m.ReturnType == typeof(Expr) &&
                                                            m.GetParameters().Length == 1 &&
                                                            m.GetParameters().FirstOrDefault()?.ParameterType == typeof(IList<Expr>))
                                                .ToDictionary(m => m.Name, m => m.CreateDelegate<Func<BuiltinOperations, IList<Expr>, Expr>>());
        }

        private readonly Evaluator _evaluator;

        public BuiltinOperations(Evaluator evaluator)
        {
            _evaluator = evaluator;
        }

        public bool TryPerformOperation(string opName, IList<Expr> args, out Expr result)
        {
            try
            {
                result = _methods.TryGetValue(opName, out var action) ? (
                    (action.Method.GetCustomAttribute<SymbolicAttribute>()?.MinArgs ?? 0) <= args.Count
                        ? action(this, args)
                        : throw new ApplicationException("Insufficient arguments number")
                    ) : null;
            }
            catch (ApplicationException ex)
            {
                result = new Symbol(opName)[args.ToArray()].MakeAbort(ex.Message);
            }

            return result != null;
        }

        [Symbolic(2)]
        public Expr Delayed(IList<Expr> args)
        {
            _evaluator.CurrentContext.Set(args[0], args[1]);
            return KnownNames.Null;
        }

        [Symbolic(2)]
        public Expr Set(IList<Expr> args)
        {
            var value = _evaluator.Evaluate(args[1]);
            _evaluator.CurrentContext.Set(args[0], value);
            return value;
        }

        [Symbolic(1)]
        public Expr Clear(IList<Expr> args)
        {
            _evaluator.CurrentContext.Clear(args[0]);
            return KnownNames.Null;
        }

        [Symbolic]
        public Expr Seq(IList<Expr> args)
        {
            Expr result = KnownNames.Null;

            foreach (var item in args)
            {
                result = _evaluator.Evaluate(item);
                if (result.IsAbortExpr(out var msg))
                    throw new ApplicationException(msg);
            }

            return result;
        }

        private bool TryBooleanize(Expr expr, out bool value)
        {
            if (expr == KnownNames.True)
            {
                value = true;
                return true;
            }
            else if (expr == KnownNames.False)
            {
                value = false;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        [Symbolic(1)]
        public Expr Not(IList<Expr> args)
        {
            return this.TryBooleanize(args.First(), out var value) ? (value ? KnownNames.False : KnownNames.True) : KnownNames.Not[args.ToArray()];
        }

        [Symbolic(2)]
        public Expr And(IList<Expr> args)
        {
            return this.PerformBooleanOperation((a, b) => a && b, args, KnownNames.And, (h, ee, r) => r ? h[ee.ToArray()] : KnownNames.False);
        }

        [Symbolic(2)]
        public Expr Or(IList<Expr> args)
        {
            return this.PerformBooleanOperation((a, b) => a || b, args, KnownNames.Or, (h, ee, r) => r ? KnownNames.True : h[ee.ToArray()]);
        }

        [Symbolic(2)]
        public Expr Xor(IList<Expr> args)
        {
            return this.PerformBooleanOperation((a, b) => a ^ b, args, KnownNames.Xor, (h, ee, r) => r ? KnownNames.Not[h[ee.ToArray()]] : h[ee.ToArray()]);
        }

        private Expr PerformBooleanOperation(Func<bool, bool, bool> operation, IList<Expr> args, Expr opHead, Func<Expr, IList<Expr>, bool, Expr> postAct)
        {
            var exprs = new List<Expr>(args.Count);
            var bools = new List<bool>(args.Count);

            foreach (var item in args)
            {
                if (this.TryBooleanize(item, out var value))
                    bools.Add(value);
                else
                    exprs.Add(item);
            }

            var boolsCount = bools.Count;
            if (boolsCount > 0)
            {
                var result = bools[0];
                for (int i = 1; i < boolsCount; i++)
                    result = operation(result, bools[i]);

                return exprs.Count > 0 ? postAct(opHead, exprs, result) : result;
            }
            else
            {
                return opHead[exprs.ToArray()];
            }
        }

        [Symbolic(1)]
        public Expr Sum(IList<Expr> args) { return this.PerformOrderlessNumbericOperation((a, b) => a + b, KnownNames.Sum, args); }
        [Symbolic(2)]
        public Expr Mul(IList<Expr> args) { return this.PerformOrderlessNumbericOperation((a, b) => a * b, KnownNames.Mul, args); }
        [Symbolic(2)]
        public Expr Sub(IList<Expr> args) { return this.PerformNonOrderlessNumericOperation((a, b) => a - b, KnownNames.Sub, args, this.Sum); }
        [Symbolic(2)]
        public Expr Div(IList<Expr> args) { return this.PerformNonOrderlessNumericOperation((a, b) => a / b, KnownNames.Div, args, this.Mul); }
        [Symbolic(2)]
        public Expr Mod(IList<Expr> args)
        {
            var exprs = new List<Expr>();

            var firstArg = args.First();
            if (firstArg is Number firstNum)
            {
                var result = firstNum.Value.value;

                int i = 1;
                for (; i < args.Count && !args[i] is Number num; i++)
                    result = result % num.Value.value;

                return i < args.Count ? KnownNames.Mod[args.Skip(i).Prepend(result).ToArray()] : result;
            }
            else
            {
                return KnownNames.Mod[exprs.ToArray()];
            }
        }

        private Expr PerformNonOrderlessNumericOperation(Func<double, double, double> operation, Expr opHead, IList<Expr> args, Func<IList<Expr>, Expr> inverseOp)
        {
            var exprs = new List<Expr>();

            var firstArg = args.First();
            if (firstArg is Number firstNum)
            {
                var result = firstNum.Value.value;

                foreach (var item in args.Skip(1))
                {
                    if (item is Number num)
                    {
                        result = operation(result, num.Value.value);
                    }
                    else
                    {
                        exprs.Add(item);
                    }
                }

                if (exprs.Count == 0)
                {
                    return result;
                }
                else
                {
                    return opHead[exprs.Prepend(result).ToArray()];
                }
            }
            else
            {
                return opHead[firstArg, inverseOp(args.Skip(1).ToList())];
            }
        }

        private Expr PerformOrderlessNumbericOperation(Func<double, double, double> operation, Expr head, IList<Expr> args)
        {
            var exprs = new List<Expr>(args.Count);
            var nums = new List<Value>(args.Count);

            foreach (var item in args)
            {
                if (item is Number num)
                    nums.Add(num.Value);
                else
                    exprs.Add(item);
            }

            var numsCount = nums.Count;
            if (numsCount > 0)
            {
                var result = nums[0].value;
                for (int i = 1; i < numsCount; i++)
                    result = operation(result, nums[i].value);

                if (exprs.Count > 0)
                    exprs.Add(result);

                var resultExpr = exprs.Count > 0 ? head[exprs.ToArray()] : result;
                return resultExpr;
            }
            else
            {
                return head[exprs.ToArray()];
            }
        }

        [Symbolic(2)]
        public Expr Equal(IList<Expr> args) { return this.PerformSymbolCompare(true, KnownNames.Equal, args) ?? this.PerformCompareOperation((a, b) => a == b, KnownNames.Equal, args); }
        [Symbolic(2)]
        public Expr NotEqual(IList<Expr> args) { return this.PerformSymbolCompare(false, KnownNames.Equal, args) ?? this.PerformCompareOperation((a, b) => a != b, KnownNames.NotEqual, args); }
        [Symbolic(2)]
        public Expr Greater(IList<Expr> args) { return this.PerformCompareOperation((a, b) => a > b, KnownNames.Greater, args); }
        [Symbolic(2)]
        public Expr Less(IList<Expr> args) { return this.PerformCompareOperation((a, b) => a < b, KnownNames.Less, args); }
        [Symbolic(2)]
        public Expr GreaterOrEqual(IList<Expr> args) { return this.PerformCompareOperation((a, b) => a >= b, KnownNames.GreaterOrEqual, args); }
        [Symbolic(2)]
        public Expr LessOrEqual(IList<Expr> args) { return this.PerformCompareOperation((a, b) => a <= b, KnownNames.LessOrEqual, args); }

        private Expr PerformSymbolCompare(bool eq, Expr head, IList<Expr> args)
        {
            return args[0] is Symbol s0 && args[1] is Symbol s1
                //? ((eq && (s0.Name.Text == s1.Name.Text)) || (!eq && (s0.Name.Text != s1.Name.Text)) ? KnownNames.True : KnownNames.False)
                ? (!eq ^ (s0.Name.Text == s1.Name.Text) ? KnownNames.True : KnownNames.False)
                : null;
        }

        private Expr PerformCompareOperation(Func<double, double, bool> op, Expr head, IList<Expr> args)
        {
            return args[0] is Number n0 && args[1] is Number n1
                ? (op(n0.Value.value, n1.Value.value) ? KnownNames.True : KnownNames.False)
                : head[args.ToArray()];
        }

        [Symbolic(2)]
        public Expr SetAttrs(IList<Expr> args)
        {
            if (args[0] is Symbol symbol)
            {
                _evaluator.CurrentContext.AddAttributes(symbol, args.Skip(1).ToArray());
                return KnownNames.Null;
            }
            else
            {
                throw new ApplicationException("Attributes could be bound only to symbols");
            }
        }

        [Symbolic(1)]
        public Expr GetAttrs(IList<Expr> args)
        {
            if (args[0] is Symbol symbol)
            {
                return _evaluator.CurrentContext.GetAttributes(symbol);
            }
            else
            {
                throw new ApplicationException("Attributes could be bound only to symbols");
            }
        }

        [Symbolic(1)]
        public Expr Definition(IList<Expr> args)
        {
            if (args[0] is Symbol symbol)
            {
                var ctx = _evaluator.CurrentContext;
                var defs = new List<Expr>();
                if (ctx.TryGetSymbolInfo(symbol, out var info))
                {
                    do
                    {
                        defs.Add(KnownNames.Entry[
                            KnownNames.Patterns[info.Select(kv => KnownNames.Entry[kv.Pattern, kv.Value]).ToArray()],
                            KnownNames.Attributes[info.GetAttributes().ToArray()]
                        ]);

                        info = info.Prev;
                    } while (info != null);
                }

                return defs.Count > 0 ? "Symbol".Apply(symbol, KnownNames.List[defs.ToArray()]).MakeAbort("Symbol info")
                                      : symbol.MakeAbort("Unknown symbol");
            }
            else
            {
                throw new ApplicationException("Symbol required");
            }
        }

        [Symbolic(2)]
        public Expr Append(IList<Expr> args)
        {
            if (args[0] is Apply list)
            {
                return new Apply(list.Head, list.Args.Concat(args.Skip(1)).ToArray());
            }
            else
            {
                throw new ApplicationException("Append requires list and item");
            }
        }

        [Symbolic(2)]
        public Expr Prepend(IList<Expr> args)
        {
            if (args[0] is Apply list)
            {
                return new Apply(list.Head, args.Skip(1).Concat(list.Args).ToArray());
            }
            else
            {
                throw new ApplicationException("Prepend requires list and item");
            }
        }

        [Symbolic(1)]
        public Expr First(IList<Expr> args)
        {
            if (args[0] is Apply list)
            {
                return list.Args.FirstOrDefault() ?? KnownNames.Null;
            }
            else
            {
                throw new ApplicationException("First requires list");
            }
        }

        [Symbolic(1)]
        public Expr Rest(IList<Expr> args)
        {
            if (args[0] is Apply list)
            {
                return new Apply(list.Head, list.Args.Skip(1).ToArray());
            }
            else
            {
                throw new ApplicationException("Rest requires list");
            }
        }

        [Symbolic(1)]
        public Expr Last(IList<Expr> args)
        {
            if (args[0] is Apply list)
            {
                return list.Args.LastOrDefault() ?? KnownNames.Null;
            }
            else
            {
                throw new ApplicationException("Last requires list");
            }
        }

        [Symbolic(1)]
        public Expr Head(IList<Expr> args)
        {
            if (args[0] is Apply list)
            {
                return list.Head;
            }
            else
            {
                throw new ApplicationException("Head requires list");
            }
        }

        [Symbolic(1)]
        public Expr Length(IList<Expr> args)
        {
            if (args[0] is Apply list)
            {
                return list.Args.Count;
            }
            else if (args[0] is String str)
            {
                return str.Text.Length;
            }
            else
            {
                throw new ApplicationException("Length requires list");
            }
        }

        [Symbolic(2)]
        public Expr Block(IList<Expr> args)
        {
            try
            {
                _evaluator.PushContext((args.First() as Apply)?.Args);
                return this.Seq(args.Skip(1).ToList());
            }
            finally
            {
                _evaluator.PopContext();
            }
        }

        [Symbolic(2)]
        public Expr Module(IList<Expr> args)
        {

            try
            {
                if (args.First() is Apply localsList)
                {

                    _moduleNumber++;
                    var locals = localsList.Args.Select(a => a.FindSymbol()).Where(s => s != null)
                                           .ToDictionary<Symbol, Expr, Expr>(a => a, a => new Symbol(a.Name.Text + "$" + _moduleNumber));

                    return KnownNames.Seq[args.Skip(1).ToArray()].Replace(locals);

                    // return this.Seq(args.Skip(1).ToList());
                }
                else
                {
                    throw new ApplicationException("Module requires list of local symbols");
                }
            }
            finally
            {
                // _evaluator.PopContext();
            }
        }

        [Symbolic(2)]
        public Expr Concat(IList<Expr> args)
        {
            if (args.All(a => a is String))
            {
                return string.Concat(args.OfType<String>().Select(s => s.Text));
            }
            else
            {
                throw new ApplicationException("Concat requires list of strings");
            }
        }


        [Symbolic(3)]
        public Expr Substring(IList<Expr> args)
        {
            if (args[0] is String str && args[1] is Number from && args[2] is Number len)
            {
                return str.Text.Substring((int)from.Value.value, (int)len.Value.value);
            }
            else
            {
                throw new ApplicationException("Substring requires string and range spec");
            }
        }

        [Symbolic(2)]
        public Expr Split(IList<Expr> args)
        {
            if (args.All(a => a is String))
            {
                var strs = args.OfType<String>().Select(s => s.Text).ToArray();
                var splitters = strs.Skip(1).ToArray();
                return KnownNames.List[strs[0].Split(splitters, StringSplitOptions.RemoveEmptyEntries).Select(s => (Expr)s).ToArray()];
            }
            if (args[0] is String str && args[1] is Apply list && list.Head is Symbol listHead &&
                listHead.Equals(KnownNames.List) && list.Args.All(a => a is String))
            {
                var splitters = list.Args.OfType<String>().Select(s => s.Text).ToArray();
                return KnownNames.List[str.Text.Split(splitters, StringSplitOptions.RemoveEmptyEntries).Select(s => (Expr)s).ToArray()];
            }
            else
            {
                throw new ApplicationException("Split requires list of strings");
            }
        }

        [Symbolic(2)]
        public Expr Match(IList<Expr> args)
        {
            if (args.All(a => a is String))
            {
                var strs = args.OfType<String>().Select(s => s.Text).ToArray();
                var match = Regex.Match(strs[0], strs[1]);
                if (match.Success)
                {
                    return KnownNames.Entry[match.Index, match.Length];
                }
                else
                {
                    return KnownNames.Entry[KnownNames.False];
                }
            }
            else
            {
                throw new ApplicationException("Match requires string and pattern");
            }
        }

        [Symbolic(1)]
        public Expr Introduce(IList<Expr> args)
        {
            if (args.All(a => a is String))
            {
                var str = args.OfType<String>().First().Text;
                if (double.TryParse(str, out var x))
                    return new Number(x);
                else if (Regex.IsMatch(str, "^\\w+$"))
                    return new Symbol(str);
                else
                    return KnownNames.Introduce[args.ToArray()];
            }
            else
            {
                throw new ApplicationException("Introduce requires string");
            }
        }

        [Symbolic(1)]
        public Expr Hold(IList<Expr> args)
        {
            return args.First().MakeHoldForm();
        }

        [Symbolic(1)]
        public Expr ReleaseHold(IList<Expr> args)
        {
            return args.First().MakeUnholdForm();
        }

        [Symbolic(1)]
        public Expr Log(IList<Expr> args)
        {
            if (args.First() is Symbol s && bool.TryParse(s.Name.Text, out var b))
            {
                if (b)
                    _evaluator.Logger = Console.WriteLine;
                else
                    _evaluator.Logger = null;

                return KnownNames.Null;
            }
            else
            {
                throw new ApplicationException("Log requires bool argument");
            }
        }

        [Symbolic(2)]
        public Expr ReplaceAll(IList<Expr> args)
        {
            if (args.Skip(1).First() is Apply entries)
            {

                var replacements = entries.Args.OfType<Apply>().Where(e => e.Args.Count > 1)
                                          .ToDictionary(e => e.Args[0], e => e.Args[1]);

                return args.First().SmartReplace(replacements, true);
            }
            else
            {
                throw new ApplicationException("ReplaceAll requires list of replacements");
            }
        }

        [Symbolic(2)]
        public Expr Replace(IList<Expr> args)
        {
            if (args.Skip(1).First() is Apply entries)
            {

                var replacements = entries.Args.OfType<Apply>().Where(e => e.Args.Count > 1)
                                          .ToDictionary(e => e.Args[0], e => e.Args[1]);

                return args.First().SmartReplace(replacements, false);
            }
            else
            {
                throw new ApplicationException("ReplaceAll requires list of replacements");
            }
        }

        [Symbolic(0)]
        public Expr Print(IList<Expr> args)
        {
            Console.WriteLine("Echo: ");
            foreach (var item in args)
                Console.WriteLine("\t" + item.ToString());

            return KnownNames.Null;
        }

        [Symbolic(1)]
        public Expr Echo(IList<Expr> args)
        {
            Console.WriteLine("Echo: ");
            Console.WriteLine("\t" + args.First().ToString());
            return args.First();
        }

        [Symbolic()]
        public Expr Cls()
        {
            Console.Clear();
            return KnownNames.Null;
        }
    }
}
