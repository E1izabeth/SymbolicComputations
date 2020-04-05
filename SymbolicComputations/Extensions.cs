using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SymbolicComputations
{
    class KnownNames
    {
        public static readonly Symbol Set, Delayed, Seq, If, While, Sum, Mul, Sub, Div, Mod,
                                      Less, Greater, Equal, NotEqual, LessOrEqual, GreaterOrEqual,
                                      And, Or, Xor, Not,
                                      True, False, Null, Pattern, x, y, z, Fib, Abort, Hold, HoldForm, List,
                                      HoldRest, HoldAll, HoldFirst, Orderless, Flat, Listable,
                                      SetAttrs, GetAttrs, Definition, Patterns, Attributes, Entry,
                                      Append, Prepend, First, Rest, Last, Length, Head, Echo, Print,
                                      Block, Module, Func, Clear, ReplaceAll, Replace, EmptyList,
                                      Concat, Substring, Split, Match, Introduce;

        static KnownNames()
        {
            SetupKnownNames<KnownNames>();
        }

        public static void SetupKnownNames<T>()
        {
            typeof(T).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).ToList()
                              .ForEach(f => { if (f.GetValue(null) == null) f.SetValue(null, new Symbol(f.Name)); });
        }

        public static Expr Symbol(string name)
        {
            return new Symbol(name);
        }
    }

    static class Extensions
    {
        public static Expr Apply(this Expr head, params Expr[] args)
        {
            return new Apply(head, args);
        }

        public static Expr Apply(this string head, params Expr[] args)
        {
            return new Apply(KnownNames.Symbol(head), args);
        }

        public static bool TryMatchExpr(this Expr pattern, Expr expr, out Dictionary<Expr, Expr> captures)
        {
            return expr.Apply(pattern.Apply(new CheckerBuildingVisitor(captures = new Dictionary<Expr, Expr>())));
        }

        public static Expr Replace(this Expr expr, Dictionary<Expr, Expr> replacements)
        {
            return expr.Apply(new ReplacingVisitor(replacements));
        }

        public static Expr SmartReplace(this Expr expr, Dictionary<Expr, Expr> replacements, bool fullReplace = false)
        {
            return expr.Apply(new SmartReplacingVisitor(replacements, fullReplace));
        }

        public static List<T> Append<T>(this List<T> list, T item)
        {
            list.Add(item);
            return list;
        }

        public static Expr MakeAbort(this Expr expr, string msg)
        {
            return expr.IsAbortExpr() ? expr : KnownNames.Abort[expr, new String(msg)];
        }

        public static bool IsAbortExpr(this Expr expr)
        {
            return expr.IsAbortExpr(out var msg);
        }

        public static bool TryGetSymbolName(this Expr expr, out SymbolName name)
        {
            return (name = expr.FindSymbolName()) != null;
        }

        public static SymbolName FindSymbolName(this Expr expr)
        {
            return expr.Apply(SymbolExtractingVisitor.Instance)?.Name;
        }

        public static Symbol FindSymbol(this Expr expr)
        {
            return expr.Apply(SymbolExtractingVisitor.Instance);
        }

        public static bool IsAbortExpr(this Expr expr, out string msg)
        {
            if (expr is Apply app && app.Head.Equals(KnownNames.Abort))
            {
                msg = app.Args.Count >= 2 ? (app.Args[1] as String)?.Text : null;
                return true;
            }
            else
            {
                msg = null;
                return false;
            }
        }

        public static T CreateDelegate<T>(this MethodInfo m)
            where T : Delegate
        {
            return (T)Delegate.CreateDelegate(typeof(T), m);
        }

        public static IEnumerable<T> Flatten<T>(this T node, Func<T, IEnumerable<T>> childsSelector, Func<T, bool> loopResolver = null)
        {
            return node.FlattenImpl(0, null, childsSelector, loopResolver);
        }

        public static IEnumerable<T> Flatten<T>(this T node, int debugLimit, Func<T, IEnumerable<T>> childsSelector, Func<T, bool> loopResolver = null)
        {
            return node.FlattenImpl(debugLimit, debugLimit > 0 ? new StringBuilder() : null, childsSelector, loopResolver);
        }

        static IEnumerable<T> FlattenImpl<T>(this T node, int debugLimit, StringBuilder sb, Func<T, IEnumerable<T>> childsSelector, Func<T, bool> loopResolver = null, int depth = 0)
        {
            if (sb != null)
                sb.AppendLine(new string(' ', depth) + node);
            if (debugLimit > 0 && depth > debugLimit)
                System.Diagnostics.Debugger.Break();

            yield return node;

            foreach (var child in childsSelector(node))
                if (loopResolver == null || loopResolver(child))
                    foreach (var subitem in child.FlattenImpl(debugLimit, sb, childsSelector, loopResolver, depth + 1))
                        yield return subitem;
        }

        public static IEnumerable<T> FlattenLeaves<T>(this T node, Func<T, IEnumerable<T>> childsSelector)
        {
            foreach (var child in childsSelector(node))
            {
                if (ReferenceEquals(child, node))
                    yield return node;
                else
                    foreach (var subitem in child.FlattenLeaves(childsSelector))
                        yield return subitem;
            }
        }
    }
}
