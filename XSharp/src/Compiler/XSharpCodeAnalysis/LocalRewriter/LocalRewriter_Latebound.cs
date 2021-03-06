﻿//
// Copyright (c) XSharp B.V.  All Rights Reserved.
// Licensed under the Apache License, Version 2.0.
// See License.txt in the project root for license information.
//

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using LanguageService.CodeAnalysis.XSharp.SyntaxParser;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed partial class LocalRewriter
    {
        private bool IsFoxAccessMember(BoundExpression loweredReceiver, out string areaName)
        {
            areaName = null;
            if (_compilation.Options.Dialect == XSharpDialect.FoxPro)
            {
                var xNode = loweredReceiver.Syntax.XNode;
                if (xNode?.Parent is XSharpParser.AccessMemberContext amc && amc.IsFox)
                {
                    if (loweredReceiver is BoundCall && amc.Expr is XSharpParser.PrimaryExpressionContext pc
                        && pc.Expr is XSharpParser.NameExpressionContext)
                    {
                        areaName = amc.AreaName;
                        return true;
                    }
                }
            }
            return false;
        }

        BoundLiteral _makeString(SyntaxNode node, string name)
        {
            return new BoundLiteral(node, ConstantValue.Create(name), _compilation.GetSpecialType(SpecialType.System_String));
        }
        BoundLiteral _makeLogic(SyntaxNode node, bool value)
        {
            return new BoundLiteral(node, ConstantValue.Create(value), _compilation.GetSpecialType(SpecialType.System_Boolean));
        }

        public BoundExpression MakeVODynamicGetMember(BoundExpression loweredReceiver, string name)
        {
            // check for FoxPro late access, such as Customer.LastName
            var syntax = loweredReceiver.Syntax;
            var nameExpr = _makeString(loweredReceiver.Syntax, name);
            if (IsFoxAccessMember(loweredReceiver, out var areaName))
            {
                string method = ReservedNames.FieldGetWaUndeclared;
                var exprUndeclared = _makeLogic(syntax, _compilation.Options.HasOption(CompilerOption.UndeclaredMemVars, syntax));
                var areaExpr = _makeString(syntax, areaName);
                var expr = _factory.StaticCall(_compilation.RuntimeFunctionsType(), method, areaExpr, nameExpr, exprUndeclared);
                return expr;
            }
            if (!_compilation.Options.HasOption(CompilerOption.LateBinding, syntax))
                return null;
            var usualType = _compilation.UsualType();
            if (((NamedTypeSymbol)loweredReceiver.Type).ConstructedFrom == usualType)
                loweredReceiver = _factory.StaticCall(usualType, ReservedNames.ToObject, loweredReceiver);
            loweredReceiver = MakeConversionNode(loweredReceiver, _compilation.GetSpecialType(SpecialType.System_Object), false);
            return _factory.StaticCall(_compilation.RuntimeFunctionsType(), ReservedNames.IVarGet, loweredReceiver, nameExpr);


        }

        public BoundExpression MakeVODynamicSetMember(BoundExpression loweredReceiver, string name, BoundExpression loweredValue)
        {
            var syntax = loweredReceiver.Syntax;
            var usualType = _compilation.UsualType();
            var value = loweredValue.Type == null ? new BoundDefaultExpression(syntax, usualType)
                : MakeConversionNode(loweredValue, usualType, false);
            var nameExpr = _makeString(syntax, name);
            if (IsFoxAccessMember(loweredReceiver, out var areaName))
            {
                string method =  ReservedNames.FieldSetWaUndeclared;
                var exprUndeclared = _makeLogic(syntax, _compilation.Options.HasOption(CompilerOption.UndeclaredMemVars,syntax));
                var areaExpr = _makeString(syntax, areaName);
                var expr = _factory.StaticCall(_compilation.RuntimeFunctionsType(), method, areaExpr, nameExpr,value, exprUndeclared);
                return expr;
            }

            if (!_compilation.Options.HasOption(CompilerOption.LateBinding, syntax))
                return null;

            if (((NamedTypeSymbol)loweredReceiver.Type).ConstructedFrom == usualType)
            {
                loweredReceiver = _factory.StaticCall(usualType, ReservedNames.ToObject, loweredReceiver);
            }
            loweredReceiver = MakeConversionNode(loweredReceiver, _compilation.GetSpecialType(SpecialType.System_Object), false);
            return _factory.StaticCall(_compilation.RuntimeFunctionsType(), ReservedNames.IVarPut, loweredReceiver, nameExpr, value);


        }

        public BoundExpression MakeVODynamicInvokeMember(BoundExpression loweredReceiver, string name,BoundDynamicInvocation node, ImmutableArray<BoundExpression> args)           
        {
            if (loweredReceiver.Type == _compilation.ArrayType())
            {
                if (_compilation.Options.Dialect.AllowASend())
                { 
                    
                    return MakeASend(loweredReceiver, name, args);
                }
                // This should not happen because we are not converting the method call to a dynamic call, but better safe than sorry.
                return null;
            }
            // for a method call the hierarchy is:
            // loweredReceiver = object
            // loweredReceiver.Parent = MemberAccess
            // loweredReceiver.Parent.Parent = InvocationExpression
            // loweredReceiver.Parent.Parent.Syntax.XNode = MethodCallContext
            //
            var parent = loweredReceiver.Syntax?.Parent?.Parent;
            var xnode = parent.XNode as XSharpParser.MethodCallContext;
            if (xnode != null && xnode.HasRefArguments)
            {
                return RewriteLateBoundCallWithRefParams(loweredReceiver, name, node, args);
            }

            var convArgs = new ArrayBuilder<BoundExpression>();
            var usualType = _compilation.UsualType();
            foreach (var a in args)
            {
                
                if (a.Type == null && ! a.Syntax.XIsCodeBlock)
                    convArgs.Add(new BoundDefaultExpression(a.Syntax, usualType));
                else
                   convArgs.Add(MakeConversionNode(a, usualType, false));
            }
            var aArgs = _factory.Array(usualType, convArgs.ToImmutableAndFree());
            // Note: Make sure the first parameter in __InternalSend() in the runtime is a USUAL!
            return _factory.StaticCall(_compilation.RuntimeFunctionsType(), ReservedNames.InternalSend,
                    MakeConversionNode(loweredReceiver, usualType, false),
                    new BoundLiteral(loweredReceiver.Syntax, ConstantValue.Create(name), _compilation.GetSpecialType(SpecialType.System_String)),
                    aArgs);

        }

        public BoundExpression MakeASend(BoundExpression loweredReceiver, string name, ImmutableArray<BoundExpression> args)
        {
            var convArgs = new ArrayBuilder<BoundExpression>();
            var usualType = _compilation.UsualType();
            var arrayType = _compilation.ArrayType();
            foreach (var a in args)
            {
                if (a.Type == null)
                    convArgs.Add(new BoundDefaultExpression(a.Syntax, usualType));
                else
                    convArgs.Add(MakeConversionNode(a, usualType, false));
            }
            var aArgs = _factory.Array(usualType, convArgs.ToImmutableAndFree());
            var expr = _factory.StaticCall(_compilation.RuntimeFunctionsType(), ReservedNames.ASend,
                    MakeConversionNode(loweredReceiver, arrayType, false),
                    new BoundLiteral(loweredReceiver.Syntax, ConstantValue.Create(name), _compilation.GetSpecialType(SpecialType.System_String)),
                    aArgs);
            _diagnostics.Add(new CSDiagnostic(new CSDiagnosticInfo(ErrorCode.WRN_ASend, name), loweredReceiver.Syntax.Location));
            return expr;
        }

    }
}
