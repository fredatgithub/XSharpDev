﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.Emit;

namespace XSharp.MacroCompiler.Syntax
{
    using static CodeGen;

    internal partial class Node
    {
        internal virtual void Emit(ILGenerator ilg) { }
    }
    internal partial class Expr : Node
    {
        internal virtual void Emit(ILGenerator ilg, bool preserve) { }
        internal sealed override void Emit(ILGenerator ilg) { Emit(ilg, true); }
    }
    internal partial class TypeExpr : Expr
    {
    }
    internal partial class NativeTypeExpr : TypeExpr
    {
    }
    internal partial class NameExpr : TypeExpr
    {
    }
    internal partial class IdExpr : NameExpr
    {
    }
    internal partial class MemberAccessExpr : Expr
    {
    }
    internal partial class QualifiedNameExpr : NameExpr
    {
        internal override void Emit(ILGenerator ilg, bool preserve)
        {

        }
    }
    internal partial class BinaryExpr : Expr
    {
    }
    internal partial class PrefixExpr : Expr
    {
    }
    internal partial class PostfixExpr : Expr
    {
    }
    internal partial class LiteralExpr : Expr
    {
        internal override void Emit(ILGenerator ilg, bool preserve)
        {
            if (preserve)
            {
                EmitLiteral(ilg, (Constant)Symbol);
            }
        }
    }
    internal partial class SelfExpr : Expr
    {
    }
    internal partial class SuperExpr : Expr
    {
    }
    internal partial class CheckedExpr : Expr
    {
    }
    internal partial class UncheckedExpr : Expr
    {
    }
    internal partial class TypeOfExpr : Expr
    {
    }
    internal partial class SizeOfExpr : Expr
    {
    }
    internal partial class DefaultExpr : Expr
    {
    }
    internal partial class TypeCast : Expr
    {
    }
    internal partial class TypeConversion : TypeCast
    {
    }
    internal partial class IsExpr : Expr
    {
    }
    internal partial class AsTypeExpr : Expr
    {
    }
    internal partial class MethodCallExpr : Expr
    {
        internal override void Emit(ILGenerator ilg, bool preserve)
        {
            Args.Emit(ilg);
            var m = ((MethodSymbol)Symbol).Method;
            ilg.Emit(OpCodes.Call, m);
            if (!preserve && m.ReflectedType != typeof(void))
            {
                ilg.Emit(OpCodes.Pop);
            }
        }
    }
    internal partial class CtorCallExpr : MethodCallExpr
    {
    }
    internal partial class ArrayAccessExpr : MethodCallExpr
    {
    }
    internal partial class Arg : Node
    {
        internal override void Emit(ILGenerator ilg)
        {
            Expr.Emit(ilg, true);
        }
    }
    internal partial class ExprList : Expr
    {
        internal override void Emit(ILGenerator ilg, bool preserve)
        {
            for (int i = 0; i < Exprs.Count-1; i++)
            {
                Exprs[i].Emit(ilg, false);
            }
            Exprs.LastOrDefault()?.Emit(ilg, preserve);
        }
    }
    internal partial class LiteralArray : Expr
    {
    }
    internal partial class ArgList : Node
    {
        internal override void Emit(ILGenerator ilg)
        {
            foreach (var a in Args)
            {
                a.Emit(ilg);
            }
        }
    }
    internal partial class Codeblock : Node
    {
        internal override void Emit(ILGenerator ilg)
        {
            if (Params != null)
            {
                foreach (var p in Params)
                {
                }
            }
            bool isVoid = true;
            if (Body != null)
            {
                isVoid &= Body.Datatype == typeof(void);
                Body.Emit(ilg,true);
            }
            if (isVoid)
            {
                EmitDefault(ilg,(TypeSymbol)Symbol);
            }
            ilg.Emit(OpCodes.Ret);
        }
    }
}