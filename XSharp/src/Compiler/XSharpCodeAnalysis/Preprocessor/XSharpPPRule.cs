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
using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using LanguageService.CodeAnalysis.XSharp.SyntaxParser;
using System.Diagnostics;


namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    [DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
    internal class PPRule
    {
        PPUDCType _type;
        PPMatchToken[] _matchtokens;                    
        PPResultToken[] _resulttokens;
        PPErrorMessages _errorMessages;
        internal PPUDCType Type { get { return _type; } }
        internal PPRule(XSharpToken udc, IList<XSharpToken> tokens, out PPErrorMessages errorMessages)
        {
            switch (udc.Type)
            {
                case XSharpLexer.PP_COMMAND:
                    if (udc.Text.ToLower() == "#command")
                        _type = PPUDCType.Command;
                    else
                        _type = PPUDCType.XCommand; 
                    break;
                case XSharpLexer.PP_TRANSLATE:
                    if (udc.Text.ToLower() == "#translate")
                        _type = PPUDCType.Translate; 
                    else
                        _type = PPUDCType.XTranslate;
                    break;
                default:
                    _type = PPUDCType.Define;
                    break;
            }
            var ltokens = new XSharpToken[tokens.Count];
            tokens.CopyTo(ltokens, 0);
            _errorMessages = new PPErrorMessages();
            errorMessages = null;
            if (!parseRuleTokens(udc, ltokens))
            {
                _type = PPUDCType.None;
                errorMessages = _errorMessages;
            }

        }
        bool parseRuleTokens(XSharpToken udc, XSharpToken[] _tokens)
        {
            int iSeperatorPos = -1;
            var markers = new Dictionary<string, PPMatchToken>(StringComparer.OrdinalIgnoreCase);

            if (_tokens?.Length == 0)
            {
                addErrorMessage(udc, "UDC is empty");
                return false;
            }
            for (int i = 0; i < _tokens.Length - 1; i++)
            {
                // Must be => without whitespace
                if (_tokens[i].Type == XSharpLexer.UDCSEP)
                {
                    iSeperatorPos = i;
                    break;
                }
            }
            if (iSeperatorPos < 0)
            {
                addErrorMessage(udc, "token '=>' not found in UDC ");
                return false;
            }
            XSharpToken[] _left = new XSharpToken[iSeperatorPos];
            XSharpToken[] _right = new XSharpToken[_tokens.Length - iSeperatorPos - 1];
            Array.Copy(_tokens, _left, iSeperatorPos);
            Array.Copy(_tokens, iSeperatorPos + 1, _right, 0, _right.Length);
            _matchtokens = analyzeMatchTokens(_left, markers);
            _resulttokens = analyzeResultTokens(_right,0);
            if (!checkMatchingTokens(_resulttokens, markers))
            {
                // Check to see if all result tokens have been matched
                // Unmatched Match tokens is fine (they may be deleted from the output)

                string unmatched = String.Empty;
                foreach (var r in _resulttokens)
                {
                    if (r.IsMarker&& r.MatchMarker == null)
                    {
                        addErrorMessage(r.Token, $"Result Marker '{r.Key}' not found in match list");
                    }
                }
            }
            bool result= false;
            if (_errorMessages == null || _errorMessages.Count == 0)
            {
                result = true;
            }
            else
            {
                result = false;
            }
            return result;
        }
        void addErrorMessage(XSharpToken token, string message)
        {
            _errorMessages.Add(new PPErrorMessage( token, message));
        }
        bool  checkMatchingTokens(PPResultToken[] results, Dictionary<string, PPMatchToken> markers)
        {
            bool allOk = true;
            // Set all marker indices
            for (int m = 0; m < _matchtokens.Length; m++)
            {
                var mt = _matchtokens[m];
                mt.Index = m;
                if (mt.Children != null)
                {
                    foreach (var c in mt.Children)
                    {
                        c.Index = m;
                    }
                }
            }
            for (int r = 0; r < results.Length; r++)
            {
                var restoken = results[r];
                if (restoken.IsMarker )
                {
                    var token = restoken.Token;
                    var name = restoken.Key;
                    if (markers.ContainsKey(name))
                    {
                        restoken.MatchMarker = markers[name];
                    }
                    else
                    {
                        allOk = false;
                    }
                }
                if (restoken.RuleTokenType == PPTokenType.ResultOptional)
                {
                    // not nested !
                    foreach (var e in restoken.OptionalElements)
                    {
                        if (e.IsMarker)
                        {
                            var token = e.Token;
                            var name2 = e.Key;
                            if (markers.ContainsKey(name2))
                            {
                                e.MatchMarker = markers[name2];
                            }
                            else
                            {
                                allOk = false;
                            }
                        }
                    }
                }
            }
            // we do not need the dictionary anymore. The rule has been analyzed
            return allOk;
        }
        void addToDict(Dictionary<string, PPMatchToken> markers , PPMatchToken element)
        {
            if (element.Token.IsName())
            {
                string name = element.Key;
                if (!markers.ContainsKey(name))
                {
                    markers.Add(name, element);
                }
                else
                {
                    addErrorMessage(element.Token, $"Duplicate Match marker {element.Key} found in UDC");
                }
            }

        }
        PPMatchToken[] analyzeMatchTokens(XSharpToken[] matchTokens, Dictionary<string, PPMatchToken> markers, int offset = 0, int nestLevel = 0)
        {
            var result = new List<PPMatchToken>();
            var max = matchTokens.Length;
            List<XSharpToken> more;
            XSharpToken name;
            PPMatchToken element;
            for (int i = 0; i < max; i++)
            {
                var token = matchTokens[i];
                switch (token.Type)
                {
                    
                    case XSharpLexer.LT:
                        // These conditions match IsName() as last condition 
                        // because the other matches are faster
                        if (i < max - 2
                            && matchTokens[i + 2].Type == XSharpLexer.GT
                            && matchTokens[i + 1].IsName())
                        {
                            // <idMarker>
                            name = matchTokens[i + 1];
                            element = new PPMatchToken(name, PPTokenType.MatchRegular);
                            result.Add(element);
                            addToDict(markers, element);
                            i += 2;
                        }
                        else if (i < max - 4
                            // <*idMarker*>
                            && matchTokens[i + 1].Type == XSharpLexer.MULT
                            && matchTokens[i + 3].Type == XSharpLexer.MULT
                            && matchTokens[i + 4].Type == XSharpLexer.GT
                            && matchTokens[i + 2].IsName())
                        {
                            name = matchTokens[i + 2];
                            element = new PPMatchToken(name, PPTokenType.MatchWild);
                            result.Add(element);
                            addToDict(markers, element);
                            i += 4;
                        }
                        else if (i < max - 4
                            // <(idMarker)>
                            && matchTokens[i + 1].Type == XSharpLexer.LPAREN
                            && matchTokens[i + 3].Type == XSharpLexer.RPAREN
                            && matchTokens[i + 4].Type == XSharpLexer.GT
                            && matchTokens[i + 2].IsName())

                        {
                            name = matchTokens[i + 2];
                            element = new PPMatchToken(name, PPTokenType.MatchExtended);
                            result.Add(element);
                            addToDict(markers, element);
                            i += 4;
                        }
                        else if (i < max - 4
                              && matchTokens[i + 2].Type == XSharpLexer.COMMA
                              && matchTokens[i + 3].Type == XSharpLexer.ELLIPSIS
                              && matchTokens[i + 4].Type == XSharpLexer.GT
                              && matchTokens[i + 1].IsName())
                        {
                            // <idMarker,...>
                            name = matchTokens[i + 1];
                            element = new PPMatchToken(name, PPTokenType.MatchList);
                            result.Add(element);
                            addToDict(markers, element);
                            i += 4;
                        }
                        else if (i < max - 3
                              && matchTokens[i + 2].Type == XSharpLexer.COLON
                              && matchTokens[i + 1].IsName())
                        {
                            // <idMarker:word list, separated with commas>
                            name = matchTokens[i + 1];
                            i += 3;
                            more = new List<XSharpToken>();
                            while (i <max && matchTokens[i].Type != XSharpLexer.GT)
                            {
                                token = matchTokens[i];
                                more.Add(token);
                                i++;
                            }
                            if (i == max )
                            {
                                // end of list found and not a GT then the close tag is missing
                                _errorMessages.Add(new PPErrorMessage(name, $"Bad match marker {name.Text} misses end Tag '>'"));
                            }
                            element = new PPMatchToken(name, PPTokenType.MatchRestricted);
                            result.Add(element);
                            addToDict(markers, element);
                            var tokens = new XSharpToken[more.Count];
                            more.CopyTo(tokens, 0);
                            element.Tokens = tokens;
                        }
                        else
                        {
                            // Most likely this is a normal < character ?
                        }
                        break;
                    case XSharpLexer.LBRKT:
                        /*
                         * eat block nested
                         * [...]
                         */
                        more = getNestedTokens(i, max, matchTokens);
                        if (more != null)
                        {
                            i = i + more.Count+1;
                            
                            var nested = analyzeMatchTokens(more.ToArray(), markers, result.Count, nestLevel+1);
                            if (nested.Length > 0)
                            {
                                // the '[' is added to the result list
                                // and the nested elements are added as children
                                // the type for '[' is MatchOptional
                                PPMatchToken marker = null;
                                foreach (var e in nested)
                                {
                                    if (e.IsMarker)
                                        marker = e;
                                }
                                if (marker == null)
                                {
                                    _errorMessages.Add(new PPErrorMessage(token, "Optional block does not contain a match marker"));
                                }
                                else
                                {
                                    element = new PPMatchToken(token, PPTokenType.MatchOptional, marker.Key);
                                    element.Children = nested;
                                    result.Add(element);
                                    // now walk back in the result list find the previous match marker
                                    if (element.Key.ToLower().EndsWith("n"))
                                    {
                                        findRepeats(element, result.ToArray());
                                    }
                                }
                            }
                        }

                        break;
                    case XSharpLexer.RBRKT:
                        addErrorMessage(token, "Closing bracket ']' found with missing '['");
                        break;
                    case XSharpLexer.BACKSLASH: // escape next token
                        if (i < max)
                        {
                            i++;
                            token = matchTokens[i];
                            result.Add(new PPMatchToken(token, PPTokenType.Token));
                        }
                        break;
                    default:
                        result.Add(new PPMatchToken(token, PPTokenType.Token));
                        break;
                }

            }
            // Now check for the tokens following list and repeat markers
            // So we know how to find the end of the list
            // For the command below the <list,..> token is ended when the
            // following tokens are found: OFF, TO, FOR, WHILE, NEXT, RECORD, REST and ALL
            /*
             * #command LIST [<list,...>]                                              ;
                         [<off:OFF>]                                                    ;
                         [<toPrint: TO PRINTER>]                                        ;
                         [TO FILE <(toFile)>]                                           ;
                         [FOR <for>]                                                    ;
                         [WHILE <while>]                                                ;
                         [NEXT <next>]                                                  ;
                         [RECORD <rec>]                                                 ;
                         [<rest:REST>]                                                  ;
                         [ALL]                                                          ;
             *
             */
            // The stopTokens list may contain duplicate elements. In the example above
            // it will have TO twice. That is not a problem.
            var mt = new PPMatchToken[result.Count];
            result.CopyTo(mt);
            if (nestLevel == 0)
            {
                for (int i = 0; i < mt.Length; i++)
                {
                    var marker = mt[i];
                    if (marker.RuleTokenType == PPTokenType.MatchList ||
                        marker.IsRepeat)
                    {
                        var stopTokens = new List<XSharpToken>();
                        findStopTokens(mt, i+1, stopTokens);
                        marker.Tokens = stopTokens.ToArray();
                    }
                    if (marker.IsOptional)
                    {
                        foreach (var child in marker.Children)
                        {
                            if (child.RuleTokenType == PPTokenType.MatchList)
                            {
                                var stopTokens = new List<XSharpToken>();
                                findStopTokens(mt, i+1, stopTokens);
                                child.Tokens = stopTokens.ToArray();
                                break;
                            }
                        }
                    }
                }
            }
            return mt;
        }

        void findStopTokens(PPMatchToken[] matchmarkers, int iStart, IList<XSharpToken> stoptokens)
        {
            bool finished = false;
            for (int j = iStart ; j < matchmarkers.Length && !finished; j++)
            {
                var next = matchmarkers[j];
                switch (next.RuleTokenType)
                {
                    case PPTokenType.MatchOptional:
                        // get first token in the Children of the optional clause
                        findStopTokens(next.Children, 0, stoptokens);
                        break;
                    case PPTokenType.MatchRestricted:
                        foreach (var token in next.Tokens)
                        {
                            if (token.Type != XSharpLexer.COMMA)
                                stoptokens.Add(token);
                        }
                        break;
                    case PPTokenType.Token:
                        stoptokens.Add(next.Token);
                        finished = true;
                        break;
                }
            }
        }

        void findRepeats( PPRuleToken token, IList<PPRuleToken> tokens)
        {
            // If that match marker is named abc1 and this marker ends with  n and has the name abcn 
            // then this is a repeat clause
            // repeat clauses must be optional and can appear in the source (and result) 0 .. n times
            var thisKey = token.Key.Substring(0, token.Key.Length - 1);
            for (int j = tokens.Count - 2; j > 0; j--)
            {
                var prev = tokens[j];
                if (prev.IsMarker)
                {
                    if (String.Compare(thisKey, 0, prev.Key, 0, thisKey.Length, StringComparison.OrdinalIgnoreCase) == 0
                        && prev.Key.EndsWith("1"))
                    {
                        token.IsRepeat = true;
                    }
                }
            }

        }

        List<XSharpToken> getNestedTokens(int start, int max, XSharpToken[] tokens)
        {
            XSharpToken token = tokens[start];
            List<XSharpToken> result = null;
            bool missing = false;
            var lbrkt = token;
            if (start < max - 1)   // must have at least [ name ]
            {
                result = new List<XSharpToken>();
                start++;
                var nestlevel = 1;
                while (start < max)
                {
                    token = tokens[start];
                    if (token.Type == XSharpLexer.LBRKT)
                        ++nestlevel;
                    if (token.Type == XSharpLexer.RBRKT)
                    {
                        --nestlevel;
                        if (nestlevel == 0)
                        {
                            break;
                        }
                    }
                    result.Add(token);
                    start++;
                }
                // more contains everything between the brackets including the first token
                if (nestlevel > 0)
                {
                    missing = true;
                }
            }
            if (result == null || result.Count == 0)
            {
                token = tokens[max-1];
                if (token.Type == XSharpLexer.RBRKT)
                    addErrorMessage(lbrkt, "Empty Optional clause found");
                else
                    missing = true;
            }
            if (missing)
                addErrorMessage(lbrkt, "Unclosed optional clause found (']' is missing)");
            return result;
        }
        PPResultToken[] analyzeResultTokens(XSharpToken[] resultTokens, int nestLevel)
        {
            var result = new List<PPResultToken>();
            var max = resultTokens.Length;
            List<XSharpToken> more;
            XSharpToken name;
            int lastTokenIndex = -1;
            ITokenSource lastTokenSource = null;
            for (int i = 0; i < resultTokens.Length; i++)
            {
                var token = resultTokens[i];
                if (token.TokenSource == lastTokenSource && token.TokenIndex > lastTokenIndex+1 )
                {
                    // whitespace tokens have been skipped
                    var ppWs = new XSharpToken(token, XSharpLexer.WS, " ");
                    ppWs.Channel = XSharpLexer.Hidden;
                    result.Add(new PPResultToken(ppWs, PPTokenType.Token));
                }
                lastTokenIndex = token.TokenIndex;
                lastTokenSource = token.TokenSource;
                switch (token.Type)
                {
                    case XSharpLexer.NEQ:
                        /*
                        * match #<idMarker>
                        */
                        if (i < resultTokens.Length - 3
                            && resultTokens[i + 1].Type == XSharpLexer.LT
                            && resultTokens[i + 2].IsName()
                            && resultTokens[i + 3].Type == XSharpLexer.GT)
                        {
                            name = resultTokens[i + 2];
                            result.Add(new PPResultToken(name, PPTokenType.ResultDumbStringify));
                            i += 3;
                        }
                        else
                        {
                            result.Add(new PPResultToken(token, PPTokenType.Token));
                        }
                        break;
                    case XSharpLexer.LT:
                        if (i < resultTokens.Length - 2
                            && resultTokens[i + 2].Type == XSharpLexer.GT
                            && resultTokens[i + 1].IsName())
                        {
                            // <idMarker>
                            name = resultTokens[i + 1];
                            result.Add(new PPResultToken(name, PPTokenType.ResultRegular));
                            i += 2;
                        }
                        else if (i < resultTokens.Length - 2
                            && resultTokens[i + 1].Type == XSharpLexer.STRING_CONST
                            && resultTokens[i + 2].Type == XSharpLexer.GT)
                        {
                            // <"idMarker">
                            var t = resultTokens[i + 1];
                            name = new XSharpToken(t, XSharpLexer.ID, t.Text.Substring(1, t.Text.Length - 2));
                            result.Add(new PPResultToken(name, PPTokenType.ResultNormalStringify));
                            i += 2;
                        }
                        else if (i < resultTokens.Length - 4
                            && resultTokens[i + 1].Type == XSharpLexer.LPAREN
                            && resultTokens[i + 3].Type == XSharpLexer.RPAREN
                            && resultTokens[i + 4].Type == XSharpLexer.GT
                            && resultTokens[i + 2].IsName())
                        {
                            // <(idMarker)>
                            name = resultTokens[i + 2];
                            result.Add(new PPResultToken(name, PPTokenType.ResultSmartStringify));
                            i += 4;
                        }
                        else if (i < resultTokens.Length - 4
                            && resultTokens[i + 1].Type == XSharpLexer.LCURLY
                            && resultTokens[i + 3].Type == XSharpLexer.RCURLY
                            && resultTokens[i + 4].Type == XSharpLexer.GT
                            && resultTokens[i + 2].IsName())
                        {
                            // <{idMarker}>
                            name = resultTokens[i + 2];
                            result.Add(new PPResultToken(name, PPTokenType.ResultBlockify));
                            i += 4;
                        }
                        else if (i < resultTokens.Length - 4
                            && resultTokens[i + 1].Type == XSharpLexer.DOT
                            && resultTokens[i + 3].Type == XSharpLexer.DOT
                            && resultTokens[i + 4].Type == XSharpLexer.GT
                            && resultTokens[i + 2].IsName())
                        {
                            // <.idMarker.>
                            name = resultTokens[i + 2];
                            result.Add(new PPResultToken(name, PPTokenType.ResultLogify));
                            i += 4;
                        }

                        break;
                    case XSharpLexer.LBRKT:
                        /*
                         * eat block
                        * [ ... ], 
                        * when nested then this is seen as a repeated result clause
                         */
                        if (nestLevel == 0)
                        {
                            more = getNestedTokens(i, max, resultTokens);
                            if (more != null)
                            {
                                i = i + more.Count + 1;
                                var nested = analyzeResultTokens(more.ToArray(), nestLevel + 1);
                                PPResultToken marker = null;
                                foreach (var e in nested)
                                {
                                    if (e.IsMarker)
                                        marker = e;
                                }
                                if (marker == null)
                                {
                                    _errorMessages.Add(new PPErrorMessage(token, "Optional block does not contain a match marker"));
                                }
                                else
                                {
                                    var element = new PPResultToken(token, PPTokenType.ResultOptional, marker.Key);
                                    element.OptionalElements = nested;
                                    result.Add(element);
                                    // now walk back in the result list find the previous match marker
                                    if (element.Key.ToLower().EndsWith("n"))
                                    {
                                        findRepeats(element, result.ToArray());
                                    }

                                }

                            }
                        }
                        else
                        {
                            _errorMessages.Add(new PPErrorMessage(token, "Nested result markers are not allowed"));
                        }
                        break;
                    case XSharpLexer.BACKSLASH: // escape next token
                        if (i < max)
                        {
                            i++;
                            token = resultTokens[i];
                            result.Add(new PPResultToken(token, PPTokenType.Token));
                        }
                        break;

                    default:
                        result.Add(new PPResultToken(token, PPTokenType.Token));
                        break;
                }

            }
            var resulttokens = new PPResultToken[result.Count];
            result.CopyTo(resulttokens);
            return resulttokens;

        }
        internal string Key
        {
            get
            {
                if (_matchtokens.Length > 0)
                {
                    var token = _matchtokens[0].Token;
                    if (token.IsName())
                        return token.Text.ToUpperInvariant();
                    else
                        return token.Text;
                }
                else
                    return "(Empty)";
            }
        }
        internal string Name
        {
            get
            {
                if (_matchtokens?.Length > 0)
                {
                    string result = "";
                    foreach (var token in _matchtokens)
                    {
                        result += token.SyntaxText + " ";
                    }
                    return result.Trim();
                }
                else
                {
                    return "(empty)";
                }
            }
        }
        internal string GetDebuggerDisplay()
        {
            return this.Name;
        }
        bool tokenEquals(XSharpToken lhs, XSharpToken rhs)
        {
            if (lhs != null && rhs != null)
                return stringEquals(lhs.Text, rhs.Text);
            return false;
        }
        bool stringEquals (string lhs, string rhs)
        {
            if (lhs?.Length <= 4)
                return String.Equals(lhs, rhs, StringComparison.OrdinalIgnoreCase);
            switch (this.Type)
            {
                case PPUDCType.Command:
                case PPUDCType.Translate:
                    // dBase/Clipper syntax 4 characters is enough
                    return String.Compare(lhs, 0, rhs, 0, rhs.Length, StringComparison.OrdinalIgnoreCase) == 0;
                case PPUDCType.XCommand:
                case PPUDCType.XTranslate:
                    return String.Equals(lhs, rhs, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        internal bool matchListToken(PPMatchToken mToken, IList<XSharpToken> tokens, ref int iSource, PPMatchRange[] matchInfo )
        {
            // This should match a list of expressions
            // until one of the tokens in mToken.Tokens is found
            // comma's are NOT required
            // See for example Std.ch from Clipper:
            /*
             * #command @ <row>, <col> SAY <sayxpr>                                    ;
                        [<sayClauses,...>]                              ;
                        GET <var>                                       ;
                        [<getClauses,...>]                              ;
                      => @ <row>, <col> SAY <sayxpr> [<sayClauses>]                     ;
                       ; @ Row(), Col()+1 GET <var> [<getClauses>]


             */
            if (mToken.RuleTokenType != PPTokenType.MatchList)
                return false;
            var stopTokens = mToken.Tokens;
            int iStart = iSource;
            List<PPMatchRange> matches = new List<PPMatchRange>();
            while (iSource < tokens.Count)
            {
                var token = tokens[iSource];
                // check to see if the current token is in the stoptokens list
                foreach (var stopToken in stopTokens)
                {
                    if (tokenEquals(stopToken, token))
                    {
                        matchInfo[mToken.Index] = PPMatchRange.Create(matches);
                        return true;
                    }
                }
                // after matchExpresssion iSource points to the next token after the expression
                int iEnd = matchExpression(iSource, tokens, null);
                if (iEnd != iSource)
                {
                    matches.Add(PPMatchRange.Create(iSource, iEnd - 1));
                    iSource = iEnd;
                }
                // IsOperator included comma, ellipses etc.
                else if (XSharpLexer.IsOperator(token.Type))    
                {
                    matches.Add(PPMatchRange.Create(iSource, iSource));
                    iSource += 1;
                }
                else
                {
                    return false;
                }
            }
            matchInfo[mToken.Index] = PPMatchRange.Create(matches);
            return true;
        }
        internal bool matchToken(PPMatchToken mToken, ref int iRule, int iLastRule,  ref int iSource, IList<XSharpToken> tokens, PPMatchRange[] matchInfo, IList<XSharpToken> matchedWithToken)
        {
            XSharpToken sourceToken = tokens[iSource];
            XSharpToken ruleToken = mToken.Token;
            int iChild = 0;
            int iEnd;
            bool found = false;
            switch (mToken.RuleTokenType)
            {
                 case PPTokenType.Token:
                    if (this.tokenEquals(ruleToken, sourceToken))
                    {
                        matchInfo[mToken.Index] = PPMatchRange.Token(iSource);
                        iSource += 1;
                        iRule += 1;
                        found = true;
                        matchedWithToken.Add(sourceToken);
                    }
                    break;
                case PPTokenType.MatchRegular:
                    // Matches an expression
                    // use Expression method to find the end of the list
                    // iEnd points to the next token after the expression
                    iEnd = matchExpression(iSource, tokens, null);
                    matchInfo[mToken.Index] = PPMatchRange.Create(iSource, iEnd - 1);
                    iSource = iEnd;
                    iRule += 1;
                    found = true;
                    break;
                case PPTokenType.MatchList:
                    // ignore for now
                    if (matchListToken(mToken, tokens, ref iSource, matchInfo))
                    {
                        iRule += 1;
                        found = true;
                    }
                    break;

                case PPTokenType.MatchRestricted:
                    // match the words in the list.
                    // the token in the rule is the match marker
                    // the words to be checked are the MoreTokens
                    // This list includes the original commas because some restricted markers can have more than word:
                    // LIST ....  [<toPrint: TO PRINTER>] 
                    // where others have a list of single words
                    // #command SET CENTURY <x:ON,OFF,&>      => __SetCentury( <(x)> ) 
                    XSharpToken lastToken = null;
                    int iLast = mToken.Tokens.Length - 1;
                    int iMatch = 0;
                    int iCurrent = iSource;
                    for (iChild = 0; iChild <= iLast; iChild++)
                    {
                        var child = mToken.Tokens[iChild];
                        lastToken = child;
                        if (child.Type == XSharpLexer.COMMA)
                        {
                            iMatch = 0;
                        }
                        else if (tokenEquals(child, sourceToken) )
                        {
                            iMatch += 1;
                            if (iChild == iLast) // No token following this one
                            {
                                break;
                            }
                            // next token comma ?
                            var next = mToken.Tokens[iChild + 1];
                            if (next.Type == XSharpLexer.COMMA)
                            {
                                break;
                            }
                            iCurrent += 1;
                            sourceToken = tokens[iCurrent];
                        }
                        else
                        {
                            iMatch = 0;
                        }
                    }
                    found = iMatch > 0;
                    if (found)
                    {
                        iEnd = iCurrent;
                        if (lastToken.Type == XSharpLexer.AMP)
                        {
                            // when the ampersand is the last token, then we also include the following token
                            // This happens when we match the rule
                            // #command SET CENTURY <x:ON,OFF,&>      => __SetCentury( <(x)> ) 
                            // with the source SET CENTURY &MyVar
                            // Thus generates the output __SetCentury((MyVar))
                            if (lastToken == mToken.Tokens[mToken.Tokens.Length - 1] && tokens.Count > iEnd)
                            {
                                iEnd += 1;
                                iSource = iEnd;
                            }
                        }
                        matchInfo[mToken.Index] = PPMatchRange.Create(iSource, iEnd);
                        for (int i = iSource; i <= iEnd; i++)
                        {
                            matchedWithToken.Add(tokens[i]);
                        }
                        iSource = iEnd + 1;
                        iRule += 1;
                    }
                    break;
                case PPTokenType.MatchWild:
                    matchInfo[mToken.Index] = PPMatchRange.Create(iSource, tokens.Count-1);
                    found = true;                    // matches anything until the end of the list
                    break;
                case PPTokenType.MatchExtended:
                    // either match a single token or a token in parentheses
                    if (sourceToken.Type == XSharpLexer.LPAREN)
                    {
                        if (iSource < tokens.Count - 2 &&
                            tokens[iSource + 1].IsName() &&
                            tokens[iSource + 2].Type == XSharpLexer.RPAREN)
                        {
                            // Ok
                            matchInfo[mToken.Index] = PPMatchRange.Create(iSource, iSource + 2);
                            iSource += 1;
                            iRule += 1;
                            found = true;
                        }
                    }
                    else // match single token
                    {
                        matchInfo[mToken.Index] = PPMatchRange.Create(iSource, iSource);
                        iSource += 1;
                        iRule += 1;
                        found = true;
                    }
                    break;
                case PPTokenType.MatchOptional:
                    // 
                    // Get sublist of optional token and match with source
                    var optional = mToken.Children;
                    bool optfound = true;
                    int iOriginal = iSource;
                    iChild = 0;                
                    while (iChild < optional.Length && iSource < tokens.Count  && optfound)
                    {
                        var mchild = optional[iChild];
                        if (!matchToken(mchild, ref iChild, _matchtokens.Length, ref iSource, tokens, matchInfo,matchedWithToken))
                            optfound = false;
                    }
                    if (optfound)
                    {
                        found = true;
                        if (!mToken.IsRepeat)
                            iRule += 1;
                    }
                    else
                        iSource = iOriginal;
                    break;
            }
            return found;
        }
        internal bool Matches(IList<XSharpToken> tokens, out PPMatchRange[] matchInfo)
        {
            int iRule = 0;
            int iSource = 0;
            matchInfo = new PPMatchRange[_matchtokens.Length];
            List<XSharpToken> matchedWithToken = new List<XSharpToken>();
            while (iRule < _matchtokens.Length && iSource < tokens.Count)
            {
                var mtoken = _matchtokens[iRule];
                if (!matchToken(mtoken, ref iRule, _matchtokens.Length,  ref iSource, tokens, matchInfo, matchedWithToken))
                {
                    if (! mtoken.IsOptional)
                        return false;
                    matchInfo[iRule] = PPMatchRange.Optional();
                    iRule++;
                }
            } 
            while (iRule < _matchtokens.Length)
            {
                // check to see if the remaining tokens are optional
                // when not, then there is no match
                if (!_matchtokens[iRule].IsOptional)
                {
                    return false;
                }
                matchInfo[iRule] = PPMatchRange.Optional();
                iRule++;
            }
            // Now mark the tokens that were matched with tokens in the UDC with the keyword color
            foreach (var token in matchedWithToken)
            {
                if (token.Type == XSharpLexer.ID)
                    token.Type = XSharpLexer.UDC_KEYWORD;
            }

            return true;

        }
        internal IList<XSharpToken> Replace(IList<XSharpToken> tokens, PPMatchRange[] matchInfo)
        {
            Debug.Assert(matchInfo.Length == _matchtokens.Length);
            return Replace(_resulttokens, tokens, matchInfo);

        }
        internal IList<XSharpToken> Replace(PPResultToken[] resulttokens, IList<XSharpToken> tokens, PPMatchRange[] matchInfo)
        {
            Debug.Assert(matchInfo.Length == _matchtokens.Length);
            IList<XSharpToken> result = new List<XSharpToken>();
            foreach (var resultToken in resulttokens)
            {
                switch (resultToken.RuleTokenType)
                {
                    case PPTokenType.Token:
                        result.Add(resultToken.Token);
                        break;
                    case PPTokenType.ResultLogify:
                        // check to see if the token was matched.
                        logifyResult(resultToken, tokens, matchInfo, result);
                        break;
                    case PPTokenType.ResultBlockify:
                        blockifyResult(resultToken, tokens, matchInfo, result);
                        break;
                    case PPTokenType.ResultRegular:
                        regularResult(resultToken, tokens, matchInfo, result);
                        break;
                    case PPTokenType.ResultSmartStringify:
                    case PPTokenType.ResultNormalStringify:
                    case PPTokenType.ResultDumbStringify:
                        stringifyResult(resultToken, tokens, matchInfo, result);
                        break;
                    case PPTokenType.ResultOptional:
                        optionalResult(resultToken, tokens, matchInfo, result);
                        break;
                }
            }
            // we need to determine the tokens at the end of the tokens list that are not matched 
            // in the results and then copy these to the result as well
            int last = -1;
            foreach (var m in matchInfo)
            {
                if (m.End > last)
                    last = m.End;
            }
            for (int i = last + 1; i < tokens.Count; i++)
            {
                result.Add(tokens[i]);                
            }

            return result;
        }

        void optionalResult(PPResultToken rule, IList<XSharpToken> tokens, PPMatchRange[] matchInfo, IList<XSharpToken> result)
        {
            if (rule.MatchMarker != null)
            {
                var block = Replace(rule.OptionalElements, tokens, matchInfo);
                foreach (var e in block)
                {
                    result.Add(e);
                }
            }
            return;
        }


        void regularResult(PPResultToken rule, IList<XSharpToken> tokens, PPMatchRange[] matchInfo, IList<XSharpToken> result)
        {
            // write input text to the result text without no changes
            // for example:
            // #command SET CENTURY (<x>) => __SetCentury( <x> )
            // the output written is the literal text for x, so the token(s) x
            var range = matchInfo[rule.MatchMarker.Index];
            if (!range.Empty )
            {
                // No special handling for List markers. Everything is copied including commas etc.
                for (int i = range.Start; i <= range.End; i++)
                {
                    var token = tokens[i];
                    result.Add(token);
                }
            }
            return;
        }

        void blockifySingleResult(PPResultToken rule, IList<XSharpToken> tokens, PPMatchRange range, IList<XSharpToken> result)
        {
            int start = range.Start;
            int end = range.End; 
            bool addBlockMarker = true;
            XSharpToken nt;
            XSharpToken t;
            if (range.Length == 1)
            {
                // for comma's and other separators
                if (XSharpLexer.IsOperator(tokens[start].Type))
                {
                    result.Add(tokens[start]);
                    return;
                }
            }
            if (range.Length> 4)
            {
                if (tokens[start].Type == XSharpLexer.LCURLY &&
                    tokens[start + 1].Type == XSharpLexer.PIPE &&
                    tokens[start + 2].Type == XSharpLexer.PIPE &&
                    tokens[end].Type == XSharpLexer.RCURLY)
                    addBlockMarker = false;
            }
            if (addBlockMarker)
            {
                t = tokens[start];
                nt = new XSharpToken(t, XSharpLexer.LCURLY, "{");
                result.Add(nt);
                nt = new XSharpToken(t, XSharpLexer.PIPE, "|");
                result.Add(nt);
                result.Add(nt);
            }
            for (int i = start; i <= end; i++)
            {
                var token = tokens[i];
                result.Add(token);
            }
            if (addBlockMarker)
            {
                t = tokens[end];
                nt = new XSharpToken(t, XSharpLexer.RCURLY, "}");
                result.Add(nt);
            }
        }

    
        void blockifyResult(PPResultToken rule, IList<XSharpToken> tokens, PPMatchRange[] matchInfo, IList<XSharpToken> result)
        {
            // write output text as codeblock
            // so prefixes with "{||"and suffixes with "}
            // if the input is already a code block then nothing is changed
            // when the input is a list then each element in the list will be
            // converted to a code block
            var range = matchInfo[rule.MatchMarker.Index];
            if (!range.Empty )
            {
                if (rule.MatchMarker.RuleTokenType == PPTokenType.MatchList && range.IsList)
                {
                    foreach (var element in range.Children)
                    {
                        blockifySingleResult(rule, tokens, element, result);
                    }
                }
                else
                {
                    blockifySingleResult(rule, tokens, range, result);
                }
            }
            return;
        }

        void stringifySingleResult(PPResultToken rule, IList<XSharpToken> tokens, PPMatchRange range, IList<XSharpToken> result)
        {
            XSharpToken newToken;
            var start = range.Start;
            var end = range.End;
            if (range.Length == 1)
            {
                // for comma's and other separators
                if (XSharpLexer.IsOperator(tokens[start].Type))
                {
                    result.Add(tokens[start]);
                    return;
                }
            }
            switch (rule.RuleTokenType)
            {
                // Handle 3 kind of stringifies:
                case PPTokenType.ResultDumbStringify:
                    // return original contents
                    // #command SET COLOR TO [<*spec*>]        => SetColor( #<spec> )
                    // this changes
                    // SET COLOR TO r/w into SetColor("r/w")
                    // Note that this match rule uses a wild match marker: this matches everything until end of statement
                    // change type of token to STRING_CONST;
                    if (!range.Empty )
                    {
                        var startindex = tokens[start].StartIndex;
                        var endindex = tokens[end].StopIndex;
                        var interval = new Interval(startindex, endindex);
                        string allText = tokens[start].TokenSource.InputStream.GetText(interval);
                        allText = "\"" + allText + "\"";
                        result.Add(new XSharpToken(tokens[start], XSharpLexer.STRING_CONST, allText));
                    }
                    else
                    {
                        // no match, then dumb stringify write an empty string
                        result.Add(new XSharpToken(tokens[0], XSharpLexer.NULL_STRING, "NULL_STRING"));
                    }
                    break;
                case PPTokenType.ResultNormalStringify:
                    // Delimit the input with string delimiters
                    if (!range.Empty )
                    {
                        var startindex = tokens[start].StartIndex;
                        var endindex = tokens[end].StopIndex;
                        var interval = new Interval(startindex, endindex);
                        string allText = tokens[start].TokenSource.InputStream.GetText(interval);
                        allText = "\"" + allText + "\"";
                        result.Add(new XSharpToken(tokens[start], XSharpLexer.STRING_CONST, allText));
                    }
                    break;


                case PPTokenType.ResultSmartStringify:
                    // Only works when input text is delimited with parentheses
                    // if the match marker is a list then each element is stringified and it stays a list
                    // for example: 
                    // #command SET CENTURY <x:ON,OFF,&>      => __SetCentury( <(x)> )
                    // the contents of x must be converted to a string

                    bool addStringDelimiters = true;
                    if (!range.Empty )
                    {
                        for (int i = start; i <= end; i++)
                        {
                            var token = tokens[i];
                            switch (token.Type)
                            {
                                case XSharpLexer.CHAR_CONST:
                                case XSharpLexer.STRING_CONST:
                                case XSharpLexer.ESCAPED_STRING_CONST:
                                case XSharpLexer.INTERPOLATED_STRING_CONST:
                                    result.Add(token);
                                    break;
                                case XSharpLexer.AMP:
                                    addStringDelimiters = false;
                                    break;
                                default:
                                    if (addStringDelimiters)
                                    {
                                        newToken = new XSharpToken(token, XSharpLexer.STRING_CONST, token.Text);
                                        newToken.Text = "\"" + token.Text + "\"";
                                    }
                                    else
                                    {
                                        newToken = new XSharpToken(token, XSharpLexer.ID, token.Text);
                                    }
                                    result.Add(newToken);
                                    break;
                            }
                        }
                    }
                    break;
            }
            return;

        }

        void stringifyResult(PPResultToken rule, IList<XSharpToken> tokens, PPMatchRange[] matchInfo, IList<XSharpToken> result)
        {
            var range = matchInfo[rule.MatchMarker.Index];
            if (!range.Empty )
            {
                if (rule.MatchMarker.RuleTokenType == PPTokenType.MatchList && range.IsList)
                {
                    bool first = true;
                    foreach (var element in range.Children)
                    {
                        if (!first)
                            result.Add(new XSharpToken(XSharpLexer.COMMA, ","));
                        stringifySingleResult(rule, tokens, element, result);
                        first = false;

                    }
                }
                else
                {
                    stringifySingleResult(rule, tokens, range, result);
                }
            }

        }

        void logifyResult(PPResultToken rule, IList<XSharpToken> tokens, PPMatchRange[] matchInfo, IList<XSharpToken> result )
        {
            // when input is empty then return a literal token FALSE
            // else return a literal token TRUE
            // copy positional information from original token
            // If input is more than one token, then change the other tokens to type WhiteSpace
            // this is normally used with the restricted match marker
            //
            // #command INDEX ON <key> TO <(file)> [<u: UNIQUE>]                       ;
            //=> dbCreateIndex(                                                 ;
            //            <(file)>, <"key">, <{key}>,                     ;
            //            if( <.u.>, .t., NIL )                           ;
            //          )
            // When the Unique keyword is found in the input then the <.u.> marker is replaced with TRUE
            // else with .F.
            // In this case no .F. is written, to make sure that the global SetUnique() setting is used
            
            var range = matchInfo[rule.MatchMarker.Index];
            if (!range.Empty )
            {
                IToken t = tokens[range.Start];
                result.Add(new XSharpToken(t, XSharpLexer.TRUE_CONST, ".T."));
            }
            else
            {
                // No token to read the line/column from
                result.Add(new XSharpToken(XSharpLexer.FALSE_CONST, ".F."));
            }
            return;
        }

        bool tokenCanStartExpression(int pos, IList<XSharpToken> tokens)
        {
            XSharpToken token;
            token = tokens[pos];
            if (! token.NeedsLeft() && ! token.IsEndOfCommand())
            {
                return true;
            }
            return false;
        }
        int matchExpression(int start, IList<XSharpToken> tokens, XSharpToken stopToken)
        {
            if (!tokenCanStartExpression(start, tokens))
                return start;
            int current = start;
            int braceLevel = 0;
            int count = tokens.Count;
            int openBrace = 0;
            int closeBrace = 0;
            XSharpToken token;
            XSharpToken lastToken = null; 
            while (current < count)
            {
                // Check to see if there is a codeblock. 
                // matchCodeBlock ends at the position after of the (nested) codeblock(s)
                current = matchCodeBlock(current, tokens);
                if (current >= count)   
                    break;
                token = tokens[current];
                if (braceLevel > 0)
                {
                    if (token.Type == openBrace)
                        braceLevel += 1;
                    else if (token.Type == closeBrace)
                        braceLevel -= 1;
                }
                else if (token.Type == XSharpLexer.COMMA)
                {
                    // expression can only have comma's when inside braces
                    // so exit the loop
                    break;
                }
                // if Open brace, scan for close brace
                else if (token.IsOpen( ref closeBrace))
                {
                    openBrace = token.Type;
                    braceLevel++;
                }
                // check to see if we have 2 tokens that can follow
                // otherwise exit
                else if ( token.IsClose() 
                          || (lastToken.NeedsRight() && !token.IsPrimary())
                          || (stopToken != null && tokenEquals(stopToken, token))
                        )
                {
                    break;
                }
                else if (!lastToken.CanJoin(token))
                {
                    break;
                }
                else if (!token.IsNeutral())
                {
                    lastToken = token;
                }
                current++;
            }
            return current;
        }


        bool isStartOfCodeBlock(int start, IList<XSharpToken> tokens, ref int startOfBody)
        {
            int count = tokens.Count;
            startOfBody = start;
            if (start > count - 4)   // we need at least {||}
                return false;
            if (tokens[start].Type != XSharpLexer.LCURLY ||
                tokens[start+1].Type != XSharpLexer.PIPE)
                return false;
            // now scan for second pipe which indicates end of parameter list
            start = start + 2;
            var lasttype = XSharpLexer.COMMA;
            while (start < count)
            {
                var token = tokens[start];
                if (lasttype == XSharpLexer.COMMA && token.IsName())
                {
                    start = start + 1;
                    lasttype = XSharpLexer.ID;
                }
                else if (lasttype == XSharpLexer.ID && token.Type == XSharpLexer.COMMA)
                {
                    start = start + 1;
                    lasttype = XSharpLexer.COMMA;
                }
                else
                {
                    break;
                }
            }
            // End of parameter list
            bool Ok = start < count && tokens[start].Type == XSharpLexer.PIPE;
            if (Ok)
                startOfBody = start + 1;
            return Ok;
        }
        bool findEndOfCodeBlock(int start, IList<XSharpToken> tokens, ref int endOfBlock)
        {
            int count = tokens.Count;
            int nested = 0;
            int closeType = 0;
            while (start < count)
            {
                XSharpToken token = tokens[start];
                if (token.IsOpen( ref closeType))
                {
                    nested += 1;
                }
                if (token.Type == XSharpLexer.RCURLY && nested == 0)
                {
                    endOfBlock = start;
                    return true;
                }
                if (token.IsClose())
                {
                    nested -= 1;
                }
                start += 1;
            }
            return false;
        }
        int matchCodeBlock(int start, IList<XSharpToken> tokens)
        {
            int body = start;
            int count = tokens.Count;
            if (isStartOfCodeBlock(start, tokens, ref body))    // found start of codeblock, body now points to the last PIPE
            {
                int nested = 1;
                body = body + 1;
                while (body < count && nested >= 1)
                {
                    if (isStartOfCodeBlock(body, tokens, ref body)) // nested codeblock , body now points to the last PIPE of nested block
                    {
                        nested += 1;
                    }
                    else if (findEndOfCodeBlock(body, tokens, ref body)) // body points to the RCURLY of one of the blocks
                    {
                        nested -= 1;
                    }
                }
                body = body + 1;
            }
            return body;    // points to the first character after the codeblock or to the original start
        }
    }



}