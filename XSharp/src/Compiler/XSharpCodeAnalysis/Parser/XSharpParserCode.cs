﻿/*
   Copyright 2016-2017 XSharp B.V.

Licensed under the X# compiler source code License, Version 1.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.xsharp.info/licenses

Unless required by applicable law or agreed to in writing, software
Distributed under the License is distributed on an "as is" basis,
without warranties or conditions of any kind, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
namespace LanguageService.CodeAnalysis.XSharp.SyntaxParser
{
    using Antlr4.Runtime;

    public partial class XSharpParser : Parser
    {
        bool _ClsFunc = true;
        public bool AllowFunctionInsideClass
        {
            get { return _ClsFunc; }
            set { _ClsFunc = value; }
        }
        bool _xBaseVars = false;
        public bool AllowXBaseVariables
        {
            get { return _xBaseVars; }
            set { _xBaseVars = value; }
        }
        bool _namedArgs = false;
        public bool AllowNamedArgs
        {
            get { return _namedArgs; }
            set { _namedArgs = value; }
        }
        bool _allowGarbage;
        public bool AllowGarbageAfterEnd
        {
            get { return _allowGarbage; }
            set { _allowGarbage = value; }
        }
    }
}