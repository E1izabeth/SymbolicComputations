using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SymbolicComputations
{
    public struct SymbolDefWeight : IComparable<SymbolDefWeight>, IEquatable<SymbolDefWeight>
    {
        public int AppParam { get; private set; }
        public int VarParam { get; private set; }

        public SymbolDefWeight(int appParam, int varParam) : this()
        {
            this.AppParam = appParam;
            this.VarParam = varParam;
        }

        public static SymbolDefWeight operator +(SymbolDefWeight w1, SymbolDefWeight w2)
        {
            return new SymbolDefWeight(w1.AppParam + w2.AppParam, w1.VarParam + w2.VarParam);
        }

        public static bool operator >(SymbolDefWeight w1, SymbolDefWeight w2)
        {
            return w1.CompareTo(w2) > 0;
        }

        public static bool operator ==(SymbolDefWeight w1, SymbolDefWeight w2)
        {
            return w1.CompareTo(w2) == 0;
        }

        public static bool operator !=(SymbolDefWeight w1, SymbolDefWeight w2)
        {
            return w1.CompareTo(w2) != 0;
        }

        public static bool operator <(SymbolDefWeight w1, SymbolDefWeight w2)
        {
            return w1.CompareTo(w2) < 0;
        }

        public static bool operator <=(SymbolDefWeight w1, SymbolDefWeight w2)
        {
            return w1.CompareTo(w2) <= 0;
        }

        public static bool operator >=(SymbolDefWeight w1, SymbolDefWeight w2)
        {
            return w1.CompareTo(w2) >= 0;
        }

        public override bool Equals(object obj)
        {
            return obj is SymbolDefWeight other ? this.Equals(other) : false;
        }

        public override int GetHashCode()
        {
            var hashCode = 1787652436;
            hashCode = hashCode * -1521134295 + this.AppParam.GetHashCode();
            hashCode = hashCode * -1521134295 + this.VarParam.GetHashCode();
            return hashCode;
        }

        public int CompareTo(SymbolDefWeight other)
        {
            if (this.AppParam > other.AppParam)
            {
                return 1;
            }
            else if (this.AppParam < other.AppParam)
            {
                return -1;
            }
            else
            {
                if (this.VarParam > other.VarParam)
                    return 1;
                else if (this.VarParam < other.VarParam)
                    return -1;
                else
                    return 0;
            }
        }

        public bool Equals(SymbolDefWeight other)
        {
            return this.CompareTo(other) == 0;
        }
    }

    class SymbolWeightCalc : IExprVisitor<SymbolDefWeight>
    {
        public static readonly SymbolWeightCalc Instance = new SymbolWeightCalc();

        public SymbolDefWeight VisitApply(Apply apply)
        {
            return apply.Args.Aggregate(apply.Head.Apply(this) + new SymbolDefWeight(1, 0), (w, arg) => w + arg.Apply(this));
        }

        public SymbolDefWeight VisitNumber(Number number)
        {
            return new SymbolDefWeight(0, 0);
        }

        public SymbolDefWeight VisitString(String str)
        {
            return new SymbolDefWeight(0, 0);
        }

        public SymbolDefWeight VisitSymbol(Symbol symbol)
        {
            return new SymbolDefWeight(0, 1);
        }
    }
}
