using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SymbolicComputations.KnownNames;

namespace SymbolicComputations
{
    public class SymbolName : IComparable<SymbolName>, IEquatable<SymbolName>
    {
        public string Text { get; private set; }
        public long Id { get; private set; }

        private SymbolName(string text, long id)
        {
            this.Text = text;
            this.Id = id;
        }

        static readonly Dictionary<string, SymbolName> _defaultScope = new Dictionary<string, SymbolName>();

        public static implicit operator SymbolName(string text)
        {
            lock (_defaultScope)
            {
                return _defaultScope.TryGetValue(text, out var value) ? value
                                 : _defaultScope[text] = new SymbolName(text, _defaultScope.Count);
            }
        }

        int IComparable<SymbolName>.CompareTo(SymbolName other)
        {
            return this.Text.CompareTo(other.Text);
        }

        public override string ToString()
        {
            return this.Text;
        }

        public override bool Equals(object obj)
        {
            return obj is SymbolName other ? this.Equals(other) : false;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        public bool Equals(SymbolName other)
        {
            return this.Id == other.Id;
        }
    }

    public struct Value
    {
        public readonly double value;

        public Value(double value)
        {
            this.value = value;
        }

        public static implicit operator Value(double value)
        {
            return new Value(value);
        }
    }

    abstract public class Expr : IComparable<Expr>, IEquatable<Expr>
    {
        public bool Hold { get; private set; }

        protected Expr() { }

        public T Apply<T>(IExprVisitor<T> visitor)
        {
            return this.ApplyImpl<T>(visitor);
        }

        protected abstract T ApplyImpl<T>(IExprVisitor<T> visitor);

        public static implicit operator Expr(string text) { return new String(text); }
        public static implicit operator Expr(double value) { return new Number(value); }
        public static implicit operator Expr(bool value) { return value ? True : False; }

        public static Expr operator +(Expr a, Expr b) { return Sum[a, b]; }
        public static Expr operator -(Expr a, Expr b) { return Sub[a, b]; }
        public static Expr operator *(Expr a, Expr b) { return Mul[a, b]; }
        public static Expr operator /(Expr a, Expr b) { return Div[a, b]; }
        public static Expr operator %(Expr a, Expr b) { return Mod[a, b]; }

        public static Expr operator <(Expr a, Expr b) { return Less[a, b]; }
        public static Expr operator >(Expr a, Expr b) { return Greater[a, b]; }
        public static Expr operator <=(Expr a, Expr b) { return LessOrEqual[a, b]; }
        public static Expr operator >=(Expr a, Expr b) { return GreaterOrEqual[a, b]; }
        //public static Expr operator ==(Expr a, Expr b) { return Equal[a, b]; }
        //public static Expr operator !=(Expr a, Expr b) { return NotEqual[a, b]; }

        public static Expr operator &(Expr a, Expr b) { return And[a, b]; }
        public static Expr operator |(Expr a, Expr b) { return Or[a, b]; }
        public static Expr operator ^(Expr a, Expr b) { return Xor[a, b]; }
        public static Expr operator !(Expr a) { return Not[a]; }

        public Expr this[params Expr[] args] { get { return new Apply(this, args); } }

        public static Expr operator ~(Expr e) { return Pattern[e]; }

        public override string ToString()
        {
            return this.Apply(ExprCollector.Instance);
        }

        public int CompareTo(Expr other)
        {
            return other == null ? -1 : this.ToString().CompareTo(other.ToString());
        }

        public bool Equals(Expr other)
        {
            return other == null ? false : this.CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            return obj is Expr other && this.Equals(other);
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        static readonly MapVisitor _holdMapping = new MapVisitor(
            applyMap: (m, e) => new Apply(e.Head.Apply(m), e.Args.Select(a => a.Apply(m)).ToArray()) { Hold = true },
            symbolMap: (m, s) => new Symbol(s.Name) { Hold = true }
        );

        static readonly MapVisitor _unholdMapping = new MapVisitor();

        public Expr MakeHoldForm() { return this.Apply(_holdMapping); }
        public Expr MakeUnholdForm() { return this.Apply(_unholdMapping); }
    }

    public class Number : Expr
    {
        public Value Value { get; private set; }

        public Number(Value value)
        {
            this.Value = value;
        }

        protected override T ApplyImpl<T>(IExprVisitor<T> visitor)
        {
            return visitor.VisitNumber(this);
        }
    }

    public class String : Expr
    {
        public string Text { get; private set; }

        public String(string text)
        {
            this.Text = text;
        }

        protected override T ApplyImpl<T>(IExprVisitor<T> visitor)
        {
            return visitor.VisitString(this);
        }
    }

    public class Symbol : Expr
    {
        public SymbolName Name { get; private set; }

        public Symbol(SymbolName name)
        {
            if (name == null)
                throw new ArgumentNullException();

            this.Name = name;
        }

        protected override T ApplyImpl<T>(IExprVisitor<T> visitor)
        {
            return visitor.VisitSymbol(this);
        }
    }

    public class Apply : Expr
    {
        public Expr Head { get; private set; }
        public ReadOnlyCollection<Expr> Args { get; private set; }

        public Apply(Expr head, params Expr[] args)
        {
            if (head == null)
                throw new ArgumentNullException();
            if (args == null)
                throw new ArgumentNullException();

            this.Head = head;
            this.Args = new ReadOnlyCollection<Expr>(args);
        }

        protected override T ApplyImpl<T>(IExprVisitor<T> visitor)
        {
            return visitor.VisitApply(this);
        }
    }

    public interface IExprVisitor<T>
    {
        T VisitApply(Apply apply);
        T VisitNumber(Number number);
        T VisitString(String str);
        T VisitSymbol(Symbol symbol);
    }


}
