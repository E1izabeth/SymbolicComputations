using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SymbolicComputations
{
    public class SmartReplacingVisitor : IExprVisitor<Expr>
    {
        private readonly Dictionary<Expr, Expr> _replacements;
        private readonly bool _fullReplace;

        public SmartReplacingVisitor(Dictionary<Expr, Expr> replacements, bool fullReplace = false)
        {
            _replacements = replacements;
            _fullReplace = fullReplace;
        }

        bool TryReplaceImpl(Expr oldExpr, out Expr result)
        {
            // return _replacements.TryGetValue(oldExpr, out var newExpr) ? newExpr : oldExpr; 

            result = null;

            foreach (var kv in _replacements)
            {
                if (kv.Key.TryMatchExpr(oldExpr, out var caps))
                {
                    result = kv.Value.Replace(caps);
                    break;
                }
            }

            return result != null;
        }

        Expr ReplaceImpl(Expr oldExpr)
        {
            return this.TryReplaceImpl(oldExpr, out var result) ? result : oldExpr;
        }

        Expr IExprVisitor<Expr>.VisitApply(Apply apply)
        {
            if (!_fullReplace && this.TryReplaceImpl(apply, out var newExpr))
            {
                return newExpr;
            }
            else
            {
                var newHead = apply.Head.Apply(this);
                var newArgs = apply.Args.Select(oldArg => (oldArg, newArg: oldArg.Apply(this)))
                                        .Select(a => (newArg: a.newArg, updated: a.newArg != a.oldArg))
                                        .ToArray();

                var newApply = newHead != apply || newArgs.Any(a => a.updated) ? new Apply(newHead, newArgs.Select(a => a.newArg).ToArray()) : apply;
                return _fullReplace ? this.ReplaceImpl(newApply) : newApply;
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
