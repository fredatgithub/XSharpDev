﻿//
// Copyright (c) XSharp B.V.  All Rights Reserved.
// Licensed under the Apache License, Version 2.0.
// See License.txt in the project root for license information.
//
using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
namespace Microsoft.CodeAnalysis.CSharp
{
    /// <summary>
    /// Represents various options that affect compilation, such as
    /// whether to emit an executable or a library, whether to optimize
    /// generated code, and so on.
    /// </summary>
    public sealed class XSharpSpecificCompilationOptions
    {
        public static readonly XSharpSpecificCompilationOptions Default = new XSharpSpecificCompilationOptions();

        static string _defaultIncludeDir;
        static string _windir;
        static string _sysdir;
        public static void SetDefaultIncludeDir(string dir)
        {
            _defaultIncludeDir = dir;
        }
        public static void SetWinDir(string dir)
        {
            _windir = dir;
        }
        public static void SetSysDir(string dir)
        {
            _sysdir = dir;
        }
        public XSharpSpecificCompilationOptions()
        {
            // All defaults are set at property level
        }

        public bool ArrayZero { get; internal set; } = false;
        public bool CaseSensitive { get; internal set; } = false;
        public int ClrVersion { get; internal set; } = 4;
        public string DefaultIncludeDir { get; internal set; } = _defaultIncludeDir;
        public XSharpDialect Dialect { get; internal set; } = XSharpDialect.Core;
        public string WindowsDir { get; internal set; } = _windir;
        public string SystemDir { get; internal set; } = _sysdir;
        public string IncludePaths { get; internal set; } = "";
        public bool ImplicitNameSpace { get; internal set; } = false;
        public bool InitLocals { get; internal set; } = false;
        public bool LateBinding { get; internal set; } = false;
        public bool AllowNamedArguments { get; internal set; } = false;
        public bool NoClipCall { get; internal set; } = false;
        public bool NoStdDef { get; internal set; } = false;
        public string NameSpace { get; set; } = "";
        public ParseLevel ParseLevel { get; set; } = ParseLevel.Complete;
        public bool PreProcessorOutput { get; internal set; } = false;
        public bool SaveAsCSharp { get; internal set; } = false;
        public bool DumpAST { get; internal set; } = false;
        public bool ShowDefs { get; internal set; } = false;
        public bool ShowIncludes { get; internal set; } = false;
        public string StdDefs { get; internal set; } = "XSharpDefs.xh";
        public XSharpTargetDLL TargetDLL { get; internal set; } = XSharpTargetDLL.Other;
        public bool Verbose { get; internal set; } = false;
        public bool Vo1 { get; internal set; } = false;
        public bool Vo2 { get; internal set; } = false;
        public bool Vo3 { get; internal set; } = false;
        public bool Vo4 { get; internal set; } = false;
        public bool Vo5 { get; internal set; } = false;
        public bool Vo6 { get; internal set; } = false;
        public bool Vo7 { get; internal set; } = false;
        public bool Vo8 { get; internal set; } = false;
        public bool Vo9 { get; internal set; } = false;
        public bool Vo10 { get; internal set; } = false;
        public bool Vo11 { get; internal set; } = false;
        public bool Vo12 { get; internal set; } = false;
        public bool Vo13 { get; internal set; } = false;
        public bool Vo14 { get; internal set; } = false;
        public bool Vo15 { get; internal set; } = false;
        public bool Vo16 { get; internal set; } = false;
        public bool Xpp1 { get; internal set; } = false;
        public bool Xpp2 { get; internal set; } = false;
        public bool Fox1 { get; internal set; } = false;
        public bool VulcanRTFuncsIncluded => RuntimeAssemblies.HasFlag(RuntimeAssemblies.VulcanRTFuncs);
        public bool VulcanRTIncluded => RuntimeAssemblies.HasFlag(RuntimeAssemblies.VulcanRT);
        public bool XSharpRTIncluded => RuntimeAssemblies.HasFlag(RuntimeAssemblies.XSharpRT);
        public bool XSharpVOIncluded => RuntimeAssemblies.HasFlag(RuntimeAssemblies.XSharpVO);
        public bool XSharpCoreIncluded => RuntimeAssemblies.HasFlag(RuntimeAssemblies.XSharpCore);
        public bool XSharpXPPIncluded => RuntimeAssemblies.HasFlag(RuntimeAssemblies.XSharpXPP);
        internal RuntimeAssemblies RuntimeAssemblies { get; set; } = RuntimeAssemblies.None;
        public bool Overflow { get; internal set; } = false;
        public bool MemVars { get; internal set; } = false;
        public bool AllowUnsafe { get; internal set; } = false;
        public bool UndeclaredMemVars { get; internal set; } = false;
        public CompilerOption ExplicitOptions { get ; internal set; } = CompilerOption.None;
        public bool UseNativeVersion { get; internal set; } = false;
        public string PreviousArgument { get; internal set; } = string.Empty;
        public TextWriter ConsoleOutput { get; internal set; }
    }

