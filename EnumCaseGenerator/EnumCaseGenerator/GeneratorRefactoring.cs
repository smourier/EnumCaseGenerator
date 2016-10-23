using System;
using System.Composition;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace EnumCaseGenerator
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(GeneratorRefactoring)), Shared]
    public class GeneratorRefactoring : CodeRefactoringProvider
    {
        public sealed async override Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var switchNode = root.FindNode(context.Span) as SwitchStatementSyntax;
            if (switchNode == null || switchNode.Expression == null)
                return;

            var model = context.Document.GetSemanticModelAsync().Result;

            // get type and check it's an enum
            var ti = model.GetTypeInfo(switchNode.Expression);
            var type = ti.ConvertedType;
            if (type == null || type.TypeKind != TypeKind.Enum)
                return;
            
            // is it a flags (multi-valued) attribute? we don't want to handle those
            var flags = type.GetAttributes().Any(at => at.AttributeClass?.ContainingNamespace?.Name == typeof(FlagsAttribute).Namespace && at.AttributeClass?.Name == typeof(FlagsAttribute).Name);
            if (flags)
                return;

            bool hasDefault;
            if (!GetMissingValues(switchNode, type, out hasDefault).Any())
                return;

            var action = CodeAction.Create(
                "Ensure all cases for enum type '" + type.Name + "'",
                ct => PopulateEnumSwitch(context, switchNode, type, ct, false));

            context.RegisterRefactoring(action);

            if (!hasDefault)
            {
                action = CodeAction.Create(
                    "Ensure all cases for enum type '" + type.Name + "' with default",
                    ct => PopulateEnumSwitch(context, switchNode, type, ct, true));

                context.RegisterRefactoring(action);
            }
        }

        private static IReadOnlyList<string> GetExistingValues(SwitchStatementSyntax switchNode, out bool hasDefault)
        {
            hasDefault = false;
            var list = new List<string>();
            foreach (var section in switchNode.Sections)
            {
                // we don't want DefaultSwitchLabelSyntax
                foreach (var label in section.Labels)
                {
                    if (label is DefaultSwitchLabelSyntax)
                    {
                        hasDefault = true;
                        continue;
                    }

                    var caseLabel = label as CaseSwitchLabelSyntax;
                    if (caseLabel == null)
                        continue;

                    var member = caseLabel.Value as MemberAccessExpressionSyntax;
                    if (member == null)
                        continue;

                    var name = member.Name as IdentifierNameSyntax;
                    if (name == null || name.Identifier == null || string.IsNullOrWhiteSpace(name.Identifier.Text))
                        continue;

                    list.Add(name.Identifier.Text);
                }
            }
            return list;
        }

        private static IReadOnlyList<string> GetMissingValues(SwitchStatementSyntax switchNode, ITypeSymbol enumType, out bool hasDefault)
        {
            var list = new List<string>();
            var existing = GetExistingValues(switchNode, out hasDefault);
            var missing = new List<string>();
            foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (existing.Contains(field.Name))
                    continue;

                list.Add(field.Name);
            }
            return list;
        }

        private async Task<Document> PopulateEnumSwitch(CodeRefactoringContext context, SwitchStatementSyntax switchNode, ITypeSymbol enumType, CancellationToken cancellationToken, bool withDefault)
        {
            var newSections = new List<SwitchSectionSyntax>(switchNode.Sections);
            bool hasDefault;
            foreach (string name in GetMissingValues(switchNode, enumType, out hasDefault))
            {
                var access = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(enumType.Name),
                    SyntaxFactory.IdentifierName(name));
                var label = SyntaxFactory.CaseSwitchLabel(access);
                var statement = SyntaxFactory.BreakStatement();
                var newSection = SyntaxFactory.SwitchSection(
                    SyntaxFactory.List(new SwitchLabelSyntax[] { label }),
                    SyntaxFactory.List(new StatementSyntax[] { statement }));
                newSections.Add(newSection);
            }

            if (!hasDefault && withDefault)
            {
                var label = SyntaxFactory.DefaultSwitchLabel();
                var statement = SyntaxFactory.BreakStatement();
                var newSection = SyntaxFactory.SwitchSection(
                    SyntaxFactory.List(new SwitchLabelSyntax[] { label }),
                    SyntaxFactory.List(new StatementSyntax[] { statement }));
                newSections.Add(newSection);
            }

            var oldRoot = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var newSwitch = SyntaxFactory.SwitchStatement(
                switchNode.SwitchKeyword,
                switchNode.OpenParenToken,
                switchNode.Expression,
                switchNode.CloseParenToken,
                switchNode.OpenBraceToken,
                SyntaxFactory.List(newSections),
                switchNode.CloseBraceToken);

            var newRoot = oldRoot.ReplaceNode(switchNode, newSwitch);
            return context.Document.WithSyntaxRoot(newRoot);
        }
    }
}