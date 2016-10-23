using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnumCaseGenerator
{
    internal class GeneratorSuggestedActionsSource : ISuggestedActionsSource
    {
        private GeneratorSuggestedActionsSourceProvider _provider;
        private ITextView _textView;
        private ITextBuffer _textBuffer;

        public event EventHandler<EventArgs> SuggestedActionsChanged;

        public GeneratorSuggestedActionsSource(GeneratorSuggestedActionsSourceProvider provider, ITextView textView, ITextBuffer textBuffer)
        {
            _provider = provider;
            _textView = textView;
            _textBuffer = textBuffer;
        }

        private void OnSuggestedActionsChanged(object sender, EventArgs e)
        {
            SuggestedActionsChanged?.Invoke(sender, e);
        }

        //private bool TryGetWordUnderCaret(out TextExtent wordExtent)
        //{
        //    var view = _textView;
        //    var buffer = _textBuffer;
        //    if (view == null || buffer == null)
        //    {
        //        wordExtent = new TextExtent();
        //        return false;
        //    }

        //    ITextCaret caret = view.Caret;
        //    SnapshotPoint point;

        //    if (caret.Position.BufferPosition > 0)
        //    {
        //        point = caret.Position.BufferPosition - 1;
        //    }
        //    else
        //    {
        //        wordExtent = default(TextExtent);
        //        return false;
        //    }

        //    ITextStructureNavigator navigator = _provider.NavigatorService.GetTextStructureNavigator(buffer);
        //    wordExtent = navigator.GetExtentOfWord(point);
        //    return true;
        //}

        public void Dispose()
        {
            // do nothing
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        private bool HasSuggestedAction(SnapshotSpan range)
        {
            ITypeSymbol symbol;
            return HasSuggestedAction(range, out symbol);
        }

        private bool HasSuggestedAction(SnapshotSpan range, out ITypeSymbol symbol)
        {
            symbol = null;
            return false;
            var ws = _textBuffer.GetWorkspace();
            if (ws == null)
                return false;

            var container = _textBuffer.AsTextContainer();
            if (container == null)
                return false;

            var documentId = ws.GetDocumentIdInCurrentContext(container);
            if (documentId == null)
                return false;

            var document = ws.CurrentSolution.GetDocument(documentId);
            if (document == null)
                return false;

            var root = document.GetSyntaxRootAsync().Result;

            var switchNode = root.FindNode(new TextSpan(range.Start, range.Length)) as SwitchStatementSyntax;
            if (switchNode == null || switchNode.Expression == null)
                return false;

            var model = document.GetSemanticModelAsync().Result;

            var ti = model.GetTypeInfo(switchNode.Expression);
            if (ti.ConvertedType == null || ti.ConvertedType.TypeKind != TypeKind.Enum)
                return false;

            symbol = ti.ConvertedType;
            return true;
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            //TextExtent extent;
            //if (TryGetWordUnderCaret(out extent) && extent.IsSignificant)
            //{
            //    ITrackingSpan trackingSpan = range.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
            //    var generateAction = new GeneratorSuggestedAction(trackingSpan);
            //    return new SuggestedActionSet[] { new SuggestedActionSet(new ISuggestedAction[] { generateAction }) };
            //}

            ITypeSymbol symbol;
            if (HasSuggestedAction(range, out symbol))
            {
                var generateAction = new GeneratorSuggestedAction(range, symbol);
                return new SuggestedActionSet[] { new SuggestedActionSet(new ISuggestedAction[] { generateAction }) };
            }
            return Enumerable.Empty<SuggestedActionSet>();
        }

        public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() =>
            {
                //TextExtent extent;
                //if (TryGetWordUnderCaret(out extent))
                //{
                //    // don't display the action if the extent has whitespace
                //    return extent.IsSignificant;
                //}
                return HasSuggestedAction(range);
            });
        }
    }
}