    public class PragmaBase
    {
        public Pragmastate State { get; private set; }
        public ParserRuleContext Context { get; private set; }
        public PragmaBase(ParserRuleContext context, Pragmastate state)
        {
            Context = context;
            State = state;
        }
        public int Line
        {
            get
            {
                if (Context == null)
                    return -1;
                return Context.Start.Line;
            }
        }
    }
    public class PragmaOption : PragmaBase
    {
        public CompilerOption Option { get; private set; }
        public PragmaOption(ParserRuleContext context, Pragmastate state, CompilerOption option) : base(context, state)
        {
            Option = option;
        }
  
    }
    public class PragmaWarning : PragmaBase
    {
        public IList<IToken> Numbers { get; private set; }
        public IToken Warning { get; private set; }
        public IToken Switch { get; private set; }
        public PragmaWarning(ParserRuleContext context, Pragmastate state, IList<IToken> tokens, IToken warning, IToken switch_) : base(context, state)
        {
            Numbers = tokens;
            Warning = warning;
            Switch = switch_;
        }
    }

    [Flags]
    public enum CompilerOption
    {
        None = 0,
        Overflow = 1 << 0,
        Vo1 = 1 << 1,
        Vo2 = 1 << 2,
        NullStrings = Vo2,
        Vo3 = 1 << 3,
        Vo4 = 1 << 4,
        SignedUnsignedConversion = Vo4,
        Vo5 = 1 << 5,
        ClipperCallingConvention = Vo5,
        Vo6 = 1 << 6,
        ResolveTypedFunctionPointersToPtr = Vo6,
        Vo7 = 1 << 7,
        ImplicitCastsAndConversions = Vo7,
        Vo8 = 1 << 8,
        Vo9 = 1 << 9,
        AllowMissingReturns = Vo9,
        Vo10 = 1 << 10,
        CompatibleIIF = Vo10,
        Vo11 = 1 << 11,
        ArithmeticConversions = Vo11,
        Vo12 = 1 << 12,
        ClipperIntegerDivisions = Vo12,
        Vo13 = 1 << 13,
        StringComparisons = Vo13,
        Vo14 = 1 << 14,
        FloatConstants = Vo14,
        Vo15 = 1 << 15,
        Vo16 = 1 << 16,
        Xpp1 = 1 << 17,
        Xpp2 = 1 << 18,
        Fox1 = 1 << 19,
        InitLocals = 1 << 20,
        NamedArgs = 1 << 21,
        ArrayZero = 1 << 22,
        LateBinding = 1 << 23,
        ImplicitNamespace = 1 << 24,
        MemVars = 1 << 25,
        UndeclaredMemVars = 1 << 26,
        ClrVersion = 1 << 27,
        All = -1

    }

    internal static class CompilerOptionDecoder
    {
        internal static CompilerOption Decode(string option)
        {
            switch (option.ToLower())
            {
                case "az":
                    return CompilerOption.ArrayZero;
                case "fovf":
                    return CompilerOption.Overflow;
                case "fox1":
                    return CompilerOption.Fox1;
                case "initlocals":
                    return CompilerOption.InitLocals;
                case "ins":
                    return CompilerOption.ImplicitNamespace;
                case "lb":
                    return CompilerOption.LateBinding;
                case "memvar":
                case "memvars":
                    return CompilerOption.MemVars;
                case "namedarguments":
                    return CompilerOption.NamedArgs;
                case "ovf":
                    return CompilerOption.Overflow;
                case "undeclared":
                    return CompilerOption.UndeclaredMemVars;
                case "vo1":
                    return CompilerOption.Vo1;
                case "vo2":
                    return CompilerOption.Vo2;
                case "vo3":
                    return CompilerOption.Vo3;
                case "vo4":
                    return CompilerOption.Vo4;
                case "vo5":
                    return CompilerOption.Vo5;
                case "vo6":
                    return CompilerOption.Vo6;
                case "vo7":
                    return CompilerOption.Vo7;
                case "vo8":
                    return CompilerOption.Vo8;
                case "vo9":
                    return CompilerOption.Vo9;
                case "vo10":
                    return CompilerOption.Vo10;
                case "vo11":
                    return CompilerOption.Vo11;
                case "vo12":
                    return CompilerOption.Vo12;
                case "vo13":
                    return CompilerOption.Vo13;
                case "vo14":
                    return CompilerOption.Vo14;
                case "xpp1":
                    return CompilerOption.Xpp1;
                case "xpp2":
                    return CompilerOption.Xpp2;
                default:
                    break;
            }
            return CompilerOption.None;
        }
    }

    public enum Pragmastate: byte
    {
        Default = 0,
        On = 1,
        Off = 2,
    }
}
