using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using S = SymbolicComputations.KnownNames;

namespace SymbolicComputations
{
    public class Evaluator : IExprVisitor<Expr>
    {
        public int IterationsLimit { get; private set; }
        public int RecursionLimit { get; private set; }

        private readonly BuiltinOperations _builtinOperations;
        private readonly Stack<SymbolContext> _contextStack = new Stack<SymbolContext>();

        //public SymbolContext CurrentContext { get { return _context; }}
        public SymbolContext CurrentContext { get { return _contextStack.Peek(); } }

        public Action<string> Logger { get; set; }

        private int _recursionDepth = 0;

        public Evaluator()
        {
            this.IterationsLimit = 1024;
            this.RecursionLimit = 256;

            _builtinOperations = new BuiltinOperations(this);

            _contextStack = new Stack<SymbolContext>();
            _contextStack.Push(new SymbolContext() {
                { KnownNames.Delayed, KnownNames.HoldAll },
                { KnownNames.Seq, KnownNames.HoldAll },
                { KnownNames.Block, KnownNames.HoldAll },
                { KnownNames.Module, KnownNames.HoldAll },
                { KnownNames.Hold, KnownNames.HoldAll },
                { KnownNames.HoldForm, KnownNames.HoldAll },
                { KnownNames.Set, KnownNames.HoldFirst },
                { KnownNames.Concat, KnownNames.Flat },
                { KnownNames.Func, KnownNames.HoldAll },
                { KnownNames.SetAttrs, KnownNames.HoldAll },
                { KnownNames.GetAttrs, KnownNames.HoldAll },
                { KnownNames.Definition, KnownNames.HoldAll },
                { KnownNames.Clear, KnownNames.HoldAll },
                { KnownNames.ReplaceAll, KnownNames.HoldRest},
            });
        }

        public Expr Evaluate(Expr expr)
        {
            if (expr.Hold)
                return expr;

            var input = expr;
            //if (expr is Apply)
            //    this.Logger?.Invoke(">" + new string(' ', _recursionDepth) + expr);

            try
            {
                _recursionDepth++;
                if (_recursionDepth > this.RecursionLimit)
                    return expr.MakeAbort("Recursion limit hit");

                var result = expr.Apply(this);
                var n = 0;
                while (!result.Equals(expr) && !result.IsAbortExpr() && !result.Hold)
                {
                    if (input is Apply)
                        this.Logger?.Invoke(string.Format("|{0} {1} --> {2}", new string(' ', _recursionDepth), expr, result));

                    expr = result;
                    n++;
                    if (n > this.IterationsLimit)
                        return expr.MakeAbort("Iterations limit hit");

                    result = expr.Apply(this);
                }

                //if (!input.Equals(result))
                //    this.Logger?.Invoke("< " + input + " --> " + result);

                //if (input is Apply)
                //    this.Logger?.Invoke("<" + new string(' ', _recursionDepth) + input + " --> " + result);

                return result;
            }
            finally
            {
                _recursionDepth--;
            }
        }

        public void PushContext(IList<Expr> locals = null)
        {
            _contextStack.Push(this.CurrentContext.MakeNext(locals));
        }

        public void PopContext()
        {
            _contextStack.Pop();
        }

        bool TryPerformBuiltinOperation(Apply apply, out Expr result)
        {
            if (apply.Head is Apply func && func.Head.Equals(KnownNames.Func))
            {
                if (func.Args.Count == 2)
                {
                    IEnumerable<Expr> funcArgs;
                    if (func.Args.First() is Apply funcArgList)
                        funcArgs = funcArgList.Args;
                    else if (func.Args.First() is Symbol arg)
                        funcArgs = new[] { arg };
                    else
                        funcArgs = null;

                    if (funcArgs != null)
                    {
                        var funcPattern = KnownNames.Func[funcArgs.Select(a => KnownNames.Pattern[a]).ToArray()];
                        var funcApply = KnownNames.Func[apply.Args.ToArray()];

                        if (funcPattern.TryMatchExpr(funcApply, out var caps))
                        {
                            var funcBody = func.Args.Last();
                            result = funcBody.Replace(caps);
                        }
                        else
                        {
                            result = apply.MakeAbort("Inconsistent lambda application");
                        }
                    }
                    else
                    {
                        result = apply.MakeAbort("Invalid lambda argument spec");
                    }
                }
                else
                {
                    result = apply.MakeAbort("Func requires two arguments");
                }
            }
            else if (!(apply.Head is Symbol head && _builtinOperations.TryPerformOperation(head.Name.Text, apply.Args, out result)))
                result = null;

            return result != null;
        }

        Expr IExprVisitor<Expr>.VisitApply(Apply apply)
        {
            var newHead = this.Evaluate(apply.Head);
            Expr[] newArgs;

            if (newHead is Symbol headSymbol)
            {
                if (this.CurrentContext.HasAttribute(headSymbol, KnownNames.HoldAll))
                {
                    newArgs = apply.Args.ToArray();
                }
                else if (this.CurrentContext.HasAttribute(headSymbol, KnownNames.HoldFirst))
                {
                    newArgs = apply.Args.Take(1).Concat(apply.Args.Skip(1).Select(this.Evaluate)).ToArray();
                }
                else if (this.CurrentContext.HasAttribute(headSymbol, KnownNames.HoldRest))
                {
                    newArgs = apply.Args.Take(1).Select(this.Evaluate).Concat(apply.Args.Skip(1)).ToArray();
                }
                else
                {
                    newArgs = apply.Args.Select(arg => this.Evaluate(arg)).ToArray();
                }

                if (this.CurrentContext.HasAttribute(headSymbol, KnownNames.Listable))
                {
                    throw new NotImplementedException();
                }

                if (this.CurrentContext.HasAttribute(headSymbol, KnownNames.Flat))
                {
                    newArgs = this.FlattenArgs(headSymbol, newArgs).ToArray();
                }

                if (this.CurrentContext.HasAttribute(headSymbol, KnownNames.Orderless))
                {
                    newArgs = newArgs.OrderBy(c => c.ToString()).ToArray();
                }
            }
            else
            {
                newArgs = apply.Args.Select(arg => this.Evaluate(arg)).ToArray();
            }

            if (newHead.IsAbortExpr(out var msg) || newArgs.Any(a => a.IsAbortExpr(out msg)))
                return apply.MakeAbort(msg);

            var newApply = new Apply(newHead, newArgs);
            return this.CurrentContext.TryResolve(newApply, out var result) || this.TryPerformBuiltinOperation(newApply, out result) ? result : newApply;
        }

        private IEnumerable<Expr> FlattenArgs(Symbol head, IEnumerable<Expr> args)
        {
            foreach (var arg in args)
            {
                if (arg is Apply apl && apl.Head.Equals(head))
                {
                    foreach (var subarg in this.FlattenArgs(head, apl.Args))
                        yield return subarg;
                }
                else
                {
                    yield return arg;
                }
            }
        }

        Expr IExprVisitor<Expr>.VisitNumber(Number number)
        {
            return number;
        }

        Expr IExprVisitor<Expr>.VisitSymbol(Symbol symbol)
        {
            return this.CurrentContext.TryResolve(symbol, out var result) ? result : symbol;
        }

        Expr IExprVisitor<Expr>.VisitString(String str)
        {
            return str;
        }
    }
}
