using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Microsoft.CodeAnalysis.Text;

namespace EnumCaseGenerator
{
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("Test Suggested Actions")]
    [ContentType("text")]
    public class GeneratorSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
    {
        [ImportingConstructor]
        public GeneratorSuggestedActionsSourceProvider(
            [Import(typeof(SVsServiceProvider), AllowDefault = true)] IServiceProvider vsServiceProvider,
            [Import(typeof(VisualStudioWorkspace), AllowDefault = true)] Workspace vsWorkspace)
        {
            var ws = vsWorkspace as VisualStudioWorkspace;
        }

        [Import(typeof(ITextStructureNavigatorSelectorService))]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            if (textBuffer == null && textView == null)
                return null;

            var ws = textBuffer.GetWorkspace();
            if (ws == null)
                return null;

            var container = textBuffer.AsTextContainer();
            if (container == null)
                return null;

            var documentId = ws.GetDocumentIdInCurrentContext(container);
            if (documentId == null)
                return null;

            var document = ws.CurrentSolution.GetDocument(documentId);
            if (document == null)
                return null;

            return new GeneratorSuggestedActionsSource(this, document, textView, textBuffer);
        }
    }
}
