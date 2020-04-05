using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SymbolicComputations
{
    class SymbolCheckingVisitor : IExprVisitor<bool>
    {
        private SymbolName _symbolName;

        public SymbolCheckingVisitor(SymbolName name)
        {
            _symbolName = name;
        }

        bool IExprVisitor<bool>.VisitApply(Apply apply)
        {
            return false;
        }

        bool IExprVisitor<bool>.VisitNumber(Number number)
        {
            return false;
        }

        bool IExprVisitor<bool>.VisitString(String str)
        {
            return false;
        }

        bool IExprVisitor<bool>.VisitSymbol(Symbol symbol)
        {
            return _symbolName == symbol.Name;
        }
    }

    class NumberCheckingVisitor : IExprVisitor<bool>
    {
        private Value _value;

        public NumberCheckingVisitor(Value value)
        {
            _value = value;
        }

        bool IExprVisitor<bool>.VisitApply(Apply apply)
        {
            return false;
        }

        bool IExprVisitor<bool>.VisitNumber(Number number)
        {
            return _value.value == number.Value.value;
        }

        bool IExprVisitor<bool>.VisitString(String str)
        {
            return false;
        }

        bool IExprVisitor<bool>.VisitSymbol(Symbol symbol)
        {
            return false;
        }
    }

    class StringCheckingVisitor : IExprVisitor<bool>
    {
        private string _text;

        public StringCheckingVisitor(string text)
        {
            _text = text;
        }

        bool IExprVisitor<bool>.VisitApply(Apply apply)
        {
            return false;
        }

        bool IExprVisitor<bool>.VisitNumber(Number number)
        {
            return false;
        }

        bool IExprVisitor<bool>.VisitString(String str)
        {
            return _text == str.Text;
        }

        bool IExprVisitor<bool>.VisitSymbol(Symbol symbol)
        {
            return false;
        }
    }

    class ApplyCheckingVisitor : IExprVisitor<bool>
    {
        private IExprVisitor<bool> _headVisitor;
        private ReadOnlyCollection<IExprVisitor<bool>> _argsVisitors;

        public ApplyCheckingVisitor(IExprVisitor<bool> headVisitor, params IExprVisitor<bool>[] argsVisitors)
        {
            _headVisitor = headVisitor;
            _argsVisitors = new ReadOnlyCollection<IExprVisitor<bool>>(argsVisitors);
        }

        bool IExprVisitor<bool>.VisitApply(Apply apply)
        {
            return apply.Args.Count == _argsVisitors.Count
               && apply.Args.Zip(_argsVisitors, (e, v) => e.Apply(v)).Aggregate(apply.Head.Apply(_headVisitor), (a, b) => a & b);
        }

        bool IExprVisitor<bool>.VisitNumber(Number number)
        {
            return false;
        }

        bool IExprVisitor<bool>.VisitString(String str)
        {
            return false;
        }

        bool IExprVisitor<bool>.VisitSymbol(Symbol symbol)
        {
            return false;
        }
    }

    class CapturingCheckingVisitor : IExprVisitor<bool>
    {
        // readonly IExprVisitor<bool> _test;
        readonly Expr _key;
        readonly Dictionary<Expr, Expr> _captures;

        public CapturingCheckingVisitor(Expr key, Dictionary<Expr,Expr> captures)
        {
            _key = key;
            _captures = captures;
        }

        private bool CaptureImpl(Expr expr)
        {
            if (_captures.TryGetValue(_key, out var existExpr))
            {
                return existExpr.Equals(expr);
            }
            else
            {
                _captures.Add(_key, expr);
                return true;
            }
        }

        bool IExprVisitor<bool>.VisitApply(Apply apply)
        {
            return this.CaptureImpl(apply);
        }

        bool IExprVisitor<bool>.VisitNumber(Number number)
        {
            return this.CaptureImpl(number);
        }

        bool IExprVisitor<bool>.VisitString(String str)
        {
            return this.CaptureImpl(str);
        }

        bool IExprVisitor<bool>.VisitSymbol(Symbol symbol)
        {
            return this.CaptureImpl(symbol);
        }
    }

    class CheckerBuildingVisitor : IExprVisitor<IExprVisitor<bool>>
    {
        readonly Dictionary<Expr, Expr> _captures;

        public CheckerBuildingVisitor(Dictionary<Expr, Expr> capturesContext)
        {
            _captures = capturesContext;
        }

        IExprVisitor<bool> IExprVisitor<IExprVisitor<bool>>.VisitApply(Apply apply)
        {
            // Pattern[x]
            if (apply.Head.Equals(KnownNames.Pattern))
            {
                return new CapturingCheckingVisitor(apply.Args.First(),  _captures);
            }
            else
            {
                return new ApplyCheckingVisitor(apply.Head.Apply(this), apply.Args.Select(g => g.Apply(this)).ToArray());
            }
        }

        IExprVisitor<bool> IExprVisitor<IExprVisitor<bool>>.VisitNumber(Number number)
        {
            return new NumberCheckingVisitor(number.Value);
        }

        IExprVisitor<bool> IExprVisitor<IExprVisitor<bool>>.VisitString(String str)
        {
            return new StringCheckingVisitor(str.Text);
        }

        IExprVisitor<bool> IExprVisitor<IExprVisitor<bool>>.VisitSymbol(Symbol symbol)
        {
            return new SymbolCheckingVisitor(symbol.Name);
        }
    }

    class SymbolExtractingVisitor: IExprVisitor<Symbol>
    {
        public static readonly SymbolExtractingVisitor Instance = new SymbolExtractingVisitor();

        private SymbolExtractingVisitor() { }

        Symbol IExprVisitor<Symbol>.VisitApply(Apply apply)
        {
            return null;
        }

        Symbol IExprVisitor<Symbol>.VisitNumber(Number number)
        {
            return null;
        }

        Symbol IExprVisitor<Symbol>.VisitString(String str)
        {
            return null;
        }

        Symbol IExprVisitor<Symbol>.VisitSymbol(Symbol symbol)
        {
            return symbol;
        }
    }
}
