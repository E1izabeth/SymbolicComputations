using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SymbolicComputations
{
    public class SymbolDefEntry
    {
        public SymbolDefWeight Weight { get; private set; }
        public Expr Pattern { get; private set; }
        public Expr Value { get; private set; }

        public SymbolDefEntry(Expr pattern, Expr value)
        {
            this.Weight = pattern.Apply(SymbolWeightCalc.Instance);
            this.Pattern = pattern;
            this.Value = value;
        }
    }

    public class SymbolInfo : IEnumerable<SymbolDefEntry>
    {
        public Symbol Symbol { get; private set; }

        public SymbolInfo Prev { get; private set; }

        readonly List<SymbolDefEntry> _defs = new List<SymbolDefEntry>();
        readonly Dictionary<SymbolName, Expr> _attrs = new Dictionary<SymbolName, Expr>();

        public SymbolInfo(Symbol symbol)
        {
            this.Symbol = symbol;
            this.Prev = null;
        }

        public SymbolInfo(SymbolInfo prev)
        {
            this.Symbol = prev.Symbol;
            this.Prev = prev;
            this.AddAttributes(prev.GetAttributes());
        }

        public SymbolInfo MakeNext()
        {
            return new SymbolInfo(this);
        }

        public void AddAttributes(IList<Expr> attrs)
        {
            foreach (var attr in attrs.OfType<Apply>())
            {
                if (attr.Head is Symbol symbol)
                {
                    _attrs[symbol.Name] = attr;
                }
            }

            foreach (var symbol in attrs.OfType<Symbol>())
            {
                _attrs[symbol.Name] = symbol;
            }
        }

        public void ClearAttribute(Symbol key = null)
        {
            if (key.Equals(null))
            {
                _attrs.Clear();
            }
            else
            {
                _attrs.Remove(key.Name);
            }
        }

        public bool TryGetAttribute(Symbol key, out Expr attr)
        {
            return _attrs.TryGetValue(key.Name, out attr);
        }

        public ReadOnlyCollection<Expr> GetAttributes()
        {
            return new ReadOnlyCollection<Expr>(_attrs.Values.ToArray());
        }

        static readonly Comparer<SymbolDefEntry> _entriesComparer = Comparer<SymbolDefEntry>.Create(
            (a, b) => a.Weight.CompareTo(b.Weight)
        );

        public void SetDef(Expr pattern, Expr value)
        {
            var entry = new SymbolDefEntry(pattern, value);

            var index = _defs.BinarySearch(entry, _entriesComparer);

            // TODO: fix pattern comparison
            // f[0, r_, 0]  := ... ; // always first
            // f[x_, y_, 0] := ... ; // identical weight with the next two, but different pattern; stored in the order of definition
            // f[0, m_, n_] := ... ; // identical pattern with the next one, should replace on consecutive definitions
            // f[0, t_, k_] := ... ;
            // f[w_, t_, k_] := ... ; // always last

            // f[a] // diff with next
            // f[b]

            // f[a_] // same with next
            // f[b_]

            // See https://reference.wolfram.com/language/tutorial/PatternsAndTransformationRules.html

            if (index < 0)
            {
                _defs.Insert(~index, entry);
            }
            else
            {
                do
                {
                    if (_defs[index].Pattern.Equals(pattern))
                    {
                        _defs[index] = entry;
                        break;
                    }
                    else if (_defs[index].Weight > entry.Weight)
                    {
                        _defs.Insert(index, entry);
                        break;
                    }
                    else
                    {
                        index++;
                    }
                }
                while (index < _defs.Count);

                if (index >= _defs.Count)
                    _defs.Add(entry);
            }
        }

        public void ClearDef(Expr pattern)
        {
            var index = _defs.FindIndex(def => def.Pattern.Equals(pattern));

            if (index >= 0)
                _defs.RemoveAt(index);
        }

        public bool TryResolve(Expr expr, out Expr value)
        {
            foreach (var def in _defs)
            {
                if (def.Pattern.TryMatchExpr(expr, out var caps))
                {
                    value = def.Value.Replace(caps);
                    return true;
                }
            }

            value = null;
            return false;
        }

        public IEnumerator<SymbolDefEntry> GetEnumerator()
        {
            return _defs.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
