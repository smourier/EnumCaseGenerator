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
using Microsoft.VisualStudio.Imaging;
using Microsoft.CodeAnalysis;

namespace EnumCaseGenerator
{
    internal class GeneratorSuggestedAction : ISuggestedAction
    {
        private SnapshotSpan _span;
        private ITypeSymbol _type;

        public GeneratorSuggestedAction(SnapshotSpan span, ITypeSymbol type)
        {
            _span = span;
            _type = type;
            DisplayText = "Ensure all cases for enum type '" + _type.Name + "'";
        }

        public string DisplayText { get; private set; }

        public bool HasActionSets
        {
            get
            {
                return false;
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
                return KnownMonikers.EnumerationSnippet;
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
            textBlock.Inlines.Add(new Run() { Text = _type.Name });
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
