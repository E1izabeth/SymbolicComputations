using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SymbolicComputations
{
    class MapVisitor : IExprVisitor<Expr>
    {
        readonly Func<MapVisitor, Apply, Expr> _applyMap;
        readonly Func<MapVisitor, Number, Expr> _numberMap;
        readonly Func<MapVisitor, String, Expr> _stringMap;
        readonly Func<MapVisitor, Symbol, Expr> _symbolMap;

        public MapVisitor(Func<MapVisitor, Apply, Expr> applyMap = null,
                          Func<MapVisitor, Number, Expr> numberMap = null,
                          Func<MapVisitor, String, Expr> stringMap = null,
                          Func<MapVisitor, Symbol, Expr> symbolMap = null)
        {
            _applyMap = applyMap;
            _numberMap = numberMap;
            _stringMap = stringMap;
            _symbolMap = symbolMap;
        }

        Expr IExprVisitor<Expr>.VisitApply(Apply apply)
        {
            return _applyMap?.Invoke(this, apply) ?? new Apply(apply.Head.Apply(this), apply.Args.Select(a => a.Apply(this)).ToArray());
        }

        Expr IExprVisitor<Expr>.VisitNumber(Number number)
        {
            return _numberMap?.Invoke(this, number) ?? number;
        }

        Expr IExprVisitor<Expr>.VisitString(String str)
        {
            return _stringMap?.Invoke(this, str) ?? str;
        }

        Expr IExprVisitor<Expr>.VisitSymbol(Symbol symbol)
        {
            return _symbolMap?.Invoke(this, symbol) ?? symbol;
        }
    }
}
