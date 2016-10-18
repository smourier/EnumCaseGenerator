using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace EnumCaseGenerator
{
    internal class GeneratorSuggestedAction : ISuggestedAction
    {
        private ITrackingSpan _span;
        private ITextSnapshot _snapshot;

        public GeneratorSuggestedAction(ITrackingSpan span)
        {
            _span = span;
            _snapshot = span.TextBuffer.CurrentSnapshot;
            DisplayText = "Ensure all cases for enum text '" + span.GetText(_snapshot) + "'";
        }

        public string DisplayText { get; private set; }

        public bool HasActionSets
        {
            get
            {
                return true;
            }
        }

        public bool HasPreview
        {
            get
            {
                return true;
            }
        }

        public string IconAutomationText
        {
            get
            {
                return null;
            }
        }

        public ImageMoniker IconMoniker
        {
            get
            {
                return new ImageMoniker();
            }
        }

        public string InputGestureText
        {
            get
            {
                return null;
            }
        }

        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            // nothing
            return Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
        }

        public Task<object> GetPreviewAsync(CancellationToken cancellationToken)
        {
            var textBlock = new TextBlock();
            textBlock.Padding = new Thickness(5);
            textBlock.Inlines.Add(new Run() { Text = _span.GetText(_snapshot) });
            return Task.FromResult<object>(textBlock);
        }

        public void Invoke(CancellationToken cancellationToken)
        {
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        public void Dispose()
        {
            // do nothing
        }
    }
}
