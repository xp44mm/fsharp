﻿// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Composition
open System.Threading.Tasks

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "ChangeRefCellDerefToNotExpression"); Shared>]
type internal FSharpChangeRefCellDerefToNotExpressionCodeFixProvider
    [<ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider, 
        projectInfoManager: FSharpProjectOptionsManager
    ) =
    inherit CodeFixProvider()
    
    static let userOpName = "FSharpChangeRefCellDerefToNotExpressionCodeFix"
    let fixableDiagnosticIds = set ["FS0001"]

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override this.RegisterCodeFixesAsync context : Task =
        asyncMaybe {
            let document = context.Document
            let! parsingOptions, _ = projectInfoManager.TryGetOptionsForEditingDocumentOrProject(document, context.CancellationToken, userOpName)
            let! sourceText = context.Document.GetTextAsync(context.CancellationToken)
            let! parseResults = checkerProvider.Checker.ParseDocument(document, parsingOptions, userOpName=userOpName)

            let errorRange = RoslynHelpers.TextSpanToFSharpRange(document.FilePath, context.Span, sourceText)
            let! derefRange = parseResults.TryRangeOfRefCellDereferenceContainingPos errorRange.Start
            let! derefSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, derefRange)
            
            let title = SR.UseNotForNegation()

            let diagnostics =
                context.Diagnostics
                |> Seq.filter (fun x -> fixableDiagnosticIds |> Set.contains x.Id)
                |> Seq.toImmutableArray

            let codeFix =
                CodeFixHelpers.createTextChangeCodeFix(
                    title,
                    context,
                    (fun () -> asyncMaybe.Return [| TextChange(derefSpan, "not ") |]))

            context.RegisterCodeFix(codeFix, diagnostics)
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken) 