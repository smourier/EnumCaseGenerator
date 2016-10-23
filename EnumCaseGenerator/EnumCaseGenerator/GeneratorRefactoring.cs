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

            var ti = model.GetTypeInfo(switchNode.Expression);
            if (ti.ConvertedType == null || ti.ConvertedType.TypeKind != TypeKind.Enum)
                return;

            var symbol = ti.ConvertedType;
            var action = CodeAction.Create(
                "Ensure all cases for enum type '" + symbol.Name + "'",
                ct => PopulateSwitch(context, symbol, ct));

            context.RegisterRefactoring(action);
        }

        private async Task<Document> PopulateSwitch(CodeRefactoringContext context, ITypeSymbol symbol, CancellationToken cancellationToken)
        {
            return null;
        }
    }
}