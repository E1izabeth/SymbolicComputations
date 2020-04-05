using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SymbolicComputations
{
    class ExprCollector : IExprVisitor<string>
    {
        public static readonly ExprCollector Instance = new ExprCollector();

        string IExprVisitor<string>.VisitApply(Apply apply)
        {
            return apply.Head.Apply(this) + "["
                + string.Join(", ", apply.Args.Select(a => a.Apply(this)))
                + "]";
        }

        string IExprVisitor<string>.VisitNumber(Number number)
        {
            return number.Value.value.ToString();
        }

        string IExprVisitor<string>.VisitString(String str)
        {
            return "\"" + str.Text + "\"";
        }

        string IExprVisitor<string>.VisitSymbol(Symbol symbol)
        {
            return symbol.Name.Text;
        }
    }
}
