using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SymbolicComputations
{
    public class SymbolContext : IEnumerable<SymbolDefEntry>
    {
        class HeadResolver : IExprVisitor<Symbol>
        {
            public static readonly HeadResolver Instance = new HeadResolver();

            private HeadResolver() { }

            Symbol IExprVisitor<Symbol>.VisitApply(Apply apply)
            {
                return apply.Head as Symbol;
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

        //class Setter : IExprVisitor<Expr>
        //{
        //    public SymbolContext Owner { get; private set; }
        //    public Expr Value { get; private set; }

        //    public Setter(SymbolContext owner, Expr value)
        //    {
        //        this.Owner = owner;
        //        this.Value = value;
        //    }

        //    Expr IExprVisitor<Expr>.VisitApply(Apply apply)
        //    {
        //        return apply.Head is Symbol head
        //                    ? this.Owner.SetInternal(head, apply, this.Value)
        //                    : KnownNames.Set[apply, this.Value].MakeAbort("Association with complex head are not supported");
        //    }

        //    Expr IExprVisitor<Expr>.VisitNumber(Number number)
        //    {
        //        return KnownNames.Set[number, this.Value].MakeAbort("Failed to set something to constant");
        //    }

        //    Expr IExprVisitor<Expr>.VisitString(String str)
        //    {
        //        return KnownNames.Set[str, this.Value].MakeAbort("Failed to set something to string");
        //    }

        //    Expr IExprVisitor<Expr>.VisitSymbol(Symbol symbol)
        //    {
        //        return this.Owner.SetInternal(symbol, symbol, this.Value);
        //    }
        //}

        readonly Dictionary<SymbolName, SymbolInfo> _symbols = new Dictionary<SymbolName, SymbolInfo>();

        public SymbolContext Parent { get; private set; }

        public bool Transparent { get; private set; }

        public SymbolContext(IList<Expr> locals = null)
        {
            if (locals != null)
            {
                foreach (var item in locals.OfType<Symbol>())
                {
                    var head = item.Apply(HeadResolver.Instance);
                    this.GetActualSymbolInfo(head);
                }

                this.Transparent = locals.Any();
            }
            else
            {
                this.Transparent = false;
            }
        }

        public bool TryGetLocalSymbolInfo(Symbol head, out SymbolInfo info)
        {
            return _symbols.TryGetValue(head.Name, out info);
        }

        public bool TryGetSymbolInfo(Symbol head, out SymbolInfo info)
        {
            var ctx = this;
            while (!ctx.TryGetLocalSymbolInfo(head, out info) && ctx.Parent != null)
                ctx = ctx.Parent;

            return info != null;
        }

        private SymbolContext GetNonTransparentContext()
        {
            var ctx = this;
            while (ctx != null && ctx.Transparent)
                ctx = ctx.Parent;

            if (ctx == null)
                throw new InvalidOperationException("root context should be non-transparent always");

            return ctx;
        }

        private SymbolInfo GetActualSymbolInfo(Symbol head)
        {
            //if (!this.TryGetLocalSymbolInfo(head, out var info))
            //{
            //    info = this.TryGetSymbolInfo(head, out var oldInfo)
            //               ? oldInfo.MakeNext()
            //               : new SymbolInfo(head);

            //    _symbols.Add(head.Name, info);
            //}

            SymbolInfo info;

            var ctx = this;
            while (!ctx.TryGetLocalSymbolInfo(head, out info) && ctx.Transparent)
                ctx = ctx.Parent;

            if (info == null && !ctx.TryGetLocalSymbolInfo(head, out info))
            {
                info = ctx.TryGetSymbolInfo(head, out var oldInfo)
                           ? oldInfo.MakeNext()
                           : new SymbolInfo(head);

                ctx._symbols.Add(head.Name, info);
            }

            return info;
        }

        public SymbolContext MakeNext(IList<Expr> locals = null)
        {
            return new SymbolContext(locals) { Parent = this };
        }

        public bool TryResolve(Expr expr, out Expr result)
        {
            var head = expr.Apply(HeadResolver.Instance);

            if (head != null && this.TryGetSymbolInfo(head, out var info))
            {
                while (!info.TryResolve(expr, out result) && info.Prev != null)
                    info = info.Prev;
            }
            else
            {
                result = null;
            }

            return result != null;
        }

        public void Add(Symbol symbol, params Expr[] attrs)
        {
            this.AddAttributes(symbol, attrs);
        }

        public void AddAttributes(Symbol symbol, params Expr[] attrs)
        {
            this.GetActualSymbolInfo(symbol).AddAttributes(attrs);
        }

        public Expr GetAttributes(Symbol symbol)
        {
            return KnownNames.List[this.GetActualSymbolInfo(symbol).GetAttributes().ToArray()];
        }

        public bool TryGetAttribute(Symbol symbol, Symbol key, out Expr attr)
        {
            return this.GetActualSymbolInfo(symbol).TryGetAttribute(key, out attr);
        }

        public bool HasAttribute(Symbol symbol, Symbol key)
        {
            return this.TryGetAttribute(symbol, key, out var attr);
        }

        public void ClearAttributes(Symbol symbol, params Symbol[] symbols)
        {
            var info = this.GetActualSymbolInfo(symbol);

            if (symbols.Any())
            {
                foreach (var item in symbols)
                    info.ClearAttribute(item);
            }
            else
            {
                info.ClearAttribute();
            }
        }

        public Expr Set(Expr pattern, Expr value)
        {
            var head = pattern.Apply(HeadResolver.Instance);

            if (head != null)
            {
                this.GetActualSymbolInfo(head).SetDef(pattern, value);
                return KnownNames.Null;
            }
            else
            {
                return KnownNames.Set[pattern, value].MakeAbort("Unsupported Set target");
            }
        }

        public void Clear(Expr pattern)
        {
            var head = pattern.Apply(HeadResolver.Instance);

            if (head != null)
            {
                this.GetActualSymbolInfo(head).ClearDef(pattern);
            }
        }

        public IEnumerator<SymbolDefEntry> GetEnumerator()
        {
            return _symbols.Values.SelectMany(s => s).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}