using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace EnumCaseGenerator
{
    [Export(typeof(ISuggestedActionsSourceProvider)), ContentType("text"), Name("EnumCaseGenerator")]
    public class SuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
    {
        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            return new SuggestedActionsSource();
        }

        private class SuggestedActionsSource : ISuggestedActionsSource
        {
            public event EventHandler<EventArgs> SuggestedActionsChanged;

            public void Dispose()
            {
            }

            public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
            {
                yield break;
            }

            public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
            {
                return Task.FromResult(GetSuggestedActions(requestedActionCategories, range, cancellationToken).Any());
            }

            public bool TryGetTelemetryId(out Guid telemetryId)
            {
                telemetryId = Guid.Empty;
                return false;
            }
        }
    }
}
