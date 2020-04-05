using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SymbolicComputations
{
    public class ReplacingVisitor : IExprVisitor<Expr>
    {
        private readonly Dictionary<Expr, Expr> _replacements;

        public ReplacingVisitor(Dictionary<Expr, Expr> replacements)
        {
            _replacements = replacements;
        }

        Expr ReplaceImpl(Expr oldExpr)
        {
            return _replacements.TryGetValue(oldExpr, out var newExpr) ? newExpr : oldExpr; 
        }

        Expr IExprVisitor<Expr>.VisitApply(Apply apply)
        {
            if (_replacements.TryGetValue(apply, out var newExpr))
            {
                return newExpr;
            }
            else
            {
                //return new Apply(apply.Head.Apply(this), apply.Args.Select(arg => arg.Apply(this)).ToArray());

                var newHead = apply.Head.Apply(this);
                var newArgs = apply.Args.Select(oldArg => (oldArg, newArg: oldArg.Apply(this)))
                                        .Select(a => (newArg: a.newArg, updated: a.newArg != a.oldArg))
                                        .ToArray();

                return newHead != apply || newArgs.Any(a => a.updated) ? new Apply(newHead, newArgs.Select(a => a.newArg).ToArray()) : apply;
            }
        }

        Expr IExprVisitor<Expr>.VisitNumber(Number number)
        {
            return this.ReplaceImpl(number);
        }

        Expr IExprVisitor<Expr>.VisitString(String str)
        {
            return this.ReplaceImpl(str);
        }

        Expr IExprVisitor<Expr>.VisitSymbol(Symbol symbol)
        {
            return this.ReplaceImpl(symbol);
        }
     
    }
}
