﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    Partial Friend NotInheritable Class AnonymousTypeManager

        Partial Private NotInheritable Class AnonymousType_IEquatable_EqualsMethodSymbol
            Inherits SynthesizedRegularMethodBase

            Private ReadOnly _parameters As ImmutableArray(Of ParameterSymbol)
            Private ReadOnly _interfaceMethod As ImmutableArray(Of MethodSymbol)

            Public Sub New(container As AnonymousTypeTemplateSymbol, interfaceMethod As MethodSymbol)
                MyBase.New(VisualBasicSyntaxTree.Dummy.GetRoot(), container, WellKnownMemberNames.ObjectEquals)

                _parameters = ImmutableArray.Create(Of ParameterSymbol)(New SynthesizedParameterSimpleSymbol(Me, container, 0, "val"))
                _interfaceMethod = ImmutableArray.Create(interfaceMethod)
            End Sub

            Private ReadOnly Property AnonymousType As AnonymousTypeTemplateSymbol
                Get
                    Return DirectCast(Me.m_containingType, AnonymousTypeTemplateSymbol)
                End Get
            End Property

            Public Overrides ReadOnly Property IsOverrides As Boolean
                Get
                    Return False
                End Get
            End Property

            Public Overrides ReadOnly Property IsOverridable As Boolean
                Get
                    Return False
                End Get
            End Property

            Public Overrides ReadOnly Property IsNotOverridable As Boolean
                Get
                    Return False
                End Get
            End Property

            Public Overrides ReadOnly Property IsOverloads As Boolean
                Get
                    Return True
                End Get
            End Property

            Friend Overrides ReadOnly Property ParameterCount As Integer
                Get
                    Return 1
                End Get
            End Property

            Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
                Get
                    Return Me._parameters
                End Get
            End Property

            Public Overrides ReadOnly Property ExplicitInterfaceImplementations As ImmutableArray(Of MethodSymbol)
                Get
                    Return _interfaceMethod
                End Get
            End Property

            Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
                Get
                    Return Accessibility.Public
                End Get
            End Property

            Public Overrides ReadOnly Property IsSub As Boolean
                Get
                    Return False
                End Get
            End Property

            Public Overrides ReadOnly Property ReturnType As TypeSymbol
                Get
                    Return AnonymousType.Manager.System_Boolean
                End Get
            End Property

            Friend Overrides Sub AddSynthesizedAttributes(compilationState As ModuleCompilationState, ByRef attributes As ArrayBuilder(Of SynthesizedAttributeData))
                MyBase.AddSynthesizedAttributes(compilationState, attributes)

                Dim compilation = DirectCast(Me.ContainingType, AnonymousTypeTemplateSymbol).Manager.Compilation
                AddSynthesizedAttribute(attributes, compilation.SynthesizeDebuggerHiddenAttribute())
            End Sub

            Friend Overrides ReadOnly Property GenerateDebugInfoImpl As Boolean
                Get
                    Return False
                End Get
            End Property

            Friend Overrides Function CalculateLocalSyntaxOffset(localPosition As Integer, localTree As SyntaxTree) As Integer
                Throw ExceptionUtilities.Unreachable
            End Function
        End Class
    End Class
End Namespace
