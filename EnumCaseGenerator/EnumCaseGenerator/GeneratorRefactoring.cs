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
            if (root == null)
                return;

            var node = root.FindNode(context.Span);
            if (node == null)
                return;

            var switchNode = node.FirstAncestorOrSelf<SwitchStatementSyntax>();
            if (switchNode == null || switchNode.Expression == null)
                return;

            var model = context.Document.GetSemanticModelAsync().Result;
            if (model == null)
                return;

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
                    ct => SortEnumSwitch(context, ct, enumSwitch, true));
                context.RegisterRefactoring(action);
            }

            if (!enumSwitch.IsSortedByValues)
            {
                action = CodeAction.Create(
                    "Sort switch cases by enum value",
                    ct => SortEnumSwitch(context, ct, enumSwitch, false));
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

        private async Task<Document> SortEnumSwitch(CodeRefactoringContext context, CancellationToken cancellationToken, EnumSwitch enumSwitch, bool byAlpha)
        {
            var newSections = new List<SwitchSectionSyntax>(enumSwitch.Node.Sections);
            newSections.Sort(new SwitchSectionSyntaxComparer(enumSwitch, byAlpha));
            return await RebuildSections(context, cancellationToken, enumSwitch, newSections);
        }

        private async Task<Document> MoveDefaultCaseToEndOfEnumSwitch(CodeRefactoringContext context, CancellationToken cancellationToken, EnumSwitch enumSwitch)
        {
            var defs = new List<SwitchSectionSyntax>();
            var newSections = new List<SwitchSectionSyntax>();
            foreach (var section in enumSwitch.Node.Sections)
            {
                if (SwitchSectionSyntaxComparer.HasDefault(section))
                {
                    defs.Add(section);
                    continue;
                }
                newSections.Add(section);
            }
            newSections.AddRange(defs);
            return await RebuildSections(context, cancellationToken, enumSwitch, newSections);
        }

        private async Task<Document> PopulateEnumSwitch(CodeRefactoringContext context, CancellationToken cancellationToken, EnumSwitch enumSwitch, bool withDefault)
        {
            var newSections = new List<SwitchSectionSyntax>(enumSwitch.Node.Sections);

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

            newSections.Sort(new SwitchSectionSyntaxComparer(enumSwitch, true));
            return await RebuildSections(context, cancellationToken, enumSwitch, newSections);
        }

        private class SwitchSectionSyntaxComparer : IComparer<SwitchSectionSyntax>
        {
            private bool _alpha;
            private EnumSwitch _enumSwitch;

            public SwitchSectionSyntaxComparer(EnumSwitch enumSwitch, bool alpha)
            {
                _enumSwitch = enumSwitch;
                _alpha = alpha;
            }

            internal static bool HasDefault(SwitchSectionSyntax section)
            {
                return section.Labels.OfType<DefaultSwitchLabelSyntax>().Any();
            }

            private static IEnumerable<string> GetLabels(SwitchSectionSyntax section)
            {
                foreach (var label in section.Labels.OfType<CaseSwitchLabelSyntax>())
                {
                    var expr = label.Value as MemberAccessExpressionSyntax;
                    if (expr == null || expr.Name == null || expr.Expression == null)
                        continue;

                    var name = expr.Name as SimpleNameSyntax;
                    if (name == null || name.Identifier == null)
                        continue;

                    yield return name.Identifier.Text;
                }
            }

            private IComparable GetFirstConstantValue(SwitchSectionSyntax section)
            {
                var list = new List<IComparable>();
                foreach (var label in GetLabels(section))
                {
                    var value = _enumSwitch.GetConstantValue(label) as IComparable;
                    if (value != null)
                    {
                        list.Add(value);
                    }
                }
                if (list.Count == 0)
                    return null;

                list.Sort();
                return list[0];
            }

            private string GetFirstName(SwitchSectionSyntax section)
            {
                var list = new List<string>();
                foreach (var label in GetLabels(section))
                {
                    list.Add(label);
                }
                if (list.Count == 0)
                    return null;

                list.Sort();
                return list[0];
            }

            public int Compare(SwitchSectionSyntax x, SwitchSectionSyntax y)
            {
                if (ReferenceEquals(x, y))
                    return 0;

                if (x != null && y != null)
                {
                    // default is always last
                    if (HasDefault(x))
                    {
                        if (!HasDefault(y))
                            return 1;
                    }
                    else if (HasDefault(y))
                        return -1;

                    if (_alpha)
                    {
                        var nx = GetFirstName(x);
                        if (nx != null)
                            return nx.CompareTo(GetFirstName(y));
                    }
                    else
                    {
                        var cx = GetFirstConstantValue(x);
                        if (cx != null)
                        {
                            try
                            {
                                return cx.CompareTo(GetFirstConstantValue(y));
                            }
                            catch
                            {
                                // hmm... maybe this could happen if types are not compatible, so we do nothing
                            }
                        }
                    }
                }
                return 0; // don't know
            }
        }
    }
}