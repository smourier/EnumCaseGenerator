using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

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

            //_textBuffer.AsTextContainer
        }

        private void OnSuggestedActionsChanged(object sender, EventArgs e)
        {
            SuggestedActionsChanged?.Invoke(sender, e);
        }

        private bool TryGetWordUnderCaret(out TextExtent wordExtent)
        {
            var view = _textView;
            var buffer = _textBuffer;
            if (view == null || buffer == null)
            {
                wordExtent = new TextExtent();
                return false;
            }

            ITextCaret caret = view.Caret;
            SnapshotPoint point;

            if (caret.Position.BufferPosition > 0)
            {
                point = caret.Position.BufferPosition - 1;
            }
            else
            {
                wordExtent = default(TextExtent);
                return false;
            }

            ITextStructureNavigator navigator = _provider.NavigatorService.GetTextStructureNavigator(buffer);
            wordExtent = navigator.GetExtentOfWord(point);
            return true;
        }

        public void Dispose()
        {
            // do nothing
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            TextExtent extent;
            if (TryGetWordUnderCaret(out extent) && extent.IsSignificant)
            {
                ITrackingSpan trackingSpan = range.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
                var generateAction = new GeneratorSuggestedAction(trackingSpan);
                return new SuggestedActionSet[] { new SuggestedActionSet(new ISuggestedAction[] { generateAction }) };
            }
            return Enumerable.Empty<SuggestedActionSet>();
        }

        public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() =>
            {
                TextExtent extent;
                if (TryGetWordUnderCaret(out extent))
                {
                    // don't display the action if the extent has whitespace
                    return extent.IsSignificant;
                }
                return false;
            });
        }
    }
}
