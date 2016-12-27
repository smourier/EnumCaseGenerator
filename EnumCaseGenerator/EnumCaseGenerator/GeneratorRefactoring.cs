using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EnumCaseGenerator
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(GeneratorRefactoring)), Shared]
    public class GeneratorRefactoring : CodeRefactoringProvider
    {
        public sealed async override Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);
            if (node == null)
                return;

            var switchNode = node.FirstAncestorOrSelf<SwitchStatementSyntax>();
            if (switchNode == null || switchNode.Expression == null)
                return;

            var model = context.Document.GetSemanticModelAsync().Result;
            var enumSwitch = EnumSwitch.Parse(model, switchNode);
            if (enumSwitch == null)
                return; // unhandled

            CodeAction action;
            if (enumSwitch.HasMissingCases)
            {
                action = CodeAction.Create(
                    "Ensure all cases for enum type '" + enumSwitch.EnumType.Name + "'",
                    ct => PopulateEnumSwitch(context, ct, enumSwitch, false));
                context.RegisterRefactoring(action);
            }

            if (!enumSwitch.HasDefaultCase)
            {
                action = CodeAction.Create(
                    "Ensure all cases for enum type '" + enumSwitch.EnumType.Name + "' with default",
                    ct => PopulateEnumSwitch(context, ct, enumSwitch, true));
                context.RegisterRefactoring(action);
            }

            if (!enumSwitch.IsSortedAlphabetically)
            {
                action = CodeAction.Create(
                    "Sort switch cases alphabetically",
                    ct => SortEnumSwitch(context, ct, enumSwitch, true, false));
                context.RegisterRefactoring(action);
            }

            if (!enumSwitch.IsSortedByValues)
            {
                action = CodeAction.Create(
                    "Sort switch cases by enum value",
                    ct => SortEnumSwitch(context, ct, enumSwitch, false, true));
                context.RegisterRefactoring(action);
            }

            if (enumSwitch.HasDefaultCase && !enumSwitch.DefaultCaseIsLast.GetValueOrDefault())
            {
                action = CodeAction.Create(
                    "Move default case to end",
                    ct => MoveDefaultCaseToEndOfEnumSwitch(context, ct, enumSwitch));
                context.RegisterRefactoring(action);
            }
        }

        private async Task<Document> RebuildSections(CodeRefactoringContext context, CancellationToken cancellationToken, EnumSwitch enumSwitch, IEnumerable<SwitchSectionSyntax> sections)
        {
            var newSwitch = SyntaxFactory.SwitchStatement(
                enumSwitch.Node.SwitchKeyword,
                enumSwitch.Node.OpenParenToken,
                enumSwitch.Node.Expression,
                enumSwitch.Node.CloseParenToken,
                enumSwitch.Node.OpenBraceToken,
                SyntaxFactory.List(sections),
                enumSwitch.Node.CloseBraceToken);

            var oldRoot = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot.ReplaceNode(enumSwitch.Node, newSwitch);
            return context.Document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> SortEnumSwitch(CodeRefactoringContext context, CancellationToken cancellationToken, EnumSwitch enumSwitch, bool byAlpha, bool byValue)
        {
            return await RebuildSections(context, cancellationToken, enumSwitch, enumSwitch.Node.Sections);
        }

        private async Task<Document> MoveDefaultCaseToEndOfEnumSwitch(CodeRefactoringContext context, CancellationToken cancellationToken, EnumSwitch enumSwitch)
        {
            return await RebuildSections(context, cancellationToken, enumSwitch, enumSwitch.Node.Sections);
        }

        private async Task<Document> PopulateEnumSwitch(CodeRefactoringContext context, CancellationToken cancellationToken, EnumSwitch enumSwitch, bool withDefault)
        {
            var newSections = new List<SwitchSectionSyntax>();

            foreach (var field in enumSwitch.MissingFields)
            {
                var access = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(enumSwitch.EnumType.Name),
                    SyntaxFactory.IdentifierName(field.Name));
                var label = SyntaxFactory.CaseSwitchLabel(access);
                var statement = SyntaxFactory.BreakStatement();
                var newSection = SyntaxFactory.SwitchSection(
                    SyntaxFactory.List(new SwitchLabelSyntax[] { label }),
                    SyntaxFactory.List(new StatementSyntax[] { statement }));
                newSections.Add(newSection);
            }

            if (!enumSwitch.HasDefaultCase && withDefault)
            {
                var label = SyntaxFactory.DefaultSwitchLabel();
                var statement = SyntaxFactory.BreakStatement();
                var newSection = SyntaxFactory.SwitchSection(
                    SyntaxFactory.List(new SwitchLabelSyntax[] { label }),
                    SyntaxFactory.List(new StatementSyntax[] { statement }));
                newSections.Add(newSection);
            }

            foreach (var section in enumSwitch.Node.Sections)
            {
                newSections.Add(section);
            }

            return await RebuildSections(context, cancellationToken, enumSwitch, newSections);
        }
    }
}